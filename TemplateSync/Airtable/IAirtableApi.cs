using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TemplateSync.Airtable
{
    /// <summary>
    /// The Airtable Web API operations the application needs. Every call is exactly one
    /// HTTP request (plus bounded retries), so callers can reason about API-call cost.
    /// </summary>
    public interface IAirtableApi
    {
        /// <summary>GET one page of records (at most 100).</summary>
        Task<ListRecordsPage> ListRecordsPageAsync(ListRecordsRequest request, CancellationToken cancellationToken);

        /// <summary>POST up to 10 new records in one request.</summary>
        Task<IReadOnlyList<AirtableRecord>> CreateRecordsAsync(string baseId, string table, IReadOnlyList<JObject> records, bool typecast, CancellationToken cancellationToken);

        /// <summary>PATCH up to 10 existing records in one request. Only the supplied fields change.</summary>
        Task<IReadOnlyList<AirtableRecord>> UpdateRecordsAsync(string baseId, string table, IReadOnlyList<RecordUpdate> updates, bool typecast, CancellationToken cancellationToken);

        /// <summary>
        /// GET the schema of every table in a base (/v0/meta/bases/{baseId}/tables). Needs the schema.bases:read scope;
        /// a token without it gets an <see cref="AirtableAuthException"/> (403).
        /// </summary>
        Task<IReadOnlyList<AirtableTableSchema>> ListTablesAsync(string baseId, CancellationToken cancellationToken);
    }
}
