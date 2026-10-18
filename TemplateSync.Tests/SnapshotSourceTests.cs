using System.Net;
using Newtonsoft.Json.Linq;
using TemplateSync.Airtable;
using TemplateSync.Storage;
using TemplateSync.Sync;
using TemplateSync.Tests.Fakes;
using Xunit;

namespace TemplateSync.Tests;

public sealed class SnapshotSourceTests : IDisposable
{
    private static readonly Uri Url = new("https://example.test/TemplateSnapshots/TG263.json");
    private readonly TempDirectory _temp = new();
    private readonly FakeHttpHandler _handler = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));

    public void Dispose() => _temp.Dispose();

    private SnapshotTableSource CreateSource(string? bundled = null)
        => new(new SnapshotSourceDefinition("TG263", Url, bundled), new HttpClient(_handler), new SnapshotStore(Path.Combine(_temp.Path, "cache")), new SyncSettings(), _clock);

    private static string SnapshotJson(int records, DateTimeOffset generated)
        => SnapshotStore.Serialize(new TableSnapshot
        {
            SourceName = "TG263",
            GeneratedAtUtc = generated,
            Records = AirtableJson.Records(records).Select(r => r.ToObject<AirtableRecord>()!).ToList(),
        });

    [Fact]
    public async Task First_open_downloads_once_and_later_opens_use_the_cache()
    {
        _handler.Respond(HttpStatusCode.OK, SnapshotJson(246, _clock.UtcNow), r => r.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v1\""));

        TableLoadResult first = await CreateSource().LoadAsync(LoadMode.IfStale, null, CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(3));
        TableLoadResult second = await CreateSource().LoadAsync(LoadMode.IfStale, null, CancellationToken.None);

        Assert.Equal(246, first.Snapshot!.RecordCount);
        Assert.Equal(LoadOrigin.Network, first.Origin);
        Assert.Equal(246, second.Snapshot!.RecordCount);
        Assert.Single(_handler.Requests);
        Assert.Null(_handler.Requests[0].Authorization);
    }

    [Fact]
    public async Task Stale_check_sends_the_etag_and_accepts_304()
    {
        _handler.Respond(HttpStatusCode.OK, SnapshotJson(3, _clock.UtcNow), r => r.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v1\""));
        await CreateSource().LoadAsync(LoadMode.IfStale, null, CancellationToken.None);
        _clock.Advance(TimeSpan.FromDays(2));
        _handler.Respond(_ => new HttpResponseMessage(HttpStatusCode.NotModified));

        TableLoadResult result = await CreateSource().LoadAsync(LoadMode.IfStale, null, CancellationToken.None);

        Assert.Equal("\"v1\"", _handler.Requests[1].IfNoneMatch);
        Assert.Equal(3, result.Snapshot!.RecordCount);
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task Full_refresh_ignores_the_etag()
    {
        _handler.Respond(HttpStatusCode.OK, SnapshotJson(3, _clock.UtcNow), r => r.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v1\""));
        await CreateSource().LoadAsync(LoadMode.IfStale, null, CancellationToken.None);
        _handler.Respond(HttpStatusCode.OK, SnapshotJson(4, _clock.UtcNow));

        TableLoadResult result = await CreateSource().LoadAsync(LoadMode.FullRefresh, null, CancellationToken.None);

        Assert.Null(_handler.Requests[1].IfNoneMatch);
        Assert.Equal(4, result.Snapshot!.RecordCount);
    }

    [Fact]
    public async Task Offline_first_run_uses_the_bundled_copy_with_a_warning()
    {
        string bundled = _temp.File("bundled.json");
        File.WriteAllText(bundled, SnapshotJson(7, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)));
        _handler.Throw(new HttpRequestException("offline"));

        TableLoadResult result = await CreateSource(bundled).LoadAsync(LoadMode.IfStale, null, CancellationToken.None);

        Assert.Equal(LoadOrigin.Bundled, result.Origin);
        Assert.Equal(7, result.Snapshot!.RecordCount);
        Assert.Contains("shipped with this program", result.Warning);
    }

    [Fact]
    public async Task Cache_only_never_touches_the_network()
    {
        TableLoadResult result = await CreateSource().LoadAsync(LoadMode.CacheOnly, null, CancellationToken.None);

        Assert.Null(result.Snapshot);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task Unpublished_snapshot_reports_404_clearly()
    {
        _handler.Respond(HttpStatusCode.NotFound, "404: Not Found");

        TableLoadResult result = await CreateSource().LoadAsync(LoadMode.IfStale, null, CancellationToken.None);

        Assert.Null(result.Snapshot);
        Assert.Contains("not been published yet", result.Warning);
    }

    [Fact]
    public async Task Corrupt_download_keeps_the_previous_copy()
    {
        _handler.Respond(HttpStatusCode.OK, SnapshotJson(5, _clock.UtcNow));
        await CreateSource().LoadAsync(LoadMode.IfStale, null, CancellationToken.None);
        _handler.Respond(HttpStatusCode.OK, "{ not json");

        TableLoadResult result = await CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None);

        Assert.Equal(5, result.Snapshot!.RecordCount);
        Assert.Contains("could not be read", result.Warning);
    }

    [Fact]
    public async Task Newer_bundled_copy_wins_over_an_older_cache()
    {
        _handler.Respond(HttpStatusCode.OK, SnapshotJson(5, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        await CreateSource().LoadAsync(LoadMode.IfStale, null, CancellationToken.None);
        string bundled = _temp.File("bundled.json");
        File.WriteAllText(bundled, SnapshotJson(9, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)));

        TableLoadResult result = await CreateSource(bundled).LoadAsync(LoadMode.CacheOnly, null, CancellationToken.None);

        Assert.Equal(LoadOrigin.Bundled, result.Origin);
        Assert.Equal(9, result.Snapshot!.RecordCount);
    }
}

