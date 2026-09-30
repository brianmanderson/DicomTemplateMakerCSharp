using System.Net;
using Newtonsoft.Json.Linq;
using TemplateSync.Airtable;
using TemplateSync.Model;
using TemplateSync.Storage;
using TemplateSync.Sync;
using TemplateSync.Tests.Fakes;
using Xunit;

namespace TemplateSync.Tests;

public sealed class AirtableTableSourceTests : IDisposable
{
    private const string BaseId = "appzWlVKRp9TrrTUJ";
    private const string TableId = "tblltR3aTxlJUwaGa";
    private readonly TempDirectory _temp = new();
    private readonly FakeHttpHandler _handler = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly SyncSettings _settings = new();

    public void Dispose() => _temp.Dispose();

    private SnapshotStore Store => new(_temp.Path);

    private AirtableTableSource CreateSource()
    {
        var api = new AirtableHttpClient(new HttpClient(_handler), "patTEST.token", _clock,
            new AirtableClientOptions { MaxJitter = TimeSpan.Zero }, new RequestThrottle(TimeSpan.Zero));
        return new AirtableTableSource(new AirtableSourceDefinition("Clinic", BaseId, TableId), api, Store, _settings, _clock);
    }

    private static Action<HttpResponseMessage> Dated(DateTimeOffset date) => r => r.Headers.Date = date;

