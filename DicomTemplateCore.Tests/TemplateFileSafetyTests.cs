using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;
using Xunit;
using static DicomTemplateCore.Tests.TemplateTestData;

namespace DicomTemplateCore.Tests;

/// <summary>
/// Atomic template writes, and damaged template files: a corrupt All_ROIs.json is an error, never an empty
/// template, and nothing in the program saves over it.
/// </summary>
public class TemplateFileSafetyTests
{
    [Fact]
    public void Atomic_writes_produce_the_same_bytes_as_File_WriteAllText_and_WriteAllLines()
    {
        using var folder = new TestFolder();
        const string text = "[\n  \"C\u00f4lon\u02c6LN_3cm+\"\n]";
        string[] lines = { @"\\server\images\CT", "", "D:\\\u00e9t\u00e9" };

        File.WriteAllText(Path.Combine(folder.Path, "expected.json"), text);
        AtomicFile.WriteAllText(Path.Combine(folder.Path, "actual.json"), text);
        File.WriteAllLines(Path.Combine(folder.Path, "expected.txt"), lines);
        AtomicFile.WriteAllLines(Path.Combine(folder.Path, "actual.txt"), lines);

        Assert.Equal(File.ReadAllBytes(Path.Combine(folder.Path, "expected.json")), File.ReadAllBytes(Path.Combine(folder.Path, "actual.json")));
        Assert.Equal(File.ReadAllBytes(Path.Combine(folder.Path, "expected.txt")), File.ReadAllBytes(Path.Combine(folder.Path, "actual.txt")));
    }

    [Fact]
    public void Atomic_write_that_fails_while_writing_keeps_the_old_content_and_leaves_no_temp_file()
    {
        using var folder = new TestFolder();
        string target = WriteFile(folder.Path, "All_ROIs.json", "[]");
        byte[] before = File.ReadAllBytes(target);

        var failure = Assert.Throws<IOException>(() => AtomicFile.Write(target, writer =>
        {
            writer.Write(new string('x', 100_000));
            throw new IOException("disk full");
        }));

        Assert.Equal("disk full", failure.Message);
        Assert.Equal(before, File.ReadAllBytes(target));
        Assert.Equal(new[] { "All_ROIs.json" }, Files(folder.Path));
    }

    [Fact]
    public void Atomic_write_that_cannot_replace_the_target_leaves_no_temp_file()
    {
        using var folder = new TestFolder();
        string target = folder.Sub("Paths.txt");

        Exception? failure = Record.Exception(() => AtomicFile.WriteAllLines(target, new[] { "C:\\Images" }));

        Assert.True(failure is IOException || failure is UnauthorizedAccessException, failure?.ToString());
        Assert.True(Directory.Exists(target));
        Assert.Empty(Files(folder.Path));
    }

    [Fact]
    public void Atomic_write_creates_a_missing_file()
    {
        using var folder = new TestFolder();
        string target = Path.Combine(folder.Path, "DicomTags.txt");

        AtomicFile.WriteAllLines(target, new[] { "Series Description\\CT" });

        Assert.Equal(new[] { "Series Description\\CT" }, File.ReadAllLines(target));
    }

    [Fact]
    public void Create_only_write_never_replaces_an_existing_file()
    {
        // A migration uses it so that an All_ROIs.json saved meanwhile (for example by the GUI) is not replaced.
        using var folder = new TestFolder();
        string target = WriteFile(folder.Path, "All_ROIs.json", "[\"Saved meanwhile\"]");
        byte[] before = File.ReadAllBytes(target);

        Assert.False(AtomicFile.TryCreateText(target, "[\"Migrated\"]"));

        Assert.Equal(before, File.ReadAllBytes(target));
        Assert.Equal(new[] { "All_ROIs.json" }, Files(folder.Path));
    }

    [Fact]
    public void Create_only_write_creates_a_missing_file()
    {
        using var folder = new TestFolder();
        string target = Path.Combine(folder.Path, "All_Ontologies.json");

        Assert.True(AtomicFile.TryCreateText(target, "[]"));

        Assert.Equal("[]", File.ReadAllText(target));
        Assert.Equal(new[] { "All_Ontologies.json" }, Files(folder.Path));
    }

    public static TheoryData<string, string> UnreadableRoisFiles => new()
    {
        { TruncatedJson, "Unexpected end" },
        { "", "empty or holds null" },
        { "   \r\n", "empty or holds null" },
        { "null", "empty or holds null" },
        { "[null]", "null entry" },
        { "{\"ROIName\": \"Bladder\"}", "Cannot deserialize" },
    };

