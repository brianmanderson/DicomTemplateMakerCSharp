using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TemplateSync.Model;
using TemplateSync.Storage;
using Xunit;

namespace TemplateSync.Tests;

/// <summary>The frozen TG-263 snapshot: <c>TemplateSnapshots/TG263.json</c> as of commit b185504 (generated 2026-09-30).</summary>
internal static class FrozenSnapshot
{
    public const string FileName = "TG263.2026-09-30.json";

    public static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "TestData", FileName);

    public static byte[] Bytes => File.ReadAllBytes(Path);

    public static string Text => Encoding.UTF8.GetString(Bytes);

    /// <summary>The file as written: header values not recomputed, field values as Airtable sent them.</summary>
    public static JObject Raw()
    {
        using var reader = new JsonTextReader(new StringReader(Text)) { DateParseHandling = DateParseHandling.None };
        return JObject.Load(reader);
    }

    public static TableSnapshot Load() => SnapshotStore.Parse(Text);
}

/// <summary>
/// Pins every fact of docs/EVALUATION_PROTOCOL.md section 1.2 (plan step P1.3) on the frozen snapshot, so any
/// change to the fixture, or to how the program reads it, fails here first.
/// </summary>
public sealed class SnapshotFactsTests
{
    private static readonly TableSnapshot Snapshot = FrozenSnapshot.Load();

    private static readonly List<AirTableEntry> Entries = Snapshot.Records.Select(AirTableEntry.FromRecord).ToList();

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static HashSet<string> SitesOf(AirTableEntry entry) => entry.Template_Recommend.Concat(entry.Template_Consider).ToHashSet(StringComparer.Ordinal);

    private static List<string> AllSites() => Entries.SelectMany(SitesOf).Distinct(StringComparer.Ordinal).ToList();

    [Fact]
    public void Fixture_is_the_b185504_snapshot_byte_for_byte()
    {
        byte[] bytes = FrozenSnapshot.Bytes;

        // The git blob (LF), stored without line-ending conversion (.gitattributes).
        Assert.Equal(177_534, bytes.Length);
        Assert.Equal("94d0fbf4d6d7548fc5810050f04ec864c7af129766918c6511b9ffe3faa86d71", Sha256(bytes));

        // The same file as a Windows checkout (CRLF) is the 184,255-byte form the overhaul plan quotes.
        byte[] crlf = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\n", "\r\n", StringComparison.Ordinal));
        Assert.Equal(184_255, crlf.Length);
        Assert.Equal("059cd49e731fcc564ff36444ff7b9761fd43fd08ddae4448e7e383d51eac03c4", Sha256(crlf));
    }

    [Fact]
    public void Header()
    {
        JObject raw = FrozenSnapshot.Raw();

        Assert.Equal(1, (int)raw["schemaVersion"]!);
        Assert.Equal("TG263", (string?)raw["sourceName"]);
        Assert.Equal("appYl68lenTOFqTDh", (string?)raw["baseId"]);
        Assert.Equal("tblex7IPsmm8hvVEc", (string?)raw["tableId"]);
        Assert.Equal(246, (int)raw["recordCount"]!);
        Assert.Equal("2026-09-30T21:46:50.2740828+00:00", (string?)raw["generatedAtUtc"]);
        Assert.Equal(new[] { "schemaVersion", "sourceName", "baseId", "tableId", "generatedAtUtc", "recordCount", "records" }, raw.Properties().Select(p => p.Name));
        Assert.Equal(DateTimeOffset.Parse("2026-09-30T21:46:50.2740828+00:00", System.Globalization.CultureInfo.InvariantCulture), Snapshot.GeneratedAtUtc);
    }

