using System;
using System.Collections.Generic;
using System.Linq;
using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.Editors
{
    /// <summary>Rules for the entries of the ontology library, used by the ontology editor.</summary>
    public static class OntologyEntryRules
    {
        /// <summary>
        /// Why an entry with these values cannot be added or saved, or null when it can. The common name, code value and
        /// coding scheme are required, and no other entry may have the same code value: templates are matched to library
        /// entries by code value when they are read.
        /// </summary>
        /// <param name="self">The entry being edited, or null for a new entry.</param>
        public static string? Problem(string? name, string? codeValue, string? scheme, IEnumerable<OntologyCodeClass> library, OntologyCodeClass? self)
        {
            ArgumentNullException.ThrowIfNull(library);
            if (string.IsNullOrWhiteSpace(name) || OntologyCodeClass.RemoveIllegalCharacters(name).Trim().Length == 0)
            {
                return "Enter a common name.";
            }

            if (string.IsNullOrWhiteSpace(codeValue))
            {
                return "Enter a code value (required).";
            }

            if (string.IsNullOrWhiteSpace(scheme))
            {
                return "Enter a coding scheme (for example FMA, SCT or 99VMS_STRUCTCODE).";
            }

            string code = codeValue.Trim();
            OntologyCodeClass? same = library.FirstOrDefault(o => !ReferenceEquals(o, self) && string.Equals(o.CodeValue?.Trim(), code, StringComparison.Ordinal));
            if (same != null)
            {
                return $"Code value {code} is already used by \"{same.CodeMeaning}\" ({same.Scheme ?? "no scheme"}).";
            }

            return null;
        }

        /// <summary>The non-blocking warning for a common name another entry already has, or null.</summary>
        public static string? Warning(string? name, IEnumerable<OntologyCodeClass> library, OntologyCodeClass? self)
        {
            ArgumentNullException.ThrowIfNull(library);
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string wanted = name.Trim();
            // CodeMeaning is null for a hand-edited file that says "CodeMeaning": null.
            OntologyCodeClass? same = library.FirstOrDefault(o => !ReferenceEquals(o, self) && string.Equals(((string?)o.CodeMeaning)?.Trim(), wanted, StringComparison.OrdinalIgnoreCase));
            return same == null
                ? null
                : $"Another entry is also named \"{same.CodeMeaning}\" (code {same.CodeValue ?? "none"}); both appear under that name when adding ROIs.";
        }

        /// <summary>True when the entry's name, code value or scheme contains <paramref name="query"/> (ignoring case); missing values do not match.</summary>
        public static bool Matches(OntologyCodeClass entry, string? query)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            string wanted = query.Trim();
            return Contains(entry.CodeMeaning, wanted) || Contains(entry.CodeValue, wanted) || Contains(entry.Scheme, wanted);
        }

        /// <summary>
        /// True when an ROI whose code is <paramref name="code"/> uses library entry <paramref name="entry"/>: the same
        /// object, or the same code value (the rule used when templates are read). An entry without a code value is used
        /// by codes without one that have the same name.
        /// </summary>
        public static bool Uses(OntologyCodeClass? code, OntologyCodeClass entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (code == null)
            {
                return false;
            }

            if (ReferenceEquals(code, entry))
            {
                return true;
            }

            string? value = entry.CodeValue?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                return string.IsNullOrWhiteSpace(code.CodeValue) && string.Equals(code.CodeMeaning, entry.CodeMeaning, StringComparison.Ordinal);
            }

            return string.Equals(code.CodeValue?.Trim(), value, StringComparison.Ordinal);
        }

        /// <summary>The names of the templates with at least one ROI that uses <paramref name="entry"/>, sorted.</summary>
        public static IReadOnlyList<string> TemplatesUsing(OntologyCodeClass entry, IEnumerable<TemplateMaker> templates)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(templates);
            return templates
                .Where(template => template.ROIs.Any(roi => Uses(roi.Ontology_Class, entry)))
                .Select(template => template.TemplateName ?? "(unnamed)")
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static bool Contains(string? value, string wanted)
        {
            return value != null && value.Contains(wanted, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
