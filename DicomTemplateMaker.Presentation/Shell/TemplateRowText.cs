using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.Shell
{
    /// <summary>Texts for a template's row in the main window.</summary>
    public static class TemplateRowText
    {
        /// <summary>The row's tooltip: folder, ROI count, monitored folders, DICOM requirements and kept legacy files.</summary>
        public static string Tooltip(TemplateMaker template)
        {
            ArgumentNullException.ThrowIfNull(template);
            var text = new StringBuilder();
            text.AppendLine(template.TemplateName ?? "(unnamed template)");
            if (template.path != null)
            {
                text.Append("Folder: ").AppendLine(template.path);
            }

            text.AppendLine(Text.Count(template.ROIs.Count, "ROI"));
            text.AppendLine();
            if (template.Paths.Count == 0)
            {
                text.AppendLine("No monitored folders: the RT generator writes no RTs for this template until one is added (Edit ROIs and monitored DICOM paths).");
            }
            else
            {
                text.AppendLine("Monitored folders (" + template.Paths.Count + "):");
                text.AppendLine(Text.List(template.Paths, 10));
            }

            List<string> requirements = template.DicomTags
                .Where(tag => tag.Value.Any(v => !string.IsNullOrWhiteSpace(v)))
                .Select(tag => tag.Key + " contains any of: " + string.Join(", ", tag.Value.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => "\"" + v + "\"")))
                .ToList();
            text.AppendLine();
            text.AppendLine(requirements.Count == 0 ? "DICOM requirements: none (every image series gets an RT)." : "DICOM requirements:");
            if (requirements.Count > 0)
            {
                text.AppendLine(Text.List(requirements, 10));
            }

            if (template.LoadWarnings.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Legacy files kept because they could not be migrated:");
                text.AppendLine(Text.List(template.LoadWarnings, 10));
            }

            return text.ToString().TrimEnd();
        }
    }

    /// <summary>The template search in the main window.</summary>
    public static class TemplateSearch
    {
        /// <summary>True when <paramref name="query"/> is blank or <paramref name="name"/> contains it, ignoring case.</summary>
        public static bool Matches(string? name, string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return (name ?? string.Empty).Contains(query.Trim(), StringComparison.CurrentCultureIgnoreCase);
        }
    }

    /// <summary>
    /// The rows a bulk action applies to. Selected rows hidden by the search are never acted on; they are counted so
    /// confirmations can say so.
    /// </summary>
    public sealed class SelectionScope<T>
    {
        private SelectionScope(IReadOnlyList<T> all, IReadOnlyList<T> visibleSelected, IReadOnlyList<T> hiddenSelected)
        {
            All = all;
            VisibleSelected = visibleSelected;
            HiddenSelected = hiddenSelected;
        }

        public IReadOnlyList<T> All { get; }

        /// <summary>Selected rows the user can see: what bulk actions act on.</summary>
        public IReadOnlyList<T> VisibleSelected { get; }

        /// <summary>Selected rows hidden by the current search: never acted on.</summary>
        public IReadOnlyList<T> HiddenSelected { get; }

        public static SelectionScope<T> Of(IEnumerable<T> rows, Func<T, bool> isSelected, Func<T, bool> isVisible)
        {
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(isSelected);
            ArgumentNullException.ThrowIfNull(isVisible);
            List<T> all = rows.ToList();
            return new SelectionScope<T>(
                all,
                all.Where(r => isSelected(r) && isVisible(r)).ToList(),
                all.Where(r => isSelected(r) && !isVisible(r)).ToList());
        }
    }
}
