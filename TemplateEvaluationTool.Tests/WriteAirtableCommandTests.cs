using System.Net;
using DicomTemplateMakerGUI.Services;
using Newtonsoft.Json.Linq;
using ROIOntologyClass;
using TemplateSync.Airtable;
using TemplateSync.Storage;
using TemplateSync.Sync;
using TemplateSync.Tests.Fakes;
using Xunit;

namespace TemplateEvaluationTool.Tests;

/// <summary>Plan step P2.7: the write-airtable driver, against scripted HTTP only. No test here reaches the network.</summary>
public sealed class WriteAirtableCommandTests : IDisposable
{
    private const string TargetBase = "appTARGET00000000";
    private const string TargetTable = "tblTARGET00000000";
    private const string Token = "test-secret-not-a-token";

    private readonly string scratch = Path.Combine(Path.GetTempPath(), "TemplateEvaluationToolTests", Guid.NewGuid().ToString("N"));

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

    private string Folder(string name) => Path.Combine(scratch, name);

    /// <summary>SiteA: Brain, Brainstem, an optional Lens_L. SiteB: Brainstem again and Bladder.</summary>
    private string Sites()
    {
        string sites = Folder("sites");
        ROIClassTools.SaveROIsToFolder(new List<ROIClass> { Roi("Brain", "50801"), Roi("Brainstem", "79876"), Roi("Lens_L", "58243", include: false) }, Path.Combine(sites, "SiteA"));
        ROIClassTools.SaveROIsToFolder(new List<ROIClass> { Roi("Brainstem", "79876"), Roi("Bladder", "15900") }, Path.Combine(sites, "SiteB"));
        Directory.CreateDirectory(Path.Combine(sites, "not-a-template"));
        return sites;
    }

    private static ROIClass Roi(string name, string code, bool include = true)
    {
        var roi = new ROIClass(255, 0, 0, name, "ORGAN", new OntologyCodeClass(name, code, "FMA"));
        roi.Include = include;
        return roi;
    }

    private sealed class Host
    {
        public Host(FakeHttpHandler handler, Dictionary<string, string?> environment)
        {
            Handler = handler;
            Value = new WriteAirtableHost
            {
                GetEnvironmentVariable = name => environment.TryGetValue(name, out string? value) ? value : null,
                CreateHandler = () =>
                {
                    HandlersCreated++;
                    return handler;
                },
                Clock = new FakeClock(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero)),
                Throttle = new RequestThrottle(TimeSpan.Zero),
                MaxJitter = TimeSpan.Zero,
            };
        }

        public FakeHttpHandler Handler { get; }

        public WriteAirtableHost Value { get; }