    [Theory]
    [MemberData(nameof(UnreadableRoisFiles))]
    public void Unreadable_rois_file_throws_and_is_left_untouched(string content, string problem)
    {
        using var folder = new TestFolder();
        string jsonFile = WriteFile(folder.Path, "All_ROIs.json", content);
        byte[] before = File.ReadAllBytes(jsonFile);

        var ex = Assert.Throws<TemplateLoadException>(() => ROIClassTools.LoadROIsFromFolder(folder.Path, new List<OntologyCodeClass>()));

        Assert.Equal(jsonFile, ex.FilePath);
        Assert.Contains(jsonFile, ex.Message);
        Assert.Contains(problem, ex.Message);
        Assert.Equal(before, File.ReadAllBytes(jsonFile));
        Assert.Equal(new[] { "All_ROIs.json" }, Files(folder.Path));
    }

    [Fact]
    public void Parse_error_is_kept_as_the_inner_exception()
    {
        using var folder = new TestFolder();
        WriteFile(folder.Path, "All_ROIs.json", TruncatedJson);

        var ex = Assert.Throws<TemplateLoadException>(() => ROIClassTools.LoadROIsFromFolder(folder.Path, new List<OntologyCodeClass>()));

        Newtonsoft.Json.JsonException inner = Assert.IsAssignableFrom<Newtonsoft.Json.JsonException>(ex.InnerException);
        Assert.Contains(inner.Message, ex.Message);
    }

    [Fact]
    public void Unreadable_rois_file_is_not_replaced_by_the_legacy_files()
    {
        using var folder = new TestFolder();
        string jsonFile = WriteFile(folder.Path, "All_ROIs.json", TruncatedJson);
        WriteFile(folder.Path, "ROIs/Bladder.txt", "255\\255\\0\nBladder\\15900\\FMA\nORGAN");
        byte[] before = File.ReadAllBytes(jsonFile);

        Assert.Throws<TemplateLoadException>(() => ROIClassTools.LoadROIsFromFolder(folder.Path, new List<OntologyCodeClass>()));

        Assert.Equal(before, File.ReadAllBytes(jsonFile));
        Assert.Equal(new[] { "All_ROIs.json", "ROIs/Bladder.txt" }, Files(folder.Path));
    }

