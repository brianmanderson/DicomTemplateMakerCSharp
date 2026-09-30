using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DicomTemplateMakerGUI.DicomTemplateServices;
using Microsoft.Extensions.Logging;

namespace DicomTemplateMakerGUI.Shell
{
    public enum RunnerState
    {
        Stopped,

        /// <summary>Scanning the monitored folders every interval.</summary>
        Running,

        /// <summary>Stopped for the duration of another operation (deleting template folders); running again afterwards.</summary>
        Paused,
    }

    /// <summary>
    /// The background RT generator: runs <see cref="DicomTemplateRunner.RunAsync"/> for the template folder until it is
    /// stopped. Start, stop, pause and folder changes are serialized, and each one completes only when the loop has
    /// really started or ended, so the caller can await a stop before closing. A failing cycle never ends the loop
    /// (the runner reports it); nothing here can end the process.
    /// </summary>
    public sealed class RunnerService
    {
        private readonly Func<string, DicomTemplateRunner> createRunner;
        private readonly ILogger logger;
        private readonly TimeSpan interval;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private DicomTemplateRunner runner;
        private CancellationTokenSource? cancellation;
        private Task? loop;
        private IProgress<RunReport>? progress;
        private volatile RunnerState state = RunnerState.Stopped;
        // Errors in the previous cycle, so a recurring error is logged when it appears or goes, not every cycle.
        private int previousErrorCount;

        /// <param name="templateRoot">The folder that holds the templates.</param>
        /// <param name="createRunner">Builds the runner for a template folder (template RT, logger and clock are its business).</param>
        /// <param name="logger">Receives starts, stops and a summary of each cycle that did something.</param>
        /// <param name="interval">The pause between cycles.</param>
        public RunnerService(string templateRoot, Func<string, DicomTemplateRunner> createRunner, ILogger logger, TimeSpan interval)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(templateRoot);
            ArgumentNullException.ThrowIfNull(createRunner);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentOutOfRangeException.ThrowIfLessThan(interval, TimeSpan.Zero);
            this.createRunner = createRunner;
            this.logger = logger;
            this.interval = interval;
            TemplateRoot = Path.GetFullPath(templateRoot);
            runner = createRunner(TemplateRoot);
        }

        public string TemplateRoot { get; private set; }

        public RunnerState State => state;

