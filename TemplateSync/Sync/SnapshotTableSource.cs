using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TemplateSync.Infrastructure;
using TemplateSync.Storage;

namespace TemplateSync.Sync
{
    public sealed class SnapshotSourceDefinition
    {
        public SnapshotSourceDefinition(string name, Uri url, string? bundledPath)
        {
            Name = name;
            Url = url;
            BundledPath = bundledPath;
        }

        public string Name { get; }

        /// <summary>HTTPS location of the published snapshot JSON.</summary>
        public Uri Url { get; }

        /// <summary>Copy shipped next to the program, used on first run or when offline.</summary>
        public string? BundledPath { get; }

        public string CacheKey => "snapshot-" + Name;
    }

    /// <summary>
    /// The shared, read-only TG-263 templates, downloaded as a published JSON snapshot. No Airtable
    /// token and no Airtable API calls are involved. The download is conditional (ETag), happens at
    /// most once per <see cref="SyncSettings.SnapshotCheckInterval"/> unless the user refreshes, and
    /// falls back to the cached or bundled copy when offline.
    /// </summary>
    public sealed class SnapshotTableSource : ITemplateTableSource
    {
        private readonly HttpClient _http;
        private readonly SnapshotStore _store;
        private readonly SyncSettings _settings;
        private readonly IClock _clock;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private TableSnapshot? _cached;
        private bool _loaded;

        public SnapshotTableSource(SnapshotSourceDefinition definition, HttpClient http, SnapshotStore store, SyncSettings? settings = null, IClock? clock = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _settings = settings ?? new SyncSettings();
            _clock = clock ?? SystemClock.Instance;
        }

        public SnapshotSourceDefinition Definition { get; }

        public string Name => Definition.Name;

        public bool IsWritable => false;

        public TableSnapshot? Current { get; private set; }

        public LoadOrigin CurrentOrigin { get; private set; }

        public async Task<TableLoadResult> LoadAsync(LoadMode mode, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureLocalLoaded();
                DateTimeOffset now = _clock.UtcNow;
                if (mode == LoadMode.CacheOnly)
                {
                    return new TableLoadResult(Current, CurrentOrigin);
                }

                if (mode == LoadMode.IfStale && _cached?.LastCheckedUtc != null && now - _cached.LastCheckedUtc.Value < _settings.SnapshotCheckInterval)
                {
                    return new TableLoadResult(Current, CurrentOrigin);
                }

                return await DownloadAsync(conditional: mode != LoadMode.FullRefresh, progress, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        public void DeleteCache()
        {
            _store.Delete(Definition.CacheKey);
            _cached = null;
            _loaded = false;
        }

        private void EnsureLocalLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _cached = _store.TryLoad(Definition.CacheKey);
            TableSnapshot? bundled = string.IsNullOrEmpty(Definition.BundledPath) ? null : SnapshotStore.TryLoadFile(Definition.BundledPath!);
            SelectNewest(bundled);
            _loaded = true;
        }

        private void SelectNewest(TableSnapshot? bundled)
        {
            if (_cached != null && (bundled == null || _cached.GeneratedAtUtc >= bundled.GeneratedAtUtc))
            {
                Current = _cached;
                CurrentOrigin = LoadOrigin.Cache;
            }
            else if (bundled != null)
            {
                Current = bundled;
                CurrentOrigin = LoadOrigin.Bundled;
            }
            else
            {
                Current = null;
                CurrentOrigin = LoadOrigin.None;
            }
        }

        private async Task<TableLoadResult> DownloadAsync(bool conditional, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            progress?.Report($"Checking for updated {Name} templates…");
            using (var request = new HttpRequestMessage(HttpMethod.Get, Definition.Url))
            {
                request.Headers.UserAgent.ParseAdd("DicomTemplateMaker");
                if (conditional && _cached?.ETag != null)
                {
                    request.Headers.TryAddWithoutValidation("If-None-Match", _cached.ETag);
                }

                HttpResponseMessage response;
                try
                {
                    response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
                {
                    return Fallback($"Could not download the shared {Name} templates ({ex.Message}).");
                }

                using (response)
                {
                    DateTimeOffset now = _clock.UtcNow;
                    if (response.StatusCode == HttpStatusCode.NotModified && _cached != null)
                    {
                        _cached.LastCheckedUtc = now;
                        _store.Save(Definition.CacheKey, _cached);
                        progress?.Report($"{Name} templates are up to date.");
                        return new TableLoadResult(Current, CurrentOrigin);
                    }

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return Fallback($"The shared {Name} templates have not been published yet at {Definition.Url}.");
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        return Fallback($"Could not download the shared {Name} templates (HTTP {(int)response.StatusCode}).");
                    }

                    TableSnapshot downloaded;
                    try
                    {
                        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        downloaded = SnapshotStore.Parse(json);
                    }
                    catch (Exception ex) when (ex is JsonException || ex is InvalidDataException)
                    {
                        return Fallback($"The downloaded {Name} snapshot could not be read ({ex.Message}).");
                    }

                    EntityTagHeaderValue? etag = response.Headers.ETag;
                    downloaded.ETag = etag?.ToString();
                    downloaded.LastCheckedUtc = now;
                    _store.Save(Definition.CacheKey, downloaded);
                    _cached = downloaded;
                    TableSnapshot? bundled = CurrentOrigin == LoadOrigin.Bundled ? Current : null;
                    SelectNewest(bundled);
                    progress?.Report($"Downloaded {downloaded.RecordCount} {Name} record(s).");
                    return new TableLoadResult(Current, CurrentOrigin == LoadOrigin.Cache ? LoadOrigin.Network : CurrentOrigin);
                }
            }
        }

        private TableLoadResult Fallback(string problem)
        {
            if (Current == null)
            {
                return new TableLoadResult(null, LoadOrigin.None, problem);
            }

            string copy = CurrentOrigin == LoadOrigin.Bundled ? "the copy that shipped with this program" : "the copy downloaded";
            return new TableLoadResult(Current, CurrentOrigin, $"{problem} Using {copy} (generated {Current.GeneratedAtUtc.ToLocalTime():yyyy-MM-dd}).");
        }
    }
}
