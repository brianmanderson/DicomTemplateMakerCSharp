using DicomTemplateMaker.Presentation.Tests.Fakes;
using DicomTemplateMakerGUI.Editors;
using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class EditorMessagesTests : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> FmaToSct = new Dictionary<string, string> { ["7088"] = "80891009" };
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    [Fact]
    public void Replacing_a_template_says_what_is_replaced_and_what_is_kept()
    {
        string text = EditorMessages.ConfirmReplaceTemplate("Prostate", @"C:\Templates\Prostate", 12, 0);

        Assert.Contains("\"Prostate\" already exists in C:\\Templates\\Prostate (12 ROIs)", text, StringComparison.Ordinal);
        Assert.Contains("replaces its ROIs with the ROIs in this window (none yet)", text, StringComparison.Ordinal);
        Assert.Contains("monitored folders and DICOM requirements are kept", text, StringComparison.Ordinal);
        Assert.EndsWith("Replace the ROIs of \"Prostate\"?", text, StringComparison.Ordinal);
        Assert.DoesNotContain("(", EditorMessages.ConfirmReplaceTemplate("P", "f", null, 3).Split('.')[0], StringComparison.Ordinal);
        Assert.Contains("with the 3 ROIs in this window", EditorMessages.ConfirmReplaceTemplate("P", "f", null, 3), StringComparison.Ordinal);
    }

    [Fact]
    public void The_airtable_confirmation_names_the_table_and_says_shared_fields_change_for_every_site()
    {
        string text = EditorMessages.ConfirmAirtableWrite("UCSD", "Prostate", 1, saveFirst: true);

        Assert.StartsWith("Update table \"UCSD\" with template \"Prostate\" (1 ROI)?", text, StringComparison.Ordinal);
        Assert.Contains("shared fields such as colour", text, StringComparison.Ordinal);
        Assert.Contains("change for every site", text, StringComparison.Ordinal);
        Assert.Contains("saved first", text, StringComparison.Ordinal);
        Assert.DoesNotContain("saved first", EditorMessages.ConfirmAirtableWrite("UCSD", "Prostate", 2, saveFirst: false), StringComparison.Ordinal);
    }

    [Fact]
    public void Deleting_an_entry_in_use_names_the_templates_and_says_it_comes_back()
    {
        var heart = new OntologyCodeClass("Heart", "7088", "FMA");

        string used = EditorMessages.ConfirmDeleteOntology(heart, new[] { "Breast", "Lung" });
        string unused = EditorMessages.ConfirmDeleteOntology(heart, Array.Empty<string>());

        Assert.Contains("Delete \"Heart\" (code 7088, FMA)", used, StringComparison.Ordinal);
        Assert.Contains("used by 2 templates", used, StringComparison.Ordinal);
        Assert.Contains("Breast", used, StringComparison.Ordinal);
        Assert.Contains("added back to the library", used, StringComparison.Ordinal);
        Assert.DoesNotContain("used by", unused, StringComparison.Ordinal);
        Assert.Contains("saved when you press Save", unused, StringComparison.Ordinal);
    }

    [Fact]
    public void The_conversion_confirmation_and_report_give_the_counts()
    {
        var brain = new TemplateMaker { TemplateName = "Brain" };
        brain.ROIs.Add(new ROIClass(1, 2, 3, "Heart", "ORGAN", new OntologyCodeClass("Heart", "7088", "FMA")));
        brain.ROIs.Add(new ROIClass(1, 2, 3, "Eye", "ORGAN", new OntologyCodeClass("Eye", "12514", "FMA")));
        var library = new[] { new OntologyCodeClass("Heart", "7088", "FMA") };
        SchemeConversionPlan plan = OntologySchemeConverter.Plan(new[] { brain }, library, "FMA", "SCT", FmaToSct);

        string confirm = EditorMessages.ConfirmSchemeConversion(plan, "FMA", "SNOMED CT");

        Assert.StartsWith("Convert codes from FMA to SNOMED CT?", confirm, StringComparison.Ordinal);
        Assert.Contains("1 ROI in 1 template: Brain (1)", confirm, StringComparison.Ordinal);
        Assert.Contains("1 entry of the ontology library", confirm, StringComparison.Ordinal);
        Assert.Contains("All_ROIs.json is replaced", confirm, StringComparison.Ordinal);
        Assert.Contains("no SNOMED CT equivalent: 1 ROI and 0 library entries", confirm, StringComparison.Ordinal);

        Templates.Make(folder.Path, "Brain", rois: brain.ROIs);
        SchemeConversionResult result = OntologySchemeConverter.Apply(DicomTemplateMakerGUI.Shell.TemplateLibraryScanner.Scan(folder.Path, cancellationToken: TestContext.Current.CancellationToken).Templates, Path.Combine(folder.Path, "Ontologies"), "FMA", "SCT", FmaToSct, cancellationToken: TestContext.Current.CancellationToken);
        string report = EditorMessages.DescribeSchemeConversion(result, "FMA", "SNOMED CT");

        Assert.StartsWith("Converted from FMA to SNOMED CT: 1 ROI in 1 template, and 1 library entry.", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_to_convert_is_said_plainly()
    {
        var plan = OntologySchemeConverter.Plan(Array.Empty<TemplateMaker>(), Array.Empty<OntologyCodeClass>(), "SCT", "FMA", FmaToSct);

        Assert.True(plan.IsEmpty);
        Assert.Contains("nothing was changed", EditorMessages.NothingToConvert(plan, "SNOMED CT", "FMA"), StringComparison.Ordinal);
    }

    [Fact]
    public void Other_messages_state_the_consequence()
    {
        Assert.Contains("Yes saves them. No discards them.", EditorMessages.UnsavedChanges("template \"Brain\""), StringComparison.Ordinal);
        Assert.Contains("was left as it was", EditorMessages.RenameFailed(@"C:\T\Brain", "Head", "Access denied"), StringComparison.Ordinal);
        Assert.Contains("Fix this entry first", EditorMessages.OntologyNotSaved(new[] { "Heart: Enter a code value" }), StringComparison.Ordinal);
        string rename = EditorMessages.RenameConsequences("Brain");
        Assert.Contains("keep the old name (Brain_UID", rename, StringComparison.Ordinal);
        Assert.Contains("writes a new RT", rename, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_template_rois_are_counted_without_changing_anything()
    {
        string template = Templates.Make(folder.Path, "Brain");
        string broken = folder.Sub("Broken");
        File.WriteAllText(Path.Combine(broken, "All_ROIs.json"), "not json");

        Assert.Equal(2, ExistingTemplate.CountRois(template));
        Assert.Null(ExistingTemplate.CountRois(broken));
        Assert.Null(ExistingTemplate.CountRois(folder.Sub("Empty")));
        Assert.Equal("not json", File.ReadAllText(Path.Combine(broken, "All_ROIs.json")));
    }
}
