using System;
using System.Collections.Generic;

namespace TemplateSync.Sync
{
    /// <summary>When to go back to the network. Defaults follow docs/MODERNIZATION_PLAN.md.</summary>
    public sealed class SyncSettings
    {
        /// <summary>A cached Airtable table younger than this is used without any API call.</summary>
        public TimeSpan CacheTimeToLive { get; set; } = TimeSpan.FromHours(24);

        /// <summary>After this long, a refresh re-downloads the whole table so deleted records disappear.</summary>
        public TimeSpan FullRefreshInterval { get; set; } = TimeSpan.FromDays(7);

        /// <summary>Overlap subtracted from the sync cursor so edits made during a fetch are not missed.</summary>
        public TimeSpan DeltaSafetyMargin { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>A downloaded shared snapshot younger than this is used without contacting the server.</summary>
        public TimeSpan SnapshotCheckInterval { get; set; } = TimeSpan.FromHours(24);
    }

    public enum LoadMode
    {
        /// <summary>Use what is on disk. Never touches the network.</summary>
        CacheOnly,

        /// <summary>Use the cache unless it is older than its time-to-live.</summary>
        IfStale,

        /// <summary>User asked for a refresh: fetch only changes when possible.</summary>
        Refresh,

        /// <summary>User asked for a full refresh: fetch everything again.</summary>
        FullRefresh,
    }

    public enum LoadOrigin
    {
        None,
        Cache,
        Bundled,
        Network,
    }

    public sealed class TableLoadResult
    {
        public TableLoadResult(Storage.TableSnapshot? snapshot, LoadOrigin origin, string? warning = null, int apiCalls = 0, bool usedDelta = false, int changedRecords = 0, string? cacheWarning = null)
        {
            Snapshot = snapshot;
            Origin = origin;
            Warning = warning;
            ApiCalls = apiCalls;
            UsedDelta = usedDelta;
            ChangedRecords = changedRecords;
            CacheWarning = cacheWarning;
        }

        /// <summary>Set when fresh data arrived but could not be saved to the local cache (not a reason to refuse writes).</summary>
        public string? CacheWarning { get; }

        public Storage.TableSnapshot? Snapshot { get; }

        public LoadOrigin Origin { get; }

        /// <summary>Set when the network step failed and older data is being shown instead.</summary>
        public string? Warning { get; }

        /// <summary>Airtable API calls this load spent.</summary>
        public int ApiCalls { get; }

        public bool UsedDelta { get; }

        public int ChangedRecords { get; }
    }

    /// <summary>One site's template to publish.</summary>
    public sealed class TemplateWrite
    {
        public TemplateWrite(string site, IReadOnlyList<Model.LocalRoi> rois)
        {
            Site = site;
            Rois = rois;
        }

        public string Site { get; }

        public IReadOnlyList<Model.LocalRoi> Rois { get; }
    }

    public sealed class WriteResult
    {
        public string Site { get; internal set; } = string.Empty;

        public int Created { get; internal set; }

        public int Updated { get; internal set; }

        public int Unchanged { get; internal set; }

        public int ApiCalls { get; internal set; }

        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>A multi-template write stopped part-way. Completed templates are listed; nothing is lost.</summary>
    public sealed class AirtableWriteException : Airtable.AirtableException
    {
        public AirtableWriteException(IReadOnlyList<WriteResult> completed, string failedSite, int templateCount, Airtable.AirtableException inner)
            : base($"Stopped while writing '{failedSite}' ({completed.Count} of {templateCount} template(s) finished): {inner.Message}", inner.StatusCode, inner.ErrorType, inner, inner.ApiMessage)
        {
            Completed = completed;
            FailedSite = failedSite;
        }

        public IReadOnlyList<WriteResult> Completed { get; }

        public string FailedSite { get; }
    }
}
