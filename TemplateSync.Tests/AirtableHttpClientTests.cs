using System.Net;
using Newtonsoft.Json.Linq;
using TemplateSync.Airtable;
using TemplateSync.Tests.Fakes;
using Xunit;

namespace TemplateSync.Tests;

public class AirtableHttpClientTests
{
    private readonly FakeHttpHandler _handler = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));

    private AirtableHttpClient CreateClient(int maxAttempts = 3)
        => new(new HttpClient(_handler), "patTEST.token", _clock,
               new AirtableClientOptions { MaxAttempts = maxAttempts, MaxJitter = TimeSpan.Zero },
               new RequestThrottle(TimeSpan.FromMilliseconds(220)), new Random(1));

    [Fact]
    public async Task List_request_carries_page_size_fields_formula_offset_and_bearer_token()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(2)));
        var request = new ListRecordsRequest("appAAAAAAAAAAAAAA", "tblBBBBBBBBBBBBBB")
        {
            Fields = new[] { "Structure", "Template Recommend" },
            FilterByFormula = "IS_AFTER(LAST_MODIFIED_TIME(), '2026')",
            Offset = "itr/abc",
        };

        ListRecordsPage page = await CreateClient().ListRecordsPageAsync(request, CancellationToken.None);

        Assert.Equal(2, page.Records.Count);
        RecordedRequest sent = Assert.Single(_handler.Requests);
        Assert.Equal("/v0/appAAAAAAAAAAAAAA/tblBBBBBBBBBBBBBB", sent.Uri.AbsolutePath);
        Assert.Equal(new[] { "100" }, sent.Uri.QueryValues("pageSize"));
        Assert.Equal(new[] { "Structure", "Template Recommend" }, sent.Uri.QueryValues("fields[]"));
        Assert.Equal(new[] { "IS_AFTER(LAST_MODIFIED_TIME(), '2026')" }, sent.Uri.QueryValues("filterByFormula"));
        Assert.Equal(new[] { "itr/abc" }, sent.Uri.QueryValues("offset"));
        Assert.Equal("Bearer patTEST.token", sent.Authorization);
    }

    [Fact]
    public async Task Server_date_header_is_exposed_as_the_sync_cursor()
    {
        var date = new DateTimeOffset(2026, 9, 28, 11, 59, 30, TimeSpan.Zero);
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(1)), r => r.Headers.Date = date);

        ListRecordsPage page = await CreateClient().ListRecordsPageAsync(new ListRecordsRequest("appAAAAAAAAAAAAAA", "tbl"), CancellationToken.None);

        Assert.Equal(date, page.ServerDate);
    }

    [Fact]
    public async Task Billing_limit_429_fails_immediately_without_retrying()
    {
        _handler.Respond((HttpStatusCode)429, AirtableJson.Error("PUBLIC_API_BILLING_LIMIT_EXCEEDED", "Monthly API call limit exceeded"));

        await Assert.ThrowsAsync<AirtableQuotaExceededException>(() =>
            CreateClient().ListRecordsPageAsync(new ListRecordsRequest("appAAAAAAAAAAAAAA", "tbl"), CancellationToken.None));

        Assert.Single(_handler.Requests);
        Assert.DoesNotContain(_clock.Delays, d => d >= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Rate_limit_429_waits_30_seconds_then_succeeds()
    {
        _handler.Respond((HttpStatusCode)429, AirtableJson.Error("RATE_LIMIT_REACHED", "Rate limit exceeded"))
                .Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(1)));

        ListRecordsPage page = await CreateClient().ListRecordsPageAsync(new ListRecordsRequest("appAAAAAAAAAAAAAA", "tbl"), CancellationToken.None);

        Assert.Single(page.Records);
        Assert.Equal(2, _handler.Requests.Count);
        Assert.Contains(TimeSpan.FromSeconds(30), _clock.Delays);
    }

    [Fact]
    public async Task Rate_limit_retries_are_bounded_by_max_attempts()
    {
        for (int i = 0; i < 3; i++)
        {
            _handler.Respond((HttpStatusCode)429, AirtableJson.Error("RATE_LIMIT_REACHED", "Rate limit exceeded"));
        }

        await Assert.ThrowsAsync<AirtableRateLimitException>(() =>
            CreateClient(maxAttempts: 3).ListRecordsPageAsync(new ListRecordsRequest("appAAAAAAAAAAAAAA", "tbl"), CancellationToken.None));

        Assert.Equal(3, _handler.Requests.Count);
        Assert.Equal(0, _handler.Remaining);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, typeof(AirtableAuthException))]
    [InlineData(HttpStatusCode.Forbidden, typeof(AirtableAuthException))]
    [InlineData(HttpStatusCode.NotFound, typeof(AirtableNotFoundException))]
    public async Task Auth_and_not_found_errors_are_not_retried(HttpStatusCode status, Type expected)
    {
        _handler.Respond(status, AirtableJson.Error("AUTHENTICATION_REQUIRED", "nope"));

        Exception ex = await Assert.ThrowsAnyAsync<AirtableException>(() =>
            CreateClient().ListRecordsPageAsync(new ListRecordsRequest("appAAAAAAAAAAAAAA", "tbl"), CancellationToken.None));

        Assert.IsType(expected, ex);
        Assert.Single(_handler.Requests);
        Assert.DoesNotContain("patTEST", ex.Message);
    }

    [Fact]
    public async Task Server_errors_back_off_and_give_up_after_max_attempts()
    {
        _handler.Respond(HttpStatusCode.ServiceUnavailable, "")
                .Respond(HttpStatusCode.BadGateway, "")
                .Respond(HttpStatusCode.InternalServerError, "");

        await Assert.ThrowsAsync<AirtableUnavailableException>(() =>
            CreateClient().ListRecordsPageAsync(new ListRecordsRequest("appAAAAAAAAAAAAAA", "tbl"), CancellationToken.None));

        Assert.Equal(3, _handler.Requests.Count);
        Assert.Contains(TimeSpan.FromSeconds(1), _clock.Delays);
        Assert.Contains(TimeSpan.FromSeconds(2), _clock.Delays);
    }

    [Fact]
    public async Task Network_failure_is_retried_then_reported_as_unavailable()
    {
        _handler.Throw(new HttpRequestException("DNS failure"))
                .Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(1)));

        ListRecordsPage page = await CreateClient().ListRecordsPageAsync(new ListRecordsRequest("appAAAAAAAAAAAAAA", "tbl"), CancellationToken.None);

        Assert.Single(page.Records);
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task Unknown_field_422_surfaces_type_and_raw_message()
    {
        _handler.Respond((HttpStatusCode)422, AirtableJson.Error("UNKNOWN_FIELD_NAME", "Unknown field name: \"CommonName\""));

        AirtableException ex = await Assert.ThrowsAsync<AirtableException>(() =>
            CreateClient().ListRecordsPageAsync(new ListRecordsRequest("appAAAAAAAAAAAAAA", "tbl"), CancellationToken.None));

        Assert.Equal("UNKNOWN_FIELD_NAME", ex.ErrorType);
        Assert.Equal("Unknown field name: \"CommonName\"", ex.ApiMessage);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task Billing_limit_in_the_errors_array_shape_is_recognised()
    {
        _handler.Respond((HttpStatusCode)429, "{\"errors\":[{\"error\":\"PUBLIC_API_BILLING_LIMIT_EXCEEDED\",\"message\":\"API billing plan limit exceeded\"}]}");

        await Assert.ThrowsAsync<AirtableQuotaExceededException>(() =>
            CreateClient().ListRecordsPageAsync(new ListRecordsRequest("appAAAAAAAAAAAAAA", "tbl"), CancellationToken.None));

        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task Billing_limit_is_recognised_from_an_unexpected_body()
    {
        _handler.Respond((HttpStatusCode)429, "PUBLIC_API_BILLING_LIMIT_EXCEEDED");

        await Assert.ThrowsAsync<AirtableQuotaExceededException>(() =>
            CreateClient().ListRecordsPageAsync(new ListRecordsRequest("appAAAAAAAAAAAAAA", "tbl"), CancellationToken.None));
    }

    [Fact]
    public async Task Create_is_retried_after_a_rate_limit_but_not_after_a_timeout()
    {
        _handler.Respond((HttpStatusCode)429, AirtableJson.Error("RATE_LIMIT_REACHED", "slow down"))
                .Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(1)));
        await CreateClient().CreateRecordsAsync("appAAAAAAAAAAAAAA", "tbl", new[] { new JObject() }, true, CancellationToken.None);
        Assert.Equal(2, _handler.Requests.Count);

        _handler.Throw(new TaskCanceledException("timeout"));
        await Assert.ThrowsAsync<AirtableUnavailableException>(() =>
            CreateClient().CreateRecordsAsync("appAAAAAAAAAAAAAA", "tbl", new[] { new JObject() }, true, CancellationToken.None));
        Assert.Equal(3, _handler.Requests.Count);
    }

    [Fact]
    public async Task Update_is_retried_after_a_server_error_because_patch_is_idempotent()
    {
        _handler.Respond(HttpStatusCode.ServiceUnavailable, "")
                .Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(1)));

        await CreateClient().UpdateRecordsAsync("appAAAAAAAAAAAAAA", "tbl", new[] { new RecordUpdate("rec1", new JObject()) }, true, CancellationToken.None);

        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task Create_sends_one_request_with_typecast_and_up_to_ten_records()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(10)));
        var records = Enumerable.Range(0, 10).Select(i => new JObject { ["Structure"] = $"ROI_{i}" }).ToList();

        IReadOnlyList<AirtableRecord> created = await CreateClient().CreateRecordsAsync("appAAAAAAAAAAAAAA", "tbl", records, true, CancellationToken.None);

        Assert.Equal(10, created.Count);
        RecordedRequest sent = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        JObject body = JObject.Parse(sent.Body!);
        Assert.True((bool)body["typecast"]!);
        Assert.Equal(10, ((JArray)body["records"]!).Count);
        Assert.Equal("ROI_3", (string?)body["records"]![3]!["fields"]!["Structure"]);
    }

    [Fact]
    public async Task Update_uses_patch_with_record_ids()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(1)));

        await CreateClient().UpdateRecordsAsync("appAAAAAAAAAAAAAA", "tbl", new[] { new RecordUpdate("rec00000000000000", new JObject { ["RGB"] = "1,2,3" }) }, true, CancellationToken.None);

        RecordedRequest sent = Assert.Single(_handler.Requests);
        Assert.Equal("PATCH", sent.Method.Method);
        JObject body = JObject.Parse(sent.Body!);
        Assert.Equal("rec00000000000000", (string?)body["records"]![0]!["id"]);
        Assert.Equal("1,2,3", (string?)body["records"]![0]!["fields"]!["RGB"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task Write_batches_outside_1_to_10_records_are_rejected_before_sending(int count)
    {
        var records = Enumerable.Range(0, count).Select(_ => new JObject()).ToList();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateClient().CreateRecordsAsync("appAAAAAAAAAAAAAA", "tbl", records, true, CancellationToken.None));

        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task Requests_to_one_base_are_spaced_by_the_throttle()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(1)))
                .Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(1)));
        AirtableHttpClient client = CreateClient();

        await client.ListRecordsPageAsync(new ListRecordsRequest("appAAAAAAAAAAAAAA", "tbl"), CancellationToken.None);
        await client.ListRecordsPageAsync(new ListRecordsRequest("appAAAAAAAAAAAAAA", "tbl"), CancellationToken.None);

        Assert.Contains(TimeSpan.FromMilliseconds(220), _clock.Delays);
    }

    [Fact]
    public async Task Field_values_that_look_like_dates_are_kept_verbatim()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(new[] { AirtableJson.Record("rec00000000000001", new { ContextGroupVersion = "2016-12-09T00:00:00Z" }) }));

        ListRecordsPage page = await CreateClient().ListRecordsPageAsync(new ListRecordsRequest("appAAAAAAAAAAAAAA", "tbl"), CancellationToken.None);

        Assert.Equal(JTokenType.String, page.Records[0].Fields["ContextGroupVersion"]!.Type);
        Assert.Equal("2016-12-09T00:00:00Z", (string?)page.Records[0].Fields["ContextGroupVersion"]);
    }
}
