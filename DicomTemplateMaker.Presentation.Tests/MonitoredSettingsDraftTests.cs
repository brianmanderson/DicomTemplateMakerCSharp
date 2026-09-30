using DicomTemplateMakerGUI.Editors;
using DicomTemplateMakerGUI.Services;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class MonitoredSettingsDraftTests
{
    private static TemplateMaker Template()
    {
        var maker = new TemplateMaker { TemplateName = "Brain" };
        maker.Paths.Add(@"\\server\ct\brain");
        maker.DicomTags["Series Description"] = new List<string> { "CT", "Brain" };
        return maker;
    }

    [Fact]
    public void Edits_change_only_the_draft_until_applied()
    {
        TemplateMaker maker = Template();
        var draft = MonitoredSettingsDraft.From(maker);

        Assert.Null(draft.AddPath(@"D:\incoming"));
        Assert.True(draft.RemovePath(@"\\server\ct\brain"));
        Assert.Null(draft.AddRequirement("Study Description", "Head"));
        Assert.True(draft.RemoveRequirement("Series Description", "CT"));

        Assert.True(draft.IsDirty);
        Assert.Equal(new[] { @"\\server\ct\brain" }, maker.Paths);
        Assert.Equal(new[] { "CT", "Brain" }, maker.DicomTags["Series Description"]);
        Assert.False(maker.DicomTags.ContainsKey("Study Description"));

        draft.ApplyTo(maker);

        Assert.Equal(new[] { @"D:\incoming" }, maker.Paths);
        Assert.Equal(new[] { "Brain" }, maker.DicomTags["Series Description"]);
        Assert.Equal(new[] { "Head" }, maker.DicomTags["Study Description"]);
    }

    [Fact]
    public void Applying_keeps_the_template_list_instances()
    {
        TemplateMaker maker = Template();
        List<string> paths = maker.Paths;
        var draft = MonitoredSettingsDraft.From(maker);
        draft.AddPath(@"D:\incoming");

        draft.ApplyTo(maker);

        Assert.Same(paths, maker.Paths);
    }

    [Fact]
    public void A_snapshot_restores_the_template_after_a_refused_save()
    {
        TemplateMaker maker = Template();
        var original = MonitoredSettingsDraft.From(maker);
        var draft = MonitoredSettingsDraft.From(maker);
        draft.AddPath(@"D:\incoming");
        draft.RemoveRequirement("Series Description", "CT");
        draft.ApplyTo(maker);

        original.ApplyTo(maker);

        Assert.Equal(new[] { @"\\server\ct\brain" }, maker.Paths);
        Assert.Equal(new[] { "CT", "Brain" }, maker.DicomTags["Series Description"]);
    }

    [Theory]
    [InlineData(@"\\server\ct\brain")]
    [InlineData(@"\\SERVER\CT\Brain")]
    [InlineData(@"\\server\ct\brain\")]
    [InlineData("//server/ct/brain")]
    [InlineData(@" \\server\ct\brain ")]
    public void A_folder_that_is_already_monitored_is_refused(string path)
    {
        var draft = MonitoredSettingsDraft.From(Template());

        string? problem = draft.AddPath(path);

        Assert.Contains("already monitored", problem, StringComparison.Ordinal);
        Assert.Single(draft.Paths);
        Assert.False(draft.IsDirty);
    }

    [Fact]
    public void Drive_roots_keep_their_separator_when_compared()
    {
        Assert.True(MonitoredSettingsDraft.SamePath(@"C:\", "c:/"));
        Assert.False(MonitoredSettingsDraft.SamePath(@"C:\", @"C:\data"));
        Assert.False(MonitoredSettingsDraft.SamePath(@"C:\data", @"C:\data2"));
    }

    [Theory]
    [InlineData("Series Description", "ct", "already requires \"ct\"")]
    [InlineData("Series Description", " CT ", "already requires \"CT\"")]
    [InlineData("Series Description", "", "blank requirement never matches")]
    [InlineData("Series Description", "   ", "blank requirement never matches")]
    [InlineData("Series Description", "CT\\Head", "backslash")]
    [InlineData("Series Description", "CT\nHead", "line break")]
    [InlineData("Modality", "CT", "Choose Series Description or Study Description")]
    public void Unusable_requirements_are_refused_with_a_reason(string key, string value, string reason)
    {
        var draft = MonitoredSettingsDraft.From(Template());

        string? problem = draft.AddRequirement(key, value);

        Assert.Contains(reason, problem, StringComparison.Ordinal);
        Assert.False(draft.IsDirty);
    }

    [Fact]
    public void The_same_value_under_the_other_key_is_allowed_and_values_are_trimmed()
    {
        var draft = MonitoredSettingsDraft.From(Template());

        Assert.Null(draft.AddRequirement("Study Description", "  CT "));

        Assert.Contains(("Study Description", "CT"), draft.Requirements);
    }

    [Fact]
    public void Deleting_requirements_removes_exactly_that_value_and_then_the_empty_key()
    {
        var draft = MonitoredSettingsDraft.From(Template());

        Assert.False(draft.RemoveRequirement("Study Description", "CT"));
        Assert.False(draft.RemoveRequirement("Series Description", "Lung"));
        Assert.False(draft.IsDirty);
        Assert.True(draft.RemoveRequirement("Series Description", "CT"));
        Assert.True(draft.RemoveRequirement("Series Description", "Brain"));
        Assert.False(draft.RemoveRequirement("Series Description", "Brain"));

        Assert.Empty(draft.Requirements);
        TemplateMaker maker = Template();
        draft.ApplyTo(maker);
        Assert.Empty(maker.DicomTags);
    }
}
