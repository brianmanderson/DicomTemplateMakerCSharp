using DicomTemplateMakerGUI.Services;
using Newtonsoft.Json;
using ROIOntologyClass;
using Xunit;
using static DicomTemplateCore.Tests.TemplateTestData;

namespace DicomTemplateCore.Tests;

/// <summary>
/// The shared ontology library (Ontologies/All_Ontologies.json): template builds and imports only add to it, a
/// damaged library is never replaced, and legacy text files are only deleted when all of them were migrated.
/// </summary>
public class OntologyLibraryTests
{
    private static OntologyCodeClass UserEntry() => new OntologyCodeClass("My structure", "999", "LOCAL");

    /// <summary>A library the user curated: an entry no template uses, and their own name for Bladder.</summary>
    private static string CuratedLibrary(TestFolder root)
    {
        string library = root.Sub("Ontologies");
        OntologyTools.SaveOntologiesToFolder(new List<OntologyCodeClass> { UserEntry(), new OntologyCodeClass("Bladder (mine)", "15900", "FMA") }, library);
        return library;
    }

    private static List<(string Meaning, string? Code, string? Scheme)> Entries(string library)
    {
        return OntologyTools.LoadOntologiesFromFolder(library).Select(o => (o.CodeMeaning, o.CodeValue, o.Scheme)).ToList();
    }

    [Fact]
    public void A_template_build_keeps_library_entries_it_does_not_use()
    {
        using var root = new TestFolder();
        string library = CuratedLibrary(root);
        // As the online-template builder makes it: the library is never loaded into Ontologies.
        var builder = new TemplateMaker();
        builder.set_onto_path(library);
        builder.define_output(Path.Combine(root.Path, "AbdPelv_Anal"));
        builder.ROIs.Add(Roi("Bladder", "15900"));
        builder.ROIs.Add(Roi("Rectum", "14544"));
        builder.ROIs.Add(Roi("GTVp", "GTV", "99VMS_STRUCTCODE", "GTV"));

        builder.make_template();

        Assert.Equal(new (string, string?, string?)[]
        {
            ("Bladder (mine)", "15900", "FMA"),
            ("GTVp", "GTV", "99VMS_STRUCTCODE"),
            ("My structure", "999", "LOCAL"),
            ("Rectum", "14544", "FMA"),
        }, Entries(library));
    }

    [Fact]
    public void The_same_code_in_another_scheme_is_a_separate_entry()
    {
        using var root = new TestFolder();
        string library = CuratedLibrary(root);

        int added = OntologyTools.MergeOntologiesIntoFolder(new[] { new OntologyCodeClass("Bladder", "15900", "SCT"), new OntologyCodeClass("Bladder again", "15900", "FMA") }, library);

        Assert.Equal(1, added);
        Assert.Contains(("Bladder", "15900", "SCT"), Entries(library));
        Assert.Contains(("Bladder (mine)", "15900", "FMA"), Entries(library));
        Assert.Equal(3, Entries(library).Count);
    }

    [Fact]
    public void Merge_does_not_rewrite_the_library_when_nothing_is_new()
    {
        using var root = new TestFolder();
        string library = root.Sub("Ontologies");
        // Compact JSON: any rewrite by the program would indent it.
        string file = WriteFile(library, "All_Ontologies.json", JsonConvert.SerializeObject(new List<OntologyCodeClass> { UserEntry() }));
        byte[] before = File.ReadAllBytes(file);

        int added = OntologyTools.MergeOntologiesIntoFolder(new OntologyCodeClass?[] { UserEntry(), new OntologyCodeClass("No code", "", "FMA"), null }, library);

        Assert.Equal(0, added);
        Assert.Equal(before, File.ReadAllBytes(file));
    }

    [Fact]
    public void Loading_a_template_keeps_library_entries_it_does_not_use()
    {
        using var root = new TestFolder();
        string library = CuratedLibrary(root);
        string template = Template(root.Sub("Pelvis"), Roi("Rectum", "14544"));
        var maker = new TemplateMaker();
        maker.set_onto_path(library);
        maker.define_path(template);
        maker.define_output(template);

        maker.categorize_folder();

        Assert.Contains(("My structure", "999", "LOCAL"), Entries(library));
        Assert.Contains(("Rectum", "14544", "FMA"), Entries(library));
        Assert.Equal(3, Entries(library).Count);
    }

    [Fact]
    public void Varian_import_keeps_library_entries_it_does_not_use()
    {
        using var root = new TestFolder();
        string library = CuratedLibrary(root);

        VarianXmlReport report = VarianXmlReader.Import(TestFolder.Data("Abdomen.xml"), root.Path);

        Assert.False(report.Failed, report.Error);
        List<(string Meaning, string? Code, string? Scheme)> entries = Entries(library);
        Assert.Contains(("My structure", "999", "LOCAL"), entries);
        Assert.Contains(("Bladder (mine)", "15900", "FMA"), entries);
        Assert.Contains(entries, e => e.Code == "7131" && e.Scheme == "FMA");
        Assert.Equal(entries.Count, entries.Select(e => (e.Code, e.Scheme)).Distinct().Count());
    }

    [Fact]
    public void AddOntologyIfNew_adds_to_the_library_and_keeps_the_rest()
    {
        using var root = new TestFolder();
        string library = CuratedLibrary(root);
        var maker = new TemplateMaker();
        maker.set_onto_path(library);

        Assert.True(maker.AddOntologyIfNew(new OntologyCodeClass("Rectum", "14544", "FMA")));

        Assert.Equal(3, Entries(library).Count);
        Assert.Contains(("My structure", "999", "LOCAL"), Entries(library));
    }

