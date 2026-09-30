using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TemplateSync.Airtable
{
    /// <summary>One Airtable record exactly as the Web API returns it.</summary>
    public sealed class AirtableRecord
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>ISO-8601 creation time, kept as the raw string Airtable sent.</summary>
        [JsonProperty("createdTime")]
        public string? CreatedTime { get; set; }

        [JsonProperty("fields")]
        public JObject Fields { get; set; } = new JObject();

        public AirtableRecord Clone()
        {
            return new AirtableRecord { Id = Id, CreatedTime = CreatedTime, Fields = (JObject)Fields.DeepClone() };
        }
    }

    /// <summary>Parameters for one GET /v0/{baseId}/{table} request.</summary>
    public sealed class ListRecordsRequest
    {
        public ListRecordsRequest(string baseId, string table)
        {
            BaseId = baseId;
            Table = table;
        }

        public string BaseId { get; }

        public string Table { get; }

        /// <summary>Only these fields are returned. Null returns every field.</summary>
        public IReadOnlyList<string>? Fields { get; set; }

        public string? FilterByFormula { get; set; }

        public string? View { get; set; }

        /// <summary>Server-side sort, e.g. by an autonumber column, for a deterministic record order.</summary>
        public IReadOnlyList<string>? SortFields { get; set; }

        /// <summary>Records per page. Airtable's maximum and default is 100.</summary>
        public int PageSize { get; set; } = AirtableLimits.MaxPageSize;

        public string? Offset { get; set; }

        public ListRecordsRequest WithOffset(string? offset)
        {
            return new ListRecordsRequest(BaseId, Table)
            {
                Fields = Fields,
                FilterByFormula = FilterByFormula,
                View = View,
                SortFields = SortFields,
                PageSize = PageSize,
                Offset = offset,
            };
        }
    }

    public sealed class ListRecordsPage
    {
        public ListRecordsPage(IReadOnlyList<AirtableRecord> records, string? offset, System.DateTimeOffset? serverDate)
        {
            Records = records;
            Offset = offset;
            ServerDate = serverDate;
        }

        public IReadOnlyList<AirtableRecord> Records { get; }

        /// <summary>Cursor for the next page, or null on the last page.</summary>
        public string? Offset { get; }

        /// <summary>The HTTP Date header of the response, used as a clock-skew-free sync cursor.</summary>
        public System.DateTimeOffset? ServerDate { get; }
    }

    public sealed class RecordUpdate
    {
        public RecordUpdate(string id, JObject fields)
        {
            Id = id;
            Fields = fields;
        }

        public string Id { get; }

        public JObject Fields { get; }
    }

    public static class AirtableLimits
    {
        /// <summary>Maximum records per list page.</summary>
        public const int MaxPageSize = 100;

        /// <summary>Maximum records per create or update request.</summary>
        public const int MaxRecordsPerWrite = 10;

        /// <summary>Documented limit: 5 requests per second per base.</summary>
        public const int RequestsPerSecondPerBase = 5;
    }
}
