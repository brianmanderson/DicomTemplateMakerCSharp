using DicomTemplateMakerGUI.Editors;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class FolderProbeTests
{
    [Fact]
    public async Task An_existing_and_a_missing_folder_are_told_apart()
    {
        Assert.Equal(FolderState.Exists, await FolderProbe.CheckAsync(Path.GetTempPath(), TimeSpan.FromSeconds(10)));
        Assert.Equal(FolderState.Missing, await FolderProbe.CheckAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task A_check_that_does_not_answer_in_time_is_unreachable()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            FolderState state = await FolderProbe.CheckAsync(@"\\down\share", TimeSpan.FromMilliseconds(50), _ =>
            {
                release.Task.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                return true;
            });

            Assert.Equal(FolderState.Unreachable, state);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [Fact]
    public async Task A_check_that_throws_counts_as_missing()
    {
        FolderState state = await FolderProbe.CheckAsync("x", TimeSpan.FromSeconds(10), _ => throw new IOException("denied"));

        Assert.Equal(FolderState.Missing, state);
    }

    [Fact]
    public void Missing_and_unreachable_folders_get_a_warning()
    {
        Assert.Null(FolderProbe.Describe(FolderState.Exists, FolderProbe.DefaultTimeout));
        Assert.Contains("does not exist", FolderProbe.Describe(FolderState.Missing, FolderProbe.DefaultTimeout), StringComparison.Ordinal);
        Assert.Contains("within 5 seconds", FolderProbe.Describe(FolderState.Unreachable, FolderProbe.DefaultTimeout), StringComparison.Ordinal);
    }
}