    [Fact]
    public void Records_and_sites()
    {
        Assert.Equal(246, Snapshot.Records.Count);
        Assert.Equal(246, Snapshot.Records.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(53, AllSites().Count);
        Assert.Equal(55, Entries.Count(e => SitesOf(e).Count == 0));
        Assert.Equal(191, Entries.Count(e => SitesOf(e).Count > 0));
        Assert.Equal(1_178, Entries.Sum(e => SitesOf(e).Count));
        Assert.Equal(1_178, Entries.Sum(e => e.Template_Recommend.Count + e.Template_Consider.Count));
    }

    [Fact]
    public void Eight_sites_are_recommend_only_and_none_consider_only()
    {
        var recommend = Entries.SelectMany(e => e.Template_Recommend).ToHashSet(StringComparer.Ordinal);
        var consider = Entries.SelectMany(e => e.Template_Consider).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            new[] { "Brachy_Breast", "Brachy_Endobronch", "Brachy_Ocular", "Brachy_Prostate", "Brachy_Skin", "Thorax_BreastPBI", "Thorax_BreastWBI", "_Targets_RNI" },
            recommend.Except(consider).Order(StringComparer.Ordinal));
        Assert.Empty(consider.Except(recommend));
    }

    [Fact]
    public void Key_presence()
    {
        var counts = Snapshot.Records.SelectMany(r => r.Fields.Properties().Select(p => p.Name))
            .GroupBy(name => name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var expected = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Colors_RGB"] = 246,
            ["DVH_Color"] = 246,
            ["DVH_Style"] = 246,
            ["DVH_Type_Index"] = 246,
            ["DVH_Width"] = 246,
            ["Scheme"] = 246,
            ["SchemeCode"] = 246,
            ["Structure"] = 246,
            ["TG_263"] = 246,
            ["TG_263R"] = 246,
            ["Type"] = 246,
            ["TG_263Spanish"] = 173,
            ["TG_263French"] = 138,
            ["Template_Recommend"] = 150,
            ["Template_Consider"] = 124,
        };
        Assert.Equal(expected.OrderBy(p => p.Key, StringComparer.Ordinal), counts.OrderBy(p => p.Key, StringComparer.Ordinal));

        string[] never =
        {
            "CommonName", "FMAID", "Id", "RGB", "Record",
            "ContextGroupVersion", "MappingResource", "ContextIdentifier", "MappingResourceName", "MappingResourceUID", "ContextUID",
        };
        Assert.All(never, key => Assert.False(counts.ContainsKey(key), key));
        Assert.All(Snapshot.Records, r => Assert.Equal(r.Fields.Properties().Select(p => p.Name).Order(StringComparer.Ordinal), r.Fields.Properties().Select(p => p.Name)));
    }

    [Fact]
    public void Scheme_and_type()
    {
        Assert.All(Entries, e => Assert.Equal("SCT", e.Scheme));

        var types = Entries.GroupBy(e => e.Type!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var expected = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["ORGAN"] = 135,
            ["CTV"] = 43,
            ["PTV"] = 42,
            ["GTV"] = 14,
            ["AVOIDANCE"] = 3,
            ["IRRAD_VOLUME"] = 3,
            ["MARKER"] = 2,
            ["BRACHY_SRC_APP"] = 2,
            ["EXTERNAL"] = 1,
            ["Control"] = 1,
        };
        Assert.Equal(expected.OrderBy(p => p.Key, StringComparer.Ordinal), types.OrderBy(p => p.Key, StringComparer.Ordinal));
    }

    [Fact]
    public void Only_TS_CTVsb_is_duplicated_once_red_once_navy_blue_in_no_site()
    {
        var duplicates = Entries.GroupBy(e => e.Structure!, StringComparer.Ordinal).Where(g => g.Count() > 1).ToList();

        IGrouping<string, AirTableEntry> duplicate = Assert.Single(duplicates);
        Assert.Equal("_TS_CTVsb", duplicate.Key);
        Assert.Equal(new[] { "recVu5JdL6BYCPCGc", "recBAais5daEWac4n" }, duplicate.Select(e => e.Id));
        Assert.Equal(new[] { "Red:255,000,000", "Navy Blue:006,082,255" }, duplicate.Select(e => Assert.Single(e.Colors_RGB!)));
        Assert.All(duplicate, e => Assert.Empty(SitesOf(e)));
        Assert.DoesNotContain(Entries.GroupBy(e => e.Structure!, StringComparer.OrdinalIgnoreCase), g => g.Select(e => e.Structure).Distinct(StringComparer.Ordinal).Count() > 1);
    }