        /// <summary>Starts the loop unless it runs; each cycle's report goes to <paramref name="reportProgress"/> (kept for restarts).</summary>
        public async Task StartAsync(IProgress<RunReport>? reportProgress)
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                progress = reportProgress;
                if (!LoopRunning)
                {
                    StartLoop();
                }
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>Stops the loop and waits until the cycle in progress has ended.</summary>
        public async Task StopAsync()
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await StopLoopAsync().ConfigureAwait(false);
                state = RunnerState.Stopped;
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Runs <paramref name="work"/> on the thread pool with the loop stopped, then restarts the loop if it was
        /// running (also when <paramref name="work"/> throws; the exception then propagates).
        /// </summary>
        public async Task<T> RunPausedAsync<T>(Func<DicomTemplateRunner, CancellationToken, T> work, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(work);
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            bool wasRunning = LoopRunning;
            try
            {
                if (wasRunning)
                {
                    await StopLoopAsync().ConfigureAwait(false);
                    state = RunnerState.Paused;
                    logger.LogInformation("The RT generator is paused for another operation.");
                }

                DicomTemplateRunner current = runner;
                return await Task.Run(() => work(current, cancellationToken), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (wasRunning)
                {
                    StartLoop();
                }
                else
                {
                    state = RunnerState.Stopped;
                }

                gate.Release();
            }
        }

        /// <summary>
        /// Stops the loop if it runs, then runs <paramref name="work"/> on the thread pool. Unlike
        /// <see cref="RunPausedAsync{T}"/>, the loop is not started again: used to delete generated RTs, which a running
        /// loop would write again within a cycle (it writes an RT for every matching series that has none). The state is
        /// <see cref="RunnerState.Stopped"/> afterwards, also when <paramref name="work"/> throws.
        /// </summary>
        public async Task<T> StopThenRunAsync<T>(Func<DicomTemplateRunner, CancellationToken, T> work, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(work);
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (LoopRunning)
                {
                    await StopLoopAsync().ConfigureAwait(false);
                    logger.LogInformation("The RT generator was stopped for another operation and stays stopped.");
                }

                state = RunnerState.Stopped;
                DicomTemplateRunner current = runner;
                return await Task.Run(() => work(current, cancellationToken), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Switches to another template folder. The loop is restarted there when it was running and
        /// <paramref name="keepRunning"/> is true; otherwise it is left stopped.
        /// </summary>
        public async Task ChangeRootAsync(string templateRoot, bool keepRunning = true)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(templateRoot);
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                bool wasRunning = LoopRunning;
                await StopLoopAsync().ConfigureAwait(false);
                TemplateRoot = Path.GetFullPath(templateRoot);
                runner = createRunner(TemplateRoot);
                if (wasRunning && keepRunning)
                {
                    StartLoop();
                }
                else
                {
                    state = RunnerState.Stopped;
                }
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>A separate runner for the current folder, for one-off work that must not share the loop's state.</summary>
        public DicomTemplateRunner CreateRunner()
        {
            return createRunner(TemplateRoot);
        }

        private bool LoopRunning => loop != null && !loop.IsCompleted;

        private void StartLoop()
        {
            cancellation = new CancellationTokenSource();
            previousErrorCount = 0;
            DicomTemplateRunner current = runner;
            string root = TemplateRoot;
            CancellationToken token = cancellation.Token;
            // On the thread pool, so the loop never runs on (or posts back to) the caller's UI thread.
            loop = Task.Run(() => RunLoopAsync(current, root, token), CancellationToken.None);
            state = RunnerState.Running;
        }

        private async Task StopLoopAsync()
        {
            if (loop == null)
            {
                return;
            }

            try
            {
                cancellation?.Cancel();
                await loop.ConfigureAwait(false);
            }
            finally
            {
                cancellation?.Dispose();
                cancellation = null;
                loop = null;
            }
        }

        private async Task RunLoopAsync(DicomTemplateRunner current, string root, CancellationToken cancellationToken)
        {
            logger.LogInformation("The RT generator started for {TemplateRoot} (a scan every {Interval}).", root, interval);
            try
            {
                await current.RunAsync(interval, new ReportForwarder(this), cancellationToken).ConfigureAwait(false);
                logger.LogInformation("The RT generator stopped.");
            }
            catch (Exception ex)
            {
                // RunAsync contains every cycle's failures; this is a last line of defence for the loop itself.
                state = RunnerState.Stopped;
                logger.LogError(ex, "The RT generator stopped unexpectedly.");
            }
        }

        /// <summary>
        /// Logs a summary of each cycle that wrote an RT, left out an ROI, or changed the number of errors (the runner
        /// logs every entry itself as it happens); other cycles are logged at Debug level only.
        /// </summary>
        private void OnReport(RunReport report)
        {
            bool errorsChanged = report.ErrorCount != previousErrorCount;
            previousErrorCount = report.ErrorCount;
            if (report.WrittenCount > 0 || report.RoiFailureCount > 0 || errorsChanged)
            {
                logger.LogInformation(
                    "Scan at {StartedAt:u} took {Duration}: {Summary}.{Details}",
                    report.StartedAt,
                    report.Duration,
                    report.Summary,
                    Environment.NewLine + string.Join(Environment.NewLine, RunnerStatus.DescribeReport(report)));
            }
            else
            {
                logger.LogDebug("Scan at {StartedAt:u} took {Duration}: {Summary}.", report.StartedAt, report.Duration, report.Summary);
            }

            progress?.Report(report);
        }

        private sealed class ReportForwarder : IProgress<RunReport>
        {
            private readonly RunnerService owner;

            public ReportForwarder(RunnerService owner)
            {
                this.owner = owner;
            }

            public void Report(RunReport value)
            {
                owner.OnReport(value);
            }
        }
    }
}
