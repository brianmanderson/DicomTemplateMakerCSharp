using System;
using System.Collections.Generic;
using System.Linq;

namespace DicomTemplateMakerGUI.DicomTemplateServices
{
    /// <summary>Why a series or a directory produced no RT in a run.</summary>
    public enum SkipReason
    {
        /// <summary>The series' RT file already exists.</summary>
        AlreadyDone,

        /// <summary>The series' descriptions do not match the template's requirements.</summary>
        NoMatch,

        /// <summary>The directory's files are still changing or too recent; it is looked at again next cycle.</summary>
        Waiting,

        /// <summary>A file in the directory could not be read (in use); retried after a back-off of 1, 2, 4 and 8 cycles.</summary>
        BackingOff,

        /// <summary>The directory has not changed since it was processed; nothing to do.</summary>
        Unchanged,

        /// <summary>A failure for the directory's current files was already reported; retried when its files or the template change.</summary>
        PreviouslyFailed,

        /// <summary>A path listed for the template does not exist (for example, a disconnected drive).</summary>
        PathNotFound,
    }

    /// <summary>An RT Structure Set written by a run.</summary>
    public sealed record WrittenRt(string TemplateName, string Directory, string SeriesInstanceUid, string Path, int RoiCount);

    /// <summary>A series (or, when <see cref="SeriesInstanceUid"/> is null, a whole directory) that got no RT, and why.</summary>
    public sealed record SkippedItem(string TemplateName, string Directory, string? SeriesInstanceUid, SkipReason Reason, string Detail);

    /// <summary>An ROI that was left out of a written RT (or of an RT that was then not written), and why.</summary>
    public sealed record RoiFailure(string TemplateName, string RoiName, string Reason, string Directory, string SeriesInstanceUid);

    /// <summary>A failure, with where it happened. <see cref="Context"/> is a readable summary of the other fields.</summary>
    public sealed record RunError(string Context, string Message, string? TemplateName = null, string? Directory = null, string? SeriesInstanceUid = null)
    {
        internal static RunError Create(string message, string? templateName = null, string? directory = null, string? seriesInstanceUid = null)
        {
            var parts = new List<string>();
            if (templateName != null)
            {
                parts.Add($"template '{templateName}'");
            }

            if (directory != null)
            {
                parts.Add($"directory '{directory}'");
            }

            if (seriesInstanceUid != null)
            {
                parts.Add($"series {seriesInstanceUid}");
            }

            string context = parts.Count == 0 ? "run" : string.Join(", ", parts);
            return new RunError(context, message, templateName, directory, seriesInstanceUid);
        }
    }

    /// <summary>The outcome of one run cycle (<see cref="DicomTemplateRunner.RunOnce"/>) or one folder run.</summary>
    public sealed class RunReport
    {
        public RunReport(
            DateTimeOffset startedAt,
            TimeSpan duration,
            IReadOnlyList<WrittenRt> written,
            IReadOnlyList<SkippedItem> skipped,
            IReadOnlyList<RoiFailure> roiFailures,
            IReadOnlyList<RunError> errors)
        {
            StartedAt = startedAt;
            Duration = duration;
            Written = written;
            Skipped = skipped;
            RoiFailures = roiFailures;
            Errors = errors;
        }

        /// <summary>When the scan started (from the runner's TimeProvider).</summary>
        public DateTimeOffset StartedAt { get; }

        /// <summary>How long the scan took.</summary>
        public TimeSpan Duration { get; }

        public IReadOnlyList<WrittenRt> Written { get; }

        public IReadOnlyList<SkippedItem> Skipped { get; }

        public IReadOnlyList<RoiFailure> RoiFailures { get; }

        public IReadOnlyList<RunError> Errors { get; }

        public int WrittenCount => Written.Count;

        public int RoiFailureCount => RoiFailures.Count;

        public int ErrorCount => Errors.Count;

        /// <summary>Directories deferred because their files are still changing or too recent.</summary>
        public int WaitingCount => Count(SkipReason.Waiting);

        /// <summary>Directories deferred after a read failure (file in use).</summary>
        public int BackingOffCount => Count(SkipReason.BackingOff);

        public bool HasErrors => Errors.Count > 0;

        /// <summary>One line for a status bar or console: RTs written, ROI failures, errors and folders waiting.</summary>
        public string Summary =>
            $"{WrittenCount} RT(s) written, {RoiFailureCount} ROI failure(s), {ErrorCount} error(s), {WaitingCount} folder(s) waiting"
            + (BackingOffCount > 0 ? $", {BackingOffCount} folder(s) backing off" : string.Empty);

        public int Count(SkipReason reason) => Skipped.Count(s => s.Reason == reason);

        /// <summary>A report for a cycle that failed as a whole with <paramref name="exception"/>.</summary>
        public static RunReport ForFailedCycle(DateTimeOffset startedAt, Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            return new RunReport(
                startedAt,
                TimeSpan.Zero,
                Array.Empty<WrittenRt>(),
                Array.Empty<SkippedItem>(),
                Array.Empty<RoiFailure>(),
                new[] { RunError.Create($"The run cycle failed: {exception.Message}") });
        }
    }

    /// <summary>A generated RT file that could not be deleted.</summary>
    public sealed record DeleteFailure(string Path, string Message);

    /// <summary>The outcome of <see cref="DicomTemplateRunner.DeleteGenerated(System.Threading.CancellationToken)"/>.</summary>
    public sealed class DeleteReport
    {
        public DeleteReport(IReadOnlyList<string> deleted, IReadOnlyList<DeleteFailure> failed, IReadOnlyList<RunError> errors)
        {
            Deleted = deleted;
            Failed = failed;
            Errors = errors;
        }

        /// <summary>Full paths of the deleted files.</summary>
        public IReadOnlyList<string> Deleted { get; }

        /// <summary>Files that matched but could not be deleted.</summary>
        public IReadOnlyList<DeleteFailure> Failed { get; }

        /// <summary>Templates or paths that could not be searched.</summary>
        public IReadOnlyList<RunError> Errors { get; }

        public bool HasFailures => Failed.Count > 0 || Errors.Count > 0;
    }
}
