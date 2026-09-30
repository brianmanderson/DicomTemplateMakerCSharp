using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FellowOakDicom;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DicomTemplateMakerGUI.DicomTemplateServices
{
    /// <summary>
    /// Writes an empty RT Structure Set (the template's ROIs, no contours) next to every image series found
    /// under the paths of each template in a template folder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A template folder holds one sub-folder per template with <c>Paths.txt</c> (folders to scan,
    /// recursively), the ROIs (<c>All_ROIs.json</c> or legacy <c>ROIs\*.txt</c>) and optionally
    /// <c>DicomTags.txt</c> (Series/Study Description requirements, see <see cref="TemplateMatcher"/>).
    /// Each series gets <c>{template}_UID{SeriesInstanceUID}.dcm</c> in its own directory unless that file
    /// exists; an existing RT that does not reference every image of its series (images arrived after it was
    /// written) is reported as an error, never rewritten. Only planning images get an RT (see
    /// <see cref="PlanningImages"/>).
    /// </para>
    /// <para>
    /// <see cref="RunOnce"/> (and <see cref="RunAsync"/>, which repeats it) only reads a directory once it
    /// has settled: its fingerprint (names, sizes and last-write times of its files, generated RTs left out)
    /// equals the one seen on the previous scan and its newest file is at least <see cref="SettleTime"/> old;
    /// a directory seen for the first time whose files are all older than that is ready at once. A directory
    /// whose files cannot be read because they are in use is retried after 1, 2, 4 and 8 cycles, then given
    /// up until its files change. A directory whose fingerprint has not changed since it was processed is not
    /// read again.
    /// </para>
    /// <para>
    /// Failures are contained per template, path, directory, series and ROI, logged with their context and
    /// listed in the returned <see cref="RunReport"/>. Public methods may be called from any thread; calls on
    /// one instance run one at a time.
    /// </para>
    /// </remarks>
    public class DicomTemplateRunner
    {
        /// <summary>How long a directory's newest file must be unchanged before it is read (default).</summary>
        public static readonly TimeSpan DefaultSettleTime = TimeSpan.FromSeconds(10);

        /// <summary>Attempts after a read failure: the first plus retries after 1, 2, 4 and 8 cycles.</summary>
        internal const int MaxTransientAttempts = 5;

        private static readonly EnumerationOptions RecursiveEnumeration = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = 0,
        };

        private readonly object gate = new object();
        private readonly string templateFolder;
        private readonly string templateRsPath;
        private readonly ILogger logger;
        private readonly TimeProvider timeProvider;
        private readonly Dictionary<(string Template, string Directory), DirectoryState> states = new Dictionary<(string, string), DirectoryState>();
        private readonly HashSet<(string Template, string Path)> missingPaths = new HashSet<(string, string)>();
        // Errors that recur every cycle until fixed (unloadable template, missing template RT): logged when they change.
        private readonly Dictionary<string, string> recurringErrors = new Dictionary<string, string>(StringComparer.Ordinal);
        private RtStructureBuilder? cachedBuilder;
        private (long Length, DateTime LastWriteUtc) cachedBuilderIdentity;
        private long cycle;
        private TimeSpan settleTime = DefaultSettleTime;
        // Set by build_dictionary for the legacy methods; null until it has been called.
        private IReadOnlyList<RunnerTemplate>? legacyTemplates;

        /// <summary>
        /// Series and Study Description requirements per template, filled by <see cref="build_dictionary"/>
        /// (blank requirements are left out).
        /// </summary>
        public Dictionary<string, Dictionary<string, List<string>>> Template_DicomTags = new Dictionary<string, Dictionary<string, List<string>>>();

        /// <summary>Uses the current folder as template folder and the program folder's template_RS.dcm.</summary>
        public DicomTemplateRunner()
            : this(".")
        {
        }

        /// <summary>Uses the program folder's template_RS.dcm.</summary>
        public DicomTemplateRunner(string template_folder)
            : this(template_folder, DefaultTemplateRsPath)
        {
        }

        public DicomTemplateRunner(string template_folder, string template_rs_path)
            : this(template_folder, template_rs_path, null, null)
        {
        }

        /// <param name="templateFolder">The folder holding one sub-folder per template.</param>
        /// <param name="templateRsPath">The RT Structure Set every generated RT is built from (read when first needed).</param>
        /// <param name="logger">Receives what the runner does; nothing is logged when null.</param>
        /// <param name="timeProvider">Clock for the settle rule, the RT dates and the loop interval; the system clock when null.</param>
        public DicomTemplateRunner(string templateFolder, string templateRsPath, ILogger? logger = null, TimeProvider? timeProvider = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(templateFolder);
            ArgumentException.ThrowIfNullOrWhiteSpace(templateRsPath);
            this.templateFolder = Path.GetFullPath(templateFolder);
            this.templateRsPath = Path.GetFullPath(templateRsPath);
            this.logger = logger ?? NullLogger.Instance;
            this.timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>template_RS.dcm in the program folder (next to the executable, also for a single-file build).</summary>
        public static string DefaultTemplateRsPath => Path.Combine(AppContext.BaseDirectory, "template_RS.dcm");

        /// <summary>How long a directory's newest file must be unchanged before the directory is read.</summary>
        public TimeSpan SettleTime
        {
            get => settleTime;
            set
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
                settleTime = value;
            }
        }

        /// <summary>
        /// One full cycle over every template: reads the templates and each template's Paths.txt, and writes
        /// the RTs of the settled directories. Throws only <see cref="OperationCanceledException"/>; every
        /// other failure is in the report.
        /// </summary>
        public RunReport RunOnce(CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long started = timeProvider.GetTimestamp();
                DateTimeOffset startedAt = timeProvider.GetUtcNow();
                var report = new RunReportBuilder(logger, recurringErrors);
                long cycleNumber = ++cycle;
                try
                {
                    string[] templateFolders = ListTemplateFolders(report);
                    var templates = new List<RunnerTemplate>();
                    foreach (string folder in templateFolders)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        RunnerTemplate? template = TryLoadTemplate(folder, readPaths: true, report);
                        if (template != null)
                        {
                            templates.Add(template);
                        }
                    }

                    RtStructureBuilder? builder = templates.Count > 0 ? GetBuilder(report) : null;
                    if (builder != null)
                    {
                        var context = new CycleContext(cycleNumber, builder, GeneratedRtNames.ProducedByAny(templateFolders.Select(f => Path.GetFileName(f))), report);
                        foreach (RunnerTemplate template in templates)
                        {
                            ProcessPaths(template, template.Paths, ScanMode.Watch, context, cancellationToken);
                        }

                        PruneStates(cycleNumber);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    report.AddError($"The run cycle failed: {ex.Message}", exception: ex);
                }

                return report.Build(startedAt, timeProvider.GetElapsedTime(started));
            }
        }

        /// <summary>
        /// Writes the RTs of one template for the series in <paramref name="folder"/> and its sub-folders,
        /// without reading or writing the template's Paths.txt. The template's requirements apply as usual.
        /// </summary>
        /// <remarks>
        /// The folder is read at once, without the settle rule, and nothing is remembered for later cycles: the
        /// caller vouches that the folder is complete, typically because it has just written it (the GUI's
        /// "Create folder with loadable RTs"). A file that is in use is reported as an error rather than retried.
        /// </remarks>
        public RunReport RunForFolder(string templateName, string folder, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
            ArgumentException.ThrowIfNullOrWhiteSpace(folder);
            lock (gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long started = timeProvider.GetTimestamp();
                DateTimeOffset startedAt = timeProvider.GetUtcNow();
                var report = new RunReportBuilder(logger);
                long cycleNumber = ++cycle;
                try
                {
                    RunnerTemplate? template = TryLoadNamedTemplate(templateName, report);
                    RtStructureBuilder? builder = template != null ? GetBuilder(report) : null;
                    if (template != null && builder != null)
                    {
                        string[] templateFolders = ListTemplateFolders(report);
                        IEnumerable<string> names = templateFolders.Select(f => Path.GetFileName(f)).Append(template.Name);
                        var context = new CycleContext(cycleNumber, builder, GeneratedRtNames.ProducedByAny(names), report);
                        ProcessPaths(template, new[] { folder }, ScanMode.Immediate, context, cancellationToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    report.AddError($"The folder run failed: {ex.Message}", templateName, folder, exception: ex);
                }

                return report.Build(startedAt, timeProvider.GetElapsedTime(started));
            }
        }

        /// <summary>
        /// Deletes the RTs generated by every template (<c>{template}_UID{uid}.dcm</c> under the template's
        /// paths, recursively). Only deletes; never writes an RT.
        /// </summary>
        public DeleteReport DeleteGenerated(CancellationToken cancellationToken = default)
        {
            return DeleteGeneratedCore(null, cancellationToken);
        }

        /// <summary>As <see cref="DeleteGenerated(CancellationToken)"/>, limited to the named templates.</summary>
        public DeleteReport DeleteGenerated(IEnumerable<string> templateNames, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(templateNames);
            return DeleteGeneratedCore(new HashSet<string>(templateNames, StringComparer.Ordinal), cancellationToken);
        }

        /// <summary>
        /// Runs <see cref="RunOnce"/> every <paramref name="interval"/> until <paramref name="cancellationToken"/>
        /// is cancelled, reporting each cycle to <paramref name="progress"/>. A failing cycle is logged and
        /// reported, and the loop goes on. Returns (without throwing) when cancelled. Cycles run on the thread pool.
        /// </summary>
        public async Task RunAsync(TimeSpan interval, IProgress<RunReport>? progress, CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(interval, TimeSpan.Zero);
            while (!cancellationToken.IsCancellationRequested)
            {
                RunReport report;
                try
                {
                    report = await Task.Run(() => RunCycle(cancellationToken), CancellationToken.None).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "A run cycle failed; the next one starts after the interval.");
                    report = RunReport.ForFailedCycle(timeProvider.GetUtcNow(), ex);
                }

                try
                {
                    progress?.Report(report);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "The progress handler failed.");
                }

                try
                {
                    await Task.Delay(interval, timeProvider, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        /// <summary>One cycle of <see cref="RunAsync"/>; a seam for tests.</summary>
        internal virtual RunReport RunCycle(CancellationToken cancellationToken) => RunOnce(cancellationToken);

        /// <summary>Reads the templates for the legacy methods and fills <see cref="Template_DicomTags"/>.</summary>
        /// <remarks>Legacy API; a template that cannot be loaded is logged and left out.</remarks>
        public void build_dictionary()
        {
            lock (gate)
            {
                var report = new RunReportBuilder(logger);
                var templates = new List<RunnerTemplate>();
                foreach (string folder in ListTemplateFolders(report))
                {
                    RunnerTemplate? template = TryLoadTemplate(folder, readPaths: true, report);
                    if (template != null)
                    {
                        templates.Add(template);
                    }
                }

                legacyTemplates = templates;
                Template_DicomTags = templates.ToDictionary(
                    t => t.Name,
                    t => new Dictionary<string, List<string>>
                    {
                        { TemplateRequirements.StudyDescriptionKey, t.Requirements.StudyDescriptions.ToList() },
                        { TemplateRequirements.SeriesDescriptionKey, t.Requirements.SeriesDescriptions.ToList() },
                    });
            }
        }

        /// <summary>
        /// Legacy API over the templates read by <see cref="build_dictionary"/>: deletes their generated RTs
        /// (<paramref name="delete_RT"/> true) or writes the missing ones. Unlike <see cref="RunOnce"/>, folders
        /// are read at once (no settle rule) and nothing is remembered between calls.
        /// </summary>
        public void walk_down_folders(bool delete_RT)
        {
            IReadOnlyList<RunnerTemplate> templates = RequireBuilt();
            foreach (RunnerTemplate template in templates)
            {
                RunLegacy(template, template.Paths, delete_RT);
            }
        }

        /// <summary>Legacy API: <see cref="walk_down_folders"/> for one template and one of its paths.</summary>
        public void run_for_path(string template_name, string path, bool delete_RT)
        {
            RunLegacy(RequireBuiltTemplate(template_name), new[] { path }, delete_RT);
        }

        /// <summary>Legacy API: <see cref="walk_down_folders"/> for one template.</summary>
        public void run_for_template_key(string template_name, bool delete_RT)
        {
            RunnerTemplate template = RequireBuiltTemplate(template_name);
            RunLegacy(template, template.Paths, delete_RT);
        }

        /// <summary>Legacy API: <see cref="DeleteGenerated(CancellationToken)"/>. Deletes only; it no longer generates RTs.</summary>
        public void delete_rts()
        {
            DeleteGenerated();
        }

        /// <summary>Legacy API: <see cref="RunAsync"/> every 3 seconds, forever, blocking the calling thread.</summary>
        public void run()
        {
            RunAsync(TimeSpan.FromSeconds(3), null, CancellationToken.None).GetAwaiter().GetResult();
        }

        private IReadOnlyList<RunnerTemplate> RequireBuilt()
        {
            return legacyTemplates ?? throw new InvalidOperationException("build_dictionary must be called first.");
        }

        private RunnerTemplate RequireBuiltTemplate(string templateName)
        {
            return RequireBuilt().FirstOrDefault(t => t.Name == templateName)
                ?? throw new KeyNotFoundException($"Template '{templateName}' was not loaded by build_dictionary.");
        }

        private void RunLegacy(RunnerTemplate template, IReadOnlyList<string> paths, bool delete)
        {
            if (delete)
            {
                DeleteForTemplates(new[] { (template.Name, paths) }, new RunReportBuilder(logger), CancellationToken.None);
                return;
            }

            lock (gate)
            {
                var report = new RunReportBuilder(logger);
                RtStructureBuilder? builder = GetBuilder(report);
                if (builder != null)
                {
                    IEnumerable<string> names = RequireBuilt().Select(t => t.Name).Append(template.Name);
                    var context = new CycleContext(++cycle, builder, GeneratedRtNames.ProducedByAny(names), report);
                    ProcessPaths(template, paths, ScanMode.Immediate, context, CancellationToken.None);
                }
            }
        }

        private string[] ListTemplateFolders(RunReportBuilder report)
        {
            const string key = "template-folder";
            try
            {
                string[] folders = Directory.GetDirectories(templateFolder);
                Array.Sort(folders, StringComparer.Ordinal);
                report.ClearRecurring(key);
                return folders;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                report.AddError($"The template folder '{templateFolder}' cannot be read: {ex.Message}", recurringKey: key);
                return Array.Empty<string>();
            }
        }

        /// <summary>Loads a template folder; null when it is not a template or cannot be loaded (reported).</summary>
        private RunnerTemplate? TryLoadTemplate(string folder, bool readPaths, RunReportBuilder report)
        {
            string name = Path.GetFileName(folder);
            string key = "template:" + name;
            string legacyKey = "template-legacy:" + name;
            RunnerTemplate? template;
            var warnings = new List<string>();
            try
            {
                template = RunnerTemplate.IsRunnable(folder) ? RunnerTemplate.Load(folder, readPaths, warnings) : null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Any failure to load one template (a corrupt ROI file, an unreadable Paths.txt, ...) only skips it.
                report.AddError($"The template could not be loaded and was skipped: {ex.Message}", name, exception: ex, recurringKey: key);
                return null;
            }

            // Legacy ROI files that did not make it into All_ROIs.json: the template is used without them, which the
            // user must know (its RTs lack those ROIs). Reported every cycle while the files are there, logged once.
            if (warnings.Count > 0)
            {
                report.AddError("Some of the template's ROIs may be missing from its RTs: " + string.Join(" ", warnings), name, recurringKey: legacyKey);
            }
            else
            {
                report.ClearRecurring(legacyKey);
            }

            if (template != null && template.Rois.Count == 0)
            {
                report.AddError("The template has no ROIs and was skipped.", name, recurringKey: key);
                return null;
            }

            report.ClearRecurring(key);
            return template;
        }

        private RunnerTemplate? TryLoadNamedTemplate(string templateName, RunReportBuilder report)
        {
            if (templateName != Path.GetFileName(templateName) || templateName == "." || templateName == "..")
            {
                report.AddError("The template name must be the name of a folder in the template folder.", templateName);
                return null;
            }

            string folder = Path.Combine(templateFolder, templateName);
            try
            {
                if (!Directory.Exists(folder) || !ROIOntologyClass.ROIClassTools.IsValidTemplateFolder(folder))
                {
                    report.AddError($"There is no template '{templateName}' in '{templateFolder}'.", templateName);
                    return null;
                }

                RunnerTemplate template = RunnerTemplate.Load(folder, readPaths: false);
                if (template.Rois.Count == 0)
                {
                    report.AddError("The template has no ROIs.", templateName);
                    return null;
                }

                return template;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                report.AddError($"The template could not be loaded: {ex.Message}", templateName, exception: ex);
                return null;
            }
        }

        /// <summary>The template RT, read again only when its file changes; null when it cannot be read (reported).</summary>
        private RtStructureBuilder? GetBuilder(RunReportBuilder report)
        {
            const string key = "template-rt";
            try
            {
                var info = new FileInfo(templateRsPath);
                if (!info.Exists)
                {
                    report.AddError($"The template RT file '{templateRsPath}' does not exist, so no RT can be written.", recurringKey: key);
                    return null;
                }

                (long, DateTime) identity = (info.Length, info.LastWriteTimeUtc);
                if (cachedBuilder == null || cachedBuilderIdentity != identity)
                {
                    DicomDataset dataset = DicomFile.Open(templateRsPath, FileReadOption.ReadAll).Dataset;
                    if (dataset.GetSingleValueOrDefault(DicomTag.Modality, string.Empty) != "RTSTRUCT")
                    {
                        report.AddError($"The template RT file '{templateRsPath}' is not an RT Structure Set, so no RT can be written.", recurringKey: key);
                        return null;
                    }

                    cachedBuilder = new RtStructureBuilder(dataset);
                    cachedBuilderIdentity = identity;
                }

                report.ClearRecurring(key);
                return cachedBuilder;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                report.AddError($"The template RT file '{templateRsPath}' could not be read, so no RT can be written: {ex.Message}", exception: ex, recurringKey: key);
                return null;
            }
        }

        private void ProcessPaths(RunnerTemplate template, IEnumerable<string> paths, ScanMode mode, CycleContext context, CancellationToken cancellationToken)
        {
            // Overlapping paths (a folder and its parent) must not decide a directory twice in one run.
            var visited = new HashSet<string>(GeneratedRtNames.FileNameComparer);
            foreach (string path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    ProcessPath(template, path, mode, context, visited, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    context.Report.AddError($"The path could not be scanned: {ex.Message}", template.Name, path, exception: ex);
                }
            }
        }

        private void ProcessPath(RunnerTemplate template, string path, ScanMode mode, CycleContext context, HashSet<string> visited, CancellationToken cancellationToken)
        {
            string root = Path.GetFullPath(path);
            if (!Directory.Exists(root))
            {
                if (missingPaths.Add((template.Name, root)))
                {
                    logger.LogWarning("Path {Path} of template {Template} does not exist.", root, template.Name);
                }

                context.Report.AddSkipped(template.Name, root, null, SkipReason.PathNotFound, "The path does not exist.");
                return;
            }

            missingPaths.Remove((template.Name, root));
            var directories = new List<string> { root };
            directories.AddRange(Directory.EnumerateDirectories(root, "*", RecursiveEnumeration).OrderBy(d => d, StringComparer.Ordinal));
            foreach (string directory in directories.Where(visited.Add))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    ProcessDirectory(template, directory, mode, context, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    context.Report.AddError($"The directory could not be processed: {ex.Message}", template.Name, directory, exception: ex);
                }
            }
        }

        private void ProcessDirectory(RunnerTemplate template, string directory, ScanMode mode, CycleContext context, CancellationToken cancellationToken)
        {
            DirectoryFingerprint fingerprint;
            try
            {
                fingerprint = DirectoryFingerprint.Take(directory, context.IsGeneratedFile);
            }
            catch (DirectoryNotFoundException)
            {
                return; // Removed since the path was listed.
            }
            catch (IOException ex) when (mode == ScanMode.Watch)
            {
                // Typically a file removed while the folder was listed: the folder is changing.
                context.Report.AddSkipped(template.Name, directory, null, SkipReason.Waiting, $"The folder could not be listed ({ex.Message}); it is looked at again next cycle.");
                return;
            }

            if (!fingerprint.HasDicomFiles)
            {
                return;
            }

            if (mode == ScanMode.Immediate)
            {
                DirectoryOutcome immediate = GenerateForDirectory(template, fingerprint, context, cancellationToken);
                if (immediate.TransientFailure != null)
                {
                    context.Report.AddError($"A file is in use ({immediate.TransientFailure}); run again when it is free.", template.Name, directory);
                }

                return;
            }

            DirectoryState state = GetState(template.Name, directory);
            state.LastVisitedCycle = context.Cycle;
            string? previous = state.LastSeen;
            state.LastSeen = fingerprint.Hash;

            if (state.FailureFingerprint != null && fingerprint.Hash != state.FailureFingerprint)
            {
                state.ResetBackoff();
            }

            if (state.GaveUp)
            {
                context.Report.AddSkipped(template.Name, directory, null, SkipReason.PreviouslyFailed,
                    $"Gave up after {MaxTransientAttempts} attempts ({state.LastTransientFailure}); retried when the folder's files change.");
                return;
            }

            if (state.TransientFailures > 0 && context.Cycle < state.RetryAtCycle)
            {
                context.Report.AddSkipped(template.Name, directory, null, SkipReason.BackingOff,
                    $"Next attempt in {state.RetryAtCycle - context.Cycle} cycle(s) ({state.LastTransientFailure}).");
                return;
            }

            if (state.Processed is { } done
                && done.Fingerprint == fingerprint.Hash
                && done.TemplateSignature == template.Signature
                && done.ExpectedOutputs.All(File.Exists))
            {
                if (done.HadErrors)
                {
                    context.Report.AddSkipped(template.Name, directory, null, SkipReason.PreviouslyFailed,
                        "The errors for these files were reported earlier; retried when the folder's files or the template change.");
                }
                else
                {
                    context.Report.AddSkipped(template.Name, directory, null, SkipReason.Unchanged, "Not changed since it was processed.");
                }

                return;
            }

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            TimeSpan age = now - fingerprint.NewestUtc;
            bool stable = previous == null || previous == fingerprint.Hash;
            if (!stable || age < settleTime)
            {
                string detail = stable
                    ? $"The newest file is {FormatAge(age)} old; waiting until it is {FormatAge(settleTime)} old."
                    : "The files changed since the last scan; waiting for them to settle.";
                context.Report.AddSkipped(template.Name, directory, null, SkipReason.Waiting, detail);
                return;
            }

            DirectoryOutcome outcome = GenerateForDirectory(template, fingerprint, context, cancellationToken);
            if (outcome.TransientFailure == null)
            {
                state.ResetBackoff();
                state.Processed = new ProcessedRecord(fingerprint.Hash, template.Signature, outcome.HadErrors, outcome.ExpectedOutputs);
                return;
            }

            state.TransientFailures++;
            state.FailureFingerprint = fingerprint.Hash;
            state.LastTransientFailure = outcome.TransientFailure;
            state.Processed = null;
            if (state.TransientFailures >= MaxTransientAttempts)
            {
                state.GaveUp = true;
                context.Report.AddError(
                    $"Gave up after {state.TransientFailures} attempts: a file is in use ({outcome.TransientFailure}). The folder is retried when its files change.",
                    template.Name, directory);
                return;
            }

            long delay = 1L << (state.TransientFailures - 1);
            state.RetryAtCycle = context.Cycle + delay;
            logger.LogWarning("A file in {Directory} is in use (template {Template}, attempt {Attempt}): {Failure}. Next attempt in {Delay} cycle(s).",
                directory, template.Name, state.TransientFailures, outcome.TransientFailure, delay);
            context.Report.AddSkipped(template.Name, directory, null, SkipReason.BackingOff,
                $"Attempt {state.TransientFailures} failed ({outcome.TransientFailure}); next attempt in {delay} cycle(s).");
        }

        private DirectoryOutcome GenerateForDirectory(RunnerTemplate template, DirectoryFingerprint fingerprint, CycleContext context, CancellationToken cancellationToken)
        {
            string directory = fingerprint.Directory;
            DirectoryScan scan = DicomParser.Scan(fingerprint.Files, cancellationToken);
            if (scan.TransientFailures.Count > 0)
            {
                return DirectoryOutcome.Transient(scan.TransientFailures[0].Describe());
            }

            if (scan.UnreadableFiles.Count > 0)
            {
                string files = string.Join("; ", scan.UnreadableFiles.Take(3).Select(f => f.Describe()))
                    + (scan.UnreadableFiles.Count > 3 ? $"; and {scan.UnreadableFiles.Count - 3} more" : string.Empty);
                context.Report.AddError(
                    $"{scan.UnreadableFiles.Count} file(s) could not be read as DICOM ({files}). No RT is written in this folder until they are fixed or removed, since a series could be incomplete.",
                    template.Name, directory);
                return DirectoryOutcome.Done(hadErrors: true, Array.Empty<string>());
            }

            var expectedOutputs = new List<string>();
            bool hadErrors = false;
            string? transient = null;
            foreach (ImageSeries series in scan.Series)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    hadErrors |= !GenerateForSeries(template, directory, series, context, expectedOutputs);
                }
                catch (Exception ex) when (DicomParser.IsTransient(ex))
                {
                    transient ??= $"{series.SeriesInstanceUid}: {ex.Message}";
                    logger.LogWarning(ex, "The RT for series {SeriesInstanceUid} in {Directory} (template {Template}) could not be written.", series.SeriesInstanceUid, directory, template.Name);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    hadErrors = true;
                    context.Report.AddError($"The RT could not be created: {ex.Message}", template.Name, directory, series.SeriesInstanceUid, ex);
                }
            }

            return transient != null ? DirectoryOutcome.Transient(transient) : DirectoryOutcome.Done(hadErrors, expectedOutputs);
        }

        /// <summary>Decides one series and writes its RT when needed. Returns false when an error was reported.</summary>
        private bool GenerateForSeries(RunnerTemplate template, string directory, ImageSeries series, CycleContext context, List<string> expectedOutputs)
        {
            string uid = series.SeriesInstanceUid;
            if (!GeneratedRtNames.IsValidUid(uid))
            {
                context.Report.AddError("The series has an invalid Series Instance UID, which cannot be used in a file name; no RT was written.", template.Name, directory, uid);
                return false;
            }

            string outputPath = Path.Combine(directory, GeneratedRtNames.FileName(template.Name, uid));
            if (File.Exists(outputPath))
            {
                expectedOutputs.Add(outputPath);

                // An RT written while its series was still arriving (a transfer that stalled longer than the settle time,
                // or file times that looked old) is never rewritten: say so, rather than let it stay incomplete unnoticed.
                string? incomplete = DescribeImagesMissingFromRt(outputPath, series);
                if (incomplete != null)
                {
                    context.Report.AddError(incomplete, template.Name, directory, uid);
                    return false;
                }

                context.Report.AddSkipped(template.Name, directory, uid, SkipReason.AlreadyDone, "The RT file already exists.");
                return true;
            }

            DicomDataset first = series.Slices[0].Dataset;
            string? notPlanning = PlanningImages.WhyNotPlanningSeries(first);
            if (notPlanning != null)
            {
                context.Report.AddSkipped(template.Name, directory, uid, SkipReason.NoMatch, notPlanning);
                return true;
            }

            string? seriesDescription = RtStructureBuilder.GetValue(first, DicomTag.SeriesDescription);
            string? studyDescription = RtStructureBuilder.GetValue(first, DicomTag.StudyDescription);
            if (!TemplateMatcher.Matches(template.Requirements, seriesDescription, studyDescription))
            {
                context.Report.AddSkipped(template.Name, directory, uid, SkipReason.NoMatch,
                    $"Series description '{seriesDescription}' and study description '{studyDescription}' do not match the template's requirements.");
                return true;
            }

            RtBuildResult result = context.Builder.Build(series.Slices.Select(s => s.Dataset).ToList(), template.Name, template.Rois, timeProvider.GetLocalNow());
            foreach ((string roiName, string reason) in result.RoiFailures)
            {
                context.Report.AddRoiFailure(template.Name, roiName, reason, directory, uid);
            }

            if (result.File == null)
            {
                context.Report.AddError(result.Error ?? "The RT could not be built.", template.Name, directory, uid);
                return false;
            }

            SaveAtomically(result.File, outputPath);
            expectedOutputs.Add(outputPath);
            context.Report.AddWritten(template.Name, directory, uid, outputPath, result.RoiCount);
            return true;
        }

        /// <summary>
        /// Why the existing RT <paramref name="rtPath"/> does not reference every image of <paramref name="series"/>, or
        /// null when it does, references no image at all (a hand-made RT; nothing to compare) or cannot be read.
        /// </summary>
        private string? DescribeImagesMissingFromRt(string rtPath, ImageSeries series)
        {
            HashSet<string> referenced;
            try
            {
                DicomDataset rt = DicomFile.Open(rtPath).Dataset;
                referenced = new HashSet<string>(
                    rt.GetSequence(DicomTag.ReferencedFrameOfReferenceSequence).Items
                        .SelectMany(frame => frame.TryGetSequence(DicomTag.RTReferencedStudySequence, out DicomSequence? studies) ? studies.Items : Enumerable.Empty<DicomDataset>())
                        .SelectMany(study => study.TryGetSequence(DicomTag.RTReferencedSeriesSequence, out DicomSequence? seriesItems) ? seriesItems.Items : Enumerable.Empty<DicomDataset>())
                        .SelectMany(item => item.TryGetSequence(DicomTag.ContourImageSequence, out DicomSequence? images) ? images.Items : Enumerable.Empty<DicomDataset>())
                        .Select(image => RtStructureBuilder.GetValue(image, DicomTag.ReferencedSOPInstanceUID) ?? string.Empty),
                    StringComparer.Ordinal);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "The existing RT {Path} could not be read to check its image references.", rtPath);
                return null;
            }

            referenced.Remove(string.Empty);
            if (referenced.Count == 0)
            {
                return null;
            }

            List<string> instances = series.Slices
                .Select(slice => RtStructureBuilder.GetValue(slice.Dataset, DicomTag.SOPInstanceUID) ?? string.Empty)
                .Where(u => u.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            int missing = instances.Count(u => !referenced.Contains(u));
            if (missing == 0)
            {
                return null;
            }

            return $"The series now has {instances.Count} images, but its RT {Path.GetFileName(rtPath)} references only {instances.Count - missing} of them: "
                + "images arrived after the RT was written. Delete that file so that the RT is written again for the complete series.";
        }

        /// <summary>
        /// Saves through a temporary file next to the target, so a reader never sees a partial RT and an existing
        /// file is never overwritten.
        /// </summary>
        private void SaveAtomically(DicomFile file, string outputPath)
        {
            string temporary = $"{outputPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                file.Save(temporary);
                File.Move(temporary, outputPath, overwrite: false);
            }
            finally
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "The temporary file {Path} could not be deleted.", temporary);
                }
            }
        }

        private DeleteReport DeleteGeneratedCore(HashSet<string>? onlyTemplates, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                var report = new RunReportBuilder(logger);
                var targets = new List<(string Name, IReadOnlyList<string> Paths)>();
                var found = new HashSet<string>(StringComparer.Ordinal);
                foreach (string folder in ListTemplateFolders(report))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string name = Path.GetFileName(folder);
                    if (onlyTemplates != null && !onlyTemplates.Contains(name))
                    {
                        continue;
                    }

                    try
                    {
                        if (RunnerTemplate.IsRunnable(folder))
                        {
                            found.Add(name);
                            targets.Add((name, RunnerTemplate.ReadPaths(folder)));
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        found.Add(name);
                        report.AddError($"The template's paths could not be read: {ex.Message}", name, exception: ex);
                    }
                }

                foreach (string missing in (onlyTemplates ?? new HashSet<string>()).Except(found).OrderBy(n => n, StringComparer.Ordinal))
                {
                    report.AddError($"There is no template '{missing}' in '{templateFolder}'.", missing);
                }

                return DeleteForTemplates(targets, report, cancellationToken);
            }
        }

        private DeleteReport DeleteForTemplates(IEnumerable<(string Name, IReadOnlyList<string> Paths)> templates, RunReportBuilder report, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                var deleted = new List<string>();
                var failed = new List<DeleteFailure>();
                var seen = new HashSet<string>(GeneratedRtNames.FileNameComparer);
                foreach ((string name, IReadOnlyList<string> paths) in templates)
                {
                    foreach (string path in paths)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            string root = Path.GetFullPath(path);
                            if (!Directory.Exists(root))
                            {
                                logger.LogDebug("Path {Path} of template {Template} does not exist; nothing to delete there.", root, name);
                                continue;
                            }

                            foreach (string file in Directory.EnumerateFiles(root, "*", RecursiveEnumeration))
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if (!GeneratedRtNames.IsGeneratedRt(Path.GetFileName(file), name) || !seen.Add(file))
                                {
                                    continue;
                                }

                                try
                                {
                                    File.Delete(file);
                                    deleted.Add(file);
                                    logger.LogInformation("Deleted {Path} (template {Template}).", file, name);
                                }
                                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                                {
                                    failed.Add(new DeleteFailure(file, ex.Message));
                                    logger.LogError(ex, "Could not delete {Path} (template {Template}).", file, name);
                                }
                            }
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            report.AddError($"The path could not be searched: {ex.Message}", name, path, exception: ex);
                        }
                    }
                }

                return new DeleteReport(deleted, failed, report.Errors.ToArray());
            }
        }

        private DirectoryState GetState(string templateName, string directory)
        {
            if (!states.TryGetValue((templateName, directory), out DirectoryState? state))
            {
                state = new DirectoryState();
                states.Add((templateName, directory), state);
            }

            return state;
        }

        /// <summary>Forgets directories that were not visited in the cycle (removed, or no longer listed).</summary>
        private void PruneStates(long cycleNumber)
        {
            foreach ((string, string) key in states.Where(s => s.Value.LastVisitedCycle < cycleNumber).Select(s => s.Key).ToList())
            {
                states.Remove(key);
            }
        }

        private static string FormatAge(TimeSpan age)
        {
            return age < TimeSpan.Zero ? "less than 0 s" : $"{age.TotalSeconds:0.#} s";
        }

        private enum ScanMode
        {
            /// <summary>Settle rule, back-off and the processed-folder cache (RunOnce, RunAsync).</summary>
            Watch,

            /// <summary>Read at once, remember nothing (RunForFolder, legacy methods).</summary>
            Immediate,
        }

        private sealed class CycleContext
        {
            public CycleContext(long cycle, RtStructureBuilder builder, Func<string, bool> isGeneratedFile, RunReportBuilder report)
            {
                Cycle = cycle;
                Builder = builder;
                IsGeneratedFile = isGeneratedFile;
                Report = report;
            }

            public long Cycle { get; }

            public RtStructureBuilder Builder { get; }

            /// <summary>True for file names of RTs (or their temporary files) written by any template.</summary>
            public Func<string, bool> IsGeneratedFile { get; }

            public RunReportBuilder Report { get; }
        }

        /// <summary>What the runner remembers of one directory for one template; fingerprints are stored as hashes.</summary>
        private sealed class DirectoryState
        {
            /// <summary>The fingerprint seen on the previous scan (for the settle rule).</summary>
            public string? LastSeen { get; set; }

            public ProcessedRecord? Processed { get; set; }

            public int TransientFailures { get; set; }

            public long RetryAtCycle { get; set; }

            /// <summary>The fingerprint the read failures happened with; a different one resets the back-off.</summary>
            public string? FailureFingerprint { get; set; }

            public string? LastTransientFailure { get; set; }

            public bool GaveUp { get; set; }

            public long LastVisitedCycle { get; set; }

            public void ResetBackoff()
            {
                TransientFailures = 0;
                RetryAtCycle = 0;
                FailureFingerprint = null;
                LastTransientFailure = null;
                GaveUp = false;
            }
        }

        private sealed record ProcessedRecord(string Fingerprint, string TemplateSignature, bool HadErrors, IReadOnlyList<string> ExpectedOutputs);

        private sealed class DirectoryOutcome
        {
            private DirectoryOutcome(string? transientFailure, bool hadErrors, IReadOnlyList<string> expectedOutputs)
            {
                TransientFailure = transientFailure;
                HadErrors = hadErrors;
                ExpectedOutputs = expectedOutputs;
            }

            /// <summary>Why the directory must be retried later, or null.</summary>
            public string? TransientFailure { get; }

            public bool HadErrors { get; }

            /// <summary>The RT files that exist for the directory's series (written or found).</summary>
            public IReadOnlyList<string> ExpectedOutputs { get; }

            public static DirectoryOutcome Transient(string failure) => new DirectoryOutcome(failure, false, Array.Empty<string>());

            public static DirectoryOutcome Done(bool hadErrors, IReadOnlyList<string> expectedOutputs) => new DirectoryOutcome(null, hadErrors, expectedOutputs);
        }
    }
}
