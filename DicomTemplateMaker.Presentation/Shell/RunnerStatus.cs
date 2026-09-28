using System;
using System.Collections.Generic;
using System.Linq;
using DicomTemplateMakerGUI.DicomTemplateServices;

namespace DicomTemplateMakerGUI.Shell
{
    /// <summary>
    /// The status line of the RT generator: totals since it was started (RTs written, ROIs skipped), the state of the
    /// last scan (errors, folders waiting) and the folders with open problems. The full reports go to the log.
    /// </summary>
    /// <remarks>
    /// The runner reports a folder's failure (an unreadable file, a series without a Frame of Reference, files in use
    /// after every retry) in one scan only, then skips the folder as <see cref="SkipReason.PreviouslyFailed"/> until its
    /// files or the template change. Such a folder gets no RT, so its problem stays listed here until a later scan
    /// handles the folder without an error, or no longer visits it (its files or the path were removed).
    /// </remarks>
    public sealed class RunnerStatus
    {
        private readonly Dictionary<(string Template, string Directory), List<RunError>> openProblems = new Dictionary<(string, string), List<RunError>>();

        public int WrittenSinceStart { get; private set; }

        public int RoisSkippedSinceStart { get; private set; }

        public RunReport? LastReport { get; private set; }

        /// <summary>Folders (per template) whose last error is not resolved: no RT is written for their series until it is.</summary>
        public int OpenProblemCount => openProblems.Count;

        /// <summary>Starts new totals (when the generator is started).</summary>
        public void Reset()
        {
            WrittenSinceStart = 0;
            RoisSkippedSinceStart = 0;
            LastReport = null;
            openProblems.Clear();
        }

        public void Record(RunReport report)
        {
            ArgumentNullException.ThrowIfNull(report);
            WrittenSinceStart += report.WrittenCount;
            RoisSkippedSinceStart += report.RoiFailureCount;
            LastReport = report;
            UpdateOpenProblems(report);
        }

        /// <summary>One line for the status bar; times are shown in <paramref name="zone"/>.</summary>
        public string Describe(RunnerState state, TimeZoneInfo zone)
        {
            ArgumentNullException.ThrowIfNull(zone);
            switch (state)
            {
                case RunnerState.Paused:
                    return "RT generator paused while another operation runs; it resumes afterwards.";
                case RunnerState.Running:
                    return LastReport == null
                        ? "RT generator running: first scan in progress…"
                        : "RT generator running. " + DescribeLastScan(zone);
                default:
                    return LastReport == null
                        ? "RT generator stopped. Start it to write RTs for new images in the monitored folders."
                        : "RT generator stopped. " + DescribeLastScan(zone);
            }
        }

        /// <summary>
        /// The last scan's errors and the folders with open problems, one per line (for a tooltip); null when there are
        /// none.
        /// </summary>
        public string? DescribeErrors(int max = 10)
        {
            var parts = new List<string>();
            if (LastReport != null && LastReport.ErrorCount > 0)
            {
                parts.Add("Errors in the last scan (details are in the log):" + Environment.NewLine
                    + Text.List(LastReport.Errors.Select(e => e.Context + ": " + e.Message), max));
            }

            List<RunError> open = openProblems.Values.SelectMany(p => p).ToList();
            if (open.Count > 0)
            {
                parts.Add("Folders with open problems; no RT is written for their series until the problem is fixed:" + Environment.NewLine
                    + Text.List(open.Select(e => e.Context + ": " + e.Message), max));
            }

            return parts.Count == 0 ? null : string.Join(Environment.NewLine + Environment.NewLine, parts);
        }

        /// <summary>Every entry of <paramref name="report"/> worth logging: written RTs, skipped ROIs and errors.</summary>
        public static IEnumerable<string> DescribeReport(RunReport report)
        {
            ArgumentNullException.ThrowIfNull(report);
            foreach (WrittenRt written in report.Written)
            {
                yield return $"  wrote {written.Path} (template '{written.TemplateName}', {written.RoiCount} ROIs)";
            }

            foreach (RoiFailure failure in report.RoiFailures)
            {
                yield return $"  ROI '{failure.RoiName}' left out (template '{failure.TemplateName}', {failure.Directory}): {failure.Reason}";
            }

            foreach (RunError error in report.Errors)
            {
                yield return $"  error ({error.Context}): {error.Message}";
            }
        }

        /// <summary>
        /// Folder errors of this scan become open problems; an open problem stays while the folder is skipped for it,
        /// backs off or waits, and is dropped when the folder is handled without an error or not visited at all.
        /// </summary>
        private void UpdateOpenProblems(RunReport report)
        {
            var errorsNow = new Dictionary<(string, string), List<RunError>>();
            foreach (RunError error in report.Errors)
            {
                if (error.TemplateName == null || error.Directory == null)
                {
                    continue; // template-level errors are reported by every scan while they last
                }

                (string, string) key = (error.TemplateName, error.Directory);
                if (!errorsNow.TryGetValue(key, out List<RunError>? list))
                {
                    list = new List<RunError>();
                    errorsNow.Add(key, list);
                }

                list.Add(error);
            }

            var stillOpen = new HashSet<(string, string)>(report.Skipped
                .Where(s => s.SeriesInstanceUid == null && (s.Reason == SkipReason.PreviouslyFailed || s.Reason == SkipReason.BackingOff || s.Reason == SkipReason.Waiting))
                .Select(s => (s.TemplateName, s.Directory)));
            foreach ((string, string) key in openProblems.Keys.ToList())
            {
                if (!stillOpen.Contains(key))
                {
                    openProblems.Remove(key);
                }
            }

            foreach (KeyValuePair<(string, string), List<RunError>> entry in errorsNow)
            {
                openProblems[entry.Key] = entry.Value;
            }
        }

        private string DescribeLastScan(TimeZoneInfo zone)
        {
            RunReport report = LastReport!;
            DateTimeOffset local = TimeZoneInfo.ConvertTime(report.StartedAt, zone);
            string text = $"Last scan {local:HH:mm:ss}: {Text.Count(WrittenSinceStart, "RT")} written and {Text.Count(RoisSkippedSinceStart, "ROI")} skipped since start; "
                + $"{Text.Count(report.ErrorCount, "error")}, {Text.Count(report.WaitingCount, "folder")} waiting";
            if (report.BackingOffCount > 0)
            {
                text += $", {Text.Count(report.BackingOffCount, "folder")} retried later (files in use)";
            }

            if (openProblems.Count > 0)
            {
                text += $"; {Text.Count(openProblems.Count, "folder")} with open problems, no RT written there (see the tooltip)";
            }

            return text + ".";
        }
    }
}
