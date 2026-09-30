using DicomTemplateMakerGUI.Editors;
using Newtonsoft.Json;
using ROIOntologyClass;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class NameRulesTests
{
    [Theory]
    [InlineData("AbdPelv_Prostate")]
    [InlineData("Head and Neck")]
    [InlineData("Lung.v2")]
    [InlineData("CONSOLE")]
    public void Usable_template_names_pass(string name)
    {
        Assert.Null(NameRules.TemplateNameProblem(name));
    }

    [Theory]
    [InlineData(null, "Enter a template name")]
    [InlineData("", "Enter a template name")]
    [InlineData("   ", "Enter a template name")]
    [InlineData(" Lung", "starts or ends with a space")]
    [InlineData("Lung ", "starts or ends with a space")]
    [InlineData(".", "cannot be used")]
    [InlineData("..", "cannot be used")]
    [InlineData("../evil", "cannot contain: /.")]
    [InlineData("a\\b", "cannot contain: \\")]
    [InlineData("What?", "cannot contain: ?")]
    [InlineData("A:B*C", "cannot contain: : *")]
    [InlineData("Tab\there", "control character")]
    [InlineData("Lung.", "ends with a dot")]
    [InlineData("CON", "reserved by Windows")]
    [InlineData("nul.txt", "reserved by Windows")]
    [InlineData("com1", "reserved by Windows")]
    [InlineData("LPT9", "reserved by Windows")]
    [InlineData("Ontologies", "ontology library")]
    [InlineData("ontologies", "ontology library")]
    public void Template_names_that_cannot_be_a_folder_are_refused_with_a_reason(string? name, string reason)
    {
        string? problem = NameRules.TemplateNameProblem(name);

        Assert.NotNull(problem);
        Assert.Contains(reason, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_template_name_longer_than_16_characters_gets_a_warning_with_the_label_it_becomes()
    {
        Assert.Null(NameRules.TemplateNameWarning("Exactly16Chars__"));

        string? warning = NameRules.TemplateNameWarning("AbdPelv_Prostate_SIB");

        Assert.NotNull(warning);
        Assert.Contains("20 characters", warning, StringComparison.Ordinal);
        Assert.Contains("\"AbdPelv_Prostate\"", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void An_roi_name_longer_than_16_characters_gets_a_warning_but_is_allowed()
    {
        var rois = new List<ROIClass>();

        Assert.Null(NameRules.RoiNameProblem("Parotid_Left_Superficial", rois, null));
        string? warning = NameRules.RoiNameWarning("Parotid_Left_Superficial");
        Assert.NotNull(warning);
        Assert.Contains("\"Parotid_Left_Sup\"", warning, StringComparison.Ordinal);
        Assert.Null(NameRules.RoiNameWarning("Parotid_L"));
    }

    [Theory]
    [InlineData("brain")]
    [InlineData("BRAIN")]
    [InlineData("Brain")]
    public void A_duplicate_roi_name_is_refused_ignoring_case(string name)
    {
        var brain = Roi("Brain");
        var rois = new List<ROIClass> { brain, Roi("PTV_High") };

        string? problem = NameRules.RoiNameProblem(name, rois, null);

        Assert.NotNull(problem);
        Assert.Contains("already named \"Brain\"", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void An_roi_may_keep_its_own_name_or_change_its_case()
    {
        var brain = Roi("Brain");
        var rois = new List<ROIClass> { brain, Roi("PTV_High") };

        Assert.Null(NameRules.RoiNameProblem("Brain", rois, brain));
        Assert.Null(NameRules.RoiNameProblem("BRAIN", rois, brain));
        Assert.NotNull(NameRules.RoiNameProblem("ptv_high", rois, brain));
    }

    [Theory]
    [InlineData("", "Enter an ROI name")]
    [InlineData(" Brain", "starts or ends with a space")]
    [InlineData("Brain\\Stem", "backslash")]
    [InlineData("Line\nbreak", "control character")]
    public void Unusable_roi_names_are_refused_with_a_reason(string name, string reason)
    {
        string? problem = NameRules.RoiNameProblem(name, new List<ROIClass>(), null);

        Assert.NotNull(problem);
        Assert.Contains(reason, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void An_roi_name_longer_than_dicom_allows_is_refused()
    {
        Assert.Null(NameRules.RoiNameProblem(new string('a', 64), new List<ROIClass>(), null));
        Assert.Contains("at most 64", NameRules.RoiNameProblem(new string('a', 65), new List<ROIClass>(), null), StringComparison.Ordinal);
    }

    [Fact]
    public void An_roi_with_a_null_name_in_the_list_does_not_break_the_duplicate_check()
    {
        // What Newtonsoft makes of a hand-edited All_ROIs.json that says "ROIName": null.
        ROIClass broken = JsonConvert.DeserializeObject<ROIClass>("{\"ROIName\": null}") ?? throw new InvalidOperationException();
        var rois = new List<ROIClass> { broken, Roi("Brain") };

        Assert.Null(NameRules.RoiNameProblem("Heart", rois, null));
    }

    private static ROIClass Roi(string name) => new(1, 2, 3, name, "ORGAN", new OntologyCodeClass(name, "1", "FMA"));
}