    [Fact]
    public void RemoveOntology_removes_only_that_entry_from_the_library()
    {
        using var root = new TestFolder();
        string library = CuratedLibrary(root);
        var maker = new TemplateMaker();
        maker.set_onto_path(library);
        var bladder = new OntologyCodeClass("Bladder (mine)", "15900", "FMA");
        maker.Ontologies.Add(bladder);

        Assert.True(maker.RemoveOntology(bladder));

        Assert.Equal(new (string, string?, string?)[] { ("My structure", "999", "LOCAL") }, Entries(library));
    }

    public static TheoryData<string> UnreadableLibraries => new() { TruncatedJson, "", "null", "[null]" };

    [Theory]
    [MemberData(nameof(UnreadableLibraries))]
    public void Unreadable_library_throws_and_is_left_untouched(string content)
    {
        using var root = new TestFolder();
        string library = root.Sub("Ontologies");
        string file = WriteFile(library, "All_Ontologies.json", content);
        WriteFile(library, "Bladder.txt", "15900\nFMA");
        byte[] before = File.ReadAllBytes(file);

        var ex = Assert.Throws<TemplateLoadException>(() => OntologyTools.LoadOntologiesFromFolder(library));

        Assert.Equal(file, ex.FilePath);
        Assert.Equal(before, File.ReadAllBytes(file));
        Assert.Equal(new[] { "All_Ontologies.json", "Bladder.txt" }, Files(library));
    }

    [Fact]
    public void A_template_build_does_not_replace_an_unreadable_library_and_writes_nothing()
    {
        using var root = new TestFolder();
        string library = root.Sub("Ontologies");
        string file = WriteFile(library, "All_Ontologies.json", TruncatedJson);
        byte[] before = File.ReadAllBytes(file);
        var builder = new TemplateMaker();
        builder.set_onto_path(library);
        builder.define_output(Path.Combine(root.Path, "AbdPelv_Anal"));
        builder.ROIs.Add(Roi("Bladder", "15900"));

        var ex = Assert.Throws<TemplateLoadException>(() => builder.make_template());

        Assert.Equal(file, ex.FilePath);
        Assert.Equal(before, File.ReadAllBytes(file));
        Assert.Equal(new[] { "Ontologies/All_Ontologies.json" }, Files(root.Path));
    }

    private static string LegacyLibrary(TestFolder root)
    {
        string library = Path.Combine(root.Path, "Ontologies");
        CopyFolder(TestFolder.Data("Legacy", "Ontologies"), library);
        return library;
    }

    [Fact]
    public void Legacy_library_whose_files_all_migrate_is_converted_and_the_text_files_removed()
    {
        using var root = new TestFolder();
        string library = LegacyLibrary(root);
        File.Delete(Path.Combine(library, "Bladder-CTV.txt"));
        var warnings = new List<string>();

        List<OntologyCodeClass> ontologies = OntologyTools.LoadOntologiesFromFolder(library, warnings);

        Assert.Equal(new[] { "A_Aorta", "BODY", "Bladder" }, ontologies.Select(o => o.CodeMeaning).OrderBy(n => n, StringComparer.Ordinal));
        OntologyCodeClass aorta = ontologies.Single(o => o.CodeMeaning == "A_Aorta");
        Assert.Equal(("3734", "FMA", "VMS011", "1.2.246.352.7.2.11"), (aorta.CodeValue, aorta.Scheme, aorta.ContextIdentifier, aorta.ContextUID));
        Assert.Empty(warnings);
        Assert.Equal(new[] { "All_Ontologies.json" }, Files(library));
    }

    [Fact]
    public void Legacy_library_migration_keeps_the_text_files_when_one_is_unreadable_or_a_duplicate()
    {
        using var root = new TestFolder();
        string library = LegacyLibrary(root);
        WriteFile(library, "Broken.txt", "12345");
        var warnings = new List<string>();

        List<OntologyCodeClass> ontologies = OntologyTools.LoadOntologiesFromFolder(library, warnings);

        // Bladder.txt and Bladder-CTV.txt share code 15900; whichever is read first is migrated.
        Assert.Equal(3, ontologies.Count);
        Assert.Equal(new[] { "A_Aorta.txt", "All_Ontologies.json", "BODY.txt", "Bladder-CTV.txt", "Bladder.txt", "Broken.txt" }, Files(library));
        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, w => w.Contains("Broken.txt"));
        Assert.Contains(warnings, w => w.Contains("15900") && w.Contains("Bladder"));
        // Later saves (the ontology editor) keep the text files too.
        OntologyTools.SaveOntologiesToFolder(ontologies, library);
        Assert.Equal(6, Files(library).Count);
    }

    [Fact]
    public void Legacy_library_with_no_readable_file_throws_and_writes_nothing()
    {
        using var root = new TestFolder();
        string library = root.Sub("Ontologies");
        WriteFile(library, "Broken.txt", "12345");

        var ex = Assert.Throws<TemplateLoadException>(() => OntologyTools.LoadOntologiesFromFolder(library));

        Assert.Equal(library, ex.FilePath);
        Assert.Equal(new[] { "Broken.txt" }, Files(library));
    }
}
