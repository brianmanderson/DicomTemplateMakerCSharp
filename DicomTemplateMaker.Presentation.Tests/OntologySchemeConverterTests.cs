using DicomTemplateMaker.Presentation.Tests.Fakes;
using DicomTemplateMakerGUI.Editors;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.Shell;
using ROIOntologyClass;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class OntologySchemeConverterTests : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> FmaToSct = new Dictionary<string, string>
    {
        ["7088"] = "80891009", // heart
        ["50801"] = "12738006", // brain
        ["7197"] = "10200004", // liver
    };

    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    [Theory]
    [InlineData("FMA", "FMA", false)]
    [InlineData("SCT", "sct", false)]
    [InlineData("FMA", "SCT", true)]
    [InlineData("SCT", "FMA", true)]
    [InlineData(null, "SCT", false)]
    [InlineData("FMA", null, false)]
    public void Converting_is_only_possible_between_two_different_schemes(string? from, string? to, bool expected)
    {
        Assert.Equal(expected, OntologySchemeConverter.CanConvert(from, to));
    }

    [Fact]
    public void Converting_a_scheme_into_itself_is_refused_before_anything_changes()
    {
        TemplateMaker template = InMemory("Brain", Code("Heart", "7088", "FMA"));

        Assert.Throws<ArgumentException>(() => OntologySchemeConverter.Plan(new[] { template }, new List<OntologyCodeClass>(), "FMA", "FMA", FmaToSct));
        Assert.Throws<ArgumentException>(() => OntologySchemeConverter.Apply(new[] { template }, folder.Path, "FMA", "FMA", FmaToSct, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("7088", template.ROIs[0].Ontology_Class?.CodeValue);
    }

    [Fact]
    public void The_plan_counts_templates_rois_and_library_entries_and_what_has_no_equivalent()
    {
        TemplateMaker brain = InMemory("Brain", Code("Heart", "7088", "FMA"), Code("Brain", "50801", "FMA"), Code("Eye", "12514", "FMA"), Code("PTV", "PTV", "99VMS_STRUCTCODE"));
        TemplateMaker lung = InMemory("Lung", Code("Heart", "7088", "FMA"));
        TemplateMaker none = InMemory("Sct", Code("Heart", "80891009", "SCT"));
        var library = new List<OntologyCodeClass> { Code("Heart", "7088", "FMA"), Code("Liver", "7197", "FMA"), Code("Odd", "99999", "FMA"), Code("Brain", "12738006", "SCT") };

        SchemeConversionPlan plan = OntologySchemeConverter.Plan(new[] { brain, lung, none }, library, "FMA", "SCT", FmaToSct);

        Assert.Equal(3, plan.RoiCount);
        Assert.Equal(2, plan.TemplateCount);
        Assert.Equal(new[] { ("Brain", 2), ("Lung", 1) }, plan.Templates);
        Assert.Equal(2, plan.LibraryEntryCount);
        Assert.Equal(1, plan.RoisWithoutEquivalent);
        Assert.Equal(1, plan.LibraryEntriesWithoutEquivalent);
        Assert.False(plan.IsEmpty);
        Assert.Equal("7088", brain.ROIs[0].Ontology_Class?.CodeValue); // planning changes nothing
    }

    [Fact]
    public void Codes_without_a_code_value_or_scheme_do_not_break_the_conversion()
    {
        var noValue = new OntologyCodeClass { CodeMeaning = "No value", CodeValue = null, Scheme = "FMA" };
        var noScheme = new OntologyCodeClass { CodeMeaning = "No scheme", CodeValue = "7088", Scheme = null };
        TemplateMaker template = InMemory("Brain", noValue, noScheme, Code("Heart", "7088", "FMA"));
        template.ROIs.Add(new ROIClass(1, 2, 3, "No code", "ORGAN", noValue) { Ontology_Class = null });

        SchemeConversionPlan plan = OntologySchemeConverter.Plan(new[] { template }, new[] { noValue, noScheme }, "FMA", "SCT", FmaToSct);
        int changed = OntologySchemeConverter.ConvertRois(template.ROIs, "FMA", "SCT", FmaToSct);

        Assert.Equal(1, plan.RoiCount);
        Assert.Equal(1, plan.RoisWithoutEquivalent);
        Assert.Equal(0, plan.LibraryEntryCount);
        Assert.Equal(1, changed);
        Assert.Null(noValue.CodeValue);
        Assert.Equal("7088", noScheme.CodeValue);
    }

    [Fact]
    public void Rois_sharing_one_code_object_are_counted_each_but_converted_once()
    {
        OntologyCodeClass heart = Code("Heart", "7088", "FMA");
        TemplateMaker template = InMemory("Brain", heart, heart);

        int changed = OntologySchemeConverter.ConvertRois(template.ROIs, "FMA", "SCT", FmaToSct);

        Assert.Equal(2, changed);
        Assert.Equal("80891009", heart.CodeValue);
        Assert.Equal("SCT", heart.Scheme);
    }

    [Fact]
    public void A_converted_library_entry_whose_new_code_is_already_listed_is_merged()
    {
        var library = new List<OntologyCodeClass> { Code("Heart", "7088", "FMA"), Code("Heart", "80891009", "SCT"), Code("Liver", "7197", "FMA") };

        (int changed, int merged) = OntologySchemeConverter.ConvertLibrary(library, "FMA", "SCT", FmaToSct);

        Assert.Equal(2, changed);
        Assert.Equal(1, merged);
        Assert.Equal(new[] { "80891009", "10200004" }, library.Select(o => o.CodeValue));
        Assert.All(library, o => Assert.Equal("SCT", o.Scheme));
    }

    [Fact]
    public void Apply_saves_the_library_and_each_changed_template_and_reports_the_counts()
    {
        Templates.Make(folder.Path, "Brain", rois: new[] { new ROIClass(1, 2, 3, "Heart", "ORGAN", Code("Heart", "7088", "FMA")), new ROIClass(1, 2, 3, "Brain", "ORGAN", Code("Brain", "50801", "FMA")) });
        Templates.Make(folder.Path, "Sct", rois: new[] { new ROIClass(1, 2, 3, "PTV", "PTV", Code("PTV", "PTV", "99VMS_STRUCTCODE")) });
        string ontoPath = Path.Combine(folder.Path, "Ontologies");
        TemplateScanResult scan = TemplateLibraryScanner.Scan(folder.Path, cancellationToken: TestContext.Current.CancellationToken);

        SchemeConversionResult result = OntologySchemeConverter.Apply(scan.Templates, ontoPath, "FMA", "SCT", FmaToSct, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.RoisChanged);
        Assert.Equal(new[] { "Brain" }, result.TemplatesSaved);
        Assert.Equal(2, result.LibraryEntriesChanged);
        Assert.Empty(result.Failures);
        List<ROIClass> saved = ROIClassTools.LoadROIsFromFolder(Path.Combine(folder.Path, "Brain"), new List<OntologyCodeClass>());
        Assert.Equal(new[] { "80891009", "12738006" }, saved.Select(r => r.Ontology_Class?.CodeValue));
        Assert.All(saved, r => Assert.Equal("SCT", r.Ontology_Class?.Scheme));
        List<OntologyCodeClass> library = OntologyTools.LoadOntologiesFromFolder(ontoPath);
        Assert.DoesNotContain(library, o => o.Scheme == "FMA");
        Assert.Single(library, o => o.CodeValue == "80891009");
    }

    [Fact]
    public void Apply_stops_before_any_template_changes_when_the_library_cannot_be_read()
    {
        string template = Templates.Make(folder.Path, "Brain", rois: new[] { new ROIClass(1, 2, 3, "Heart", "ORGAN", Code("Heart", "7088", "FMA")) });
        string before = File.ReadAllText(Path.Combine(template, "All_ROIs.json"));
        TemplateScanResult scan = TemplateLibraryScanner.Scan(folder.Path, cancellationToken: TestContext.Current.CancellationToken);
        string ontoPath = Path.Combine(folder.Path, "Ontologies");
        File.WriteAllText(Path.Combine(ontoPath, "All_Ontologies.json"), "{ broken");

        Assert.Throws<TemplateLoadException>(() => OntologySchemeConverter.Apply(scan.Templates, ontoPath, "FMA", "SCT", FmaToSct, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(before, File.ReadAllText(Path.Combine(template, "All_ROIs.json")));
        Assert.Equal("{ broken", File.ReadAllText(Path.Combine(ontoPath, "All_Ontologies.json")));
    }

    [Fact]
    public void A_template_that_cannot_be_saved_is_reported_and_the_others_go_on()
    {
        Templates.Make(folder.Path, "Bad", rois: new[] { new ROIClass(1, 2, 3, "Heart", "ORGAN", Code("Heart", "7088", "FMA")) });
        Templates.Make(folder.Path, "Good", rois: new[] { new ROIClass(1, 2, 3, "Heart", "ORGAN", Code("Heart", "7088", "FMA")) });
        TemplateScanResult scan = TemplateLibraryScanner.Scan(folder.Path, cancellationToken: TestContext.Current.CancellationToken);
        // Damaged after it was read: make_template refuses to replace an unreadable All_ROIs.json.
        File.WriteAllText(Path.Combine(folder.Path, "Bad", "All_ROIs.json"), "not json");

        SchemeConversionResult result = OntologySchemeConverter.Apply(scan.Templates, Path.Combine(folder.Path, "Ontologies"), "FMA", "SCT", FmaToSct, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Good" }, result.TemplatesSaved);
        string failure = Assert.Single(result.Failures);
        Assert.StartsWith("Bad: ", failure, StringComparison.Ordinal);
        Assert.Equal("not json", File.ReadAllText(Path.Combine(folder.Path, "Bad", "All_ROIs.json")));
    }

    [Fact]
    public void The_shipped_table_maps_both_ways()
    {
        var table = new FMAID_SNOMED_OntologyClass(Templates.RepositoryFile("DicomTemplateMakerGUI", "FMA_SNOMEDCT_Key.txt"));

        Assert.Equal("15825003", OntologySchemeConverter.MapFrom(table, "FMA")["3734"]);
        Assert.Equal("3734", OntologySchemeConverter.MapFrom(table, "SCT")["15825003"]);
        Assert.Throws<ArgumentException>(() => OntologySchemeConverter.MapFrom(table, "99VMS"));
        Assert.Equal(new[] { "FMA", "SCT" }, OntologySchemeConverter.Choices.Select(c => c.Designator));
    }

    private static OntologyCodeClass Code(string meaning, string value, string scheme) => new(meaning, value, scheme);

    private static TemplateMaker InMemory(string name, params OntologyCodeClass[] codes)
    {
        var maker = new TemplateMaker { TemplateName = name };
        int i = 0;
        foreach (OntologyCodeClass code in codes)
        {
            maker.ROIs.Add(new ROIClass(1, 2, 3, name + "_" + i++, "ORGAN", code));
        }

        return maker;
    }
}
