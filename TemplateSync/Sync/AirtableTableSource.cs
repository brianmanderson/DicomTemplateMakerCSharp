using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TemplateSync.Airtable;
using TemplateSync.Infrastructure;
using TemplateSync.Model;
using TemplateSync.Storage;

namespace TemplateSync.Sync
{
    /// <summary>A user's Airtable table (base id + table id). Tokens are not part of the definition.</summary>
    public sealed class AirtableSourceDefinition
    {
        public AirtableSourceDefinition(string name, string baseId, string tableId)
        {
            Name = name;
            BaseId = baseId;
            TableId = tableId;
        }

        public string Name { get; }

        public string BaseId { get; }

        public string TableId { get; }

        public string CacheKey => "airtable-" + BaseId + "-" + TableId;
    }

    /// <summary>
    /// Serves an Airtable table from a local cache and talks to Airtable only when asked to refresh,
    /// when the cache has expired, or before a write. Refreshes fetch only records changed since the
    /// last sync unless a full refresh is due.
    /// </summary>
    public sealed class AirtableTableSource : ITemplateTableSource
    {
        private readonly IAirtableApi _api;
        private readonly SnapshotStore _store;
        private readonly SyncSettings _settings;
        private readonly IClock _clock;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private bool _cacheLoaded;
        private bool _schemaChecked;

        public AirtableTableSource(AirtableSourceDefinition definition, IAirtableApi api, SnapshotStore store, SyncSettings? settings = null, IClock? clock = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _settings = settings ?? new SyncSettings();
            _clock = clock ?? SystemClock.Instance;
        }

        public AirtableSourceDefinition Definition { get; }

        public string Name => Definition.Name;

        public bool IsWritable => true;

        public TableSnapshot? Current { get; private set; }

