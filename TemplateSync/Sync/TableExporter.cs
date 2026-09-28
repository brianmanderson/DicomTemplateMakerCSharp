using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TemplateSync.Airtable;
using TemplateSync.Infrastructure;
using TemplateSync.Storage;

namespace TemplateSync.Sync
{
    /// <summary>
    /// Produces the published, token-free snapshot of a table (used by the CI refresh job).
    /// Output is deterministic: records follow the requested server-side sort (Airtable's default
    /// order is unspecified) and each record's fields are sorted, so an unchanged table yields a
    /// byte-identical file and no git change.
    /// </summary>
    public static class TableExporter
    {
        public static async Task<TableSnapshot> ExportAsync(
            IAirtableApi api,
            string baseId,
            string tableId,
            string sourceName,
            IEnumerable<string>? knownMissingFields,
            IReadOnlyList<string>? sortFields,
            IClock? clock,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            IClock time = clock ?? SystemClock.Instance;
            FetchResult fetched = await TableFetcher.FetchAsync(api, baseId, tableId, null, knownMissingFields, false, sortFields, time, progress, cancellationToken, allowUnfilteredFallback: false).ConfigureAwait(false);
            return new TableSnapshot
            {
                SourceName = sourceName,
                BaseId = baseId,
                TableId = tableId,
                GeneratedAtUtc = time.UtcNow,
                Records = fetched.Records.Select(Normalize).ToList(),
            };
        }

        /// <summary>True when both snapshots hold the same records with the same values.</summary>
        public static bool SameRecords(TableSnapshot a, TableSnapshot b)
        {
            string left = JsonConvert.SerializeObject(a.Records.Select(Normalize), Formatting.None);
            string right = JsonConvert.SerializeObject(b.Records.Select(Normalize), Formatting.None);
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static readonly HashSet<string> Published = new HashSet<string>(Model.AirTableEntry.FieldNames, StringComparer.Ordinal);

        /// <summary>Keeps only the mapped template columns (never anything else a table might hold) and sorts them.</summary>
        private static AirtableRecord Normalize(AirtableRecord record)
        {
            var sorted = new JObject();
            foreach (JProperty property in record.Fields.Properties().Where(p => Published.Contains(p.Name)).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                sorted[property.Name] = property.Value.DeepClone();
            }

            return new AirtableRecord { Id = record.Id, CreatedTime = record.CreatedTime, Fields = sorted };
        }
    }
}
