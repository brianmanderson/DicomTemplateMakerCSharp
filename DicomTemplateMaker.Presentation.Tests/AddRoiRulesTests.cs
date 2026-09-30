using DicomTemplateMakerGUI.Editors;
using ROIOntologyClass;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class AddRoiRulesTests
{
    private static readonly List<ROIClass> Rois = new() { new ROIClass(255, 0, 0, "Bladder", "ORGAN", new OntologyCodeClass("Bladder", "15900", "FMA")) };

    [Fact]
    public void Nothing_can_be_added_before_the_template_is_built()
    {
        Assert.Equal(new AddRoiState(false, null, null, AddRoiRules.BuildFirstHint), AddRoiRules.Decide(false, "Rectum", Rois, true, true));
    }

    [Fact]
    public void An_empty_name_is_a_hint_not_an_error()
    {
        Assert.Equal(new AddRoiState(false, null, null, AddRoiRules.NameHint), AddRoiRules.Decide(true, string.Empty, Rois, true, true));
        Assert.Equal(new AddRoiState(false, null, null, AddRoiRules.NameHint), AddRoiRules.Decide(true, null, Rois, true, true));
    }

    [Fact]
    public void A_name_that_cannot_be_used_blocks_and_hides_the_later_steps()
    {
        AddRoiState duplicate = AddRoiRules.Decide(true, "bladder", Rois, false, false);

        Assert.False(duplicate.CanAdd);
        Assert.NotNull(duplicate.Problem);
        Assert.Null(duplicate.Hint);
        Assert.Null(duplicate.Warning);
    }

    [Fact]
    public void A_code_and_then_a_type_must_be_chosen()
    {
        Assert.Equal(new AddRoiState(false, null, null, AddRoiRules.CodeHint), AddRoiRules.Decide(true, "Rectum", Rois, false, true));
        Assert.Equal(new AddRoiState(false, null, null, AddRoiRules.TypeHint), AddRoiRules.Decide(true, "Rectum", Rois, true, false));
        Assert.Equal(new AddRoiState(true, null, null, null), AddRoiRules.Decide(true, "Rectum", Rois, true, true));
    }

    [Fact]
    public void A_long_name_can_be_added_with_its_warning_shown()
    {
        string name = "Rectum_with_a_long_name";
        AddRoiState state = AddRoiRules.Decide(true, name, Rois, true, true);

        Assert.True(state.CanAdd);
        Assert.Equal(NameRules.RoiNameWarning(name), state.Warning);
        Assert.NotNull(state.Warning);
    }
}