    [Fact]
    public void Name_defects()
    {
        List<string> names = Entries.Select(e => e.Structure!).ToList();

        Assert.Equal(new[] { "CTV_HighˆLN_3cm+", "CTV_MidˆLN_3cm-" }, names.Where(n => n.Any(c => c > 127)));
        Assert.All(names.Where(n => n.Any(c => c > 127)), n => Assert.Equal(new[] { 'ˆ' }, n.Where(c => c > 127)));
        Assert.Equal(new[] { "CTV1^GTV/Cervix/Uterus", "CTV2^Parametria/Vagina" }, names.Where(n => n.Contains('/') || n.Contains('^')));
        Assert.Equal(new[] { "Hi HU" }, names.Where(n => n.Any(char.IsWhiteSpace)));
        Assert.Equal(
            new[] { "Esophagus_Cervical", "PTV_ChestWall_Eval", "_TS_PTV_ChestWall", "_TS_PTV_Breast_Eval", "_TS_PTV_Scar_Eval", "CTV1^GTV/Cervix/Uterus", "CTV2^Parametria/Vagina" },
            names.Where(n => n.Length > 16));
        Assert.Equal(23, Entries.Count(e => !string.Equals(e.Structure, e.TG_263, StringComparison.Ordinal)));

        List<string> longSites = AllSites().Where(s => s.Length > 16).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(
            new[] { "AbdPelv_Paraaortic", "AbdPelv_RenalAdrenal", "AbdPelv_RetroperitonealSpace", "Brachy_Endobronch", "H&N_LX_HPX_Thyroid", "Thorax_Mediastinum_Thymus" },
            longSites);
        Assert.Equal(6, longSites.Select(s => s[..16]).Distinct(StringComparer.Ordinal).Count());
        Assert.All(AllSites(), site => Assert.True(SnapshotCheck.IsUsableSiteName(site), site));
    }

    [Fact]
    public void Codes()
    {
        var notSctIds = Entries.Where(e => SnapshotCheck.SctIdProblem(e.SchemeCode!) != null).Select(e => (e.Structure, e.SchemeCode)).ToList();

        Assert.Equal(7, notSctIds.Count);
        Assert.Equal(new[] { "Eye_Origin_L", "Eye_Origin_R", "zPTV_High", "zPTV_Mid", "zPTVopt" }, notSctIds.Where(c => c.SchemeCode == "88").Select(c => c.Structure).Order(StringComparer.Ordinal));
        Assert.Equal(new[] { "Fid", "Hi HU" }, notSctIds.Where(c => c.SchemeCode == "99").Select(c => c.Structure).Order(StringComparer.Ordinal));
        Assert.All(Entries, e => Assert.True(e.SchemeCode!.Length <= 16, e.Structure));

        var shared = Entries.GroupBy(e => e.SchemeCode!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        Assert.Equal(41, shared["228792002"]);
        Assert.Equal(40, shared["228793007"]);
        Assert.Equal(14, shared["228791009"]);
    }

    [Fact]
    public void Colours()
    {
        Assert.All(Entries, e => Assert.Single(e.Colors_RGB!));

        List<string> options = Entries.Select(e => e.Colors_RGB![0]).Distinct(StringComparer.Ordinal).ToList();
        Assert.Equal(22, options.Count);
        Assert.All(options, option =>
        {
            Assert.Matches(@"^[A-Za-z ]+:\d{3},\d{3},\d{3}$", option);
            Assert.True(SnapshotCheck.TryParseColourOption(option, out _, out _, out _, out _), option);
        });
        Assert.All(Entries, e => Assert.True(e.TryGetRgb(out _, out _, out _), e.Structure));
    }

    [Fact]
    public void Order_is_by_created_time_from_Record_1_to_Record_325()
    {
        List<string> created = Snapshot.Records.Select(r => r.CreatedTime!).ToList();

        Assert.All(created.Zip(created.Skip(1)), pair => Assert.True(string.CompareOrdinal(pair.First, pair.Second) <= 0, $"{pair.First} > {pair.Second}"));
        Assert.Equal("recw0PPvcjLPh0yC8", Snapshot.Records[0].Id);
        Assert.Equal("recy9f5hBfw1QhB44", Snapshot.Records[^1].Id);
    }
}
