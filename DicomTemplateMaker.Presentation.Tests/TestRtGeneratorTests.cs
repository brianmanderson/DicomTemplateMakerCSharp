using DicomTemplateMaker.Presentation.Tests.Fakes;
using DicomTemplateMakerGUI.DicomTemplateServices;
using DicomTemplateMakerGUI.Shell;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class TestRtGeneratorTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Root => folder.Sub("templates");

    private static string SampleFolder => Templates.RepositoryFile("DicomTemplateMakerGUI", "SmallCT");

    private DicomTemplateRunner Runner() => new(Root, Templates.RepositoryFile("DicomTemplateMakerGUI", "template_RS.dcm"), null, TimeProvider.System);

    [Fact]
    public void Writes_one_rt_per_template_on_the_sample_and_leaves_paths_alone()
    {
        string brain = Templates.Make(Root, "Brain", new[] { "/monitored" });
        Templates.Make(Root, "Lung");
        string output = Path.Combine(folder.Path, "out", "Template_Output");
        string pathsBefore = File.ReadAllText(Path.Combine(brain, "Paths.txt"));
        var messages = new List<string>();

        TestRtResult result = TestRtGenerator.Run(Runner(), new[] { "Brain", "Lung" }, SampleFolder, output, new SyncProgress(messages), TestContext.Current.CancellationToken);

        Assert.Empty(result.Errors);
        Assert.Equal(4, result.SampleFilesCopied);
        Assert.Equal(2, result.WrittenCount);
        Assert.Single(Directory.GetFiles(output, "Brain_UID*.dcm"));
        Assert.Single(Directory.GetFiles(output, "Lung_UID*.dcm"));
        Assert.Equal(pathsBefore, File.ReadAllText(Path.Combine(brain, "Paths.txt")));
        Assert.DoesNotContain(output, File.ReadAllText(Path.Combine(Root, "Lung", "Paths.txt")), StringComparison.Ordinal);
        Assert.Equal(new[] { "Writing test RTs: 1 of 2 (Brain)", "Writing test RTs: 2 of 2 (Lung)" }, messages);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void A_second_run_keeps_the_existing_files()
    {
        Templates.Make(Root, "Brain");
        string output = folder.Sub("out");
        TestRtGenerator.Run(Runner(), new[] { "Brain" }, SampleFolder, output, null, TestContext.Current.CancellationToken);

        TestRtResult again = TestRtGenerator.Run(Runner(), new[] { "Brain" }, SampleFolder, output, null, TestContext.Current.CancellationToken);

        Assert.Equal(0, again.SampleFilesCopied);
        Assert.Equal(0, again.WrittenCount);
        Assert.Equal(1, again.Reports.Single().Report.Count(SkipReason.AlreadyDone));
    }

    [Fact]
    public void A_missing_sample_folder_is_reported_and_nothing_is_written()
    {
        Templates.Make(Root, "Brain");

        TestRtResult result = TestRtGenerator.Run(Runner(), new[] { "Brain" }, Path.Combine(folder.Path, "no-sample"), folder.Sub("out"), null, TestContext.Current.CancellationToken);

        Assert.Single(result.Errors);
        Assert.Empty(result.Reports);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void An_unknown_template_is_reported_in_its_report()
    {
        TestRtResult result = TestRtGenerator.Run(Runner(), new[] { "Missing" }, SampleFolder, folder.Sub("out"), null, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Reports.Single().Report.ErrorCount);
        Assert.True(result.HasFailures);
    }

    /// <summary>Reports synchronously, so the test sees every message in order.</summary>
    private sealed class SyncProgress : IProgress<string>
    {
        private readonly List<string> messages;

        public SyncProgress(List<string> messages) => this.messages = messages;

        public void Report(string value) => messages.Add(value);
    }
}
