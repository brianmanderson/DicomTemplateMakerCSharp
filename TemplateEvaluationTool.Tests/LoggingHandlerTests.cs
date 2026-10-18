using System.Net;
using Newtonsoft.Json.Linq;
using TemplateSync.Airtable;
using TemplateSync.Tests.Fakes;
using Xunit;

namespace TemplateEvaluationTool.Tests;

/// <summary>Plan step P2.6: one JSON line per request, bodies where they help, never a header value or the token.</summary>
public sealed class LoggingHandlerTests : IDisposable
{
    // Deliberately not token-shaped (the repository is public); the point is only that this text never reaches the log.
    private const string Token = "test-secret-not-a-token";
    private const string BaseId = "appAAAAAAAAAAAAAA";
    private const string TableId = "tblBBBBBBBBBBBBBB";

    private readonly string scratch = Path.Combine(Path.GetTempPath(), "TemplateEvaluationToolTests", Guid.NewGuid().ToString("N"));
    private readonly FakeHttpHandler fake = new();
    private readonly FakeClock clock = new(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));

    public void Dispose()
    {
        try
        {
            Directory.Delete(scratch, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string LogPath => Path.Combine(scratch, LoggingHandler.FileName);

    private AirtableHttpClient Client(int maxAttempts = 3)
        => new(new HttpClient(new LoggingHandler(LogPath, fake, new[] { Token })), Token, clock,
               new AirtableClientOptions { MaxAttempts = maxAttempts, MaxJitter = TimeSpan.Zero }, new RequestThrottle(TimeSpan.Zero));

    private List<JObject> Lines() => File.ReadAllLines(LogPath).Select(JObject.Parse).ToList();

    [Fact]
    public async Task Each_request_is_one_line_with_method_url_status_and_time()
    {
        fake.Respond(HttpStatusCode.OK, AirtableJson.Page(AirtableJson.Records(2)));

        await Client().ListRecordsPageAsync(new ListRecordsRequest(BaseId, TableId), CancellationToken.None);

        JObject line = Assert.Single(Lines());
        Assert.Equal(new[] { "startedUtc", "method", "url", "status", "elapsedMs", "authorization" }, line.Properties().Select(p => p.Name));
        Assert.Equal("GET", (string?)line["method"]);
        Assert.Equal(fake.Requests[0].Uri.ToString(), (string?)line["url"]);
        Assert.Equal(200, (int)line["status"]!);
        Assert.True((long)line["elapsedMs"]! >= 0);
        Assert.Equal("redacted", (string?)line["authorization"]);
        Assert.True(DateTimeOffset.TryParse((string?)line["startedUtc"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _));
    }

    [Fact]
    public async Task Writes_record_the_request_body_and_failures_the_full_response_body()
    {
        string error = AirtableJson.Error("INVALID_MULTIPLE_CHOICE_OPTIONS", "Insufficient permissions to create new select option \"\"SiteA\"\"");
        fake.Respond(HttpStatusCode.OK, AirtableJson.Page(new[] { AirtableJson.Record("rec00000000000001", new { Structure = "Brain" }) }))
            .Respond((HttpStatusCode)422, error);
        AirtableHttpClient client = Client();

        await client.CreateRecordsAsync(BaseId, TableId, new[] { new JObject { ["Structure"] = "Brain" } }, true, CancellationToken.None);
        await Assert.ThrowsAsync<AirtableException>(() => client.UpdateRecordsAsync(BaseId, TableId, new[] { new RecordUpdate("rec00000000000001", new JObject { ["Template_Recommend"] = new JArray("SiteA") }) }, true, CancellationToken.None));

        List<JObject> lines = Lines();
        Assert.Equal(2, lines.Count);
        Assert.Equal("POST", (string?)lines[0]["method"]);
        Assert.Equal(fake.Requests[0].Body, (string?)lines[0]["requestBody"]);
        Assert.Null(lines[0]["responseBody"]);
        Assert.Equal("PATCH", (string?)lines[1]["method"]);
        Assert.Equal(422, (int)lines[1]["status"]!);
        Assert.Equal(fake.Requests[1].Body, (string?)lines[1]["requestBody"]);
        Assert.Equal(error, (string?)lines[1]["responseBody"]);
    }

    [Fact]
    public async Task Every_retry_is_its_own_line()
    {
        fake.Respond(HttpStatusCode.ServiceUnavailable, "upstream unavailable")
            .Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));

        await Client().ListRecordsPageAsync(new ListRecordsRequest(BaseId, TableId), CancellationToken.None);

        List<JObject> lines = Lines();
        Assert.Equal(new[] { 503, 200 }, lines.Select(l => (int)l["status"]!));
        Assert.Equal("upstream unavailable", (string?)lines[0]["responseBody"]);
    }

    [Fact]
    public async Task A_transport_failure_is_logged_with_its_error_and_still_thrown()
    {
        fake.Throw(new HttpRequestException("connection reset"));

        await Assert.ThrowsAsync<AirtableUnavailableException>(() => Client(maxAttempts: 1).ListRecordsPageAsync(new ListRecordsRequest(BaseId, TableId), CancellationToken.None));

        JObject line = Assert.Single(Lines());
        Assert.Equal(JTokenType.Null, line["status"]!.Type);
        Assert.Equal("HttpRequestException: connection reset", (string?)line["error"]);
    }

    [Fact]
    public async Task No_token_text_ever_reaches_the_file()
    {
        // Even a response that echoes the token (Airtable does not, but a proxy might) is written without it.
        fake.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()))
            .Respond(HttpStatusCode.Unauthorized, AirtableJson.Error("AUTHENTICATION_REQUIRED", "Bearer " + Token + " is not valid"))
            .Respond(HttpStatusCode.Forbidden, "Authorization: Bearer " + Token);
        AirtableHttpClient client = Client();

        await client.ListRecordsPageAsync(new ListRecordsRequest(BaseId, TableId), CancellationToken.None);
        await Assert.ThrowsAsync<AirtableAuthException>(() => client.CreateRecordsAsync(BaseId, TableId, new[] { new JObject { ["Structure"] = "Brain" } }, true, CancellationToken.None));
        await Assert.ThrowsAsync<AirtableAuthException>(() => client.ListTablesAsync(BaseId, CancellationToken.None));

        string text = File.ReadAllText(LogPath);
        Assert.Equal(3, Lines().Count);
        Assert.Equal("Bearer " + Token, fake.Requests[0].Authorization);
        Assert.DoesNotContain(Token, text, StringComparison.Ordinal);
        Assert.All(Lines(), l => Assert.Equal("redacted", (string?)l["authorization"]));
        Assert.Contains("[redacted]", (string?)Lines()[1]["responseBody"], StringComparison.Ordinal);
    }
}
