using DicomTemplateMakerGUI.DicomTemplateServices;
using DicomTemplateMakerGUI.Shell;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class RunnerStatusTests
{
    private static readonly TimeZoneInfo PlusTwo = TimeZoneInfo.CreateCustomTimeZone("Test+02", TimeSpan.FromHours(2), "Test+02", "Test+02");
    private static readonly DateTimeOffset Noon = new(2026, 9, 28, 12, 0, 5, TimeSpan.Zero);

    internal static RunReport Report(int written = 0, int roiFailures = 0, int errors = 0, int waiting = 0, int backingOff = 0, DateTimeOffset? at = null)
    {
        var skipped = Enumerable.Range(0, waiting).Select(i => new SkippedItem("Brain", "/d" + i, null, SkipReason.Waiting, "recent"))
            .Concat(Enumerable.Range(0, backingOff).Select(i => new SkippedItem("Brain", "/b" + i, null, SkipReason.BackingOff, "in use")))
            .ToList();
        return new RunReport(
            at ?? Noon,
            TimeSpan.FromMilliseconds(40),
            Enumerable.Range(0, written).Select(i => new WrittenRt("Brain", "/d", "1.2." + i, "/d/Brain_UID1.2." + i + ".dcm", 2)).ToList(),
            skipped,
            Enumerable.Range(0, roiFailures).Select(i => new RoiFailure("Brain", "PTV" + i, "no colour", "/d", "1.2")).ToList(),
            Enumerable.Range(0, errors).Select(i => new RunError("template 'Brain'", "broken " + i, "Brain")).ToList());
    }

    [Fact]
    public void Stopped_before_any_scan_says_how_to_start()
    {
        string text = new RunnerStatus().Describe(RunnerState.Stopped, PlusTwo);

        Assert.StartsWith("RT generator stopped.", text, StringComparison.Ordinal);
        Assert.Contains("Start", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Running_shows_totals_since_start_and_the_last_scan_in_local_time()
    {
        var status = new RunnerStatus();
        status.Record(Report(written: 2, roiFailures: 1));
        status.Record(Report(written: 1, errors: 2, waiting: 3, at: Noon.AddSeconds(3)));

        string text = status.Describe(RunnerState.Running, PlusTwo);

        Assert.Equal("RT generator running. Last scan 14:00:08: 3 RTs written and 1 ROI skipped since start; 2 errors, 3 folders waiting.", text);
    }

    [Fact]
    public void Backing_off_folders_are_mentioned()
    {
        var status = new RunnerStatus();
        status.Record(Report(backingOff: 1));

        Assert.Contains("1 folder retried later (files in use)", status.Describe(RunnerState.Running, PlusTwo), StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_starts_new_totals()
    {
        var status = new RunnerStatus();
        status.Record(Report(written: 4));

        status.Reset();

        Assert.Equal(0, status.WrittenSinceStart);
        Assert.Null(status.LastReport);
        Assert.Equal("RT generator running: first scan in progress\u2026", status.Describe(RunnerState.Running, PlusTwo));
    }

    [Fact]
    public void Paused_says_it_resumes()
    {
        Assert.Contains("resumes", new RunnerStatus().Describe(RunnerState.Paused, PlusTwo), StringComparison.Ordinal);
    }

    [Fact]
    public void Errors_of_the_last_scan_are_listed_for_the_tooltip()
    {
        var status = new RunnerStatus();
        Assert.Null(status.DescribeErrors());
        status.Record(Report(errors: 2));

        string? errors = status.DescribeErrors();

        Assert.NotNull(errors);
        Assert.Contains("template 'Brain': broken 0", errors, StringComparison.Ordinal);
        Assert.Contains("template 'Brain': broken 1", errors, StringComparison.Ordinal);
    }

    [Fact]
    public void The_log_report_lists_written_rts_skipped_rois_and_errors()
    {
        List<string> lines = RunnerStatus.DescribeReport(Report(written: 1, roiFailures: 1, errors: 1)).ToList();

        Assert.Equal(3, lines.Count);
        Assert.Contains("wrote /d/Brain_UID1.2.0.dcm", lines[0], StringComparison.Ordinal);
        Assert.Contains("ROI 'PTV0' left out", lines[1], StringComparison.Ordinal);
        Assert.Contains("broken 0", lines[2], StringComparison.Ordinal);
    }

    private static RunReport FolderReport(IEnumerable<RunError>? errors = null, IEnumerable<SkippedItem>? skipped = null, IEnumerable<WrittenRt>? written = null, int second = 0) =>
        new(Noon.AddSeconds(second), TimeSpan.FromMilliseconds(40), (written ?? Array.Empty<WrittenRt>()).ToList(), (skipped ?? Array.Empty<SkippedItem>()).ToList(), Array.Empty<RoiFailure>(), (errors ?? Array.Empty<RunError>()).ToList());

    private static RunError BrokenFile => new("template 'Brain', directory '/ct'", "1 file(s) could not be read as DICOM (broken.dcm: not DICOM).", "Brain", "/ct");

    private static SkippedItem Skip(SkipReason reason, string directory = "/ct") => new("Brain", directory, null, reason, "detail");

    [Fact]
    public void A_folder_problem_stays_visible_while_the_runner_skips_the_folder_for_it()
    {
        // The runner reports a blocked folder once, then skips it as PreviouslyFailed: the status used to show
        // "0 errors" and no tooltip from the second scan on, while the folder never got its RT.
        var status = new RunnerStatus();
        status.Record(FolderReport(errors: new[] { BrokenFile }));
        status.Record(FolderReport(skipped: new[] { Skip(SkipReason.PreviouslyFailed) }, second: 3));
        status.Record(FolderReport(skipped: new[] { Skip(SkipReason.PreviouslyFailed) }, second: 6));

        string line = status.Describe(RunnerState.Running, PlusTwo);
        string? tooltip = status.DescribeErrors();

        Assert.Equal(1, status.OpenProblemCount);
        Assert.Contains("0 errors", line, StringComparison.Ordinal);
        Assert.Contains("1 folder with open problems, no RT written there", line, StringComparison.Ordinal);
        Assert.NotNull(tooltip);
        Assert.Contains("broken.dcm", tooltip, StringComparison.Ordinal);
        Assert.DoesNotContain("Errors in the last scan", tooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void A_folder_problem_stays_while_the_changed_folder_waits_and_goes_once_the_folder_is_processed()
    {
        var status = new RunnerStatus();
        status.Record(FolderReport(errors: new[] { BrokenFile }));
        status.Record(FolderReport(skipped: new[] { Skip(SkipReason.Waiting) }, second: 3));
        Assert.Equal(1, status.OpenProblemCount);

        status.Record(FolderReport(written: new[] { new WrittenRt("Brain", "/ct", "1.2.3", "/ct/Brain_UID1.2.3.dcm", 2) }, second: 6));

        Assert.Equal(0, status.OpenProblemCount);
        Assert.DoesNotContain("open problems", status.Describe(RunnerState.Running, PlusTwo), StringComparison.Ordinal);
        Assert.Null(status.DescribeErrors());
    }

    [Fact]
    public void A_folder_problem_goes_when_the_folder_is_no_longer_visited_and_on_reset()
    {
        var status = new RunnerStatus();
        status.Record(FolderReport(errors: new[] { BrokenFile }));
        status.Record(FolderReport(skipped: new[] { Skip(SkipReason.Unchanged, "/other") }, second: 3));
        Assert.Equal(0, status.OpenProblemCount);

        status.Record(FolderReport(errors: new[] { BrokenFile }, second: 6));
        status.Reset();
        Assert.Equal(0, status.OpenProblemCount);
    }

    [Fact]
    public void Template_errors_are_not_folder_problems_since_every_scan_reports_them()
    {
        var status = new RunnerStatus();
        status.Record(Report(errors: 1));

        Assert.Equal(0, status.OpenProblemCount);
    }

    [Fact]
    public void Real_runner_cycles_keep_a_blocked_folder_in_the_status()
    {
        using var folder = new Fakes.TestFolder();
        string templates = folder.Sub("templates");
        string images = folder.Sub("incoming");
        foreach (string file in Directory.GetFiles(Fakes.Templates.RepositoryFile("DicomTemplateMakerGUI", "SmallCT")))
        {
            File.Copy(file, Path.Combine(images, Path.GetFileName(file)));
        }

        File.WriteAllText(Path.Combine(images, "broken.dcm"), "not dicom");
        Fakes.Templates.Make(templates, "Brain", new[] { images });
        var runner = new DicomTemplateRunner(templates, Fakes.Templates.RepositoryFile("DicomTemplateMakerGUI", "template_RS.dcm"), null, TimeProvider.System) { SettleTime = TimeSpan.Zero };
        var status = new RunnerStatus();

        for (int cycle = 0; cycle < 3; cycle++)
        {
            status.Record(runner.RunOnce(TestContext.Current.CancellationToken));
        }

        Assert.Equal(0, status.LastReport!.ErrorCount);
        Assert.Equal(1, status.OpenProblemCount);
        Assert.Contains("broken.dcm", status.DescribeErrors(), StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(images, "Brain_UID*.dcm"));
    }
}
