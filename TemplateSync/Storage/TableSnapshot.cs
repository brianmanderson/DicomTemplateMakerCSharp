using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TemplateSync.Airtable;

namespace TemplateSync.Storage
{
    /// <summary>
    /// A full copy of one template table. The same shape is used for the published TG-263
    /// snapshot (only the content members are set) and for local caches (sync metadata too).
    /// </summary>
    public sealed class TableSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        [JsonProperty("schemaVersion", Order = 0)]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        [JsonProperty("sourceName", Order = 1)]
        public string? SourceName { get; set; }

        [JsonProperty("baseId", Order = 2)]
        public string? BaseId { get; set; }

        [JsonProperty("tableId", Order = 3)]
        public string? TableId { get; set; }

        /// <summary>When the records were last confirmed against the source (UTC).</summary>
        [JsonProperty("generatedAtUtc", Order = 4)]
        public DateTimeOffset GeneratedAtUtc { get; set; }

        [JsonProperty("recordCount", Order = 5)]
        public int RecordCount { get; set; }

        // ----- Local cache metadata (not present in the published snapshot) -----

        /// <summary>When the last complete (non-delta) fetch finished.</summary>
        [JsonProperty("lastFullRefreshUtc", Order = 6)]
        public DateTimeOffset? LastFullRefreshUtc { get; set; }

        /// <summary>Airtable server time at the start of the last successful fetch; the next delta asks for changes after it.</summary>
        [JsonProperty("syncCursorUtc", Order = 7)]
        public DateTimeOffset? SyncCursorUtc { get; set; }

        /// <summary>When a downloaded snapshot was last checked against its URL.</summary>
        [JsonProperty("lastCheckedUtc", Order = 8)]
        public DateTimeOffset? LastCheckedUtc { get; set; }

        [JsonProperty("etag", Order = 9)]
        public string? ETag { get; set; }

        /// <summary>Mapped columns this table does not have; left out of field filters and writes.</summary>
        [JsonProperty("missingFields", Order = 10)]
        public List<string>? MissingFields { get; set; }

        /// <summary>Set when Airtable rejected the field filter in a way we could not attribute to one column.</summary>
        [JsonProperty("fieldsFilterDisabled", Order = 11, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool FieldsFilterDisabled { get; set; }

        [JsonProperty("records", Order = 12)]
        public List<AirtableRecord> Records { get; set; } = new List<AirtableRecord>();

        public TableSnapshot Clone()
        {
            var copy = (TableSnapshot)MemberwiseClone();
            copy.MissingFields = MissingFields == null ? null : new List<string>(MissingFields);
            copy.Records = Records.ConvertAll(r => r.Clone());
            return copy;
        }
    }
}
