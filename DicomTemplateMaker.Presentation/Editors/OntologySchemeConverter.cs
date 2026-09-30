using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DicomTemplateMakerGUI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.Editors
{
    /// <summary>A coding scheme offered by the scheme converter.</summary>
    /// <param name="DisplayName">What the window shows.</param>
    /// <param name="Designator">The Coding Scheme Designator stored in the codes.</param>
    public sealed record SchemeChoice(string DisplayName, string Designator)
    {
        public override string ToString() => DisplayName;
    }

    /// <summary>What a conversion would change; see <see cref="OntologySchemeConverter.Plan"/>.</summary>
    public sealed class SchemeConversionPlan
    {
        internal SchemeConversionPlan(IReadOnlyList<(string Template, int Rois)> templates, int libraryEntries, int roisWithoutEquivalent, int libraryEntriesWithoutEquivalent)
        {
            Templates = templates;
            LibraryEntryCount = libraryEntries;
            RoisWithoutEquivalent = roisWithoutEquivalent;
            LibraryEntriesWithoutEquivalent = libraryEntriesWithoutEquivalent;
        }

        /// <summary>The templates with ROIs to convert, and how many in each.</summary>
        public IReadOnlyList<(string Template, int Rois)> Templates { get; }

        public int TemplateCount => Templates.Count;

        public int RoiCount => Templates.Sum(t => t.Rois);

        public int LibraryEntryCount { get; }

        /// <summary>ROIs in the source scheme whose code has no equivalent (or no code value); they are left unchanged.</summary>
        public int RoisWithoutEquivalent { get; }

        /// <summary>Library entries in the source scheme whose code has no equivalent (or no code value); they are left unchanged.</summary>
        public int LibraryEntriesWithoutEquivalent { get; }

        public bool IsEmpty => RoiCount == 0 && LibraryEntryCount == 0;
    }

    /// <summary>What a conversion changed; see <see cref="OntologySchemeConverter.Apply"/>.</summary>
    public sealed class SchemeConversionResult
    {
        internal SchemeConversionResult(int roisChanged, IReadOnlyList<string> templatesSaved, int libraryEntriesChanged, int libraryEntriesMerged, IReadOnlyList<string> failures)
        {
            RoisChanged = roisChanged;
            TemplatesSaved = templatesSaved;
            LibraryEntriesChanged = libraryEntriesChanged;
            LibraryEntriesMerged = libraryEntriesMerged;
            Failures = failures;
        }

        /// <summary>ROIs converted in the templates that were saved.</summary>
        public int RoisChanged { get; }

        public IReadOnlyList<string> TemplatesSaved { get; }

        public int LibraryEntriesChanged { get; }

        /// <summary>Converted library entries dropped because the library already had an entry with the new code.</summary>
        public int LibraryEntriesMerged { get; }

        /// <summary>One line per template that could not be saved ("name: reason"); its file is unchanged.</summary>
        public IReadOnlyList<string> Failures { get; }
    }

    /// <summary>
    /// Converts ontology codes between FMA and SNOMED CT with the table shipped with the program
    /// (<see cref="FMAID_SNOMED_OntologyClass"/>), in the templates' ROIs and in the ontology library.
    /// </summary>
    public static class OntologySchemeConverter
    {
        public const string Fma = "FMA";
        public const string SnomedCt = "SCT";

        public static IReadOnlyList<SchemeChoice> Choices { get; } = new[]
        {
            new SchemeChoice("FMA", Fma),
            new SchemeChoice("SNOMED CT", SnomedCt),
        };

        /// <summary>True when both schemes are chosen and differ: converting a scheme into itself would corrupt every code.</summary>
        public static bool CanConvert(string? from, string? to)
        {
            return !string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to) && !string.Equals(from, to, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The code table for converting from <paramref name="from"/> (FMA or SCT).</summary>
        public static IReadOnlyDictionary<string, string> MapFrom(FMAID_SNOMED_OntologyClass table, string from)
        {
            ArgumentNullException.ThrowIfNull(table);
            if (string.Equals(from, Fma, StringComparison.OrdinalIgnoreCase))
            {
                return table.FMA_to_SNOMED;
            }

            if (string.Equals(from, SnomedCt, StringComparison.OrdinalIgnoreCase))
            {
                return table.SNOMED_To_FMA;
            }

            throw new ArgumentException("Only FMA and SCT codes can be converted.", nameof(from));
        }

        /// <summary>Counts what <see cref="Apply"/> would change, without changing anything.</summary>
        public static SchemeConversionPlan Plan(IEnumerable<TemplateMaker> templates, IEnumerable<OntologyCodeClass> library, string from, string to, IReadOnlyDictionary<string, string> map)
        {
            ArgumentNullException.ThrowIfNull(templates);
            ArgumentNullException.ThrowIfNull(library);
            ArgumentNullException.ThrowIfNull(map);
            RequireDifferent(from, to);
            var perTemplate = new List<(string Template, int Rois)>();
            int roisWithout = 0;
            foreach (TemplateMaker template in templates)
            {
                int convertible = template.ROIs.Count(roi => IsConvertible(roi.Ontology_Class, from, map));
                roisWithout += template.ROIs.Count(roi => HasScheme(roi.Ontology_Class, from) && !IsConvertible(roi.Ontology_Class, from, map));
                if (convertible > 0)
                {
                    perTemplate.Add((template.TemplateName ?? "(unnamed)", convertible));
                }
            }

            List<OntologyCodeClass> entries = library.ToList();
            int libraryConvertible = entries.Count(entry => IsConvertible(entry, from, map));
            int libraryWithout = entries.Count(entry => HasScheme(entry, from) && !IsConvertible(entry, from, map));
            return new SchemeConversionPlan(perTemplate, libraryConvertible, roisWithout, libraryWithout);
        }

        /// <summary>
        /// Converts the library in <paramref name="ontoPath"/> and saves it, then converts each template's ROIs and saves
        /// each template that changed (<see cref="TemplateMaker.make_template"/>). The library is read first, so an
        /// unreadable library (<see cref="TemplateLoadException"/>) or a failed library save stops the conversion before
        /// any template changes. A template that cannot be saved is reported in
        /// <see cref="SchemeConversionResult.Failures"/> and the others go on; its ROIs are converted in memory only, so
        /// the caller should read the templates again afterwards.
        /// </summary>
        public static SchemeConversionResult Apply(IReadOnlyList<TemplateMaker> templates, string ontoPath, string from, string to, IReadOnlyDictionary<string, string> map, ILogger? logger = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(templates);
            ArgumentException.ThrowIfNullOrWhiteSpace(ontoPath);
            ArgumentNullException.ThrowIfNull(map);
            RequireDifferent(from, to);
            logger ??= NullLogger.Instance;

            List<OntologyCodeClass> library = OntologyTools.LoadOntologiesFromFolder(ontoPath);
            (int libraryChanged, int merged) = ConvertLibrary(library, from, to, map);
            if (libraryChanged > 0)
            {
                OntologyTools.SaveOntologiesToFolder(library, ontoPath);
                logger.LogInformation("Converted {Count} ontology library entries from {From} to {To} ({Merged} merged with an existing entry).", libraryChanged, from, to, merged);
            }

            int roisChanged = 0;
            var saved = new List<string>();
            var failures = new List<string>();
            foreach (TemplateMaker template in templates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = template.TemplateName ?? "(unnamed)";
                int changed = ConvertRois(template.ROIs, from, to, map);
                if (changed == 0)
                {
                    continue;
                }

                try
                {
                    template.make_template();
                    roisChanged += changed;
                    saved.Add(name);
                    logger.LogInformation("Converted {Count} ROI code(s) of template {Template} from {From} to {To}.", changed, name, from, to);
                }
                catch (Exception ex) when (ex is TemplateLoadException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
                {
                    failures.Add(name + ": " + ex.Message);
                    logger.LogWarning(ex, "Template {Template} was not saved after converting its codes from {From} to {To}.", name, from, to);
                }
            }

            return new SchemeConversionResult(roisChanged, saved, libraryChanged, merged, failures);
        }

        /// <summary>
        /// Converts the library entries in place. A converted entry whose new code the library already has (in the target
        /// scheme) is removed, so each code stays listed once.
        /// </summary>
        /// <returns>The number of entries converted, and how many of them were removed as duplicates.</returns>
        internal static (int Changed, int Merged) ConvertLibrary(List<OntologyCodeClass> library, string from, string to, IReadOnlyDictionary<string, string> map)
        {
            List<OntologyCodeClass> converted = library.Where(entry => IsConvertible(entry, from, map)).ToList();
            foreach (OntologyCodeClass entry in converted)
            {
                Convert(entry, to, map);
            }

            int merged = 0;
            foreach (OntologyCodeClass entry in converted)
            {
                bool duplicate = library.Any(other => !ReferenceEquals(other, entry)
                    && string.Equals(other.CodeValue, entry.CodeValue, StringComparison.Ordinal)
                    && string.Equals(other.Scheme, entry.Scheme, StringComparison.OrdinalIgnoreCase));
                if (duplicate)
                {
                    library.Remove(entry);
                    merged++;
                }
            }

            return (converted.Count, merged);
        }

        /// <summary>Converts the codes of <paramref name="rois"/> in place; ROIs sharing one code object are converted once.</summary>
        /// <returns>The number of ROIs whose code changed.</returns>
        internal static int ConvertRois(IEnumerable<ROIClass> rois, string from, string to, IReadOnlyDictionary<string, string> map)
        {
            List<ROIClass> targets = rois.Where(roi => IsConvertible(roi.Ontology_Class, from, map)).ToList();
            var codes = new HashSet<OntologyCodeClass>(targets.Select(roi => roi.Ontology_Class).OfType<OntologyCodeClass>(), ReferenceEqualityComparer.Instance);
            foreach (OntologyCodeClass code in codes)
            {
                Convert(code, to, map);
            }

            return targets.Count;
        }

        private static void Convert(OntologyCodeClass code, string to, IReadOnlyDictionary<string, string> map)
        {
            string? value = code.CodeValue?.Trim();
            if (value != null && map.TryGetValue(value, out string? target))
            {
                code.CodeValue = target;
                code.Scheme = to;
            }
        }

        private static bool HasScheme(OntologyCodeClass? code, string scheme)
        {
            return code != null && string.Equals(code.Scheme?.Trim(), scheme, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>In <paramref name="from"/>, with a code value the table maps. A code without a code value is never converted.</summary>
        private static bool IsConvertible(OntologyCodeClass? code, string from, IReadOnlyDictionary<string, string> map)
        {
            if (!HasScheme(code, from))
            {
                return false;
            }

            string? value = code?.CodeValue?.Trim();
            return !string.IsNullOrEmpty(value) && map.ContainsKey(value);
        }

        private static void RequireDifferent(string from, string to)
        {
            if (!CanConvert(from, to))
            {
                throw new ArgumentException("Choose two different coding schemes.");
            }
        }
    }
}
