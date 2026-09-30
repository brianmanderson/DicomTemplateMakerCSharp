using DicomTemplateMakerGUI.Editors;
using ROIOntologyClass;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class RoiListTests
{
    [Fact]
    public void There_is_one_list_of_interpreted_types_with_no_blank_entry()
    {
        Assert.Equal(22, InterpretedTypes.All.Count);
        Assert.Equal(InterpretedTypes.All.Count, InterpretedTypes.All.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(InterpretedTypes.All, string.IsNullOrWhiteSpace);
        Assert.Contains("MARKER", InterpretedTypes.All);
        Assert.Contains(InterpretedTypes.Default, InterpretedTypes.All);
    }

    [Theory]
    [InlineData("ORGAN", "ORGAN")]
    [InlineData("Organ", "ORGAN")]
    [InlineData(" ptv ", "PTV")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("FOO", null)]
    public void Interpreted_types_are_found_ignoring_case(string? value, string? expected)
    {
        Assert.Equal(expected, InterpretedTypes.Find(value));
    }

    [Fact]
    public void A_blank_or_unknown_interpreted_type_is_a_problem_to_fix()
    {
        Assert.Contains("No interpreted type", InterpretedTypes.Problem(null), StringComparison.Ordinal);
        Assert.Contains("No interpreted type", InterpretedTypes.Problem(" "), StringComparison.Ordinal);
        Assert.Contains("Unknown interpreted type \"FOO\"", InterpretedTypes.Problem("FOO"), StringComparison.Ordinal);
        Assert.Null(InterpretedTypes.Problem("Organ"));
    }

    [Fact]
    public void Rois_without_an_interpreted_type_or_ontology_are_listed_and_searched_without_failing()
    {
        var noType = Roi("Mystery", null);
        var noCode = Roi("Heart", "ORGAN");
        noCode.Ontology_Class = null;
        var rois = new List<ROIClass> { noType, noCode, Roi("PTV_High", "PTV") };

        Assert.Equal(3, RoiList.Arrange(rois, null).Count);
        Assert.Equal(new[] { "Mystery" }, RoiList.Arrange(rois, "myst").Select(r => r.ROIName));
        Assert.Equal(new[] { "Heart" }, RoiList.Arrange(rois, "organ").Select(r => r.ROIName));
    }

    [Theory]
    [InlineData("PTV")]
    [InlineData("ptv")]
    [InlineData(" Ptv_h ")]
    public void The_search_ignores_case_and_surrounding_spaces(string query)
    {
        var rois = new List<ROIClass> { Roi("PTV_High", "PTV"), Roi("Brain", "ORGAN") };

        Assert.Equal(new[] { "PTV_High" }, RoiList.Arrange(rois, query).Select(r => r.ROIName));
    }

    [Fact]
    public void The_search_also_matches_the_ontology_name()
    {
        var rois = new List<ROIClass> { Roi("Lt_Parotid", "ORGAN", "Left parotid gland"), Roi("Brain", "ORGAN") };

        Assert.Equal(new[] { "Lt_Parotid" }, RoiList.Arrange(rois, "gland").Select(r => r.ROIName));
    }

    [Fact]
    public void Included_rois_come_first_then_ptvs_ctvs_gtvs_and_the_rest_by_name()
    {
        var excludedPtv = Roi("PTV_Low", "PTV");
        excludedPtv.Include = false;
        var rois = new List<ROIClass>
        {
            Roi("Spinal_Cord", "ORGAN"),
            excludedPtv,
            Roi("GTV", "GTV"),
            Roi("CTV_60", "ctv"),
            Roi("Brain", "ORGAN"),
            Roi("PTV_60", "PTV"),
            Roi("Blank", null),
        };

        IEnumerable<string> order = RoiList.Arrange(rois, "").Select(r => r.ROIName);

        Assert.Equal(new[] { "PTV_60", "CTV_60", "GTV", "Blank", "Brain", "Spinal_Cord", "PTV_Low" }, order);
    }

    private static ROIClass Roi(string name, string? type, string? meaning = null) => new(1, 2, 3, name, type, new OntologyCodeClass(meaning ?? name, name + "_code", "FMA"));
}
