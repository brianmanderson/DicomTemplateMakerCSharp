using DicomTemplateMakerGUI.DicomTemplateServices;
using Xunit;

namespace DicomTemplateCore.Tests;

/// <summary><see cref="DicomTemplateRunner.RunAsync"/>: survives failing cycles and returns when cancelled.</summary>
public class RunnerLoopTests
{
    /// <summary>Throws on its first cycle and cancels the loop on its third.</summary>
    private sealed class FlakyRunner : DicomTemplateRunner
    {
        private readonly CancellationTokenSource stop;

        public FlakyRunner(string templateFolder, CancellationTokenSource stop)
            : base(templateFolder, DefaultTemplateRsPath, null, RunnerClock.AfterFilesSettled())
        {
            this.stop = stop;
        }

        public int Cycles { get; private set; }

        internal override RunReport RunCycle(CancellationToken cancellationToken)
        {
            Cycles++;
            if (Cycles == 1)
            {
                throw new InvalidOperationException("injected cycle failure");
            }

            if (Cycles == 3)
            {
                stop.Cancel();
            }

            return base.RunCycle(cancellationToken);
        }
    }

    private sealed class CollectingProgress : IProgress<RunReport>
    {
        private readonly Action<RunReport>? onReport;

        public CollectingProgress(Action<RunReport>? onReport = null)
        {
            this.onReport = onReport;
        }

        public List<RunReport> Reports { get; } = new List<RunReport>();

        public void Report(RunReport value)
        {
            Reports.Add(value);
            onReport?.Invoke(value);
        }
    }

    [Fact]
    public async Task A_failing_cycle_is_reported_and_the_loop_goes_on_until_cancelled()
    {
        using var folder = new TestFolder();
        using var stop = new CancellationTokenSource();
        var runner = new FlakyRunner(folder.Sub("Templates"), stop);
        var progress = new CollectingProgress();

        Task loop = runner.RunAsync(TimeSpan.Zero, progress, stop.Token);
        Task finished = await Task.WhenAny(loop, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        Assert.Same(loop, finished);
        Assert.True(loop.IsCompletedSuccessfully); // returned, did not throw OperationCanceledException
        Assert.Equal(3, runner.Cycles);
        // Cycle 1 failed and was reported; cycle 2 ran normally; cycle 3 was cancelled inside RunOnce.
        Assert.Equal(2, progress.Reports.Count);
        Assert.Contains("injected cycle failure", Assert.Single(progress.Reports[0].Errors).Message);
        Assert.False(progress.Reports[1].HasErrors);
    }

    [Fact]
    public async Task Cancelling_during_the_interval_returns_promptly()
    {
        using var folder = new TestFolder();
        using var stop = new CancellationTokenSource();
        var runner = new DicomTemplateRunner(folder.Sub("Templates"));
        var progress = new CollectingProgress(_ => stop.Cancel());

        Task loop = runner.RunAsync(TimeSpan.FromHours(1), progress, stop.Token);
        Task finished = await Task.WhenAny(loop, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        Assert.Same(loop, finished);
        Assert.True(loop.IsCompletedSuccessfully);
        Assert.Single(progress.Reports);
    }

    [Fact]
    public void Run_once_throws_only_when_cancelled()
    {
        using var folder = new TestFolder();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => new DicomTemplateRunner(folder.Path).RunOnce(cancelled.Token));
    }

    [Fact]
    public void A_missing_template_folder_or_template_rt_is_an_error_in_the_report()
    {
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        SeriesFactory.Write(images, "ct");
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images });

        RunReport noFolder = new DicomTemplateRunner(Path.Combine(folder.Path, "Missing")).RunOnce(TestContext.Current.CancellationToken);
        RunReport noRt = new DicomTemplateRunner(templates, Path.Combine(folder.Path, "missing_RS.dcm")).RunOnce(TestContext.Current.CancellationToken);

        Assert.Contains("cannot be read", Assert.Single(noFolder.Errors).Message);
        Assert.Contains("missing_RS.dcm", Assert.Single(noRt.Errors).Message);
        Assert.Empty(Directory.GetFiles(images, "Brain_Test_UID*"));
    }
}
