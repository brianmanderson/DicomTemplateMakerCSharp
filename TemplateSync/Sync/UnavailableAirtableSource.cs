using System;
using System.Threading;
using System.Threading.Tasks;
using TemplateSync.Airtable;
using TemplateSync.Storage;

namespace TemplateSync.Sync
{
    /// <summary>
    /// Stands in for a saved connection that cannot be opened (for example its token cannot be decrypted
    /// by this Windows account), so it still appears in the list and can be removed.
    /// </summary>
    public sealed class UnavailableAirtableSource : ITemplateTableSource
    {
        private readonly SnapshotStore _store;

        public UnavailableAirtableSource(AirtableSourceDefinition definition, SnapshotStore store, string problem)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            Problem = problem;
        }

        public AirtableSourceDefinition Definition { get; }

        public string Problem { get; }

        public string Name => Definition.Name;

        public bool IsWritable => true;

        public TableSnapshot? Current => null;

        public Task<TableLoadResult> LoadAsync(LoadMode mode, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            if (mode == LoadMode.CacheOnly)
            {
                return Task.FromResult(new TableLoadResult(null, LoadOrigin.None, Problem));
            }

            throw new AirtableException(Problem);
        }

        public void DeleteCache()
        {
            _store.Delete(Definition.CacheKey);
        }
    }
}
