using TemplateSync.Airtable;
using TemplateSync.Infrastructure;
using TemplateSync.Storage;
using TemplateSync.Tests.Fakes;
using Xunit;

namespace TemplateSync.Tests;

/// <summary>
/// The atomic save of a snapshot or cache (<see cref="FileSwap"/>, also used for the connections file). On Windows a
/// rename over a file that another process has open without delete sharing fails; a virus scanner or the indexer does
/// this for a moment after a write, which made saves (and the tests that save) fail now and then.
/// </summary>
public sealed class SnapshotStoreSwapTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static TableSnapshot Snapshot(string structure) => new()
    {
        SourceName = "Clinic",
        Records = { new AirtableRecord { Id = "rec00000000000001", Fields = new Newtonsoft.Json.Linq.JObject { ["Structure"] = structure } } },
    };

    private static FileStream HoldWithoutDeleteSharing(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

    [Fact]
    public void Saving_over_an_existing_file_replaces_it_and_leaves_no_temporary_file()
    {
        string path = _temp.File("table.json");
        SnapshotStore.WriteFile(path, Snapshot("Old"));

        SnapshotStore.WriteFile(path, Snapshot("New"));

        Assert.Equal("New", (string?)SnapshotStore.TryLoadFile(path)!.Records[0].Fields["Structure"]);
        Assert.Equal(new[] { path }, Directory.GetFiles(_temp.Path));
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal((byte)'{', bytes[0]);
        Assert.DoesNotContain("\r\n", System.Text.Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_target_held_open_for_a_moment_is_replaced_once_it_is_released()
    {
        string path = _temp.File("table.json");
        SnapshotStore.WriteFile(path, Snapshot("Old"));
        FileStream holder = HoldWithoutDeleteSharing(path);
        Task release = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }
            finally
            {
                holder.Dispose();
            }
        }, TestContext.Current.CancellationToken);

        SnapshotStore.WriteFile(path, Snapshot("New"));
        await release;

        Assert.Equal("New", (string?)SnapshotStore.TryLoadFile(path)!.Records[0].Fields["Structure"]);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void A_target_held_open_throughout_keeps_its_content_and_the_temporary_file_is_removed()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows refuses to rename over an open file.");
        string path = _temp.File("table.json");
        SnapshotStore.WriteFile(path, Snapshot("Old"));

        Exception error;
        using (HoldWithoutDeleteSharing(path))
        {
            error = Assert.ThrowsAny<Exception>(() => SnapshotStore.WriteFile(path, Snapshot("New")));
        }

        Assert.True(error is IOException || error is UnauthorizedAccessException, error.GetType().Name);
        Assert.Equal("Old", (string?)SnapshotStore.TryLoadFile(path)!.Records[0].Fields["Structure"]);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void The_retries_wait_less_than_a_second_in_total()
    {
        TimeSpan total = FileSwap.RetryDelays.Aggregate(TimeSpan.Zero, (sum, delay) => sum + delay);

        Assert.InRange(total, TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(1));
    }
}