        public async Task<TableLoadResult> LoadAsync(LoadMode mode, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await LoadCoreAsync(mode, keepColumnState: false, progress, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Publishes one site's template. See <see cref="WriteTemplatesAsync"/>.</summary>
        public async Task<WriteResult> WriteTemplateAsync(string site, IReadOnlyList<LocalRoi> rois, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            IReadOnlyList<WriteResult> results = await WriteTemplatesAsync(new[] { new TemplateWrite(site, rois) }, progress, cancellationToken).ConfigureAwait(false);
            return results[0];
        }

        /// <summary>
        /// Publishes several sites' templates in one session: one refresh of recent changes first (so other
        /// people's edits are merged, not overwritten), then for each site the minimal creates/updates,
        /// 10 records per request. If Airtable reports that a matched record no longer exists (deleted since
        /// the last full download), the table is downloaded in full once and the site is re-planned.
        /// </summary>
        public async Task<IReadOnlyList<WriteResult>> WriteTemplatesAsync(IReadOnlyList<TemplateWrite> templates, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            if (templates == null)
            {
                throw new ArgumentNullException(nameof(templates));
            }

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var results = new List<WriteResult>();
                if (templates.Count == 0)
                {
                    return results;
                }

                TableLoadResult refreshed = await LoadCoreAsync(LoadMode.Refresh, keepColumnState: true, progress, cancellationToken).ConfigureAwait(false);
                if (refreshed.Warning != null || Current == null)
                {
                    throw new AirtableException("Nothing was written because the table could not be refreshed first: " + (refreshed.Warning ?? "no data"));
                }

                int schemaCalls = await LearnColumnNamesAsync(templates, progress, cancellationToken).ConfigureAwait(false);
                bool fullRefreshUsed = false;
                foreach (TemplateWrite template in templates)
                {
                    try
                    {
                        WriteResult result = await WriteOneAsync(template, !fullRefreshUsed, () => fullRefreshUsed = true, progress, cancellationToken).ConfigureAwait(false);
                        if (results.Count == 0)
                        {
                            result.ApiCalls += refreshed.ApiCalls + schemaCalls;
                        }

                        results.Add(result);
                    }
                    catch (AirtableException ex) when (templates.Count > 1)
                    {
                        throw new AirtableWriteException(results, template.Site, templates.Count, ex);
                    }
                }

                return results;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// What <see cref="WriteTemplatesAsync"/> would send, computed from the local cache only (an empty table when
        /// there is none): no refresh, no schema lookup, no request of any kind. Sites are planned in order and each
        /// sees the records the earlier ones would create or change, as in a real session.
        /// </summary>
        public async Task<IReadOnlyList<WritePreview>> PreviewWritesAsync(IReadOnlyList<TemplateWrite> templates, CancellationToken cancellationToken)
        {
            if (templates == null)
            {
                throw new ArgumentNullException(nameof(templates));
            }

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await LoadCoreAsync(LoadMode.CacheOnly, keepColumnState: true, null, cancellationToken).ConfigureAwait(false);
                bool fromCache = Current != null;
                TableSnapshot table = Current?.Clone() ?? new TableSnapshot();
                var missing = new HashSet<string>(table.MissingFields ?? new List<string>(), StringComparer.Ordinal);
                RequireTemplateColumns(missing);
                var previews = new List<WritePreview>();
                int created = 0;
                foreach (TemplateWrite template in templates)
                {
                    WritePlan plan = WritePlanner.Plan(template.Site, template.Rois, table.Records, missing, table.ColumnNames);
                    var preview = new WritePreview(template.Site, plan, fromCache);
                    preview.Warnings.AddRange(plan.Warnings);
                    preview.Warnings.AddRange(MissingColumnWarnings(missing));
                    previews.Add(preview);

                    foreach (JObject fields in plan.Creates)
                    {
                        created++;
                        table.Records.Add(new AirtableRecord { Id = "dryRun" + created.ToString(System.Globalization.CultureInfo.InvariantCulture), Fields = (JObject)fields.DeepClone() });
                    }

                    foreach (RecordUpdate update in plan.Updates)
                    {
                        AirtableRecord record = table.Records.First(r => string.Equals(r.Id, update.Id, StringComparison.Ordinal));
                        foreach (JProperty field in update.Fields.Properties())
                        {
                            record.Fields[field.Name] = field.Value.DeepClone();
                        }
                    }
                }

                return previews;
            }
            finally
            {
                _gate.Release();
            }
        }

        public void DeleteCache()
        {
            _store.Delete(Definition.CacheKey);
            Current = null;
            _cacheLoaded = true;
        }

        private async Task<WriteResult> WriteOneAsync(TemplateWrite template, bool allowFullRefresh, Action onFullRefresh, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            var result = new WriteResult { Site = template.Site };
            int writtenBeforeRetry = 0;
            for (int attempt = 0; ; attempt++)
            {
                TableSnapshot? snapshot = Current;
                if (snapshot == null)
                {
                    throw new AirtableException($"The connection to {Name} was removed while writing.");
                }

                var missing = new HashSet<string>(snapshot.MissingFields ?? new List<string>(), StringComparer.Ordinal);
                var columnNames = new Dictionary<string, string>(snapshot.ColumnNames ?? new Dictionary<string, string>(), StringComparer.Ordinal);
                RequireTemplateColumns(missing);
                WritePlan plan = WritePlanner.Plan(template.Site, template.Rois, snapshot.Records, missing, columnNames);
                result.Unchanged = Math.Max(0, plan.Unchanged - writtenBeforeRetry);
                if (attempt == 0)
                {
                    result.Warnings.AddRange(plan.Warnings);
                    result.Warnings.AddRange(MissingColumnWarnings(missing));
                }

                if (plan.IsEmpty)
                {
                    progress?.Report($"'{template.Site}' is already up to date in {Name}.");
                    return result;
                }

                int total = plan.Creates.Count + plan.Updates.Count;
                int done = 0;
                try
                {
                    foreach (List<JObject> batch in Chunk(plan.Creates))
                    {
                        IReadOnlyList<AirtableRecord> created = await SendWithFieldRecoveryAsync(
                            batch,
                            missing,
                            columnNames,
                            b => _api.CreateRecordsAsync(Definition.BaseId, Definition.TableId, b, true, cancellationToken),
                            f => f,
                            (f, fields) => fields,
                            result,
                            progress).ConfigureAwait(false);
                        TableFetcher.MergeInto(snapshot.Records, created);
                        result.Created += created.Count;
                        done += created.Count;
                        progress?.Report($"{template.Site}: wrote {done} of {total} change(s) to {Name}…");
                    }

                    foreach (List<RecordUpdate> batch in Chunk(plan.Updates))
                    {
                        IReadOnlyList<AirtableRecord> updated = await SendWithFieldRecoveryAsync(
                            batch,
                            missing,
                            columnNames,
                            b => _api.UpdateRecordsAsync(Definition.BaseId, Definition.TableId, b, true, cancellationToken),
                            u => u.Fields,
                            (u, fields) => new RecordUpdate(u.Id, fields),
                            result,
                            progress).ConfigureAwait(false);
                        TableFetcher.MergeInto(snapshot.Records, updated);
                        result.Updated += updated.Count;
                        done += updated.Count;
                        progress?.Report($"{template.Site}: wrote {done} of {total} change(s) to {Name}…");
                    }

                    return result;
                }
                catch (AirtableException ex) when (attempt == 0 && allowFullRefresh && IsMissingRecord(ex))
                {
                    onFullRefresh();
                    writtenBeforeRetry = result.Created + result.Updated;
                    progress?.Report($"A record in {Name} was deleted since the last full download; downloading the whole table again…");
                    TableLoadResult full = await LoadCoreAsync(LoadMode.FullRefresh, keepColumnState: true, progress, cancellationToken).ConfigureAwait(false);
                    result.ApiCalls += full.ApiCalls;
                    if (full.Warning != null)
                    {
                        throw new AirtableException($"Wrote {writtenBeforeRetry} change(s) to {Name}, then could not download the table again: {full.Warning}");
                    }
                }
                catch (AirtableException ex)
                {
                    throw new AirtableException(
                        $"Wrote {done} of {total} change(s) for '{template.Site}' to {Name} before an error: {ex.Message} The completed changes are saved; writing again continues from there.",
                        ex.StatusCode,
                        ex.ErrorType,
                        ex,
                        ex.ApiMessage);
                }
                finally
                {
                    // After the full-download retry, Current is a newer snapshot; never overwrite it with this one.
                    if (ReferenceEquals(snapshot, Current))
                    {
                        snapshot.MissingFields = missing.Count == 0 ? null : missing.OrderBy(m => m, StringComparer.Ordinal).ToList();
                        snapshot.ColumnNames = columnNames.Count == 0 ? null : columnNames;
                        string? saveProblem = TrySave(snapshot);
                        if (saveProblem != null)
                        {
                            result.Warnings.Add(saveProblem);
                        }
                    }
                }
            }
        }

        private static readonly string[] RequiredColumns =
        {
            nameof(AirTableEntry.Structure), nameof(AirTableEntry.Template_Recommend), nameof(AirTableEntry.Template_Consider),
        };

        private void RequireTemplateColumns(ICollection<string> missing)
        {
            string[] absent = RequiredColumns.Where(missing.Contains).ToArray();
            if (absent.Length > 0)
            {
                throw new AirtableException($"{Name} has no {string.Join(", ", absent.Select(a => "'" + a + "'"))} column, which templates need. Nothing was written. Add the column(s) in Airtable, then press Full refresh.");
            }
        }

        private IEnumerable<string> MissingColumnWarnings(IEnumerable<string> missing)
        {
            return missing.OrderBy(m => m, StringComparer.Ordinal).Select(column => $"{Name} has no {DescribeColumn(column)} column, so that value was not written.");
        }

        /// <summary>'TG_263SpanishR' (or 'TG_263RSpanish') for a reversed-language column, otherwise the quoted name.</summary>
        private static string DescribeColumn(string column)
        {
            return AirTableEntry.AlternateColumnNames.TryGetValue(column, out string? alternate) ? $"'{column}' (or '{alternate}')" : $"'{column}'";
        }

        /// <summary>
        /// Before a write that sets a reversed-language column whose spelling in this table is not known yet, asks
        /// Airtable for the table schema once (one API call, at most once per source). The schema gives the spelling
        /// and every mapped column the table lacks. A token without schema.bases:read gets 403 (or 404): the spelling
        /// stays unknown and is learned from Airtable's unknown-field error while writing. Returns the calls spent.
        /// </summary>
        private async Task<int> LearnColumnNamesAsync(IReadOnlyList<TemplateWrite> templates, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            TableSnapshot? snapshot = Current;
            if (snapshot == null || _schemaChecked)
            {
                return 0;
            }

            var missing = new HashSet<string>(snapshot.MissingFields ?? new List<string>(), StringComparer.Ordinal);
            bool needed = AirTableEntry.AlternateColumnNames.Keys.Any(column =>
                !missing.Contains(column)
                && (snapshot.ColumnNames == null || !snapshot.ColumnNames.ContainsKey(column))
                && templates.Any(t => t.Rois.Any(roi => roi.Desired.GetField(column) is string)));
            if (!needed)
            {
                return 0;
            }

            _schemaChecked = true;
            IReadOnlyList<AirtableTableSchema> tables;
            try
            {
                tables = await _api.ListTablesAsync(Definition.BaseId, cancellationToken).ConfigureAwait(false);
            }
            catch (AirtableException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                progress?.Report($"The token cannot read the schema of {Name} (HTTP {(int)ex.StatusCode!.Value}); column spellings are learned while writing.");
                return 1;
            }

            AirtableTableSchema? table = tables.FirstOrDefault(t => string.Equals(t.Id, Definition.TableId, StringComparison.Ordinal))
                ?? tables.FirstOrDefault(t => string.Equals(t.Name, Definition.TableId, StringComparison.Ordinal));
            if (table == null)
            {
                progress?.Report($"The schema of base {Definition.BaseId} does not list table {Definition.TableId}; column spellings are learned while writing.");
                return 1;
            }

            ApplySchema(snapshot, table);
            string? saveProblem = TrySave(snapshot);
            if (saveProblem != null)
            {
                progress?.Report(saveProblem);
            }

            return 1;
        }

        /// <summary>Records the spelling of each reversed-language column and every mapped column the table does not have.</summary>
        internal static void ApplySchema(TableSnapshot snapshot, AirtableTableSchema table)
        {
            var columns = new HashSet<string>(table.Fields.Select(f => f.Name), StringComparer.Ordinal);
            var missing = new HashSet<string>(snapshot.MissingFields ?? new List<string>(), StringComparer.Ordinal);
            var columnNames = new Dictionary<string, string>(snapshot.ColumnNames ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            foreach (string property in AirTableEntry.FieldNames)
            {
                bool hasAlternate = AirTableEntry.AlternateColumnNames.TryGetValue(property, out string? alternate);
                if (columns.Contains(property))
                {
                    if (hasAlternate)
                    {
                        columnNames[property] = property;
                    }
                }
                else if (hasAlternate && columns.Contains(alternate!))
                {
                    columnNames[property] = alternate!;
                }
                else
                {
                    missing.Add(property);
                    columnNames.Remove(property);
                }
            }

            snapshot.MissingFields = missing.Count == 0 ? null : missing.OrderBy(m => m, StringComparer.Ordinal).ToList();
            snapshot.ColumnNames = columnNames.Count == 0 ? null : columnNames;
        }

        private static bool IsMissingRecord(AirtableException ex)
        {
            return ex.StatusCode == System.Net.HttpStatusCode.NotFound
                || ex.ErrorType == "ROW_DOES_NOT_EXIST"
                || ex.ErrorType == "MODEL_ID_NOT_FOUND"
                || ex.ErrorType == "NOT_FOUND";
        }

        private string? TrySave(TableSnapshot snapshot)
        {
            try
            {
                _store.Save(Definition.CacheKey, snapshot);
                return null;
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                return $"The local copy of {Name} could not be saved ({ex.Message}); it will be downloaded again next time.";
            }
        }

        private async Task<TableLoadResult> LoadCoreAsync(LoadMode mode, bool keepColumnState, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            if (!_cacheLoaded)
            {
                Current = _store.TryLoad(Definition.CacheKey);
                _cacheLoaded = true;
            }

            DateTimeOffset now = _clock.UtcNow;
            if (mode == LoadMode.CacheOnly)
            {
                return new TableLoadResult(Current, Current == null ? LoadOrigin.None : LoadOrigin.Cache);
            }

            if (mode == LoadMode.IfStale && Current != null && now - Current.GeneratedAtUtc < _settings.CacheTimeToLive)
            {
                return new TableLoadResult(Current, LoadOrigin.Cache);
            }

            TableSnapshot? previous = Current;
            bool delta = mode != LoadMode.FullRefresh
                && previous?.SyncCursorUtc != null
                && previous.LastFullRefreshUtc != null
                && now - previous.LastFullRefreshUtc.Value < _settings.FullRefreshInterval;

            // Columns found missing during writes are forgotten on every full download, so a column added in
            // Airtable later is used again. A user-requested Full refresh also retries the field filter.
            bool userFullRefresh = mode == LoadMode.FullRefresh && !keepColumnState;
            IEnumerable<string>? knownMissing = delta ? previous?.MissingFields : null;
            bool filterDisabled = !userFullRefresh && (previous?.FieldsFilterDisabled ?? false);

            FetchResult fetched;
            try
            {
                progress?.Report(delta ? $"Checking {Name} for changes…" : $"Downloading {Name}…");
                fetched = await TableFetcher.FetchAsync(
                    _api,
                    Definition.BaseId,
                    Definition.TableId,
                    delta ? previous!.SyncCursorUtc!.Value - _settings.DeltaSafetyMargin : (DateTimeOffset?)null,
                    knownMissing,
                    previous?.ColumnNames,
                    filterDisabled,
                    null,
                    _clock,
                    progress,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (AirtableException ex) when (previous != null)
            {
                return new TableLoadResult(previous, LoadOrigin.Cache, $"{ex.Message} Showing the copy saved {Describe(previous.GeneratedAtUtc)}.");
            }

            TableSnapshot next = delta ? previous!.Clone() : new TableSnapshot();
            next.SourceName = Name;
            next.BaseId = Definition.BaseId;
            next.TableId = Definition.TableId;
            int changed;
            if (delta)
            {
                changed = TableFetcher.MergeInto(next.Records, fetched.Records);
            }
            else
            {
                // Spellings are facts about the table and survive full downloads; missing columns are re-learned.
                next.ColumnNames = previous?.ColumnNames == null ? null : new Dictionary<string, string>(previous.ColumnNames, StringComparer.Ordinal);
                next.Records = fetched.Records.ToList();
                next.LastFullRefreshUtc = _clock.UtcNow;
                changed = next.Records.Count;
            }

            next.GeneratedAtUtc = _clock.UtcNow;
            next.SyncCursorUtc = fetched.CursorUtc;
            next.MissingFields = fetched.MissingFields.Count == 0 ? null : fetched.MissingFields.OrderBy(m => m, StringComparer.Ordinal).ToList();
            next.FieldsFilterDisabled = fetched.FieldsFilterDisabled;
            Current = next;
            string? saveProblem = TrySave(next);
            if (saveProblem != null)
            {
                progress?.Report(saveProblem);
            }

            progress?.Report(delta
                ? $"{Name}: {changed} changed record(s) ({fetched.ApiCalls} API call(s))."
                : $"{Name}: {changed} record(s) downloaded ({fetched.ApiCalls} API call(s)).");
            return new TableLoadResult(next, LoadOrigin.Network, null, fetched.ApiCalls, delta, changed, saveProblem);
        }

        /// <summary>
        /// Sends one batch. When Airtable reports an unknown column, a reversed-language column written under its
        /// property name is resent under its other spelling (once); any other unknown column is dropped and the batch
        /// resent. Both outcomes are kept for the rest of the session and saved with the cache.
        /// </summary>
        private async Task<IReadOnlyList<AirtableRecord>> SendWithFieldRecoveryAsync<T>(
            List<T> batch,
            HashSet<string> missing,
            Dictionary<string, string> columnNames,
            Func<List<T>, Task<IReadOnlyList<AirtableRecord>>> send,
            Func<T, JObject> fieldsOf,
            Func<T, JObject, T> withFields,
            WriteResult result,
            IProgress<string>? progress)
        {
            List<T> current = Prepare(batch, missing, columnNames, fieldsOf, withFields);
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    result.ApiCalls++;
                    IReadOnlyList<AirtableRecord> sent = await send(current).ConfigureAwait(false);
                    NoteAcceptedSpellings(current, columnNames, fieldsOf);
                    return sent;
                }
                catch (AirtableException ex) when (ex.ErrorType == TableFetcher.UnknownFieldErrorType && attempt < AirTableEntry.FieldNames.Count + AirTableEntry.AlternateColumnNames.Count)
                {
                    string? unknown = TableFetcher.ParseUnknownField(ex.ApiMessage ?? ex.Message);
                    if (unknown == null || missing.Contains(AirTableEntry.PropertyNameOf(unknown)) || !current.Any(item => fieldsOf(item).ContainsKey(unknown)))
                    {
                        throw;
                    }

                    string property = AirTableEntry.PropertyNameOf(unknown);
                    if (unknown == property && !columnNames.ContainsKey(property) && AirTableEntry.AlternateColumnNames.TryGetValue(property, out string? alternate))
                    {
                        columnNames[property] = alternate;
                        progress?.Report($"{Name} has no '{unknown}' column; writing it as '{alternate}'.");
                    }
                    else
                    {
                        missing.Add(property);
                        columnNames.Remove(property);
                        RequireTemplateColumns(missing);
                        result.Warnings.Add($"{Name} has no {DescribeColumn(property)} column, so that value was not written.");
                        progress?.Report($"{Name} has no '{unknown}' column; writing without it.");
                    }

                    current = Prepare(current, missing, columnNames, fieldsOf, withFields);
                }
            }
        }

        /// <summary>Drops columns the table lacks and writes each reversed-language column under the table's spelling.</summary>
        private static List<T> Prepare<T>(List<T> batch, HashSet<string> missing, IReadOnlyDictionary<string, string> columnNames, Func<T, JObject> fieldsOf, Func<T, JObject, T> withFields)
        {
            bool needed = batch.Any(item => fieldsOf(item).Properties().Any(p =>
            {
                string property = AirTableEntry.PropertyNameOf(p.Name);
                return missing.Contains(property) || AirTableEntry.ColumnName(property, columnNames) != p.Name;
            }));
            if (!needed)
            {
                return batch;
            }

            return batch.Select(item =>
            {
                var fields = new JObject();
                foreach (JProperty field in fieldsOf(item).Properties())
                {
                    string property = AirTableEntry.PropertyNameOf(field.Name);
                    if (!missing.Contains(property))
                    {
                        fields[AirTableEntry.ColumnName(property, columnNames)] = field.Value.DeepClone();
                    }
                }

                return withFields(item, fields);
            }).ToList();
        }

        /// <summary>A reversed-language column Airtable accepted is spelled that way in this table.</summary>
        private static void NoteAcceptedSpellings<T>(List<T> sent, Dictionary<string, string> columnNames, Func<T, JObject> fieldsOf)
        {
            foreach (KeyValuePair<string, string> alias in AirTableEntry.AlternateColumnNames)
            {
                if (columnNames.ContainsKey(alias.Key))
                {
                    continue;
                }

                foreach (string spelling in new[] { alias.Key, alias.Value })
                {
                    if (sent.Any(item => fieldsOf(item).ContainsKey(spelling)))
                    {
                        columnNames[alias.Key] = spelling;
                    }
                }
            }
        }

        internal static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> items)
        {
            for (int i = 0; i < items.Count; i += AirtableLimits.MaxRecordsPerWrite)
            {
                yield return items.Skip(i).Take(AirtableLimits.MaxRecordsPerWrite).ToList();
            }
        }

        internal static string Describe(DateTimeOffset whenUtc)
        {
            return "on " + whenUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
