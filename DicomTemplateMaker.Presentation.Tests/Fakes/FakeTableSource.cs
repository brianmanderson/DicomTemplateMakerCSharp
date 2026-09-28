using TemplateSync.Storage;
using TemplateSync.Sync;

namespace DicomTemplateMaker.Presentation.Tests.Fakes;

/// <summary>
/// Scripted template table. By default a load returns <see cref="Current"/> at once; set
/// <see cref="OnLoad"/> to report progress, wait, fail or observe cancellation.
/// </summary>
public sealed class FakeTableSource : ITemplateTableSource
{
    public FakeTableSource(string name, bool writable = true, TableSnapshot? current = null)
    {
        Name = name;
        IsWritable = writable;
        Current = current;
    }

    public string Name { get; }

    public bool IsWritable { get; }

    public TableSnapshot? Current { get; set; }

    public Func<LoadMode, IProgress<string>?, CancellationToken, Task<TableLoadResult>>? OnLoad { get; set; }

    public List<LoadMode> Modes { get; } = new();

    public List<CancellationToken> Tokens { get; } = new();

    public bool CacheDeleted { get; private set; }

    public Task<TableLoadResult> LoadAsync(LoadMode mode, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        Modes.Add(mode);
        Tokens.Add(cancellationToken);
        if (OnLoad != null)
        {
            return OnLoad(mode, progress, cancellationToken);
        }

        return Task.FromResult(new TableLoadResult(Current, Current == null ? LoadOrigin.None : LoadOrigin.Cache));
    }

    public void DeleteCache()
    {
        CacheDeleted = true;
        Current = null;
    }
}
