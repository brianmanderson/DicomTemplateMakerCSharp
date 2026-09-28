using System;
using System.Threading;
using System.Threading.Tasks;
using TemplateSync.Storage;

namespace TemplateSync.Sync
{
    /// <summary>A named table of template entries that can be loaded from cache or refreshed.</summary>
    public interface ITemplateTableSource
    {
        string Name { get; }

        bool IsWritable { get; }

        /// <summary>The most recently loaded data, or null before the first successful load.</summary>
        TableSnapshot? Current { get; }

        Task<TableLoadResult> LoadAsync(LoadMode mode, IProgress<string>? progress, CancellationToken cancellationToken);

        /// <summary>Removes any local cache for this source.</summary>
        void DeleteCache();
    }
}