    [Fact]
    public async Task Full_load_of_246_records_costs_three_list_calls_and_no_per_record_calls()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(100, 0), "off1"))
                .Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(100, 100), "off2"))
                .Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(46, 200)));

        TableLoadResult result = await CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None);

        Assert.Equal(3, result.ApiCalls);
        Assert.Equal(3, _handler.Requests.Count);
        Assert.Equal(246, result.Snapshot!.RecordCount);
        Assert.Equal(new[] { "off1" }, _handler.Requests[1].Uri.QueryValues("offset"));
        Assert.Equal(new[] { "off2" }, _handler.Requests[2].Uri.QueryValues("offset"));
        Assert.All(_handler.Requests, r => Assert.Equal(HttpMethod.Get, r.Method));
        Assert.False(result.UsedDelta);
    }

    [Fact]
    public async Task List_requests_ask_only_for_mapped_fields()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(1)));

        await CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None);

        Assert.Equal(AirTableEntry.FieldNames, _handler.Requests[0].Uri.QueryValues("fields[]"));
        Assert.DoesNotContain("Id", _handler.Requests[0].Uri.QueryValues("fields[]"));
    }

    [Fact]
    public async Task Startup_cache_only_load_never_calls_airtable()
    {
        TableLoadResult result = await CreateSource().LoadAsync(LoadMode.CacheOnly, null, CancellationToken.None);

        Assert.Null(result.Snapshot);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task Fresh_cache_is_served_without_any_call_across_restarts()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(5)));
        await CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(23));

        TableLoadResult again = await CreateSource().LoadAsync(LoadMode.IfStale, null, CancellationToken.None);

        Assert.Equal(LoadOrigin.Cache, again.Origin);
        Assert.Equal(5, again.Snapshot!.RecordCount);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task Expired_cache_triggers_a_delta_refresh_with_a_modified_since_formula()
    {
        var firstServerTime = new DateTimeOffset(2026, 9, 28, 11, 59, 58, TimeSpan.Zero);
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(3)), Dated(firstServerTime));
        await CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(25));
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(new[]
        {
            AirtableJson.Record("rec00000000000001", new { Structure = "ROI_1_renamed", Template_Recommend = new[] { "SiteA" } }),
            AirtableJson.Record("recNEW0000000000X", new { Structure = "Brand_new", Template_Consider = new[] { "SiteB" } }),
        }));

        TableLoadResult result = await CreateSource().LoadAsync(LoadMode.IfStale, null, CancellationToken.None);

        Assert.True(result.UsedDelta);
        Assert.Equal(1, result.ApiCalls);
        string formula = _handler.Requests[1].Uri.QueryValues("filterByFormula").Single();
        Assert.Contains("LAST_MODIFIED_TIME()", formula);
        Assert.Contains("CREATED_TIME()", formula);
        Assert.Contains("2026-09-28T11:57:58Z", formula); // server time minus the 2-minute safety margin
        Assert.Equal(new[] { "rec00000000000000", "rec00000000000001", "rec00000000000002", "recNEW0000000000X" }, result.Snapshot!.Records.Select(r => r.Id));
        Assert.Equal("ROI_1_renamed", (string?)result.Snapshot.Records[1].Fields["Structure"]);
    }

    [Fact]
    public async Task Refresh_after_the_full_refresh_interval_downloads_everything_so_deletions_disappear()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(3)));
        await CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _clock.Advance(TimeSpan.FromDays(8));
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(2)));

        TableLoadResult result = await CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None);

        Assert.False(result.UsedDelta);
        Assert.Empty(_handler.Requests[1].Uri.QueryValues("filterByFormula"));
        Assert.Equal(2, result.Snapshot!.RecordCount);
    }

    [Fact]
    public async Task Quota_exhaustion_falls_back_to_the_cache_with_a_warning_and_one_call()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(4)));
        await CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _handler.Respond((HttpStatusCode)429, AirtableJson.Error("PUBLIC_API_BILLING_LIMIT_EXCEEDED", "limit"));

        TableLoadResult result = await CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None);

        Assert.Equal(LoadOrigin.Cache, result.Origin);
        Assert.Equal(4, result.Snapshot!.RecordCount);
        Assert.Contains("monthly API call limit", result.Warning);
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task A_failing_later_page_ends_the_fetch_instead_of_looping()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(100), "off1"))
                .Respond((HttpStatusCode)429, AirtableJson.Error("PUBLIC_API_BILLING_LIMIT_EXCEEDED", "limit"));

        await Assert.ThrowsAsync<AirtableQuotaExceededException>(() => CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None));

        Assert.Equal(2, _handler.Requests.Count);
        Assert.Null(Store.TryLoad(new AirtableSourceDefinition("Clinic", BaseId, TableId).CacheKey));
    }

    [Fact]
    public async Task An_unknown_column_costs_one_retry_without_the_field_filter_and_is_remembered()
    {
        _handler.Respond((HttpStatusCode)422, AirtableJson.Error("UNKNOWN_FIELD_NAME", "Unknown field name: \"CommonName\""))
                .Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(2)));

        TableLoadResult result = await CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None);

        Assert.Equal(2, result.ApiCalls);
        Assert.True(result.Snapshot!.FieldsFilterDisabled);
        Assert.Empty(_handler.Requests[1].Uri.QueryValues("fields[]"));

        _clock.Advance(TimeSpan.FromDays(2));
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));
        await CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        Assert.Empty(_handler.Requests[2].Uri.QueryValues("fields[]"));
        Assert.Equal(3, _handler.Requests.Count);
    }

    [Fact]
    public async Task A_user_full_refresh_tries_the_field_filter_again()
    {
        _handler.Respond((HttpStatusCode)422, AirtableJson.Error("UNKNOWN_FIELD_NAME", "Unknown field name: \"RGB\""))
                .Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(1)))
                .Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(1)));
        await CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None);

        TableLoadResult result = await CreateSource().LoadAsync(LoadMode.FullRefresh, null, CancellationToken.None);

        Assert.False(result.Snapshot!.FieldsFilterDisabled);
        Assert.NotEmpty(_handler.Requests[2].Uri.QueryValues("fields[]"));
    }

    [Fact]
    public async Task A_table_without_a_structure_column_is_rejected_with_a_clear_message()
    {
        _handler.Respond((HttpStatusCode)422, AirtableJson.Error("UNKNOWN_FIELD_NAME", "Unknown field name: \"Structure\""));

        AirtableException ex = await Assert.ThrowsAsync<AirtableException>(() => CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None));

        Assert.Contains("not a template table", ex.Message);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task An_expired_page_cursor_restarts_the_download_once()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(100), "off1"))
                .Respond((HttpStatusCode)422, AirtableJson.Error("LIST_RECORDS_ITERATOR_NOT_AVAILABLE", "expired"))
                .Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(100), "off2"))
                .Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(5, 100)));

        TableLoadResult result = await CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None);

        Assert.Equal(105, result.Snapshot!.RecordCount);
        Assert.Empty(_handler.Requests[2].Uri.QueryValues("offset"));
        Assert.Equal(4, result.ApiCalls);
    }

    [Fact]
    public async Task A_second_expired_cursor_fails_instead_of_looping()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(100), "off1"))
                .Respond((HttpStatusCode)422, AirtableJson.Error("LIST_RECORDS_ITERATOR_NOT_AVAILABLE", "expired"))
                .Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(100), "off2"))
                .Respond((HttpStatusCode)422, AirtableJson.Error("LIST_RECORDS_ITERATOR_NOT_AVAILABLE", "expired"));

        await Assert.ThrowsAsync<AirtableException>(() => CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None));

        Assert.Equal(4, _handler.Requests.Count);
    }

    [Fact]
    public async Task Unattributable_unknown_field_error_disables_the_filter()
    {
        _handler.Respond((HttpStatusCode)422, AirtableJson.Error("UNKNOWN_FIELD_NAME", "Something else went wrong"))
                .Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(1)));

        TableLoadResult result = await CreateSource().LoadAsync(LoadMode.Refresh, null, CancellationToken.None);

        Assert.True(result.Snapshot!.FieldsFilterDisabled);
        Assert.Empty(_handler.Requests[1].Uri.QueryValues("fields[]"));
    }

    [Fact]
    public async Task Write_refreshes_first_then_batches_creates_and_updates_ten_per_request()
    {
        // Existing table: 12 ROIs already listed under SiteB.
        var existing = Enumerable.Range(0, 12).Select(i => AirtableJson.Record($"rec{i:D14}", new { Structure = $"ROI_{i}", Template_Recommend = new[] { "SiteB" } })).ToList();
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(existing));
        AirtableTableSource source = CreateSource();
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);

        // Template "SiteA": the 12 existing ROIs plus 11 new ones.
        var rois = Enumerable.Range(0, 23).Select(i => new LocalRoi(new AirTableEntry { Structure = $"ROI_{i}", Colors_RGB = new List<string> { "Auto:1,2,3" }, RGB = "1,2,3" }, include: true)).ToList();
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>())); // delta before writing
        _handler.Respond(r => Echo(r, created: true)).Respond(r => Echo(r, created: true));   // 11 creates -> 2 requests
        _handler.Respond(r => Echo(r, created: false)).Respond(r => Echo(r, created: false)); // 12 updates -> 2 requests

        WriteResult result = await source.WriteTemplateAsync("SiteA", rois, null, CancellationToken.None);

        Assert.Equal(11, result.Created);
        Assert.Equal(12, result.Updated);
        Assert.Equal(5, result.ApiCalls); // 1 delta + 4 writes (old code: 12 updates + 11 creates + 11 id copies = 34)
        Assert.Equal(new[] { "GET", "GET", "POST", "POST", "PATCH", "PATCH" }, _handler.Requests.Select(r => r.Method.Method));
        Assert.All(_handler.Requests.Skip(2), r => Assert.DoesNotContain("\"Id\"", r.Body));
        JObject firstUpdate = JObject.Parse(_handler.Requests[4].Body!);
        Assert.Equal(new[] { "SiteA", "SiteB" }, firstUpdate["records"]![0]!["fields"]!["Template_Recommend"]!.Values<string>());
        Assert.Equal(23, source.Current!.Records.Count);
    }

    [Fact]
    public async Task Writing_an_unchanged_template_costs_only_the_refresh()
    {
        var existing = new[] { AirtableJson.Record("rec00000000000000", new { Structure = "Brain", Type = "ORGAN", RGB = "1,2,3", Colors_RGB = new[] { "Auto:1,2,3" }, Template_Recommend = new[] { "SiteA" } }) };
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(existing));
        AirtableTableSource source = CreateSource();
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));

        WriteResult result = await source.WriteTemplateAsync("SiteA", new[] { new LocalRoi(new AirTableEntry { Structure = "Brain", Type = "ORGAN", RGB = "1,2,3", Colors_RGB = new List<string> { "Auto:1,2,3" }, Scheme = null, ContextGroupVersion = null, MappingResource = null, ContextIdentifier = null, MappingResourceName = null, MappingResourceUID = null, ContextUID = null }, true) }, null, CancellationToken.None);

        Assert.Equal(1, result.Unchanged);
        Assert.Equal(0, result.Created + result.Updated);
        Assert.Equal(1, result.ApiCalls);
    }

    [Fact]
    public async Task Write_is_refused_when_the_pre_write_refresh_fails()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(1)));
        AirtableTableSource source = CreateSource();
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _handler.Respond((HttpStatusCode)429, AirtableJson.Error("PUBLIC_API_BILLING_LIMIT_EXCEEDED", "limit"));

        await Assert.ThrowsAsync<AirtableException>(() => source.WriteTemplateAsync("SiteA", new[] { new LocalRoi(new AirTableEntry { Structure = "X" }, true) }, null, CancellationToken.None));

        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task Write_drops_a_column_the_table_does_not_have_and_retries_once()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));
        AirtableTableSource source = CreateSource();
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()))
                .Respond((HttpStatusCode)422, AirtableJson.Error("UNKNOWN_FIELD_NAME", "Unknown field name: \"CommonName\""))
                .Respond(r => Echo(r, created: true));

        WriteResult result = await source.WriteTemplateAsync("SiteA", new[] { new LocalRoi(new AirTableEntry { Structure = "Brain", CommonName = "Brain" }, true) }, null, CancellationToken.None);

        Assert.Equal(1, result.Created);
        Assert.Contains(result.Warnings, w => w.Contains("CommonName"));
        Assert.DoesNotContain("CommonName", _handler.Requests[3].Body);
        Assert.Equal(new[] { "CommonName" }, source.Current!.MissingFields);
    }

    [Fact]
    public async Task Partial_write_failure_keeps_completed_batches_in_the_cache()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));
        AirtableTableSource source = CreateSource();
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        var rois = Enumerable.Range(0, 15).Select(i => new LocalRoi(new AirTableEntry { Structure = $"ROI_{i}" }, true)).ToList();
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()))
                .Respond(r => Echo(r, created: true))
                .Respond(HttpStatusCode.BadRequest, AirtableJson.Error("INVALID_REQUEST_UNKNOWN", "bad"));

        AirtableException ex = await Assert.ThrowsAsync<AirtableException>(() => source.WriteTemplateAsync("SiteA", rois, null, CancellationToken.None));

        Assert.Contains("Wrote 10 of 15", ex.Message);
        TableSnapshot? cached = Store.TryLoad(source.Definition.CacheKey);
        Assert.Equal(10, cached!.RecordCount);
    }

    [Fact]
    public async Task A_record_deleted_remotely_triggers_one_full_download_and_the_write_succeeds()
    {
        var existing = new[] { AirtableJson.Record("recGONE0000000000", new { Structure = "Brain", Template_Recommend = new[] { "SiteB" } }) };
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(existing));
        AirtableTableSource source = CreateSource();
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()))                                 // delta before writing
                .Respond((HttpStatusCode)422, AirtableJson.Error("ROW_DOES_NOT_EXIST", "Record ID recGONE0000000000 does not exist")) // PATCH
                .Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()))                                 // full download: record is gone
                .Respond(r => Echo(r, created: true));                                                                   // re-planned as a create

        WriteResult result = await source.WriteTemplateAsync("SiteA", new[] { new LocalRoi(new AirTableEntry { Structure = "Brain" }, true) }, null, CancellationToken.None);

        Assert.Equal(1, result.Created);
        Assert.Equal(new[] { "GET", "GET", "PATCH", "GET", "POST" }, _handler.Requests.Select(r => r.Method.Method));
        Assert.Empty(_handler.Requests[3].Uri.QueryValues("filterByFormula"));
        Assert.DoesNotContain(Store.TryLoad(source.Definition.CacheKey)!.Records, r => r.Id == "recGONE0000000000");
    }

    [Fact]
    public async Task Creates_are_not_resent_after_an_ambiguous_server_error()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));
        AirtableTableSource source = CreateSource();
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()))
                .Respond(HttpStatusCode.BadGateway, "");

        await Assert.ThrowsAsync<AirtableException>(() => source.WriteTemplateAsync("SiteA", new[] { new LocalRoi(new AirTableEntry { Structure = "Brain" }, true) }, null, CancellationToken.None));

        Assert.Single(_handler.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Writing_several_templates_refreshes_once()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));
        AirtableTableSource source = CreateSource();
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()))
                .Respond(r => Echo(r, created: true))
                .Respond(r => Echo(r, created: false));
        var templates = new[]
        {
            new TemplateWrite("SiteA", new[] { new LocalRoi(new AirTableEntry { Structure = "Brain" }, true) }),
            new TemplateWrite("SiteB", new[] { new LocalRoi(new AirTableEntry { Structure = "Brain" }, false) }),
        };

        IReadOnlyList<WriteResult> results = await source.WriteTemplatesAsync(templates, null, CancellationToken.None);

        Assert.Equal(new[] { "GET", "GET", "POST", "PATCH" }, _handler.Requests.Select(r => r.Method.Method));
        Assert.Equal(1, results[0].Created);
        Assert.Equal(1, results[1].Updated);
        JObject patch = JObject.Parse(_handler.Requests[3].Body!);
        Assert.Equal(new[] { "SiteB" }, patch["records"]![0]!["fields"]!["Template_Consider"]!.Values<string>());
    }

    [Fact]
    public async Task A_failure_in_a_later_template_reports_what_finished()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));
        AirtableTableSource source = CreateSource();
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()))
                .Respond(r => Echo(r, created: true))
                .Respond(HttpStatusCode.BadRequest, AirtableJson.Error("INVALID_REQUEST_UNKNOWN", "bad"));
        var templates = new[]
        {
            new TemplateWrite("SiteA", new[] { new LocalRoi(new AirTableEntry { Structure = "Brain" }, true) }),
            new TemplateWrite("SiteB", new[] { new LocalRoi(new AirTableEntry { Structure = "Lens" }, true) }),
        };

        AirtableWriteException ex = await Assert.ThrowsAsync<AirtableWriteException>(() => source.WriteTemplatesAsync(templates, null, CancellationToken.None));

        Assert.Equal("SiteB", ex.FailedSite);
        Assert.Single(ex.Completed);
        Assert.Contains("1 of 2 template(s) finished", ex.Message);
    }

    [Fact]
    public async Task Writing_to_a_table_without_a_site_column_is_refused()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));
        AirtableTableSource source = CreateSource();
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()))
                .Respond((HttpStatusCode)422, AirtableJson.Error("UNKNOWN_FIELD_NAME", "Unknown field name: \"Template_Recommend\""));

        AirtableException ex = await Assert.ThrowsAsync<AirtableException>(() => source.WriteTemplateAsync("SiteA", new[] { new LocalRoi(new AirTableEntry { Structure = "Brain" }, true) }, null, CancellationToken.None));

        Assert.Contains("'Template_Recommend'", ex.Message);
        Assert.Single(_handler.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Columns_missing_at_write_time_are_forgotten_on_the_next_full_download()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));
        AirtableTableSource source = CreateSource();
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()))
                .Respond((HttpStatusCode)422, AirtableJson.Error("UNKNOWN_FIELD_NAME", "Unknown field name: \"RGB\""))
                .Respond(r => Echo(r, created: true));
        await source.WriteTemplateAsync("SiteA", new[] { new LocalRoi(new AirTableEntry { Structure = "Brain", RGB = "1,2,3" }, true) }, null, CancellationToken.None);
        Assert.Equal(new[] { "RGB" }, source.Current!.MissingFields);

        _clock.Advance(TimeSpan.FromDays(8));
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));
        TableLoadResult reloaded = await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);

        Assert.False(reloaded.UsedDelta);
        Assert.Null(reloaded.Snapshot!.MissingFields);
    }

    [Fact]
    public async Task A_cache_that_cannot_be_saved_still_returns_the_downloaded_data()
    {
        string blocked = Path.Combine(_temp.Path, "blocked");
        File.WriteAllText(blocked, "a file where the cache folder should be");
        var api = new AirtableHttpClient(new HttpClient(_handler), "patTEST.token", _clock, new AirtableClientOptions { MaxJitter = TimeSpan.Zero }, new RequestThrottle(TimeSpan.Zero));
        var source = new AirtableTableSource(new AirtableSourceDefinition("Clinic", BaseId, TableId), api, new SnapshotStore(blocked), _settings, _clock);
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(3)));

        TableLoadResult result = await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);

        Assert.Equal(3, result.Snapshot!.RecordCount);
        Assert.Null(result.Warning);
        Assert.NotNull(result.CacheWarning);
        Assert.Same(result.Snapshot, source.Current);
    }

    private static int _nextId;

    /// <summary>Answers a create/update like Airtable: echoes the records with ids and full fields.</summary>
    private static HttpResponseMessage Echo(HttpRequestMessage request, bool created)
    {
        JObject body = JObject.Parse(request.Content!.ReadAsStringAsync().Result);
        var records = ((JArray)body["records"]!).Select(r =>
        {
            string id = created ? $"recC{Interlocked.Increment(ref _nextId):D13}" : (string)r["id"]!;
            return AirtableJson.Record(id, r["fields"]!);
        });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(AirtableJson.Page(records)) };
    }
}
