using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace DicomTemplateMakerGUI.DicomTemplateServices
{
    /// <summary>Collects the entries of a <see cref="RunReport"/> and logs each one as it is added.</summary>
    internal sealed class RunReportBuilder
    {
        private readonly ILogger logger;
        private readonly Dictionary<string, string>? recurringErrors;
        private readonly List<WrittenRt> written = new List<WrittenRt>();
        private readonly List<SkippedItem> skipped = new List<SkippedItem>();
        private readonly List<RoiFailure> roiFailures = new List<RoiFailure>();
        private readonly List<RunError> errors = new List<RunError>();

        /// <param name="logger">Where entries are logged.</param>
        /// <param name="recurringErrors">
        /// Errors with a recurring key that were logged in earlier cycles (key to message). A recurring error is
        /// still added to every report but logged as an error only when its message changes, so a watch loop
        /// does not repeat it every few seconds. Null to log every error.
        /// </param>
        public RunReportBuilder(ILogger logger, Dictionary<string, string>? recurringErrors = null)
        {
            this.logger = logger;
            this.recurringErrors = recurringErrors;
        }

        public void AddWritten(string templateName, string directory, string seriesInstanceUid, string path, int roiCount)
        {
            written.Add(new WrittenRt(templateName, directory, seriesInstanceUid, path, roiCount));
            logger.LogInformation("Wrote {Path} (template {Template}, series {SeriesInstanceUid}, {RoiCount} ROIs).", path, templateName, seriesInstanceUid, roiCount);
        }

        public void AddSkipped(string templateName, string directory, string? seriesInstanceUid, SkipReason reason, string detail)
        {
            skipped.Add(new SkippedItem(templateName, directory, seriesInstanceUid, reason, detail));
            logger.LogDebug("Skipped (template {Template}, directory {Directory}, series {SeriesInstanceUid}): {Reason}. {Detail}", templateName, directory, seriesInstanceUid, reason, detail);
        }

        public void AddRoiFailure(string templateName, string roiName, string reason, string directory, string seriesInstanceUid)
        {
            roiFailures.Add(new RoiFailure(templateName, roiName, reason, directory, seriesInstanceUid));
            logger.LogWarning("ROI {Roi} was left out of the RT (template {Template}, directory {Directory}, series {SeriesInstanceUid}): {Reason}", roiName, templateName, directory, seriesInstanceUid, reason);
        }

        /// <param name="message">What failed, readable by users.</param>
        /// <param name="templateName">The template, when the failure concerns one.</param>
        /// <param name="directory">The directory or path, when the failure concerns one.</param>
        /// <param name="seriesInstanceUid">The series, when the failure concerns one.</param>
        /// <param name="exception">An unexpected exception, logged with its stack trace.</param>
        /// <param name="recurringKey">Identifies an error that is expected again every cycle until fixed (see the constructor).</param>
        public void AddError(string message, string? templateName = null, string? directory = null, string? seriesInstanceUid = null, Exception? exception = null, string? recurringKey = null)
        {
            RunError error = RunError.Create(message, templateName, directory, seriesInstanceUid);
            errors.Add(error);
            bool repeated = false;
            if (recurringKey != null && recurringErrors != null)
            {
                repeated = recurringErrors.TryGetValue(recurringKey, out string? previous) && previous == message;
                recurringErrors[recurringKey] = message;
            }

            logger.Log(repeated ? LogLevel.Debug : LogLevel.Error, exception, "{Context}: {Message}", error.Context, message);
        }

        /// <summary>Forgets a recurring error once its cause is gone, so it is logged again if it comes back.</summary>
        public void ClearRecurring(string recurringKey)
        {
            recurringErrors?.Remove(recurringKey);
        }

        public IReadOnlyList<RunError> Errors => errors;

        public RunReport Build(DateTimeOffset startedAt, TimeSpan duration)
        {
            return new RunReport(startedAt, duration, written.ToArray(), skipped.ToArray(), roiFailures.ToArray(), errors.ToArray());
        }
    }
}
