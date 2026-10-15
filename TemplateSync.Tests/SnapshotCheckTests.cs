using Newtonsoft.Json.Linq;
using TemplateSync.Storage;
using TemplateSync.Tests.Fakes;
using Xunit;

namespace TemplateSync.Tests;

/// <summary><c>TemplateSnapshotTool check</c> (plan step P1.4, docs/EVALUATION_PROTOCOL.md section 1.4).</summary>
public sealed class SnapshotCheckTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static string Edited(Action<JObject> edit)
    {
        JObject raw = FrozenSnapshot.Raw();
        edit(raw);
        return raw.ToString();
    }

    private static JObject Fields(JObject raw, int index) => (JObject)raw["records"]![index]!["fields"]!;

    private static List<string> Failed(SnapshotCheckReport report, SnapshotCheckKind kind)
        => report.Rules.Where(r => r.Kind == kind && r.Failed).Select(r => r.Id).ToList();

    [Fact]
    public void Frozen_snapshot_passes_every_gate_with_the_known_warnings()
    {
        SnapshotCheckReport report = SnapshotCheck.Check(FrozenSnapshot.Text);

        Assert.True(report.Passed);
        Assert.Empty(Failed(report, SnapshotCheckKind.Gate));
        Assert.Empty(report.Rule(SnapshotCheck.SiteNamesRule).Findings);
        Assert.Contains("`_TS_CTVsb` appears 2 times: recVu5JdL6BYCPCGc, recBAais5daEWac4n", Assert.Single(report.Rule(SnapshotCheck.UniqueStructureRule).Findings));
        Assert.Contains("Type `Control`", Assert.Single(report.Rule(SnapshotCheck.InterpretedTypeRule).Findings));

        List<string> codes = report.Rule(SnapshotCheck.SchemeCodeRule).Findings;
        Assert.Equal(7, codes.Count);
        Assert.Equal(5, codes.Count(f => f.Contains("SCT code `88`", StringComparison.Ordinal)));
        Assert.Equal(2, codes.Count(f => f.Contains("SCT code `99`", StringComparison.Ordinal)));
    }

    [Fact]
    public void Check_command_writes_the_summary_and_exits_0_on_the_frozen_snapshot()
    {
        string summary = _temp.File("out/summary.md");
        var output = new StringWriter();

        int exit = SnapshotCheck.RunCommand(FrozenSnapshot.Path, summary, output);

        Assert.Equal(0, exit);
        string markdown = File.ReadAllText(summary);
        Assert.Equal(output.ToString(), markdown);
        Assert.StartsWith("## TG-263 snapshot check: passed", markdown, StringComparison.Ordinal);
        Assert.Contains("SHA-256 `94d0fbf4d6d7548fc5810050f04ec864c7af129766918c6511b9ffe3faa86d71`", markdown);
        Assert.Contains("246 records (recordCount 246)", markdown);
        Assert.Contains("| 0 warnings |", markdown);
        Assert.Contains("| 7 warnings |", markdown);
        Assert.DoesNotContain("FAIL", markdown);
    }

    [Fact]
    public void A_snapshot_trimmed_to_120_records_fails_with_exit_5()
    {
        string trimmed = Edited(raw =>
        {
            var records = (JArray)raw["records"]!;
            while (records.Count > 120)
            {
                records.RemoveAt(records.Count - 1);
            }

            raw["recordCount"] = 120;
        });
        string path = _temp.File("TG263.json");
        File.WriteAllText(path, trimmed);
        string summary = _temp.File("summary.md");

        int exit = SnapshotCheck.RunCommand(path, summary, new StringWriter());

        Assert.Equal(SnapshotCheck.FailedGateExitCode, exit);
        Assert.Equal(5, exit);
        SnapshotCheckReport report = SnapshotCheck.Check(trimmed);
        Assert.Equal(new[] { SnapshotCheck.RecordCountRule }, Failed(report, SnapshotCheckKind.Gate));
        Assert.Equal("120 records; at least 240 are required.", Assert.Single(report.Rule(SnapshotCheck.RecordCountRule).Findings));
        string markdown = File.ReadAllText(summary);
        Assert.StartsWith("## TG-263 snapshot check: FAILED", markdown, StringComparison.Ordinal);
        Assert.Contains("120 records; at least 240 are required.", markdown);
    }

    [Fact]
    public void A_record_count_that_disagrees_with_the_records_fails()
    {
        SnapshotCheckReport report = SnapshotCheck.Check(Edited(raw => raw["recordCount"] = 247));

        Assert.False(report.Passed);
        Assert.Equal("recordCount is 247 but the file holds 246 records.", Assert.Single(report.Rule(SnapshotCheck.RecordCountRule).Findings));
    }

    [Theory]
    [InlineData("Structure")]
    [InlineData("SchemeCode")]
    [InlineData("Scheme")]
    public void A_record_without_a_required_field_fails(string field)
    {
        SnapshotCheckReport blank = SnapshotCheck.Check(Edited(raw => Fields(raw, 3)[field] = "  "));
        SnapshotCheckReport missing = SnapshotCheck.Check(Edited(raw => Fields(raw, 3).Remove(field)));

        Assert.Equal(new[] { SnapshotCheck.RequiredFieldsRule }, Failed(blank, SnapshotCheckKind.Gate));
        Assert.Equal(new[] { SnapshotCheck.RequiredFieldsRule }, Failed(missing, SnapshotCheckKind.Gate));
        Assert.EndsWith($"no {field}.", Assert.Single(missing.Rule(SnapshotCheck.RequiredFieldsRule).Findings), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Red:256,000,000")]
    [InlineData("Red:-1,0,0")]
    [InlineData("Red 255,0,0")]
    [InlineData(":255,0,0")]
    [InlineData("Red:255,0")]
    [InlineData("Red:255,0,0,0")]
    [InlineData("Red:255,0,x")]
    public void An_unparseable_colour_option_fails(string option)
    {
        SnapshotCheckReport report = SnapshotCheck.Check(Edited(raw => Fields(raw, 0)["Colors_RGB"] = new JArray("Lime:000,255,000", option)));

        Assert.Equal(new[] { SnapshotCheck.ColoursRule }, Failed(report, SnapshotCheckKind.Gate));
        Assert.Contains($"`{option}`", Assert.Single(report.Rule(SnapshotCheck.ColoursRule).Findings));
    }

    [Theory]
    [InlineData("Red:255,000,000", "Red", 255, 0, 0)]
    [InlineData("Navy Blue:006,082,255", "Navy Blue", 6, 82, 255)]
    [InlineData("Auto: 1, 2 ,3", "Auto", 1, 2, 3)]
    public void Colour_options_parse(string option, string name, byte r, byte g, byte b)
    {
        Assert.True(SnapshotCheck.TryParseColourOption(option, out string parsedName, out byte pr, out byte pg, out byte pb));
        Assert.Equal((name, r, g, b), (parsedName, pr, pg, pb));
    }

    [Fact]
    public void Unparseable_json_fails_and_a_missing_file_is_a_usage_error()
    {
        string path = _temp.File("broken.json");
        File.WriteAllText(path, "{\"records\": [");

        Assert.Equal(5, SnapshotCheck.RunCommand(path, null, new StringWriter()));
        Assert.Equal(2, SnapshotCheck.RunCommand(_temp.File("missing.json"), null, new StringWriter()));
        Assert.Equal(new[] { SnapshotCheck.ParsesRule, SnapshotCheck.RecordCountRule }, Failed(SnapshotCheck.Check("{\"schemaVersion\": 2, \"records\": []}"), SnapshotCheckKind.Gate));
    }

    [Fact]
    public void Warnings_never_fail_the_check()
    {
        string json = Edited(raw =>
        {
            Fields(raw, 0)["Template_Consider"] = new JArray("Bad/Site", "CON", "Trailing.");
            Fields(raw, 1)["Structure"] = "BRAIN";
            Fields(raw, 2)["Structure"] = "brain";
            Fields(raw, 3)["Type"] = "BODY";
            Fields(raw, 4)["SchemeCode"] = "228793008";
            Fields(raw, 5)["Scheme"] = "FMA";
            Fields(raw, 5)["SchemeCode"] = "7088";
            Fields(raw, 6)["Scheme"] = "FMA";
            Fields(raw, 6)["SchemeCode"] = "PTV";
        });

        SnapshotCheckReport report = SnapshotCheck.Check(json);

        Assert.True(report.Passed);
        Assert.Equal(3, report.Rule(SnapshotCheck.SiteNamesRule).Findings.Count);
        Assert.Contains(report.Rule(SnapshotCheck.UniqueStructureRule).Findings, f => f.StartsWith("Differ only in case: `BRAIN`", StringComparison.Ordinal));
        Assert.Contains(report.Rule(SnapshotCheck.InterpretedTypeRule).Findings, f => f.Contains("`BODY`", StringComparison.Ordinal));
        Assert.Contains(report.Rule(SnapshotCheck.SchemeCodeRule).Findings, f => f.Contains("`228793008` fails the Verhoeff check digit", StringComparison.Ordinal));
        Assert.Contains(report.Rule(SnapshotCheck.SchemeCodeRule).Findings, f => f.Contains("FMA code `PTV` is not an integer", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Rule(SnapshotCheck.SchemeCodeRule).Findings, f => f.Contains("`7088`", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("228793007", null)]
    [InlineData("20795001", null)]
    [InlineData("91723000", null)]
    [InlineData("1000000101100", null)] // long form (extension), partition 10
    [InlineData("88", "is not 6-18 digits")]
    [InlineData("1234567890123456789", "is not 6-18 digits")]
    [InlineData("22879300A", "is not 6-18 digits")]
    [InlineData("028793009", "starts with 0")]
    [InlineData("228793011", "has partition 01, not a concept (00 or 10)")]
    [InlineData("228793008", "fails the Verhoeff check digit")]
    public void Sct_ids(string code, string? problem)
    {
        Assert.Equal(problem, SnapshotCheck.SctIdProblem(code));
    }

    [Theory]
    [InlineData("AbdPelv_Prostate", true)]
    [InlineData("H&N_LX_HPX_Thyroid", true)]
    [InlineData("_Targets_RNI", true)]
    [InlineData("a/b", false)]
    [InlineData("a\\b", false)]
    [InlineData("a:b", false)]
    [InlineData("..", false)]
    [InlineData(" ", false)]
    [InlineData("Site.", false)]
    [InlineData("aux", false)]
    [InlineData("COM1.txt", false)]
    public void Site_folder_names(string name, bool usable)
    {
        Assert.Equal(usable, SnapshotCheck.IsUsableSiteName(name));
    }

    [Fact]
    public void Interpreted_types_are_the_25_defined_terms()
    {
        Assert.Equal(25, SnapshotCheck.InterpretedTypes.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain("Control", SnapshotCheck.InterpretedTypes);
        Assert.Contains("CONTROL", SnapshotCheck.InterpretedTypes);
    }
}
