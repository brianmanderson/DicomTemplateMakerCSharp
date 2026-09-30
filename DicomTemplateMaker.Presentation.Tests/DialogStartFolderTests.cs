using DicomTemplateMakerGUI.Services;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class DialogStartFolderTests
{
    [Fact]
    public void A_remembered_folder_inside_an_unreachable_share_is_not_tried()
    {
        // The share probe had just timed out; checking \\aria\va_transfer\XML on the UI thread froze the window again.
        IReadOnlyList<string> candidates = DialogStartFolder.Candidates(null, @"\\aria\va_transfer\XML", unreachableRoot: @"\\aria\va_transfer");

        Assert.Empty(candidates);
    }

    [Fact]
    public void Candidates_are_the_preferred_then_the_remembered_folder_without_blanks_or_repeats()
    {
        Assert.Equal(new[] { @"\\aria\share", @"D:\Xml" }, DialogStartFolder.Candidates(@"\\aria\share", @"D:\Xml"));
        Assert.Equal(new[] { @"D:\Xml" }, DialogStartFolder.Candidates("  ", @" D:\Xml "));
        Assert.Equal(new[] { @"D:\Xml" }, DialogStartFolder.Candidates(@"D:\Xml", @"d:/xml/"));
        Assert.Equal(new[] { @"D:\Xml" }, DialogStartFolder.Candidates(null, @"D:\Xml", unreachableRoot: @"\\aria\va_transfer"));
    }

    [Theory]
    [InlineData(@"\\aria\va_transfer\XML", @"\\aria\va_transfer", true)]
    [InlineData(@"\\ARIA\VA_TRANSFER", @"\\aria\va_transfer\", true)]
    [InlineData(@"\\aria\va_transfer2", @"\\aria\va_transfer", false)]
    [InlineData(@"C:\", @"C:\", true)]
    [InlineData(@"C:\Data", @"C:\", true)]
    [InlineData(@"D:\Data", @"C:\", false)]
    public void Inside_compares_windows_paths_as_text(string path, string root, bool expected)
    {
        Assert.Equal(expected, DialogStartFolder.IsSameOrInside(path, root));
    }

    [Fact]
    public async Task The_first_existing_candidate_is_chosen()
    {
        string? chosen = await DialogStartFolder.ChooseAsync(new[] { @"C:\Gone", @"D:\Xml" }, TimeSpan.FromSeconds(5), path => path == @"D:\Xml");

        Assert.Equal(@"D:\Xml", chosen);
        Assert.Null(await DialogStartFolder.ChooseAsync(new[] { @"C:\Gone" }, TimeSpan.FromSeconds(5), _ => false));
    }

    [Fact]
    public async Task The_check_does_not_block_the_caller()
    {
        using var answer = new ManualResetEventSlim();

        Task<string?> choosing = DialogStartFolder.ChooseAsync(new[] { @"D:\Xml" }, TimeSpan.FromSeconds(10), _ =>
        {
            answer.Wait(TimeSpan.FromSeconds(10));
            return true;
        });

        // The caller (the UI thread in the program) has control back while the folder is being checked.
        Assert.False(choosing.IsCompleted);
        answer.Set();
        Assert.Equal(@"D:\Xml", await choosing);
    }

    [Fact]
    public async Task A_share_that_does_not_answer_is_given_up_and_later_candidates_in_it_are_skipped()
    {
        using var release = new ManualResetEventSlim();
        var checkedPaths = new List<string>();
        bool Exists(string path)
        {
            lock (checkedPaths)
            {
                checkedPaths.Add(path);
            }

            if (path.StartsWith(@"\\aria\", StringComparison.Ordinal))
            {
                release.Wait(TimeSpan.FromSeconds(10)); // a share that hangs
            }

            return true;
        }

        string? chosen = await DialogStartFolder.ChooseAsync(new[] { @"\\aria\share\A", @"\\aria\share\B", @"D:\Xml" }, TimeSpan.FromMilliseconds(100), Exists);
        release.Set();

        Assert.Equal(@"D:\Xml", chosen);
        Assert.DoesNotContain(@"\\aria\share\B", checkedPaths);
    }
}
