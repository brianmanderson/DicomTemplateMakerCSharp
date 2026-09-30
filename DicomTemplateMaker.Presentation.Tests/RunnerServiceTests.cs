using DicomTemplateMaker.Presentation.Tests.Fakes;
using DicomTemplateMakerGUI.DicomTemplateServices;
using DicomTemplateMakerGUI.Shell;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class RunnerServiceTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private readonly TestFolder folder = new();
    private readonly CapturingLogger logger = new();
    private readonly List<string> runnerRoots = new();

    public void Dispose() => folder.Dispose();

    private RunnerService Service(string? root = null) => new(root ?? folder.Sub("templates"), CreateRunner, logger, TimeSpan.FromMilliseconds(10));

    private DicomTemplateRunner CreateRunner(string root)
    {
        lock (runnerRoots)
        {
            runnerRoots.Add(root);
        }

        return new DicomTemplateRunner(root, Templates.RepositoryFile("DicomTemplateMakerGUI", "template_RS.dcm"), null, TimeProvider.System) { SettleTime = TimeSpan.Zero };
    }

    [Fact]
    public async Task Start_reports_cycles_and_stop_waits_for_the_loop_to_end()
    {
        RunnerService service = Service();
        var reports = new ReportCollector();

        await service.StartAsync(reports);
        Assert.Equal(RunnerState.Running, service.State);
        await reports.WaitForAsync(r => r.Count >= 2);
        await service.StopAsync();
        int afterStop = reports.Count;
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(RunnerState.Stopped, service.State);
        Assert.Equal(afterStop, reports.Count);
        Assert.Contains(logger.Entries, e => e.Message.Contains("started", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, e => e.Message.Contains("stopped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stop_does_not_complete_while_a_cycle_is_still_running()
    {
        // The pause before a delete and the stop before closing rely on this wait.
        RunnerService service = Service();
        using var blocking = new BlockingProgress();
        await service.StartAsync(blocking);
        Assert.True(blocking.Entered.Wait(Timeout, TestContext.Current.CancellationToken));

        Task stop = service.StopAsync();
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.False(stop.IsCompleted, "StopAsync completed while the cycle was still reporting.");
        blocking.Release.Set();
        await stop.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.Equal(RunnerState.Stopped, service.State);
    }

    [Fact]
    public async Task Paused_work_does_not_start_while_a_cycle_is_still_running()
    {
        RunnerService service = Service();
        using var blocking = new BlockingProgress();
        await service.StartAsync(blocking);
        Assert.True(blocking.Entered.Wait(Timeout, TestContext.Current.CancellationToken));
        int workStarted = 0;

        Task<int> paused = service.RunPausedAsync((_, _) => Interlocked.Increment(ref workStarted), TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Equal(0, Volatile.Read(ref workStarted));
        blocking.StopBlocking();
        Assert.Equal(1, await paused.WaitAsync(Timeout, TestContext.Current.CancellationToken));
        await service.StopAsync();
    }

    [Fact]
    public async Task Deleting_generated_rts_stops_the_generator_and_they_are_not_written_again()
    {
        // With the loop resumed, the runner wrote every deleted RT again within one cycle.
        string root = folder.Sub("templates");
        string series = folder.Sub("incoming");
        foreach (string file in Directory.GetFiles(Templates.RepositoryFile("DicomTemplateMakerGUI", "SmallCT")))
        {
            File.Copy(file, Path.Combine(series, Path.GetFileName(file)));
        }

        Templates.Make(root, "Brain", new[] { series });
        RunnerService service = Service(root);
        var reports = new ReportCollector();
        await service.StartAsync(reports);
        await reports.WaitForAsync(r => r.Reports.Sum(x => x.WrittenCount) >= 1);

        DeleteReport deleted = await service.StopThenRunAsync((runner, token) => runner.DeleteGenerated(new[] { "Brain" }, token), TestContext.Current.CancellationToken);
        int reportsAfterDelete = reports.Count;
        await Task.Delay(300, TestContext.Current.CancellationToken); // about 30 intervals of 10 ms

        Assert.Single(deleted.Deleted);
        Assert.Equal(RunnerState.Stopped, service.State);
        Assert.Empty(Directory.GetFiles(series, "Brain_UID*.dcm"));
        Assert.Equal(reportsAfterDelete, reports.Count);
        Assert.Contains(logger.Entries, e => e.Message.Contains("stays stopped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stop_then_run_on_a_stopped_service_runs_the_work_and_leaves_it_stopped()
    {
        RunnerService service = Service();

        bool pool = await service.StopThenRunAsync((_, _) => Thread.CurrentThread.IsThreadPoolThread, TestContext.Current.CancellationToken);

        Assert.True(pool);
        Assert.Equal(RunnerState.Stopped, service.State);
    }

    [Fact]
    public async Task Changing_the_folder_without_keeping_the_generator_running_leaves_it_stopped()
    {
        RunnerService service = Service();
        var reports = new ReportCollector();
        await service.StartAsync(reports);
        string other = folder.Sub("other");

        await service.ChangeRootAsync(other, keepRunning: false);

        Assert.Equal(other, service.TemplateRoot);
        Assert.Equal(RunnerState.Stopped, service.State);
        int before = reports.Count;
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(before, reports.Count);
    }

    [Fact]
    public async Task Stopping_a_stopped_service_does_nothing()
    {
        RunnerService service = Service();

        await service.StopAsync();

        Assert.Equal(RunnerState.Stopped, service.State);
    }

    [Fact]
    public async Task Starting_twice_keeps_one_loop()
    {
        RunnerService service = Service();
        var reports = new ReportCollector();

        await service.StartAsync(reports);
        await service.StartAsync(reports);
        await reports.WaitForAsync(r => r.Count >= 1);
        await service.StopAsync();

        Assert.Single(logger.Entries, e => e.Message.Contains("started", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Paused_work_runs_with_the_loop_stopped_and_the_loop_resumes_afterwards()
    {
        RunnerService service = Service();
        var reports = new ReportCollector();
        await service.StartAsync(reports);
        await reports.WaitForAsync(r => r.Count >= 1);

        (RunnerState during, int countBefore) = await service.RunPausedAsync((runner, _) =>
        {
            int before = reports.Count;
            Thread.Sleep(100);
            // No cycle ran while paused.
            Assert.Equal(before, reports.Count);
            return (service.State, before);
        }, TestContext.Current.CancellationToken);

        Assert.Equal(RunnerState.Paused, during);
        Assert.Equal(RunnerState.Running, service.State);
        await reports.WaitForAsync(r => r.Count > countBefore);
        await service.StopAsync();
    }

    [Fact]
    public async Task Paused_work_on_a_stopped_service_leaves_it_stopped()
    {
        RunnerService service = Service();

        (bool pool, SynchronizationContext? context) = await service.RunPausedAsync(
            (_, _) => (Thread.CurrentThread.IsThreadPoolThread, SynchronizationContext.Current),
            TestContext.Current.CancellationToken);

        Assert.Equal(RunnerState.Stopped, service.State);
        Assert.True(pool);
        Assert.Null(context);
    }

    [Fact]
    public async Task A_failing_paused_operation_propagates_and_restores_the_loop()
    {
        RunnerService service = Service();
        var reports = new ReportCollector();
        await service.StartAsync(reports);

        await Assert.ThrowsAsync<IOException>(() => service.RunPausedAsync<int>((_, _) => throw new IOException("disk gone"), TestContext.Current.CancellationToken));

        Assert.Equal(RunnerState.Running, service.State);
        int before = reports.Count;
        await reports.WaitForAsync(r => r.Count > before);
        await service.StopAsync();
    }

    [Fact]
    public async Task Changing_the_folder_restarts_a_running_loop_on_the_new_folder()
    {
        RunnerService service = Service();
        var reports = new ReportCollector();
        await service.StartAsync(reports);
        string other = folder.Sub("other");

        await service.ChangeRootAsync(other);

        Assert.Equal(other, service.TemplateRoot);
        Assert.Equal(RunnerState.Running, service.State);
        Assert.Equal(other, runnerRoots.Last());
        int before = reports.Count;
        await reports.WaitForAsync(r => r.Count > before);
        await service.StopAsync();
        Assert.Equal(2, logger.Entries.Count(e => e.Message.Contains("started", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Changing_the_folder_of_a_stopped_service_does_not_start_it()
    {
        RunnerService service = Service();
        string other = folder.Sub("other");

        await service.ChangeRootAsync(other);
        service.CreateRunner();

        Assert.Equal(RunnerState.Stopped, service.State);
        Assert.Equal(other, service.TemplateRoot);
        Assert.Equal(other, runnerRoots.Last());
    }

    [Fact]
    public async Task The_running_service_writes_rts_and_logs_what_it_wrote()
    {
        string root = folder.Sub("templates");
        string series = folder.Sub("incoming");
        foreach (string file in Directory.GetFiles(Templates.RepositoryFile("DicomTemplateMakerGUI", "SmallCT")))
        {
            File.Copy(file, Path.Combine(series, Path.GetFileName(file)));
        }

        Templates.Make(root, "Brain", new[] { series });
        RunnerService service = Service(root);
        var reports = new ReportCollector();

        await service.StartAsync(reports);
        await reports.WaitForAsync(r => r.Reports.Sum(x => x.WrittenCount) >= 1);
        await service.StopAsync();

        Assert.Single(Directory.GetFiles(series, "Brain_UID*.dcm"));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("1 RT(s) written", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_recurring_error_is_summarized_in_the_log_when_it_appears_not_every_cycle()
    {
        string root = folder.Sub("templates");
        Templates.Make(root, "Brain", new[] { folder.Sub("incoming") });
        string missingTemplateRt = Path.Combine(folder.Path, "missing_RS.dcm");
        var service = new RunnerService(root, r => new DicomTemplateRunner(r, missingTemplateRt, null, TimeProvider.System), logger, TimeSpan.FromMilliseconds(10));
        var reports = new ReportCollector();

        await service.StartAsync(reports);
        await reports.WaitForAsync(r => r.Count >= 5);
        await service.StopAsync();

        Assert.All(reports.Reports, r => Assert.Equal(1, r.ErrorCount));
        (LogLevel Level, string Message) summary = Assert.Single(logger.Entries, e => e.Level == LogLevel.Information && e.Message.StartsWith("Scan at", StringComparison.Ordinal));
        Assert.Contains("missing_RS.dcm", summary.Message, StringComparison.Ordinal);
    }

    /// <summary>A progress whose first report blocks the runner's cycle until released, so a test can act mid-cycle.</summary>
    private sealed class BlockingProgress : IProgress<RunReport>, IDisposable
    {
        private int blockNext = 1;

        public ManualResetEventSlim Entered { get; } = new();

        public ManualResetEventSlim Release { get; } = new();

        public void Report(RunReport value)
        {
            if (Interlocked.Exchange(ref blockNext, 0) == 1)
            {
                Entered.Set();
                Release.Wait(Timeout);
            }
        }

        public void StopBlocking() => Release.Set();

        public void Dispose()
        {
            Release.Set();
            Entered.Dispose();
            Release.Dispose();
        }
    }

    /// <summary>Collects reports from the runner's thread and lets a test wait for a condition.</summary>
    private sealed class ReportCollector : IProgress<RunReport>
    {
        private readonly object gate = new();
        private readonly List<RunReport> reports = new();

        public int Count
        {
            get
            {
                lock (gate)
                {
                    return reports.Count;
                }
            }
        }

        public IReadOnlyList<RunReport> Reports
        {
            get
            {
                lock (gate)
                {
                    return reports.ToList();
                }
            }
        }

        public void Report(RunReport value)
        {
            lock (gate)
            {
                reports.Add(value);
            }
        }

        public async Task WaitForAsync(Func<ReportCollector, bool> condition)
        {
            DateTime deadline = DateTime.UtcNow + Timeout;
            while (!condition(this))
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("The condition was not met in time.");
                }

                await Task.Delay(10);
            }
        }
    }
}