        public int HandlersCreated { get; private set; }
    }

    private static Dictionary<string, string?> Environment(string? targetBases = TargetBase, string? token = Token)
        => new() { [WriteTargetPolicy.TargetBasesVariable] = targetBases, ["AIRTABLE_PAT"] = token };

    private string[] Args(string baseId = TargetBase, string table = TargetTable, params string[] extra)
        => new[] { "--sites", Sites(), "--base", baseId, "--table", table, "--cache-dir", Folder("cache"), "--out", Folder("out") }.Concat(extra).ToArray();

    [Theory]
    [InlineData("appYl68lenTOFqTDh", TargetTable)]
    [InlineData(TargetBase, "tblex7IPsmm8hvVEc")]
    public async Task The_public_base_and_table_are_refused_even_when_listed_and_without_any_request(string baseId, string table)
    {
        var host = new Host(new FakeHttpHandler(), Environment(targetBases: TargetBase + ",appYl68lenTOFqTDh"));

        int exit = await WriteAirtableCommand.RunAsync(Args(baseId, table), host.Value);

        Assert.Equal(3, exit);
        Assert.Equal(0, host.HandlersCreated);
        Assert.Empty(host.Handler.Requests);
        Assert.False(Directory.Exists(Folder("out")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("appOTHER000000000, appOTHER111111111")]
    public async Task A_base_not_listed_in_EVAL_TARGET_BASES_is_refused_without_any_request(string? targetBases)
    {
        var host = new Host(new FakeHttpHandler(), Environment(targetBases));

        int exit = await WriteAirtableCommand.RunAsync(Args(), host.Value);
        int dryRunExit = await WriteAirtableCommand.RunAsync(Args(extra: "--dry-run"), host.Value);

        Assert.Equal(3, exit);
        Assert.Equal(3, dryRunExit);
        Assert.Equal(0, host.HandlersCreated);
        Assert.Empty(host.Handler.Requests);
        Assert.False(Directory.Exists(Folder("out")));
    }

    [Fact]
    public void The_target_policy_accepts_a_listed_base_with_spaces_around_it()
    {
        Assert.Null(WriteTargetPolicy.Refusal(TargetBase, TargetTable, " appOTHER000000000 , " + TargetBase + " "));
        Assert.NotNull(WriteTargetPolicy.Refusal(TargetBase.ToLowerInvariant(), TargetTable, TargetBase));
        Assert.NotNull(WriteTargetPolicy.Refusal("APPYL68LENTOFQTDH", TargetTable, "APPYL68LENTOFQTDH"));
    }

    [Fact]
    public async Task A_real_write_needs_the_token_from_the_environment()
    {
        var host = new Host(new FakeHttpHandler(), Environment(token: null));

        int exit = await WriteAirtableCommand.RunAsync(Args(), host.Value);

        Assert.Equal(2, exit);
        Assert.Equal(0, host.HandlersCreated);
        Assert.Empty(host.Handler.Requests);
    }

    [Fact]
    public async Task A_dry_run_against_an_empty_table_reports_one_create_per_roi_and_sends_nothing()
    {
        var host = new Host(new FakeHttpHandler(), Environment(token: null));

        int exit = await WriteAirtableCommand.RunAsync(Args(extra: new[] { "--site", "SiteA", "--dry-run" }), host.Value);

        Assert.Equal(0, exit);
        Assert.Equal(0, host.HandlersCreated);
        Assert.Empty(host.Handler.Requests);
        Assert.False(File.Exists(Path.Combine(Folder("out"), LoggingHandler.FileName)));
        Assert.True(File.Exists(Path.Combine(Folder("out"), RunContext.ManifestFileName)));
        JObject dryRun = JObject.Parse(File.ReadAllText(Path.Combine(Folder("out"), WriteAirtableCommand.DryRunFileName)));
        Assert.False((bool)dryRun["fromCache"]!);
        JObject site = (JObject)Assert.Single((JArray)dryRun["sites"]!);
        Assert.Equal("SiteA", (string?)site["site"]);
        Assert.Equal(3, (int)site["creates"]!);
        Assert.Equal(0, (int)site["updates"]!);
        Assert.Equal(1, (int)site["requestCount"]!);
        Assert.Equal(new[] { "Brain", "Brainstem", "Lens_L" }, ((JArray)site["createFields"]!).Select(f => (string?)f["Structure"]));
        Assert.Equal(new[] { "SiteA" }, ((JArray)site["createFields"]![2]!["Template_Consider"]!).Values<string>());
    }

    [Fact]
    public async Task A_dry_run_plans_later_sites_against_the_earlier_ones_and_the_cache()
    {
        var cache = new SnapshotStore(Folder("cache"));
        var definition = new AirtableSourceDefinition("x", TargetBase, TargetTable);
        cache.Save(definition.CacheKey, new TableSnapshot
        {
            Records = { new AirtableRecord { Id = "rec00000000000001", Fields = new JObject { ["Structure"] = "Bladder", ["Template_Recommend"] = new JArray("SiteC") } } },
        });
        var host = new Host(new FakeHttpHandler(), Environment());

        int exit = await WriteAirtableCommand.RunAsync(Args(extra: "--dry-run"), host.Value);

        Assert.Equal(0, exit);
        Assert.Empty(host.Handler.Requests);
        JObject dryRun = JObject.Parse(File.ReadAllText(Path.Combine(Folder("out"), WriteAirtableCommand.DryRunFileName)));
        Assert.True((bool)dryRun["fromCache"]!);
        var sites = ((JArray)dryRun["sites"]!).ToDictionary(s => (string)s["site"]!, s => (JObject)s);
        Assert.Equal(new[] { "SiteA", "SiteB" }, sites.Keys);
        Assert.Equal(3, (int)sites["SiteA"]["creates"]!);
        // SiteB: Brainstem was created by SiteA, Bladder is in the cache, so both are updates.
        Assert.Equal(0, (int)sites["SiteB"]["creates"]!);
        Assert.Equal(2, (int)sites["SiteB"]["updates"]!);
        Assert.Equal(2, (int)dryRun["requestCount"]!);
        JObject manifest = JObject.Parse(File.ReadAllText(Path.Combine(Folder("out"), RunContext.ManifestFileName)));
        Assert.Contains(((JArray)manifest["inputs"]!).Select(i => (string?)i["role"]), r => r == "cache");
    }

    [Fact]
    public async Task A_write_session_sends_exactly_what_the_windows_would_send_for_the_same_sites()
    {
        var driverHandler = new FakeHttpHandler();
        Script(driverHandler);
        var host = new Host(driverHandler, Environment());

        int exit = await WriteAirtableCommand.RunAsync(Args(), host.Value);

        // The same sites through TemplateSourceItem.WriteTemplatesAsync, as the main window's "Write to Airtable" calls it.
        var windowHandler = new FakeHttpHandler();
        Script(windowHandler);
        var api = new AirtableHttpClient(new HttpClient(windowHandler), Token, host.Value.Clock, new AirtableClientOptions { MaxJitter = TimeSpan.Zero }, new RequestThrottle(TimeSpan.Zero));
        var item = new TemplateSourceItem(new AirtableTableSource(new AirtableSourceDefinition("Clinic", TargetBase, TargetTable), api, new SnapshotStore(Folder("window-cache")), null, host.Value.Clock));
        var templates = new[] { "SiteA", "SiteB" }
            .Select(site => new KeyValuePair<string, IEnumerable<ROIClass>>(site, ROIClassTools.LoadROIsFromFolder(Path.Combine(Folder("sites"), site), new List<OntologyCodeClass>())))
            .ToList();
        IReadOnlyList<WriteResult> windowResults = await item.WriteTemplatesAsync(templates, null, CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Equal(1, host.HandlersCreated);
        Assert.Equal(new[] { "GET", "POST", "POST", "PATCH" }, driverHandler.Requests.Select(r => r.Method.Method));
        Assert.Equal(windowHandler.Requests.Select(r => (r.Method.Method, r.Uri.ToString(), r.Body)), driverHandler.Requests.Select(r => (r.Method.Method, r.Uri.ToString(), r.Body)));
        Assert.All(driverHandler.Requests, r => Assert.Equal("Bearer " + Token, r.Authorization));
        Assert.Equal(0, driverHandler.Remaining);

        string outDir = Folder("out");
        JObject result = JObject.Parse(File.ReadAllText(Path.Combine(outDir, WriteAirtableCommand.ResultFileName)));
        Assert.Equal(JTokenType.Null, result["error"]!.Type);
        Assert.Equal(4, (int)result["apiCalls"]!);
        var sites = (JArray)result["sites"]!;
        Assert.Equal(windowResults.Select(r => (r.Site, r.ApiCalls, r.Created, r.Updated, r.Unchanged)),
            sites.Select(s => ((string)s["site"]!, (int)s["apiCalls"]!, (int)s["created"]!, (int)s["updated"]!, (int)s["unchanged"]!)));
        Assert.Equal(("SiteA", 2, 3), ((string)sites[0]["site"]!, (int)sites[0]["apiCalls"]!, (int)sites[0]["created"]!));
        Assert.Equal((1, 1), ((int)sites[1]["created"]!, (int)sites[1]["updated"]!));

        List<JObject> log = File.ReadAllLines(Path.Combine(outDir, LoggingHandler.FileName)).Select(JObject.Parse).ToList();
        Assert.Equal(driverHandler.Requests.Select(r => r.Method.Method), log.Select(l => (string)l["method"]!));
        foreach (string file in Directory.GetFiles(outDir))
        {
            Assert.DoesNotContain(Token, File.ReadAllText(file), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task An_Airtable_error_is_recorded_with_its_response_body_and_exits_1()
    {
        var handler = new FakeHttpHandler();
        handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()))
               .Respond((HttpStatusCode)422, AirtableJson.Error("INVALID_MULTIPLE_CHOICE_OPTIONS", "Insufficient permissions to create new select option"));
        var host = new Host(handler, Environment());

        int exit = await WriteAirtableCommand.RunAsync(Args(extra: new[] { "--site", "SiteA" }), host.Value);

        Assert.Equal(1, exit);
        JObject result = JObject.Parse(File.ReadAllText(Path.Combine(Folder("out"), WriteAirtableCommand.ResultFileName)));
        Assert.Contains("Insufficient permissions", (string?)result["error"], StringComparison.Ordinal);
        Assert.Equal("SiteA", (string?)result["failedSite"]);
        JObject failed = JObject.Parse(File.ReadAllLines(Path.Combine(Folder("out"), LoggingHandler.FileName))[1]);
        Assert.Equal(422, (int)failed["status"]!);
        Assert.Contains("INVALID_MULTIPLE_CHOICE_OPTIONS", (string?)failed["responseBody"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_site_is_a_usage_error()
    {
        var host = new Host(new FakeHttpHandler(), Environment());

        await Assert.ThrowsAsync<UsageException>(() => WriteAirtableCommand.RunAsync(Args(extra: new[] { "--site", "SiteZ", "--dry-run" }), host.Value));

        Assert.False(Directory.Exists(Folder("out")));
    }

    /// <summary>
    /// Airtable for a write of SiteA then SiteB to an empty table: the first full download, then SiteA's three creates,
    /// SiteB's new Bladder and its update of the Brainstem SiteA created. Record ids are numbered per handler.
    /// </summary>
    private static void Script(FakeHttpHandler handler)
    {
        int next = 0;
        HttpResponseMessage Echo(HttpRequestMessage request)
        {
            JObject body = JObject.Parse(request.Content!.ReadAsStringAsync().Result);
            var records = ((JArray)body["records"]!).Select(r => AirtableJson.Record((string?)r["id"] ?? "recNEW" + (++next).ToString("D11", System.Globalization.CultureInfo.InvariantCulture), r["fields"]!));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(AirtableJson.Page(records)) };
        }

        handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()))
               .Respond(Echo)
               .Respond(Echo)
               .Respond(Echo);
    }
}
