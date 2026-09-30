using System.Collections.ObjectModel;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMaker.Presentation.Tests.Fakes;

public sealed record AddedConnection(string Name, string BaseId, string Table, string Token);

/// <summary>In-memory catalog: records connection tests, additions and removals. No network, no files.</summary>
public sealed class FakeCatalog : ITemplateSourceCatalog
{
    public ObservableCollection<TemplateSourceItem> Sources { get; } = new();

    /// <summary>Answers connection tests; succeeds (null) by default.</summary>
    public Func<string, string, string, CancellationToken, Task<string?>> OnTest { get; set; } = (_, _, _, _) => Task.FromResult<string?>(null);

    public List<(string BaseId, string Table, string Token)> Tests { get; } = new();

    public List<AddedConnection> Added { get; } = new();

    public List<TemplateSourceItem> Removed { get; } = new();

    public Exception? AddFailure { get; set; }

    public TemplateSourceItem AddSource(FakeTableSource source)
    {
        var item = new TemplateSourceItem(source);
        Sources.Add(item);
        return item;
    }

    public Task<string?> TestConnectionAsync(string baseId, string table, string token, CancellationToken cancellationToken)
    {
        Tests.Add((baseId, table, token));
        return OnTest(baseId, table, token, cancellationToken);
    }

    public TemplateSourceItem AddConnection(string name, string baseId, string table, string token)
    {
        if (AddFailure != null)
        {
            throw AddFailure;
        }

        Added.Add(new AddedConnection(name, baseId, table, token));
        TemplateSourceItem? existing = Sources.FirstOrDefault(s => s.IsWritable && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            Sources.Remove(existing);
        }

        return AddSource(new FakeTableSource(name));
    }

    public void RemoveConnection(TemplateSourceItem item)
    {
        Removed.Add(item);
        Sources.Remove(item);
    }
}
