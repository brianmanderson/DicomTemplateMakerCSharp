using FellowOakDicom;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TemplateEvaluationTool.Tests;

/// <summary>The three commands over the published snapshot and the sample CT, as in protocol section 1.3.</summary>
public sealed class PipelineTests : IDisposable
{
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

    private static string Repository(params string[] parts) => Path.Combine(new[] { RunContext.RepositoryRoot() }.Concat(parts).ToArray());

    private string RunAll(string name)
    {
        string run = Path.Combine(scratch, name);
        Assert.Equal(0, BuildSitesCommand.Run(new[] { "--snapshot", Repository("TemplateSnapshots", "TG263.json"), "--naming", "default", "--data-dir", Path.Combine(run, "data"), "--out", Path.Combine(run, "sites") }));
        Assert.Equal(0, GenerateRtsCommand.Run(new[] { "--sites", Path.Combine(run, "sites"), "--images", Repository("DicomTemplateMakerGUI", "SmallCT"), "--out", Path.Combine(run, "rt") }));
        Assert.Equal(0, InventoryCommand.Run(new[] { "--in", Path.Combine(run, "rt"), "--out", Path.Combine(run, "inventory") }));
        return run;
    }

    /// <summary>Every output a baseline commits except the manifests, which record the time of the run.</summary>
    private static Dictionary<string, string> Outputs(string run)
    {
        var files = new List<string>
        {
            Path.Combine(run, "sites", BuildSitesCommand.WarningsFileName),
            Path.Combine(run, "sites", BuildSitesCommand.SitesFileName),
            Path.Combine(run, "rt", GenerateRtsCommand.ReportFileName),
        };
        files.AddRange(Directory.GetFiles(Path.Combine(run, "inventory"), "*.txt"));
        return files.ToDictionary(f => Path.GetRelativePath(run, f), File.ReadAllText);
    }

    [Fact]
    public void Two_runs_on_the_same_inputs_give_identical_outputs_although_the_rts_differ()
    {
        string first = RunAll("first");
        string second = RunAll("second");

        Dictionary<string, string> a = Outputs(first);
        Dictionary<string, string> b = Outputs(second);
        Assert.Equal(53 + 3, a.Count);
        Assert.Equal(a, b);

        // The RTs themselves carry new UIDs and times on every run; only the masking makes the inventories equal.
        string rtA = Directory.GetFiles(Path.Combine(first, "rt", "series"), "AbdPelv_Anal_UID*.dcm").Single();
        string rtB = Directory.GetFiles(Path.Combine(second, "rt", "series"), "AbdPelv_Anal_UID*.dcm").Single();
        Assert.NotEqual(File.ReadAllBytes(rtA), File.ReadAllBytes(rtB));
    }

    [Fact]
    public void No_output_carries_the_sample_ct_patient_name_or_id()
    {
        string run = RunAll("run");
        DicomDataset ct = DicomFile.Open(Directory.GetFiles(Repository("DicomTemplateMakerGUI", "SmallCT"), "*.dcm")[0]).Dataset;
        string name = ct.GetString(DicomTag.PatientName);
        string id = ct.GetString(DicomTag.PatientID);
        Assert.NotEmpty(name);

        var texts = Outputs(run).Values.ToList();
        texts.AddRange(Directory.GetFiles(run, RunContext.ManifestFileName, SearchOption.AllDirectories).Select(File.ReadAllText));
        foreach (string text in texts)
        {
            Assert.DoesNotContain(name, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(name.Replace('^', ' '), text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(id, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Build_sites_refuses_an_output_folder_that_is_not_empty_and_an_unknown_naming_choice()
    {
        string sites = Path.Combine(scratch, "sites");
        Directory.CreateDirectory(sites);
        File.WriteAllText(Path.Combine(sites, "stale.txt"), "x");
        string snapshot = Repository("TemplateSnapshots", "TG263.json");

        Assert.Throws<UsageException>(() => BuildSitesCommand.Run(new[] { "--snapshot", snapshot, "--naming", "default", "--data-dir", Path.Combine(scratch, "data"), "--out", sites }));
        Assert.Throws<UsageException>(() => BuildSitesCommand.Run(new[] { "--snapshot", snapshot, "--naming", "french", "--data-dir", Path.Combine(scratch, "data"), "--out", Path.Combine(scratch, "new") }));
        Assert.Throws<UsageException>(() => BuildSitesCommand.Run(new[] { "--snapshot", snapshot, "--bogus", "x" }));
    }

    [Fact]
    public void Realism_passes_the_frozen_snapshot_rts_only_with_the_repository_allowlist()
    {
        string run = Path.Combine(scratch, "realism");
        Assert.Equal(0, BuildSitesCommand.Run(new[] { "--snapshot", Repository("TemplateSync.Tests", "TestData", "TG263.2026-09-30.json"), "--naming", "default", "--data-dir", Path.Combine(run, "data"), "--out", Path.Combine(run, "sites") }));
        Assert.Equal(0, GenerateRtsCommand.Run(new[] { "--sites", Path.Combine(run, "sites"), "--images", Repository("DicomTemplateMakerGUI", "SmallCT"), "--out", Path.Combine(run, "rt") }));
        string allowlist = Repository("docs", "evidence", "validator-allowlist.json");

        Assert.Equal(0, RealismCommand.Run(new[] { "--in", Path.Combine(run, "rt"), "--out", Path.Combine(run, "allowed"), "--allowlist", allowlist }));
        Assert.Equal(1, RealismCommand.Run(new[] { "--in", Path.Combine(run, "rt"), "--out", Path.Combine(run, "bare") }));

        JObject report = JObject.Parse(File.ReadAllText(Path.Combine(run, "allowed", RealismCommand.JsonFileName)));
        Assert.Equal(53, (int)report["files"]!);
        Assert.Equal(0, (int)report["unallowedErrors"]!);
        var findings = ((JArray)report["findings"]!).Cast<JObject>().ToList();
        // The placeholder codes 88 and 99 of seven snapshot records (decision D1b) are the only errors.
        Assert.All(findings.Where(f => (string?)f["severity"] == "error"), f => Assert.Equal(("RR8", "D1b"), ((string?)f["rule"], (string?)f["allowlistedAs"])));
        Assert.Equal(53, findings.Count(f => ((string)f["message"]!).StartsWith("ROI Observation Label (3006,0085), retired", StringComparison.Ordinal)));
        Assert.True(File.Exists(Path.Combine(run, "allowed", RealismCommand.MarkdownFileName)));

        Assert.Throws<UsageException>(() => RealismCommand.Run(new[] { "--in", Path.Combine(run, "rt"), "--out", Path.Combine(run, "missing"), "--allowlist", Path.Combine(run, "none.json") }));
    }

    [Fact]
    public void No_command_writes_into_the_program_data_folder()
    {
        string inside = Path.Combine(RunContext.ProgramDataFolder, "evaluation");

        Assert.Throws<UsageException>(() => RunContext.SafeFolder(inside, "data-dir"));
        Assert.Throws<UsageException>(() => RunContext.SafeFolder(RunContext.ProgramDataFolder, "out"));
        Assert.Equal(Path.GetFullPath(scratch), RunContext.SafeFolder(scratch, "out"));
    }
}
