using System;
using System.Collections.Generic;
using System.Linq;
using DicomTemplateMakerGUI.DicomTemplateServices;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.Editors
{
    /// <summary>
    /// The paths editor's working copy of a template's monitored folders and DICOM requirements. Edits change only the
    /// copy; <see cref="ApplyTo"/> puts them into the template, so closing the editor without saving discards them.
    /// </summary>
    public sealed class MonitoredSettingsDraft
    {
        /// <summary>The DICOM attributes a requirement can test.</summary>
        public static IReadOnlyList<string> RequirementKeys { get; } = new[] { TemplateRequirements.SeriesDescriptionKey, TemplateRequirements.StudyDescriptionKey };

        private readonly List<string> paths;
        private readonly List<KeyValuePair<string, List<string>>> requirements;

        public MonitoredSettingsDraft(IEnumerable<string> paths, IEnumerable<KeyValuePair<string, List<string>>> requirements)
        {
            ArgumentNullException.ThrowIfNull(paths);
            ArgumentNullException.ThrowIfNull(requirements);
            this.paths = paths.ToList();
            this.requirements = requirements.Select(r => new KeyValuePair<string, List<string>>(r.Key, r.Value.ToList())).ToList();
        }

        /// <summary>A copy of the template's current monitored folders and requirements.</summary>
        public static MonitoredSettingsDraft From(TemplateMaker template)
        {
            ArgumentNullException.ThrowIfNull(template);
            return new MonitoredSettingsDraft(template.Paths, template.DicomTags);
        }

        public IReadOnlyList<string> Paths => paths;

        /// <summary>Every requirement, one per value, in order.</summary>
        public IReadOnlyList<(string Key, string Value)> Requirements => requirements.SelectMany(r => r.Value.Select(v => (r.Key, v))).ToList();

        /// <summary>True once anything was added or removed.</summary>
        public bool IsDirty { get; private set; }

        /// <summary>Adds a monitored folder; returns why it was not added, or null.</summary>
        public string? AddPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return PathProblem(paths, path);
            }

            string? problem = PathProblem(paths, path);
            if (problem != null)
            {
                return problem;
            }

            paths.Add(path.Trim());
            IsDirty = true;
            return null;
        }

        public bool RemovePath(string path)
        {
            bool removed = paths.Remove(path);
            IsDirty |= removed;
            return removed;
        }

        /// <summary>Adds a requirement (the value is trimmed); returns why it was not added, or null.</summary>
        public string? AddRequirement(string key, string? value)
        {
            string? problem = RequirementProblem(Requirements, key, value);
            if (problem != null || value == null)
            {
                return problem;
            }

            string trimmed = value.Trim();
            int index = requirements.FindIndex(r => r.Key == key);
            if (index < 0)
            {
                requirements.Add(new KeyValuePair<string, List<string>>(key, new List<string> { trimmed }));
            }
            else
            {
                requirements[index].Value.Add(trimmed);
            }

            IsDirty = true;
            return null;
        }

        /// <summary>Removes one requirement; a key left without values is removed too.</summary>
        public bool RemoveRequirement(string key, string value)
        {
            int index = requirements.FindIndex(r => r.Key == key);
            if (index < 0 || !requirements[index].Value.Remove(value))
            {
                return false;
            }

            if (requirements[index].Value.Count == 0)
            {
                requirements.RemoveAt(index);
            }

            IsDirty = true;
            return true;
        }

        /// <summary>Replaces the template's monitored folders and requirements (in place) with this copy's.</summary>
        public void ApplyTo(TemplateMaker template)
        {
            ArgumentNullException.ThrowIfNull(template);
            template.Paths.Clear();
            template.Paths.AddRange(paths);
            template.DicomTags.Clear();
            foreach (KeyValuePair<string, List<string>> requirement in requirements)
            {
                template.DicomTags[requirement.Key] = requirement.Value.ToList();
            }
        }

        /// <summary>Why <paramref name="candidate"/> cannot be added to <paramref name="existing"/>, or null.</summary>
        public static string? PathProblem(IEnumerable<string> existing, string? candidate)
        {
            ArgumentNullException.ThrowIfNull(existing);
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return "Choose a folder.";
            }

            string? same = existing.FirstOrDefault(p => SamePath(p, candidate));
            return same == null ? null : same + " is already monitored.";
        }

        /// <summary>Why this requirement cannot be added, or null. Values are compared ignoring case, as matching does.</summary>
        public static string? RequirementProblem(IEnumerable<(string Key, string Value)> existing, string key, string? value)
        {
            ArgumentNullException.ThrowIfNull(existing);
            if (!RequirementKeys.Contains(key))
            {
                return "Choose " + string.Join(" or ", RequirementKeys) + ".";
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return "Enter a value: a blank requirement never matches.";
            }

            if (value.Contains('\\'))
            {
                return "A requirement cannot contain a backslash (\\): DicomTags.txt uses it to separate values.";
            }

            if (value.Any(char.IsControl))
            {
                return "A requirement cannot contain a line break or other control character.";
            }

            string trimmed = value.Trim();
            if (existing.Any(r => r.Key == key && string.Equals(r.Value.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return key + " already requires \"" + trimmed + "\".";
            }

            return null;
        }

        /// <summary>
        /// True when both name the same folder, ignoring case, the kind of slash and a trailing slash (monitored folders
        /// are Windows paths).
        /// </summary>
        public static bool SamePath(string a, string b)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);
            return string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string path)
        {
            string normalized = path.Trim().Replace('/', '\\');
            // Keep the separator of a drive root ("C:\") and of a bare "\".
            while (normalized.Length > 1 && normalized.EndsWith('\\') && !normalized.EndsWith(":\\", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }

            return normalized;
        }
    }
}