public sealed class SnapshotStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Save_then_load_round_trips_records_and_metadata()
    {
        var store = new SnapshotStore(_temp.Path);
        var snapshot = new TableSnapshot
        {
            SourceName = "Clinic",
            GeneratedAtUtc = new DateTimeOffset(2026, 9, 28, 1, 2, 3, TimeSpan.Zero),
            MissingFields = new List<string> { "RGB" },
            Records = AirtableJson.Records(3).Select(r => r.ToObject<AirtableRecord>()!).ToList(),
        };

        store.Save("key", snapshot);
        TableSnapshot? loaded = store.TryLoad("key");

        Assert.NotNull(loaded);
        Assert.Equal(3, loaded!.RecordCount);
        Assert.Equal(snapshot.GeneratedAtUtc, loaded.GeneratedAtUtc);
        Assert.Equal(new[] { "RGB" }, loaded.MissingFields);
        Assert.False(File.Exists(store.PathFor("key") + ".tmp"));
    }

    [Fact]
    public void Corrupt_or_missing_files_load_as_null()
    {
        var store = new SnapshotStore(_temp.Path);
        File.WriteAllText(store.PathFor("bad"), "{{{");

        Assert.Null(store.TryLoad("bad"));
        Assert.Null(store.TryLoad("missing"));
    }

    [Fact]
    public void Newer_schema_versions_are_rejected_with_a_clear_message()
    {
        var ex = Assert.Throws<InvalidDataException>(() => SnapshotStore.Parse("{\"schemaVersion\": 99, \"records\": []}"));
        Assert.Contains("Update the program", ex.Message);
    }

    [Fact]
    public void Cache_keys_become_safe_file_names()
    {
        var store = new SnapshotStore(_temp.Path);
        Assert.Equal("snapshot-TG-263__shared_.json", Path.GetFileName(store.PathFor("snapshot-TG-263 (shared)")));
    }
}

