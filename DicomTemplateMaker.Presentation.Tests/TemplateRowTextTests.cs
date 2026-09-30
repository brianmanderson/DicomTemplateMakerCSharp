using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.Shell;
using ROIOntologyClass;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class TemplateRowTextTests
{
    [Fact]
    public void The_tooltip_shows_folder_rois_paths_requirements_and_warnings()
    {
        var maker = new TemplateMaker { TemplateName = "Brain" };
        maker.define_path("/t/Brain");
        maker.ROIs.Add(new ROIClass(1, 2, 3, "Brain", "ORGAN", new OntologyCodeClass("Brain", "50801", "FMA")));
        maker.Paths.Add(@"\\server\ct");
        maker.DicomTags["Series Description"] = new List<string> { "CT", " " };
        maker.LoadWarnings.Add("ROIs/Broken.txt: bad colour");

        string text = TemplateRowText.Tooltip(maker);

        Assert.StartsWith("Brain", text, StringComparison.Ordinal);
        Assert.Contains("Folder: /t/Brain", text, StringComparison.Ordinal);
        Assert.Contains("1 ROI", text, StringComparison.Ordinal);
        Assert.Contains(@"\\server\ct", text, StringComparison.Ordinal);
        Assert.Contains("Series Description contains any of: \"CT\"", text, StringComparison.Ordinal);
        Assert.Contains("ROIs/Broken.txt: bad colour", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_tooltip_warns_when_no_folder_is_monitored()
    {
        var maker = new TemplateMaker { TemplateName = "Brain" };

        string text = TemplateRowText.Tooltip(maker);

        Assert.Contains("No monitored folders", text, StringComparison.Ordinal);
        Assert.Contains("DICOM requirements: none", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("AbdPelv_Prostate", "prostate", true)]
    [InlineData("AbdPelv_Prostate", "PROSTATE", true)]
    [InlineData("AbdPelv_Prostate", "  pelv ", true)]
    [InlineData("AbdPelv_Prostate", "", true)]
    [InlineData("AbdPelv_Prostate", null, true)]
    [InlineData("AbdPelv_Prostate", "lung", false)]
    [InlineData(null, "lung", false)]
    public void The_search_ignores_case(string? name, string? query, bool expected)
    {
        Assert.Equal(expected, TemplateSearch.Matches(name, query));
    }

    [Fact]
    public void A_selection_separates_shown_and_hidden_selected_rows()
    {
        var rows = new[] { ("A", true, true), ("B", true, false), ("C", false, true), ("D", false, false) };

        SelectionScope<(string Name, bool Selected, bool Visible)> scope = SelectionScope<(string Name, bool Selected, bool Visible)>.Of(rows, r => r.Selected, r => r.Visible);

        Assert.Equal(new[] { "A" }, scope.VisibleSelected.Select(r => r.Name));
        Assert.Equal(new[] { "B" }, scope.HiddenSelected.Select(r => r.Name));
        Assert.Equal(4, scope.All.Count);
    }
}
