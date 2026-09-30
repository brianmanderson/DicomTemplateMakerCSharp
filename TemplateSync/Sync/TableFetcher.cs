using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TemplateSync.Airtable;
using TemplateSync.Infrastructure;
using TemplateSync.Model;

namespace TemplateSync.Sync
{
    internal sealed class FetchResult
    {
        public List<AirtableRecord> Records { get; } = new List<AirtableRecord>();

        public DateTimeOffset CursorUtc { get; set; }

        public int ApiCalls { get; set; }

        public HashSet<string> MissingFields { get; set; } = new HashSet<string>(StringComparer.Ordinal);

        public bool FieldsFilterDisabled { get; set; }
    }

    /// <summary>
    /// Pages through a table with list requests only (never one request per record). A failed page
    /// ends the fetch. The only repeats are bounded: once without the field filter if Airtable reports
    /// an unknown column, and once from the first page if Airtable expires the paging iterator.
    /// </summary>
    internal static class TableFetcher
    {
        public const string UnknownFieldErrorType = "UNKNOWN_FIELD_NAME";
        public const string IteratorExpiredErrorType = "LIST_RECORDS_ITERATOR_NOT_AVAILABLE";

        private static readonly Regex UnknownFieldPattern = new Regex("Unknown field names?:\\s*\"(?<name>[^\"]+)\"", RegexOptions.CultureInvariant);

        public static async Task<FetchResult> FetchAsync(
            IAirtableApi api,
            string baseId,
            string tableId,
            DateTimeOffset? modifiedAfterUtc,
            IEnumerable<string>? knownMissingFields,
            bool fieldsFilterDisabled,
            IReadOnlyList<string>? sortFields,
            IClock clock,
            IProgress<string>? progress,
            CancellationToken cancellationToken,
            bool allowUnfilteredFallback = true)
        {
            var result = new FetchResult
            {
                MissingFields = new HashSet<string>(knownMissingFields ?? Enumerable.Empty<string>(), StringComparer.Ordinal),
                FieldsFilterDisabled = fieldsFilterDisabled,
            };

            bool restartedAfterExpiry = false;
            while (true)
            {
                List<string>? fields = result.FieldsFilterDisabled
                    ? null
                    : AirTableEntry.FieldNames.Where(f => !result.MissingFields.Contains(f)).ToList();
                var request = new ListRecordsRequest(baseId, tableId)
                {
                    Fields = fields,
                    PageSize = AirtableLimits.MaxPageSize,
                    FilterByFormula = modifiedAfterUtc.HasValue ? ModifiedSinceFormula(modifiedAfterUtc.Value) : null,
                    SortFields = sortFields,
                };

                result.Records.Clear();
                DateTimeOffset localStart = clock.UtcNow;
                DateTimeOffset? serverStart = null;
                try
                {
                    string? offset = null;
                    do
                    {
                        ListRecordsPage page = await api.ListRecordsPageAsync(request.WithOffset(offset), cancellationToken).ConfigureAwait(false);
                        result.ApiCalls++;
                        if (serverStart == null)
                        {
                            serverStart = page.ServerDate ?? localStart;
                        }

                        result.Records.AddRange(page.Records);
                        offset = string.IsNullOrEmpty(page.Offset) ? null : page.Offset;
                        progress?.Report($"Downloaded {result.Records.Count} record(s)…");
                    }
                    while (offset != null);

                    result.CursorUtc = serverStart ?? localStart;
                    return result;
                }
                catch (AirtableException ex) when (ex.ErrorType == UnknownFieldErrorType && fields != null)
                {
                    result.ApiCalls++;
                    string? unknown = ParseUnknownField(ex.ApiMessage ?? ex.Message);
                    if (unknown == nameof(AirTableEntry.Structure))
                    {
                        throw new AirtableException(
                            "This Airtable table has no 'Structure' column, so it is not a template table. Check the table id.",
                            ex.StatusCode,
                            ex.ErrorType,
                            ex,
                            ex.ApiMessage);
                    }

                    if (!allowUnfilteredFallback)
                    {
                        throw new AirtableException(
                            $"The table has no '{unknown ?? "(unknown)"}' column. Add it to the excluded fields; all columns are never exported.",
                            ex.StatusCode,
                            ex.ErrorType,
                            ex,
                            ex.ApiMessage);
                    }

                    // One retry without the field filter costs a single call; probing column by column could cost 26.
                    result.FieldsFilterDisabled = true;
                    progress?.Report(unknown == null
                        ? "The table does not have every expected column; downloading all columns instead."
                        : $"The table has no '{unknown}' column; downloading all columns instead.");
                }
                catch (AirtableException ex) when (ex.ErrorType == IteratorExpiredErrorType && !restartedAfterExpiry)
                {
                    result.ApiCalls++;
                    restartedAfterExpiry = true;
                    progress?.Report("Airtable expired the page cursor; starting the download again.");
                }
            }
        }

        /// <summary>
        /// Records created or edited after the given instant. CREATED_TIME() is included because a
        /// record that has never been edited may have no last-modified time.
        /// </summary>
        public static string ModifiedSinceFormula(DateTimeOffset afterUtc)
        {
            string iso = afterUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            return $"OR(IS_AFTER(LAST_MODIFIED_TIME(), DATETIME_PARSE('{iso}')), IS_AFTER(CREATED_TIME(), DATETIME_PARSE('{iso}')))";
        }

        public static string? ParseUnknownField(string? message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return null;
            }

            Match match = UnknownFieldPattern.Match(message);
            return match.Success ? match.Groups["name"].Value : null;
        }

        /// <summary>Replaces changed records in place and appends new ones, keeping table order stable.</summary>
        public static int MergeInto(List<AirtableRecord> target, IEnumerable<AirtableRecord> changes)
        {
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < target.Count; i++)
            {
                index[target[i].Id] = i;
            }

            int count = 0;
            foreach (AirtableRecord change in changes)
            {
                if (string.IsNullOrEmpty(change.Id))
                {
                    continue;
                }

                if (index.TryGetValue(change.Id, out int position))
                {
                    target[position] = change;
                }
                else
                {
                    index[change.Id] = target.Count;
                    target.Add(change);
                }

                count++;
            }

            return count;
        }
    }
}