public sealed class TableExporterTests
{
    [Fact]
    public async Task Export_requests_every_column_keeps_only_the_mapped_ones_and_sorts_field_names()
    {
        var handler = new FakeHttpHandler();
        handler.Respond(HttpStatusCode.OK, AirtableJson.Page(new[] { AirtableJson.Record("rec00000000000001", new { Type = "ORGAN", Structure = "Brain", FMAID = "50801", Notes = "internal" }) }, "off"))
               .Respond(HttpStatusCode.OK, AirtableJson.Page(new[] { AirtableJson.Record("rec00000000000002", new { Structure = "Lens" }) }));
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero));
        var api = new AirtableHttpClient(new HttpClient(handler), "patTEST.token", clock, new AirtableClientOptions(), new RequestThrottle(TimeSpan.Zero));

        TableSnapshot snapshot = await TableExporter.ExportAsync(api, "appzWlVKRp9TrrTUJ", "tblltR3aTxlJUwaGa", "TG263", new[] { "Record" }, clock, null, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Empty(r.Uri.QueryValues("fields[]")));
        Assert.Equal(new[] { "Record" }, handler.Requests[0].Uri.QueryValues("sort[0][field]"));
        Assert.Equal(new[] { "asc" }, handler.Requests[0].Uri.QueryValues("sort[0][direction]"));
        Assert.Equal(new[] { "Record" }, handler.Requests[1].Uri.QueryValues("sort[0][field]"));
        Assert.Equal(new[] { "rec00000000000001", "rec00000000000002" }, snapshot.Records.Select(r => r.Id));
        Assert.Equal(new[] { "Structure", "Type" }, snapshot.Records[0].Fields.Properties().Select(p => p.Name));
        Assert.Equal(clock.UtcNow, snapshot.GeneratedAtUtc);
        Assert.True(TableExporter.SameRecords(snapshot, SnapshotStore.Parse(SnapshotStore.Serialize(snapshot))));
    }

    [Fact]
    public async Task Export_stops_on_an_Airtable_error_without_retrying()
    {
        var handler = new FakeHttpHandler();
        handler.Respond((HttpStatusCode)422, AirtableJson.Error("UNKNOWN_FIELD_NAME", "Unknown field name: \"Record\""));
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero));
        var api = new AirtableHttpClient(new HttpClient(handler), "patTEST.token", clock, new AirtableClientOptions(), new RequestThrottle(TimeSpan.Zero));

        await Assert.ThrowsAsync<AirtableException>(() =>
            TableExporter.ExportAsync(api, "appzWlVKRp9TrrTUJ", "tblltR3aTxlJUwaGa", "TG263", new[] { "Record" }, clock, null, CancellationToken.None));

        Assert.Single(handler.Requests);
    }

    /// <summary>
    /// Plan step P2.9: an all-columns export of the table behind the frozen snapshot writes that snapshot byte for
    /// byte. The response is built from the snapshot the way Airtable returns the live table (no live call is made):
    /// fields in a different order, plus the unmapped columns the table has (the autonumber <c>Record</c>, <c>FMAID</c>,
    /// a primary-field style <c>Name</c>, the other spelling of the reversed-language columns), three pages of 100.
    /// </summary>
    [Fact]
    public async Task Export_of_an_all_columns_response_writes_the_frozen_snapshot_byte_for_byte()
    {
        JObject frozen = FrozenSnapshot.Raw();
        var records = ((JArray)frozen["records"]!).Cast<JObject>().Select((record, index) =>
        {
            var fields = new JObject();
            foreach (JProperty field in ((JObject)record["fields"]!).Properties().Reverse())
            {
                fields[field.Name] = field.Value.DeepClone();
                if (field.Name == "SchemeCode")
                {
                    fields["FMAID"] = "88";
                }
                else if (field.Name == "TG_263Spanish")
                {
                    // The source table spells the reversed-language columns TG_263RSpanish / TG_263RFrench (plan P2.8).
                    fields["TG_263RSpanish"] = "R_" + (string?)field.Value;
                }
                else if (field.Name == "TG_263French")
                {
                    fields["TG_263RFrench"] = "R_" + (string?)field.Value;
                }
            }

            fields["Record"] = index + 1;
            fields["Name"] = "row " + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return new JObject { ["id"] = record["id"], ["createdTime"] = record["createdTime"], ["fields"] = fields };
        }).ToList();
        Assert.Equal(246, records.Count);

        var handler = new FakeHttpHandler();
        handler.Respond(HttpStatusCode.OK, AirtableJson.Page(records.Take(100), "itrPage2/" + records[100]["id"]))
               .Respond(HttpStatusCode.OK, AirtableJson.Page(records.Skip(100).Take(100), "itrPage3/" + records[200]["id"]))
               .Respond(HttpStatusCode.OK, AirtableJson.Page(records.Skip(200)));
        var clock = new FakeClock(DateTimeOffset.Parse((string)frozen["generatedAtUtc"]!, System.Globalization.CultureInfo.InvariantCulture));
        var api = new AirtableHttpClient(new HttpClient(handler), "patTEST.token", clock, new AirtableClientOptions(), new RequestThrottle(TimeSpan.Zero));

        TableSnapshot snapshot = await TableExporter.ExportAsync(api, "appYl68lenTOFqTDh", "tblex7IPsmm8hvVEc", "TG263", new[] { "Record" }, clock, null, CancellationToken.None);
        using var temp = new TempDirectory();
        string output = temp.File("TG263.json");
        SnapshotStore.WriteFile(output, snapshot);

        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, r =>
        {
            Assert.Empty(r.Uri.QueryValues("fields[]"));
            Assert.Equal(new[] { "Record" }, r.Uri.QueryValues("sort[0][field]"));
        });
        Assert.Equal(FrozenSnapshot.Bytes, File.ReadAllBytes(output));
    }
}
