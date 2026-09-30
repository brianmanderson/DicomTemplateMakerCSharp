using DicomTemplateMakerGUI.Shell;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class RecycleBinRuleTests
{
    private static DriveType Drives(string root) => root switch
    {
        @"C:\" => DriveType.Fixed,
        @"T:\" => DriveType.Network,
        @"E:\" => DriveType.Removable,
        @"R:\" => DriveType.CDRom,
        "/" => DriveType.Fixed,
        _ => DriveType.NoRootDirectory,
    };

    [Theory]
    [InlineData(@"C:\Templates\Brain")]
    [InlineData(@"c:/Templates/Brain")]
    [InlineData(@"\\?\C:\Templates\Brain")]
    public void Folders_on_a_local_fixed_drive_can_be_recycled(string folder)
    {
        Assert.Null(RecycleBinRule.WhyNotRecyclable(folder, Drives));
    }

    [Theory]
    [InlineData(@"\\server\RO\Templates\Prostate", "network share")]
    [InlineData("//server/RO/Templates/Prostate", "network share")]
    [InlineData(@"\\?\UNC\server\RO\Templates\Prostate", "network share")]
    [InlineData(@"T:\Templates\Prostate", @"network drive (T:\)")]
    [InlineData(@"E:\Templates\Prostate", @"removable drive (E:\)")]
    [InlineData(@"R:\Templates\Prostate", @"(R:\) has no Recycle Bin")]
    [InlineData(@"Q:\Templates\Prostate", @"(Q:\) has no Recycle Bin")]
    public void Folders_without_a_recycle_bin_are_named_with_the_reason(string folder, string expected)
    {
        // The shell deletes these permanently (and silently, with the flags "Delete selected" used) when asked to recycle.
        Assert.Contains(expected, RecycleBinRule.WhyNotRecyclable(folder, Drives), StringComparison.Ordinal);
    }

    [Fact]
    public void A_drive_check_that_fails_counts_as_no_recycle_bin()
    {
        Assert.NotNull(RecycleBinRule.WhyNotRecyclable(@"C:\Templates", _ => throw new IOException("device not ready")));
    }

    [Fact]
    public void Split_keeps_the_order_and_the_reasons()
    {
        (List<(string Name, string Folder)> recyclable, List<NotRecyclable> refused) = RecycleBinRule.Split(
            new[] { ("Brain", @"C:\t\Brain"), ("Prostate", @"\\server\t\Prostate"), ("Lung", @"C:\t\Lung") }, Drives);

        Assert.Equal(new[] { "Brain", "Lung" }, recyclable.Select(r => r.Name));
        NotRecyclable prostate = Assert.Single(refused);
        Assert.Equal(("Prostate", @"\\server\t\Prostate"), (prostate.Name, prostate.Folder));
        Assert.Contains("network share", prostate.Reason, StringComparison.Ordinal);
    }
}