    [Fact]
    public void Roi_without_an_ontology_is_an_error_when_there_are_ontologies_to_match()
    {
        using var folder = new TestFolder();
        ROIClass roi = Roi("Bladder", "15900");
        roi.Ontology_Class = null;
        Template(folder.Path, roi);
        byte[] before = File.ReadAllBytes(Path.Combine(folder.Path, "All_ROIs.json"));

        var ex = Assert.Throws<TemplateLoadException>(() => ROIClassTools.LoadROIsFromFolder(folder.Path, new List<OntologyCodeClass> { new OntologyCodeClass("Rectum", "14544", "FMA") }));

        Assert.Contains("Bladder", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(folder.Path, "All_ROIs.json")));
    }

    [Fact]
    public void TryLoad_reports_an_unreadable_template_without_throwing()
    {
        using var folder = new TestFolder();
        string jsonFile = WriteFile(folder.Path, "All_ROIs.json", TruncatedJson);

        bool loaded = ROIClassTools.TryLoadROIsFromFolder(folder.Path, new List<OntologyCodeClass>(), out List<ROIClass> rois, out string? error);

        Assert.False(loaded);
        Assert.Empty(rois);
        Assert.NotNull(error);
        Assert.Contains(jsonFile, error);
    }

    [Fact]
    public void TryLoad_returns_the_rois_of_a_readable_template()
    {
        using var folder = new TestFolder();
        Template(folder.Path, Roi("Bladder", "15900"), Roi("Rectum", "14544"));

        bool loaded = ROIClassTools.TryLoadROIsFromFolder(folder.Path, new List<OntologyCodeClass>(), out List<ROIClass> rois, out string? error);

        Assert.True(loaded);
        Assert.Null(error);
        Assert.Equal(new[] { "Bladder", "Rectum" }, rois.Select(r => r.ROIName));
    }

    /// <summary>An unreadable template with settings, and a library, whose bytes must not change.</summary>
    private static Dictionary<string, byte[]> UnreadableTemplateWithLibrary(TestFolder root, string templateJson, out string template, out string library)
    {
        template = root.Sub("Pelvis");
        library = root.Sub("Ontologies");
        WriteFile(template, "All_ROIs.json", templateJson);
        WriteFile(template, "Paths.txt", "C:\\Images\\CT\n");
        WriteFile(template, "DicomTags.txt", "Series Description\\CT Pelvis\n");
        OntologyTools.SaveOntologiesToFolder(new List<OntologyCodeClass> { new OntologyCodeClass("My structure", "999", "LOCAL") }, library);
        return Files(root.Path).ToDictionary(f => f, f => File.ReadAllBytes(Path.Combine(root.Path, f)));
    }

    private static void AssertUnchanged(TestFolder root, Dictionary<string, byte[]> before)
    {
        Assert.Equal(before.Keys.OrderBy(k => k, StringComparer.Ordinal), Files(root.Path));
        foreach (KeyValuePair<string, byte[]> file in before)
        {
            Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(root.Path, file.Key)));
        }
    }

    [Fact]
    public void Categorize_then_save_does_not_overwrite_an_unreadable_template()
    {
        using var root = new TestFolder();
        Dictionary<string, byte[]> before = UnreadableTemplateWithLibrary(root, TruncatedJson, out string template, out string library);
        var maker = new TemplateMaker();
        maker.set_onto_path(library);
        maker.define_path(template);
        maker.define_output(template);

        Assert.Throws<TemplateLoadException>(() => maker.categorize_folder());
        Assert.False(maker.is_template);
        var refused = Assert.Throws<TemplateLoadException>(() => maker.make_template());

        Assert.Contains("not saved", refused.Message);
        AssertUnchanged(root, before);
    }

    [Fact]
    public void A_new_build_into_a_folder_holding_an_unreadable_template_is_refused()
    {
        using var root = new TestFolder();
        Dictionary<string, byte[]> before = UnreadableTemplateWithLibrary(root, "", out string template, out string library);
        var builder = new TemplateMaker();
        builder.set_onto_path(library);
        builder.define_output(template);
        builder.ROIs.Add(Roi("Bladder", "15900"));

        var refused = Assert.Throws<TemplateLoadException>(() => builder.make_template());

        Assert.Equal(Path.Combine(template, "All_ROIs.json"), refused.FilePath);
        AssertUnchanged(root, before);
    }

    [Fact]
    public void Save_after_a_template_that_parses_but_cannot_be_rebuilt_is_refused()
    {
        using var root = new TestFolder();
        ROIClass roi = Roi("Bladder", "15900");
        roi.Ontology_Class = null;
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(new List<ROIClass> { roi }, Newtonsoft.Json.Formatting.Indented);
        Dictionary<string, byte[]> before = UnreadableTemplateWithLibrary(root, json, out string template, out string library);
        var maker = new TemplateMaker();
        maker.set_onto_path(library);
        maker.Ontologies = OntologyTools.LoadOntologiesFromFolder(library);
        maker.define_path(template);
        maker.define_output(template);

        Assert.Throws<TemplateLoadException>(() => maker.categorize_folder());
        Assert.Throws<TemplateLoadException>(() => maker.make_template());

        AssertUnchanged(root, before);
    }

    [Theory]
    [InlineData("Abdomen.import.golden.json", 17)]
    [InlineData("StructureTemplate.import.golden.json", 1)]
    public void Golden_import_json_loads_and_saves_back_unchanged(string golden, int count)
    {
        using var folder = new TestFolder();
        string original = File.ReadAllText(TestFolder.Data(golden));
        string template = folder.Sub("Template");
        File.WriteAllText(Path.Combine(template, "All_ROIs.json"), original);

        List<ROIClass> rois = ROIClassTools.LoadROIsFromFolder(template, new List<OntologyCodeClass>());
        string resaved = folder.Sub("Resaved");
        ROIClassTools.SaveROIsToFolder(rois, resaved);

        Assert.Equal(count, rois.Count);
        Assert.All(rois, r => Assert.NotNull(r.Ontology_Class));
        Assert.Equal(original.ReplaceLineEndings("\n").TrimEnd(), File.ReadAllText(Path.Combine(resaved, "All_ROIs.json")).ReplaceLineEndings("\n").TrimEnd());
    }

    [Fact]
    public void Golden_import_json_rois_share_the_matching_library_instances()
    {
        using var folder = new TestFolder();
        File.Copy(TestFolder.Data("Abdomen.import.golden.json"), Path.Combine(folder.Path, "All_ROIs.json"));
        var esophagus = new OntologyCodeClass("Esophagus", "7131", "FMA");
        var library = new List<OntologyCodeClass> { esophagus };

        List<ROIClass> rois = ROIClassTools.LoadROIsFromFolder(folder.Path, library);

        ROIClass first = rois[0];
        Assert.Equal("Esophagus", first.ROIName);
        Assert.Equal(("Organ", (byte)255, (byte)192, (byte)203), (first.ROI_Interpreted_type, first.R, first.G, first.B));
        Assert.Same(esophagus, first.Ontology_Class);
        Assert.Equal(17, rois.Count);
        Assert.Equal(new[] { "All_ROIs.json" }, Files(folder.Path));
    }
}
