using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TemplateSync.Airtable
{
    /// <summary>
    /// The three Airtable Web API operations the application needs. Every call is exactly one
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
    }
}
