using DicomTemplateMaker.Presentation.Tests.Fakes;
using DicomTemplateMakerGUI.Shell;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class BulkFolderActionTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    [Fact]
    public void One_failure_is_recorded_and_the_other_folders_go_on()
    {
        var logger = new CapturingLogger();
        var done = new List<string>();

        BulkFolderResult result = BulkFolderAction.Run(new[] { "/a", "/b", "/c" }, f =>
        {
            if (f == "/b")
            {
                throw new IOException("in use");
            }

            done.Add(f);
        }, logger, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "/a", "/c" }, result.Done);
        Assert.Equal(new[] { "/a", "/c" }, done);
        FolderFailure failure = Assert.Single(result.Failed);
        Assert.Equal("/b", failure.Folder);
        Assert.Equal("in use", failure.Message);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("/b", StringComparison.Ordinal));
    }

    [Fact]
    public void A_cancelled_windows_dialog_counts_as_a_failure_for_that_folder()
    {
        BulkFolderResult result = BulkFolderAction.Run(new[] { "/a", "/b" }, f =>
        {
            if (f == "/a")
            {
                throw new OperationCanceledException("The user cancelled.");
            }
        }, new CapturingLogger(), TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "/b" }, result.Done);
        Assert.Equal("/a", Assert.Single(result.Failed).Folder);
    }

    [Fact]
    public void Copies_get_the_next_free_name_and_all_files_but_no_monitored_folders()
    {
        string root = folder.Sub("templates");
        string source = Templates.Make(root, "Brain", new[] { "/monitored" });
        Directory.CreateDirectory(Path.Combine(source, "ROIs"));
        File.WriteAllText(Path.Combine(source, "ROIs", "Kept.txt"), "legacy");
        Directory.CreateDirectory(Path.Combine(root, "Brain_Copy0"));

        string name = TemplateCopier.Copy(source, root, "Brain");

        Assert.Equal("Brain_Copy1", name);
        string copy = Path.Combine(root, name);
        Assert.Equal(File.ReadAllText(Path.Combine(source, "All_ROIs.json")), File.ReadAllText(Path.Combine(copy, "All_ROIs.json")));
        Assert.Equal(string.Empty, File.ReadAllText(Path.Combine(copy, "Paths.txt")));
        Assert.Equal(File.ReadAllText(Path.Combine(source, "DicomTags.txt")), File.ReadAllText(Path.Combine(copy, "DicomTags.txt")));
        Assert.Equal("/monitored", File.ReadAllText(Path.Combine(source, "Paths.txt")).Trim());
        Assert.Equal("legacy", File.ReadAllText(Path.Combine(copy, "ROIs", "Kept.txt")));
        Assert.Equal("Brain_Copy2", TemplateCopier.NextCopyName(root, "Brain"));
    }

    [Fact]
    public void A_running_generator_writes_no_rts_for_a_fresh_copy()
    {
        // The copy used to monitor the original's folders, so it wrote "<name>_Copy0_UID….dcm" next to every series at once.
        string root = folder.Sub("templates");
        string images = folder.Sub("incoming");
        foreach (string file in Directory.GetFiles(Templates.RepositoryFile("DicomTemplateMakerGUI", "SmallCT")))
        {
            File.Copy(file, Path.Combine(images, Path.GetFileName(file)));
        }

        string source = Templates.Make(root, "Brain", new[] { images });
        var runner = new DicomTemplateMakerGUI.DicomTemplateServices.DicomTemplateRunner(root, Templates.RepositoryFile("DicomTemplateMakerGUI", "template_RS.dcm"), null, TimeProvider.System) { SettleTime = TimeSpan.Zero };
        Assert.Equal(1, runner.RunOnce(TestContext.Current.CancellationToken).WrittenCount);

        string copy = TemplateCopier.Copy(source, root, "Brain");
        DicomTemplateMakerGUI.DicomTemplateServices.RunReport report = runner.RunOnce(TestContext.Current.CancellationToken);

        Assert.Equal(0, report.WrittenCount);
        Assert.Empty(report.Errors);
        Assert.Empty(Directory.GetFiles(images, copy + "_UID*.dcm"));
    }
}
