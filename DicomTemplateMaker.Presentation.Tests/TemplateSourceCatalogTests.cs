using System.Net;
using DicomTemplateMaker.Presentation.Tests.Fakes;
using DicomTemplateMakerGUI.Services;
using TemplateSync.Credentials;
using TemplateSync.Storage;
using TemplateSync.Sync;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class TemplateSourceCatalogTests : IDisposable
{
    // Built at run time so no string in the source matches the real token format (keeps push protection quiet).
    private static readonly string Token = "pat" + new string('A', 14) + "." + string.Concat(Enumerable.Repeat("0f", 32));
    private readonly TestFolder folder = new();
    private readonly FakeHttpHandler handler = new();

    public void Dispose() => folder.Dispose();

    private string DataDirectory => Path.Combine(folder.Path, "data");

    private TemplateSourceCatalog Create(ITokenProtector? protector = null)
    {
        var catalog = new TemplateSourceCatalog(protector ?? new FakeProtector(), DataDirectory, folder.Sub("program"), new HttpClient(handler), _ => null);
        catalog.Initialize(folder.Sub("work"));
        return catalog;
    }

    [Fact]
    public void The_shared_source_comes_first_and_saved_tables_follow()
    {
        Create().AddConnection("Clinic", "appAAAAAAAAAAAAAA", "tblBBBBBBBBBBBBBB", Token);

        TemplateSourceCatalog catalog = Create();

        Assert.Equal(new[] { TemplateSourceCatalog.SharedSourceName, "Clinic" }, catalog.Sources.Select(s => s.Name));
        Assert.False(catalog.Sources[0].IsWritable);
        Assert.IsType<AirtableTableSource>(catalog.Sources[1].Source);
        Assert.Empty(catalog.Problems);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void Tokens_are_stored_through_the_injected_protector_and_never_in_plain_text()
    {
        var protector = new FakeProtector();
        TemplateSourceCatalog catalog = Create(protector);

        catalog.AddConnection("Clinic", "appAAAAAAAAAAAAAA", "tblBBBBBBBBBBBBBB", Token);

        string onDisk = File.ReadAllText(catalog.Connections.FilePath);
        Assert.StartsWith(DataDirectory, catalog.Connections.FilePath, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, onDisk, StringComparison.Ordinal);
        Assert.Contains("enc:", onDisk, StringComparison.Ordinal);
        Assert.True(protector.ProtectCalls > 0);
    }

    [Fact]
    public void Adding_a_name_that_exists_replaces_that_table()
    {
        TemplateSourceCatalog catalog = Create();
        catalog.AddConnection("Clinic", "appAAAAAAAAAAAAAA", "tblBBBBBBBBBBBBBB", Token);

        TemplateSourceItem replacement = catalog.AddConnection("clinic", "appCCCCCCCCCCCCCC", "tblDDDDDDDDDDDDDD", Token);

        TemplateSourceItem listed = Assert.Single(catalog.Sources, s => s.IsWritable);
        Assert.Same(replacement, listed);
        var source = Assert.IsType<AirtableTableSource>(listed.Source);
        Assert.Equal("appCCCCCCCCCCCCCC", source.Definition.BaseId);
        Assert.Single(catalog.Connections.Load());
    }

    [Fact]
    public void Removing_a_table_forgets_its_token_and_cached_copy_but_the_shared_source_stays()
    {
        TemplateSourceCatalog catalog = Create();
        TemplateSourceItem clinic = catalog.AddConnection("Clinic", "appAAAAAAAAAAAAAA", "tblBBBBBBBBBBBBBB", Token);
        string cacheKey = new AirtableSourceDefinition("Clinic", "appAAAAAAAAAAAAAA", "tblBBBBBBBBBBBBBB").CacheKey;
        catalog.Cache.Save(cacheKey, new TableSnapshot());
        Assert.True(File.Exists(catalog.Cache.PathFor(cacheKey)));

        catalog.RemoveConnection(clinic);
        catalog.RemoveConnection(catalog.Sources[0]);

        Assert.Equal(new[] { TemplateSourceCatalog.SharedSourceName }, catalog.Sources.Select(s => s.Name));
        Assert.Empty(catalog.Connections.Load());
        Assert.False(File.Exists(catalog.Cache.PathFor(cacheKey)));
    }

    [Fact]
    public void A_token_this_account_cannot_decrypt_keeps_the_table_listed_so_it_can_be_removed()
    {
        Create().AddConnection("Clinic", "appAAAAAAAAAAAAAA", "tblBBBBBBBBBBBBBB", Token);

        TemplateSourceCatalog catalog = Create(new FakeProtector { CannotDecrypt = true });

        Assert.IsType<UnavailableAirtableSource>(catalog.Sources[1].Source);
        Assert.Contains(catalog.Problems, p => p.Contains("'Clinic' cannot be decrypted", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_connection_test_reads_one_record_with_the_token_and_returns_null_when_it_works()
    {
        TemplateSourceCatalog catalog = Create();
        handler.Respond(HttpStatusCode.OK, "{\"records\":[]}");

        string? problem = await catalog.TestConnectionAsync(" appEEEEEEEEEEEEEE ", " tblFFFFFFFFFFFFFF ", Token, CancellationToken.None);

        Assert.Null(problem);
        RecordedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/v0/appEEEEEEEEEEEEEE/tblFFFFFFFFFFFFFF", request.Uri.AbsolutePath);
        Assert.Contains("pageSize=1", request.Uri.Query, StringComparison.Ordinal);
        Assert.Equal("Bearer " + Token, request.Authorization);
    }

    [Fact]
    public async Task The_connection_test_returns_the_problem_after_a_single_request()
    {
        TemplateSourceCatalog catalog = Create();
        handler.Respond(HttpStatusCode.ServiceUnavailable, "{\"error\":{\"type\":\"SERVICE_UNAVAILABLE\",\"message\":\"Try later\"}}");

        string? problem = await catalog.TestConnectionAsync("appGGGGGGGGGGGGGG", "tblHHHHHHHHHHHHHH", Token, CancellationToken.None);

        Assert.NotNull(problem);
        Assert.Contains("HTTP 503", problem, StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }
}
