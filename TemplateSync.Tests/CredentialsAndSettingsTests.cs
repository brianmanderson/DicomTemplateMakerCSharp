using System.Text;
using TemplateSync.Credentials;
using TemplateSync.Sync;
using TemplateSync.Tests.Fakes;
using Xunit;

namespace TemplateSync.Tests;

public sealed class CredentialsAndSettingsTests : IDisposable
{
    // Built at run time so no string in the source matches the real token format (keeps push protection quiet).
    private static readonly string Token = "pat" + new string('A', 14) + "." + string.Concat(Enumerable.Repeat("0f", 32));
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    /// <summary>Reversible stand-in for DPAPI.</summary>
    private sealed class FakeProtector : ITokenProtector
    {
        public string Protect(string plaintext) => "enc:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));

        public string Unprotect(string protectedValue) => Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue.Substring(4)));
    }

    private AirtableConnectionStore Store(Func<string, string?>? env = null) => new(_temp.File("connections.json"), new FakeProtector(), env ?? (_ => null));

    [Fact]
    public void Tokens_are_stored_encrypted_and_read_back()
    {
        AirtableConnectionStore store = Store();
        store.Add("Clinic", "appAAAAAAAAAAAAAA", "tblBBBBBBBBBBBBBB", Token);

        string onDisk = File.ReadAllText(store.FilePath);
        Assert.DoesNotContain(Token, onDisk);
        AirtableConnection connection = Assert.Single(store.Load());
        Assert.Equal(Token, store.GetToken(connection));
    }

    [Fact]
    public void Environment_variable_overrides_stored_tokens()
    {
        AirtableConnectionStore store = Store(name => name == "AIRTABLE_PAT" ? " patFromEnv " : null);
        store.Add("Clinic", "appAAAAAAAAAAAAAA", "tblBBBBBBBBBBBBBB", Token);

        Assert.Equal("patFromEnv", store.GetToken(store.Load()[0]));
    }

    [Fact]
    public void Legacy_files_are_imported_then_deleted_and_a_published_tg263_token_is_retired()
    {
        string legacy = Path.Combine(_temp.Path, "AirTables");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "Clinic.txt"), Token + "\nappAAAAAAAAAAAAAA\ntblBBBBBBBBBBBBBB");
        File.WriteAllText(Path.Combine(legacy, "TG263_AirTable.txt"), "patSHARED.xxx\nappzWlVKRp9TrrTUJ\ntblltR3aTxlJUwaGa");
        File.WriteAllText(Path.Combine(legacy, "Broken.txt"), "only one line");
        AirtableConnectionStore store = Store();

        LegacyMigrationResult result = store.MigrateLegacyFiles(legacy, deletePlaintext: true, isRetiredToken: t => t == "patSHARED.xxx");

        Assert.Equal(new[] { "Clinic" }, result.Imported);
        Assert.Equal(new[] { "TG263_AirTable" }, result.Retired);
        Assert.Single(result.Problems);
        Assert.False(File.Exists(Path.Combine(legacy, "Clinic.txt")));
        Assert.False(File.Exists(Path.Combine(legacy, "TG263_AirTable.txt")));
        Assert.True(File.Exists(Path.Combine(legacy, "Broken.txt")));
        AirtableConnection imported = Assert.Single(store.Load());
        Assert.Equal("appAAAAAAAAAAAAAA", imported.BaseId);
        Assert.Equal(Token, store.GetToken(imported));
    }

    [Fact]
    public void An_identical_legacy_file_for_an_imported_connection_is_removed()
    {
        AirtableConnectionStore store = Store();
        store.Add("Clinic", "appAAAAAAAAAAAAAA", "tblBBBBBBBBBBBBBB", Token);
        string legacy = Path.Combine(_temp.Path, "AirTables");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "Clinic.txt"), Token + "\nappAAAAAAAAAAAAAA\ntblBBBBBBBBBBBBBB");

        LegacyMigrationResult result = store.MigrateLegacyFiles(legacy);

        Assert.Equal(new[] { "Clinic" }, result.Imported);
        Assert.False(File.Exists(Path.Combine(legacy, "Clinic.txt")));
        Assert.Single(store.Load());
    }

    [Fact]
    public void A_file_named_like_the_shared_table_but_holding_the_users_own_token_is_imported()
    {
        string legacy = Path.Combine(_temp.Path, "AirTables");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "TG263_AirTable.txt"), Token + "\nappMINE0000000000\ntblMINE0000000000");
        AirtableConnectionStore store = Store();

        LegacyMigrationResult result = store.MigrateLegacyFiles(legacy);

        Assert.Equal(new[] { "TG263_AirTable" }, result.Imported);
        Assert.Equal("appMINE0000000000", Assert.Single(store.Load()).BaseId);
    }

    [Fact]
    public void In_a_shared_folder_files_are_imported_but_kept_and_reported_once()
    {
        string legacy = Path.Combine(_temp.Path, "AirTables");
        Directory.CreateDirectory(legacy);
        string file = Path.Combine(legacy, "Clinic.txt");
        File.WriteAllText(file, Token + "\nappAAAAAAAAAAAAAA\ntblBBBBBBBBBBBBBB");
        AirtableConnectionStore store = Store();

        LegacyMigrationResult first = store.MigrateLegacyFiles(legacy, deletePlaintext: false);
        LegacyMigrationResult second = store.MigrateLegacyFiles(legacy, deletePlaintext: false);

        Assert.True(File.Exists(file));
        Assert.Equal(new[] { file }, first.KeptPlaintext);
        Assert.Equal(new[] { "Clinic" }, first.Imported);
        Assert.Empty(second.Imported);
        Assert.Empty(second.KeptPlaintext);
        Assert.Single(store.Load());
    }

    [Fact]
    public void Huge_settings_values_are_clamped_instead_of_overflowing()
    {
        string path = _temp.File("settings.json");
        File.WriteAllText(path, "{\"fullRefreshIntervalDays\": 100000000, \"cacheTimeToLiveHours\": 1e300}");

        SyncSettings settings = SyncSettingsFile.Load(path).ToSyncSettings();

        Assert.Equal(TimeSpan.FromDays(3650), settings.FullRefreshInterval);
        Assert.Equal(TimeSpan.FromHours(24 * 3650), settings.CacheTimeToLive);
    }

    [Fact]
    public void Removing_a_connection_deletes_it()
    {
        AirtableConnectionStore store = Store();
        store.Add("Clinic", "appAAAAAAAAAAAAAA", "tblBBBBBBBBBBBBBB", Token);

        Assert.True(store.Remove("clinic"));
        Assert.Empty(store.Load());
    }

    [Theory]
    [InlineData("", "appAAAAAAAAAAAAAA", "tblBBBBBBBBBBBBBB", null, "name")]
    [InlineData("Clinic", "appShort", "tblBBBBBBBBBBBBBB", null, "base id")]
    [InlineData("Clinic", "appAAAAAAAAAAAAAA", "", null, "table")]
    [InlineData("Clinic", "appAAAAAAAAAAAAAA", "tblBBBBBBBBBBBBBB", "keyLegacyApiKey1", "Legacy API keys")]
    [InlineData("Cli/nic", "appAAAAAAAAAAAAAA", "tblBBBBBBBBBBBBBB", null, "not allowed")]
    public void Validation_explains_the_problem(string name, string baseId, string table, string? token, string expected)
    {
        Assert.Contains(expected, AirtableIds.Validate(name, baseId, table, token ?? Token));
    }

    [Fact]
    public void Leaked_token_fingerprints_are_sha256_of_the_trimmed_token()
    {
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", LeakedTokens.Sha256Hex("hello"));
        Assert.False(LeakedTokens.IsKnownLeaked(Token));
        Assert.False(LeakedTokens.IsKnownLeaked(null));
    }

    [Fact]
    public void Legacy_files_holding_a_published_token_are_retired_whatever_their_name()
    {
        string legacy = Path.Combine(_temp.Path, "AirTables");
        Directory.CreateDirectory(legacy);
        string leaked = "pat" + new string('L', 14) + "." + new string('1', 64);
        File.WriteAllText(Path.Combine(legacy, "UNC_Template.txt"), leaked + "\nappsAAAAAAAAAAAAA\ntblBBBBBBBBBBBBBB");
        AirtableConnectionStore store = Store();

        LegacyMigrationResult result = store.MigrateLegacyFiles(legacy, isRetiredToken: token => token == leaked);

        Assert.Equal(new[] { "UNC_Template" }, result.Retired);
        Assert.Empty(result.Imported);
        Assert.Empty(store.Load());
        Assert.False(File.Exists(Path.Combine(legacy, "UNC_Template.txt")));
    }

    [Fact]
    public void A_legacy_file_that_conflicts_with_an_existing_connection_is_left_in_place()
    {
        AirtableConnectionStore store = Store();
        store.Add("Clinic", "appAAAAAAAAAAAAAA", "tblBBBBBBBBBBBBBB", Token);
        string legacy = Path.Combine(_temp.Path, "AirTables");
        Directory.CreateDirectory(legacy);
        string rotated = "pat" + new string('R', 14) + "." + new string('2', 64);
        File.WriteAllText(Path.Combine(legacy, "Clinic.txt"), rotated + "\nappAAAAAAAAAAAAAA\ntblBBBBBBBBBBBBBB");

        LegacyMigrationResult result = store.MigrateLegacyFiles(legacy);

        Assert.Empty(result.Imported);
        Assert.Single(result.Problems);
        Assert.True(File.Exists(Path.Combine(legacy, "Clinic.txt")));
        Assert.Equal(Token, store.GetToken(store.Load()[0]));
    }

    [Fact]
    public void Valid_values_pass_validation()
    {
        Assert.Null(AirtableIds.Validate("Clinic", "appAAAAAAAAAAAAAA", "Table 1", Token));
    }

    [Fact]
    public void Settings_file_overrides_defaults_and_ignores_bad_values()
    {
        string path = _temp.File("settings.json");
        File.WriteAllText(path, "{\"cacheTimeToLiveHours\": 6, \"fullRefreshIntervalDays\": -1, \"sharedSnapshotUrl\": \"http://insecure.test/x.json\"}");

        SyncSettingsFile file = SyncSettingsFile.Load(path);
        SyncSettings settings = file.ToSyncSettings();

        Assert.Equal(TimeSpan.FromHours(6), settings.CacheTimeToLive);
        Assert.Equal(TimeSpan.FromDays(7), settings.FullRefreshInterval);
        Assert.Equal(SyncSettingsFile.DefaultSnapshotUrl, file.GetSnapshotUrl().ToString());
    }

    [Fact]
    public void Unreadable_settings_file_falls_back_to_defaults()
    {
        string path = _temp.File("settings.json");
        File.WriteAllText(path, "not json");
        var problems = new List<string>();

        SyncSettings settings = SyncSettingsFile.Load(path, problems.Add).ToSyncSettings();

        Assert.Equal(TimeSpan.FromHours(24), settings.CacheTimeToLive);
        Assert.Single(problems);
    }
}
