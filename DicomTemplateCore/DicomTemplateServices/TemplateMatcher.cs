using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace DicomTemplateMakerGUI.DicomTemplateServices
{
    /// <summary>
    /// The Series Description and Study Description requirements of one template, as read from its
    /// DicomTags.txt. Blank entries are dropped: a line such as <c>Series Description\</c> used to add an
    /// empty requirement that matched every series.
    /// </summary>
    public sealed class TemplateRequirements
    {
        public const string SeriesDescriptionKey = "Series Description";
        public const string StudyDescriptionKey = "Study Description";

        public static readonly TemplateRequirements None = new TemplateRequirements(Array.Empty<string>(), Array.Empty<string>());

        public TemplateRequirements(IEnumerable<string?> seriesDescriptions, IEnumerable<string?> studyDescriptions)
        {
            SeriesDescriptions = NonBlank(seriesDescriptions);
            StudyDescriptions = NonBlank(studyDescriptions);
        }

        /// <summary>Non-blank Series Description requirements, in file order.</summary>
        public IReadOnlyList<string> SeriesDescriptions { get; }

        /// <summary>Non-blank Study Description requirements, in file order.</summary>
        public IReadOnlyList<string> StudyDescriptions { get; }

        /// <summary>False when the template has no (non-blank) requirement, so it applies to every series.</summary>
        public bool HasAny => SeriesDescriptions.Count > 0 || StudyDescriptions.Count > 0;

        /// <summary>
        /// Parses the lines of a DicomTags.txt file: <c>key\value1\value2...</c>, where key is
        /// "Series Description" or "Study Description" (exact spelling); other keys are ignored.
        /// </summary>
        public static TemplateRequirements Parse(IEnumerable<string> dicomTagsLines)
        {
            var series = new List<string>();
            var studies = new List<string>();
            foreach (string line in dicomTagsLines)
            {
                string[] parts = line.Split('\\');
                IEnumerable<string> values = parts.Skip(1);
                if (parts[0] == SeriesDescriptionKey)
                {
                    series.AddRange(values);
                }
                else if (parts[0] == StudyDescriptionKey)
                {
                    studies.AddRange(values);
                }
            }

            return new TemplateRequirements(series, studies);
        }

        private static IReadOnlyList<string> NonBlank(IEnumerable<string?> values)
        {
            return values.OfType<string>().Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        }
    }

    /// <summary>
    /// Decides whether a template applies to an image series. The rule is the one templates have always
    /// relied on, a case-insensitive substring test in either direction, except that blank or missing
    /// descriptions never match (the old test matched them against everything).
    /// </summary>
    public static class TemplateMatcher
    {
        /// <summary>The value the old runner used for a missing tag; it is treated as missing.</summary>
        public const string MissingValueSentinel = "NA";

        /// <summary>
        /// True when the template has no requirements, or the series description matches any Series
        /// requirement, or the study description matches any Study requirement.
        /// </summary>
        /// <param name="requirements">The template's requirements.</param>
        /// <param name="seriesDescription">The series' Series Description, or null when the tag is missing.</param>
        /// <param name="studyDescription">The series' Study Description, or null when the tag is missing.</param>
        public static bool Matches(TemplateRequirements requirements, string? seriesDescription, string? studyDescription)
        {
            ArgumentNullException.ThrowIfNull(requirements);
            if (!requirements.HasAny)
            {
                return true;
            }

            return requirements.SeriesDescriptions.Any(r => ValueMatches(r, seriesDescription))
                || requirements.StudyDescriptions.Any(r => ValueMatches(r, studyDescription));
        }

        /// <summary>
        /// True when <paramref name="requirement"/> is not blank, <paramref name="value"/> is not missing (see
        /// <see cref="IsMissing"/>), and one contains the other ignoring case.
        /// </summary>
        public static bool ValueMatches(string? requirement, string? value)
        {
            if (string.IsNullOrWhiteSpace(requirement) || IsMissing(value))
            {
                return false;
            }

            string r = requirement.ToLowerInvariant();
            string v = value.ToLowerInvariant();
            return v.Contains(r, StringComparison.Ordinal) || r.Contains(v, StringComparison.Ordinal);
        }

        /// <summary>True for null, empty, whitespace-only values and the old "NA" sentinel for a missing tag.</summary>
        public static bool IsMissing([NotNullWhen(false)] string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                || string.Equals(value.Trim(), MissingValueSentinel, StringComparison.OrdinalIgnoreCase);
        }
    }
}
