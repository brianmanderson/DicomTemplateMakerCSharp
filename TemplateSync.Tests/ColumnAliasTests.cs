using System.Net;
using Newtonsoft.Json.Linq;
using TemplateSync.Airtable;
using TemplateSync.Model;
using TemplateSync.Sync;
using TemplateSync.Tests.Fakes;
using Xunit;

namespace TemplateSync.Tests;

/// <summary>
/// Plan step P2.8: the reversed-language columns are TG_263RSpanish / TG_263RFrench in one real TG-263 table (A, the
/// public source) and TG_263SpanishR / TG_263FrenchR in the other (B). Both are read; a write uses the table's spelling,
/// learned from the schema when the token may read it and from Airtable's unknown-field error otherwise.
/// </summary>
public sealed class ColumnAliasTests : IDisposable
{
    private const string BaseId = "appzWlVKRp9TrrTUJ";
    private const string TableId = "tblltR3aTxlJUwaGa";
    private static int _nextId;
    private readonly TempDirectory _temp = new();
    private readonly FakeHttpHandler _handler = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));

    public void Dispose() => _temp.Dispose();

    /// <summary>Table A as the metadata API describes it: narrow schema, R-first spelling, CommonName, no RGB or MappingResourceUID.</summary>
    private static readonly string[] TableAColumns =
    {
        "Record", "CommonName", "Type", "Structure", "Colors_RGB", "FMAID", "SchemeCode", "TG_263", "TG_263R", "TG_263French",
        "TG_263RFrench", "TG_263Spanish", "TG_263RSpanish", "Scheme", "Template_Recommend", "Template_Consider", "DVH_Color",
        "DVH_Style", "DVH_Width", "DVH_Type_Index", "DVH_ContourStyle", "Id", "ContextGroupVersion", "MappingResource",
        "ContextIdentifier", "MappingResourceName", "ContextUID",
    };

    /// <summary>Table B: the property-name spelling, MappingResourceUID and RGB, no CommonName; Scheme and SchemeCode are single selects.</summary>
    private static readonly string[] TableBColumns =
    {
        "Structure", "Type", "Colors_RGB", "RGB", "Template_Recommend", "Template_Consider", "SchemeCode", "Scheme",
        "ContextGroupVersion", "MappingResource", "ContextIdentifier", "MappingResourceName", "MappingResourceUID", "ContextUID",
        "TG_263", "TG_263R", "TG_263Spanish", "TG_263SpanishR", "TG_263French", "TG_263FrenchR",
        "DVH_Color", "DVH_Style", "DVH_Width", "DVH_Type_Index", "DVH_ContourStyle",
    };

    private static readonly HashSet<string> SingleSelects = new(StringComparer.Ordinal) { "Structure", "Type", "Scheme", "SchemeCode", "TG_263", "TG_263R" };

    private static string Schema(string[] columns) => new JObject
    {
        ["tables"] = new JArray(
            new JObject { ["id"] = "tblOTHER000000000", ["name"] = "Other", ["fields"] = new JArray() },
            new JObject
            {
                ["id"] = TableId,
                ["name"] = "Structure Templates",
                ["primaryFieldId"] = "fld00000000000000",
                ["fields"] = new JArray(columns.Select((c, i) => (object)new JObject
                {
                    ["id"] = "fld" + i.ToString("D14", System.Globalization.CultureInfo.InvariantCulture),
                    ["name"] = c,
                    ["type"] = SingleSelects.Contains(c) || c.StartsWith("TG_263", StringComparison.Ordinal) ? "singleSelect"
                        : c is "Colors_RGB" or "Template_Recommend" or "Template_Consider" ? "multipleSelects" : "singleLineText",
                }).ToArray()),
            }),
    }.ToString();

    private AirtableTableSource CreateSource()
    {
        var api = new AirtableHttpClient(new HttpClient(_handler), "patTEST.token", _clock,
            new AirtableClientOptions { MaxJitter = TimeSpan.Zero }, new RequestThrottle(TimeSpan.Zero));
        return new AirtableTableSource(new AirtableSourceDefinition("Clinic", BaseId, TableId), api, new Storage.SnapshotStore(_temp.Path), new SyncSettings(), _clock);
    }

    private static LocalRoi Brain(string structure = "Brain", bool french = true) => new(new AirTableEntry
    {
        Structure = structure,
        CommonName = "Brain",
        Type = "ORGAN",
        RGB = "1,2,3",
        SchemeCode = "78277",
        Scheme = "FMA",
        TG_263 = structure,
        TG_263R = structure,
        TG_263Spanish = "Cerebro",
        TG_263SpanishR = "Cerebro_R",
        TG_263French = french ? "Cerveau" : null,
        TG_263FrenchR = french ? "Cerveau_R" : null,
    }, include: true);

    private static JObject Fields(RecordedRequest request) => (JObject)JObject.Parse(request.Body!)["records"]![0]!["fields"]!;

    private static HttpResponseMessage Echo(HttpRequestMessage request)
    {
        JObject body = JObject.Parse(request.Content!.ReadAsStringAsync().Result);
        var records = ((JArray)body["records"]!).Select(r => AirtableJson.Record((string?)r["id"] ?? $"recA{Interlocked.Increment(ref _nextId):D13}", r["fields"]!));
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(AirtableJson.Page(records)) };
    }

    private static string UnknownField(string name) => AirtableJson.Error("UNKNOWN_FIELD_NAME", $"Unknown field name: \"{name}\"");

    [Theory]
    [InlineData("TG_263SpanishR", "TG_263FrenchR")]
    [InlineData("TG_263RSpanish", "TG_263RFrench")]
    public void A_record_is_read_under_either_spelling(string spanish, string french)
    {
        var record = new AirtableRecord { Id = "rec1", Fields = new JObject { ["Structure"] = "Brain", [spanish] = "Cerebro_R", [french] = "Cerveau_R" } };

        AirTableEntry entry = AirTableEntry.FromRecord(record);

        Assert.Equal("Cerebro_R", entry.TG_263SpanishR);
        Assert.Equal("Cerveau_R", entry.TG_263FrenchR);
        Assert.Equal("Cerebro_R", entry.GetField(nameof(AirTableEntry.TG_263SpanishR)));
    }

    [Fact]
    public void Property_names_stay_the_mapped_column_list()
    {
        Assert.Equal(26, AirTableEntry.FieldNames.Count);
        Assert.Contains("TG_263SpanishR", AirTableEntry.FieldNames);
        Assert.DoesNotContain("TG_263RSpanish", AirTableEntry.FieldNames);
        Assert.Equal("TG_263SpanishR", AirTableEntry.PropertyNameOf("TG_263RSpanish"));
        Assert.Equal("TG_263FrenchR", AirTableEntry.PropertyNameOf("TG_263RFrench"));
        Assert.Equal("Structure", AirTableEntry.PropertyNameOf("Structure"));
    }

    [Fact]
    public void The_planner_compares_against_either_spelling_and_writes_the_tables()
    {
        var spelling = new Dictionary<string, string> { ["TG_263SpanishR"] = "TG_263RSpanish" };
        var remote = new AirtableRecord { Id = "rec1", Fields = new JObject { ["Structure"] = "Brain", ["TG_263RSpanish"] = "old", ["Template_Recommend"] = new JArray("SiteA") } };
        var roi = new LocalRoi(new AirTableEntry { Structure = "Brain", TG_263SpanishR = "new", Scheme = null, ContextGroupVersion = null, MappingResource = null, ContextIdentifier = null, MappingResourceName = null, MappingResourceUID = null, ContextUID = null }, true);

        WritePlan changed = WritePlanner.Plan("SiteA", new[] { roi }, new[] { remote }, null, spelling);
        remote.Fields["TG_263RSpanish"] = "new";
        WritePlan same = WritePlanner.Plan("SiteA", new[] { roi }, new[] { remote });
        WritePlan created = WritePlanner.Plan("SiteA", new[] { roi }, Array.Empty<AirtableRecord>(), null, spelling);

        Assert.Equal(new JObject { ["TG_263RSpanish"] = "new" }, Assert.Single(changed.Updates).Fields, JToken.EqualityComparer);
        Assert.Equal(1, same.Unchanged);
        Assert.Equal("new", (string?)Assert.Single(created.Creates)["TG_263RSpanish"]);
        Assert.False(Assert.Single(created.Creates).ContainsKey("TG_263SpanishR"));
    }

    [Fact]
    public async Task The_schema_call_reads_every_table_of_the_base()
    {
        _handler.Respond(HttpStatusCode.OK, Schema(TableAColumns));
        var api = new AirtableHttpClient(new HttpClient(_handler), "patTEST.token", _clock, new AirtableClientOptions { MaxJitter = TimeSpan.Zero }, new RequestThrottle(TimeSpan.Zero));

        IReadOnlyList<AirtableTableSchema> tables = await api.ListTablesAsync(BaseId, CancellationToken.None);

        RecordedRequest sent = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal("https://api.airtable.com/v0/meta/bases/" + BaseId + "/tables", sent.Uri.ToString());
        Assert.Equal(2, tables.Count);
        Assert.Equal(TableAColumns, tables[1].Fields.Select(f => f.Name));
        Assert.Equal("singleSelect", tables[1].Fields.Single(f => f.Name == "Scheme").Type);
    }

    [Fact]
    public async Task Table_A_gets_the_R_first_spelling_and_no_columns_it_lacks()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));
        AirtableTableSource source = CreateSource();
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()))
                .Respond(HttpStatusCode.OK, Schema(TableAColumns))
                .Respond(Echo);

        WriteResult result = await source.WriteTemplateAsync("SiteA", new[] { Brain() }, null, CancellationToken.None);

        Assert.Equal(new[] { "GET", "GET", "GET", "POST" }, _handler.Requests.Select(r => r.Method.Method));
        Assert.Equal("/v0/meta/bases/" + BaseId + "/tables", _handler.Requests[2].Uri.AbsolutePath);
        JObject fields = Fields(_handler.Requests[3]);
        Assert.Equal("Cerebro_R", (string?)fields["TG_263RSpanish"]);
        Assert.Equal("Cerveau_R", (string?)fields["TG_263RFrench"]);
        Assert.Equal("Brain", (string?)fields["CommonName"]);
        foreach (string absent in new[] { "TG_263SpanishR", "TG_263FrenchR", "MappingResourceUID", "RGB" })
        {
            Assert.False(fields.ContainsKey(absent), absent);
        }

        Assert.True((bool)JObject.Parse(_handler.Requests[3].Body!)["typecast"]!);
        Assert.Equal(1, result.Created);
        Assert.Equal(3, result.ApiCalls); // refresh + schema + one create
        Assert.Contains(result.Warnings, w => w.Contains("'MappingResourceUID'", StringComparison.Ordinal));
        Assert.Equal(new[] { "MappingResourceUID", "RGB" }, source.Current!.MissingFields);
        Assert.Equal(new Dictionary<string, string> { ["TG_263SpanishR"] = "TG_263RSpanish", ["TG_263FrenchR"] = "TG_263RFrench" }, source.Current.ColumnNames);
        Assert.Equal("Cerebro_R", AirTableEntry.FromRecord(source.Current.Records.Single()).TG_263SpanishR);

        // The next refresh asks for the table's spelling, so the field filter keeps working.
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        List<string> requested = _handler.Requests[4].Uri.QueryValues("fields[]");
        Assert.Contains("TG_263RSpanish", requested);
        Assert.DoesNotContain("TG_263SpanishR", requested);
        Assert.DoesNotContain("MappingResourceUID", requested);
    }

    [Fact]
    public async Task Table_B_gets_the_property_name_spelling_and_no_CommonName()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));
        AirtableTableSource source = CreateSource();
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()))
                .Respond(HttpStatusCode.OK, Schema(TableBColumns))
                .Respond(Echo);

        WriteResult result = await source.WriteTemplateAsync("SiteA", new[] { Brain() }, null, CancellationToken.None);

        JObject body = JObject.Parse(_handler.Requests[3].Body!);
        JObject fields = Fields(_handler.Requests[3]);
        Assert.Equal("Cerebro_R", (string?)fields["TG_263SpanishR"]);
        Assert.Equal("Cerveau_R", (string?)fields["TG_263FrenchR"]);
        Assert.Equal(OntologyDefaults.MappingResourceUID, (string?)fields["MappingResourceUID"]);
        Assert.Equal("FMA", (string?)fields["Scheme"]);
        Assert.Equal("78277", (string?)fields["SchemeCode"]);
        Assert.False(fields.ContainsKey("CommonName"));
        Assert.False(fields.ContainsKey("TG_263RSpanish"));
        // Scheme and SchemeCode are single selects in table B: a new option needs typecast, which every write sets.
        Assert.True((bool)body["typecast"]!);
        Assert.Equal(new[] { "CommonName" }, source.Current!.MissingFields);
        Assert.Equal(new Dictionary<string, string> { ["TG_263SpanishR"] = "TG_263SpanishR", ["TG_263FrenchR"] = "TG_263FrenchR" }, source.Current.ColumnNames);
        Assert.Equal(1, result.Created);
    }

    [Fact]
    public async Task Without_schema_access_the_spelling_is_learned_from_the_unknown_field_error_and_kept()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));
        AirtableTableSource source = CreateSource();
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()))
                .Respond(HttpStatusCode.Forbidden, AirtableJson.Error("INVALID_PERMISSIONS_OR_MODEL_NOT_FOUND", "Invalid permissions"))
                .Respond((HttpStatusCode)422, UnknownField("TG_263SpanishR"))
                .Respond((HttpStatusCode)422, UnknownField("TG_263FrenchR"))
                .Respond(Echo);

        WriteResult result = await source.WriteTemplateAsync("SiteA", new[] { Brain() }, null, CancellationToken.None);

        Assert.Equal(new[] { "GET", "GET", "GET", "POST", "POST", "POST" }, _handler.Requests.Select(r => r.Method.Method));
        Assert.True(Fields(_handler.Requests[3]).ContainsKey("TG_263SpanishR"));
        JObject accepted = Fields(_handler.Requests[5]);
        Assert.Equal("Cerebro_R", (string?)accepted["TG_263RSpanish"]);
        Assert.Equal("Cerveau_R", (string?)accepted["TG_263RFrench"]);
        Assert.False(accepted.ContainsKey("TG_263SpanishR"));
        Assert.Equal(1, result.Created);
        Assert.Equal(5, result.ApiCalls);
        Assert.Empty(result.Warnings);
        Assert.Null(source.Current!.MissingFields);

        // A new session reads the spelling from the cache: no schema call, no unknown-field round trip.
        AirtableTableSource next = CreateSource();
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>())).Respond(Echo);
        WriteResult second = await next.WriteTemplateAsync("SiteA", new[] { Brain("Brainstem") }, null, CancellationToken.None);

        Assert.Equal(8, _handler.Requests.Count);
        Assert.Equal(2, second.ApiCalls);
        Assert.True(Fields(_handler.Requests[7]).ContainsKey("TG_263RSpanish"));
    }

    [Fact]
    public async Task A_column_missing_under_both_spellings_is_dropped_with_a_warning()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));
        AirtableTableSource source = CreateSource();
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()))
                .Respond(HttpStatusCode.Forbidden, AirtableJson.Error("INVALID_PERMISSIONS_OR_MODEL_NOT_FOUND", "Invalid permissions"))
                .Respond((HttpStatusCode)422, UnknownField("TG_263SpanishR"))
                .Respond((HttpStatusCode)422, UnknownField("TG_263RSpanish"))
                .Respond(Echo);

        WriteResult result = await source.WriteTemplateAsync("SiteA", new[] { Brain(french: false) }, null, CancellationToken.None);

        JObject accepted = Fields(_handler.Requests[5]);
        Assert.False(accepted.ContainsKey("TG_263SpanishR"));
        Assert.False(accepted.ContainsKey("TG_263RSpanish"));
        Assert.Equal(1, result.Created);
        Assert.Contains(result.Warnings, w => w.Contains("'TG_263SpanishR' (or 'TG_263RSpanish')", StringComparison.Ordinal));
        Assert.Equal(new[] { "TG_263SpanishR" }, source.Current!.MissingFields);
        Assert.Null(source.Current.ColumnNames);
    }

    [Fact]
    public async Task A_write_without_reversed_language_values_makes_no_schema_call()
    {
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>()));
        AirtableTableSource source = CreateSource();
        await source.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
        _handler.Respond(HttpStatusCode.OK, AirtableJson.Page(Array.Empty<JObject>())).Respond(Echo);

        WriteResult result = await source.WriteTemplateAsync("SiteA", new[] { new LocalRoi(new AirTableEntry { Structure = "Brain" }, true) }, null, CancellationToken.None);

        Assert.Equal(new[] { "GET", "GET", "POST" }, _handler.Requests.Select(r => r.Method.Method));
        Assert.Equal(2, result.ApiCalls);
    }
}
