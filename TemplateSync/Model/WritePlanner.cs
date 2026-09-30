using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TemplateSync.Airtable;

namespace TemplateSync.Model
{
    /// <summary>An ROI of the local template being written. The planner fills in the site lists.</summary>
    public sealed class LocalRoi
    {
        public LocalRoi(AirTableEntry desired, bool include)
        {
            Desired = desired ?? throw new ArgumentNullException(nameof(desired));
            Include = include;
        }

        public AirTableEntry Desired { get; }

        public bool Include { get; }
    }

    public sealed class WritePlan
    {
        public List<JObject> Creates { get; } = new List<JObject>();

        public List<RecordUpdate> Updates { get; } = new List<RecordUpdate>();

        public int Unchanged { get; internal set; }

        public List<string> Warnings { get; } = new List<string>();

        public bool IsEmpty => Creates.Count == 0 && Updates.Count == 0;

        /// <summary>Requests needed at 10 records per request.</summary>
        public int RequestCount =>
            (Creates.Count + AirtableLimits.MaxRecordsPerWrite - 1) / AirtableLimits.MaxRecordsPerWrite
            + (Updates.Count + AirtableLimits.MaxRecordsPerWrite - 1) / AirtableLimits.MaxRecordsPerWrite;
    }

    /// <summary>
    /// Computes the minimal creates and updates needed to publish one site's template, preserving
    /// the previous WriteToAirTableTask semantics:
    /// records are matched by exact Structure name; scalar fields take the local value whenever the
    /// local value is non-null; Colors_RGB is the union of local then remote options; the site is
    /// added to Template_Recommend (included ROIs) or Template_Consider (optional ROIs) and removed
    /// from the other list, keeping every other site. Unlike the old code, only fields whose value
    /// actually changes are sent, unchanged records cost nothing, and the "Id" column is never written.
    /// </summary>
    public static class WritePlanner
    {
        private const string Recommend = nameof(AirTableEntry.Template_Recommend);
        private const string Consider = nameof(AirTableEntry.Template_Consider);
        private const string Colors = nameof(AirTableEntry.Colors_RGB);

        public static WritePlan Plan(string site, IEnumerable<LocalRoi> rois, IEnumerable<AirtableRecord> remoteRecords, ICollection<string>? excludedFields = null)
        {
            if (string.IsNullOrWhiteSpace(site))
            {
                throw new ArgumentException("A template (site) name is required.", nameof(site));
            }

            ICollection<string> excluded = excludedFields ?? Array.Empty<string>();
            var remoteByStructure = new Dictionary<string, AirtableRecord>(StringComparer.Ordinal);
            foreach (AirtableRecord record in remoteRecords)
            {
                string? structure = AirTableEntry.Scalar(record.Fields, nameof(AirTableEntry.Structure), null);
                if (structure != null && !remoteByStructure.ContainsKey(structure))
                {
                    remoteByStructure.Add(structure, record);
                }
            }

            var plan = new WritePlan();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (LocalRoi roi in rois)
            {
                string? structure = roi.Desired.Structure;
                if (string.IsNullOrEmpty(structure))
                {
                    plan.Warnings.Add("Skipped an ROI with no name.");
                    continue;
                }

                if (!seen.Add(structure!))
                {
                    plan.Warnings.Add($"Skipped duplicate ROI name '{structure}'; only the first was written.");
                    continue;
                }

                if (remoteByStructure.TryGetValue(structure!, out AirtableRecord? remote))
                {
                    JObject changes = BuildUpdateFields(site, roi, remote.Fields, excluded);
                    if (changes.Count > 0)
                    {
                        plan.Updates.Add(new RecordUpdate(remote.Id, changes));
                    }
                    else
                    {
                        plan.Unchanged++;
                    }
                }
                else
                {
                    plan.Creates.Add(BuildCreateFields(site, roi, excluded));
                }
            }

            return plan;
        }

        internal static JObject BuildCreateFields(string site, LocalRoi roi, ICollection<string> excluded)
        {
            var fields = new JObject();
            foreach (string name in AirTableEntry.FieldNames)
            {
                if (excluded.Contains(name))
                {
                    continue;
                }

                if (name == Recommend)
                {
                    if (roi.Include)
                    {
                        fields[name] = new JArray(site);
                    }
                }
                else if (name == Consider)
                {
                    if (!roi.Include)
                    {
                        fields[name] = new JArray(site);
                    }
                }
                else if (name == Colors)
                {
                    List<string>? colors = roi.Desired.Colors_RGB;
                    if (colors != null && colors.Count > 0)
                    {
                        fields[name] = new JArray(colors);
                    }
                }
                else if (roi.Desired.GetField(name) is string value)
                {
                    fields[name] = value;
                }
            }

            return fields;
        }

        internal static JObject BuildUpdateFields(string site, LocalRoi roi, JObject remote, ICollection<string> excluded)
        {
            var changes = new JObject();
            foreach (string name in AirTableEntry.FieldNames)
            {
                if (excluded.Contains(name))
                {
                    continue;
                }

                if (name == Recommend || name == Consider)
                {
                    List<string> current = AirTableEntry.List(remote, name) ?? new List<string>();
                    bool listForThisRoi = name == Recommend ? roi.Include : !roi.Include;
                    List<string> desired = (listForThisRoi ? new List<string> { site } : new List<string>())
                        .Union(current, StringComparer.Ordinal)
                        .ToList();
                    if (!listForThisRoi)
                    {
                        desired.RemoveAll(s => string.Equals(s, site, StringComparison.Ordinal));
                    }

                    if (!SetEquals(desired, current))
                    {
                        changes[name] = new JArray(desired);
                    }
                }
                else if (name == Colors)
                {
                    List<string>? local = roi.Desired.Colors_RGB;
                    if (local == null)
                    {
                        continue;
                    }

                    List<string> current = AirTableEntry.List(remote, name) ?? new List<string>();
                    List<string> desired = local.Union(current, StringComparer.Ordinal).ToList();
                    if (!desired.SequenceEqual(current, StringComparer.Ordinal))
                    {
                        changes[name] = new JArray(desired);
                    }
                }
                else if (roi.Desired.GetField(name) is string value)
                {
                    string? current = AirTableEntry.Scalar(remote, name, null);
                    if (!string.Equals(current, value, StringComparison.Ordinal))
                    {
                        changes[name] = value;
                    }
                }
            }

            return changes;
        }

        private static bool SetEquals(List<string> a, List<string> b)
        {
            return new HashSet<string>(a, StringComparer.Ordinal).SetEquals(b);
        }
    }
}
