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

    /// <summary>
    /// The hold ends when the rename has been refused once, not after a delay. A release on a timer needs a free
    /// thread-pool thread while the swap blocks one; on a busy runner it came after the retries were spent (F-7).
    /// </summary>
    [Fact]
    public void A_target_held_open_for_a_moment_is_replaced_once_it_is_released()
    {
        string path = _temp.File("table.json");
        string temp = path + ".tmp";
        SnapshotStore.WriteFile(path, Snapshot("Old"));
        File.WriteAllText(temp, SnapshotStore.Serialize(Snapshot("New")));
        var refusals = new List<Exception>();
        using FileStream holder = HoldWithoutDeleteSharing(path);

        FileSwap.MoveOver(temp, path, (from, to) =>
        {
            try
            {
                File.Move(from, to, overwrite: true);
            }
            catch (Exception ex)
            {
                refusals.Add(ex);
                holder.Dispose();
                throw;
            }
        });

        Assert.Equal("New", (string?)SnapshotStore.TryLoadFile(path)!.Records[0].Fields["Structure"]);
        Assert.False(File.Exists(temp));
        if (OperatingSystem.IsWindows())
        {
            Assert.NotEmpty(refusals);
        }
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public void A_refused_rename_is_retried_until_it_succeeds(Type refusal)
    {
        int attempts = 0;

        FileSwap.MoveOver(_temp.File("table.json.tmp"), _temp.File("table.json"), (_, _) =>
        {
            if (++attempts < 3)
            {
                throw (Exception)Activator.CreateInstance(refusal)!;
            }
        });

        Assert.Equal(3, attempts);
    }

    [Fact]
    public void A_rename_denied_on_every_attempt_fails_after_the_last_retry_and_the_temporary_file_is_removed()
    {
        string temp = _temp.File("table.json.tmp");
        File.WriteAllText(temp, "{}");
        int attempts = 0;

        Assert.Throws<UnauthorizedAccessException>(() => FileSwap.MoveOver(temp, _temp.File("table.json"), (_, _) =>
        {
            attempts++;
            throw new UnauthorizedAccessException();
        }));

        Assert.Equal(FileSwap.RetryDelays.Length + 1, attempts);
        Assert.False(File.Exists(temp));
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
