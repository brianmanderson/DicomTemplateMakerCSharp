using DicomTemplateMakerGUI.DicomTemplateServices;
using Xunit;

namespace DicomTemplateCore.Tests;

/// <summary>
/// The settle rule, the processed-folder cache and the back-off after read failures, driven by a fake clock and
/// by counting cycles. Nothing here sleeps.
/// </summary>
public class RunnerSettleTests
{
    private static (string Images, string Templates) Setup(TestFolder folder, out WrittenSeries series)
    {
        string images = folder.Sub("Images");
        series = SeriesFactory.Write(images, "ct");
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images });
        return (images, templates);
    }

    private static RunReport Cycle(DicomTemplateRunner runner) => runner.RunOnce(TestContext.Current.CancellationToken);

    [Fact]
    public void Recent_files_are_deferred_until_they_are_older_than_the_settle_time()
    {
        using var folder = new TestFolder();
        (string images, string templates) = Setup(folder, out WrittenSeries series);
        var clock = new RunnerClock(DateTimeOffset.UtcNow); // the files were written just now
        DicomTemplateRunner runner = RunnerTemplates.Runner(templates, clock);

        RunReport first = Cycle(runner);
        clock.Advance(runner.SettleTime - TimeSpan.FromSeconds(3));
        RunReport second = Cycle(runner);
        clock.Advance(TimeSpan.FromSeconds(5));
        RunReport third = Cycle(runner);

        Assert.Equal(1, first.WaitingCount);
        Assert.Equal(1, second.WaitingCount);
        Assert.Empty(first.Written);
        Assert.Empty(second.Written);
        Assert.Equal(series.SeriesInstanceUid, Assert.Single(third.Written).SeriesInstanceUid);
        Assert.Equal(0, third.WaitingCount);
        Assert.True(File.Exists(RunnerTemplates.OutputPath(images, "Brain_Test", series.SeriesInstanceUid)));
    }

    [Fact]
    public void Old_files_seen_for_the_first_time_are_processed_at_once()
    {
        using var folder = new TestFolder();
        (_, string templates) = Setup(folder, out WrittenSeries series);

        RunReport report = Cycle(RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()));

        Assert.Equal(0, report.WaitingCount);
        Assert.Equal(series.SeriesInstanceUid, Assert.Single(report.Written).SeriesInstanceUid);
    }

    [Fact]
    public void A_folder_whose_files_changed_since_the_last_scan_waits_again_even_when_the_files_are_old()
    {
        // A copy that keeps the source's timestamps looks old; only the changed fingerprint shows it is in progress.
        using var folder = new TestFolder();
        (string images, string templates) = Setup(folder, out _);
        var clock = RunnerClock.AfterFilesSettled();
        DicomTemplateRunner runner = RunnerTemplates.Runner(templates, clock);
        Assert.Single(Cycle(runner).Written);

        WrittenSeries added = SeriesFactory.Write(images, "new");
        foreach (string file in added.Files)
        {
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-30));
        }

        clock.Advance(TimeSpan.FromSeconds(3));
        RunReport changed = Cycle(runner);
        clock.Advance(TimeSpan.FromSeconds(3));
        RunReport settled = Cycle(runner);

        Assert.Equal(SkipReason.Waiting, Assert.Single(changed.Skipped).Reason);
        Assert.Empty(changed.Written);
        Assert.Equal(added.SeriesInstanceUid, Assert.Single(settled.Written).SeriesInstanceUid);
    }

    [Fact]
    public void An_unchanged_folder_is_not_read_again()
    {
        using var folder = new TestFolder();
        (_, string templates) = Setup(folder, out WrittenSeries series);
        var clock = RunnerClock.AfterFilesSettled();
        DicomTemplateRunner runner = RunnerTemplates.Runner(templates, clock);
        Assert.Single(Cycle(runner).Written);

        RunReport again;
        // Were the folder read again, the locked file would make it back off.
        using (new FileStream(series.Files[0], FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            clock.Advance(TimeSpan.FromSeconds(3));
            again = Cycle(runner);
        }

        Assert.Equal(SkipReason.Unchanged, Assert.Single(again.Skipped).Reason);
        Assert.Empty(again.Written);
        Assert.Empty(again.Errors);
    }

    [Fact]
    public void A_deleted_rt_is_written_again()
    {
        using var folder = new TestFolder();
        (string images, string templates) = Setup(folder, out WrittenSeries series);
        var clock = RunnerClock.AfterFilesSettled();
        DicomTemplateRunner runner = RunnerTemplates.Runner(templates, clock);
        Assert.Single(Cycle(runner).Written);

        File.Delete(RunnerTemplates.OutputPath(images, "Brain_Test", series.SeriesInstanceUid));
        clock.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(series.SeriesInstanceUid, Assert.Single(Cycle(runner).Written).SeriesInstanceUid);
    }

    [Fact]
    public void An_rt_written_before_its_series_was_complete_is_reported_until_it_is_deleted()
    {
        // A transfer that stalls past the settle time (or files whose times look old) got an RT for the slices present
        // then; when the rest arrived the RT was "already done" and stayed incomplete without a word.
        using var folder = new TestFolder();
        (string images, string templates) = Setup(folder, out WrittenSeries series);
        string aside = folder.Sub("Aside");
        foreach (string file in series.Files.Skip(2))
        {
            File.Move(file, Path.Combine(aside, Path.GetFileName(file)));
        }

        var clock = RunnerClock.AfterFilesSettled();
        DicomTemplateRunner runner = RunnerTemplates.Runner(templates, clock);
        Assert.Single(Cycle(runner).Written); // an RT for 2 of the 4 slices

        foreach (string file in Directory.GetFiles(aside))
        {
            File.Move(file, Path.Combine(images, Path.GetFileName(file)));
        }

        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(SkipReason.Waiting, Assert.Single(Cycle(runner).Skipped).Reason);
        clock.Advance(TimeSpan.FromSeconds(3));
        RunReport grown = Cycle(runner);

        RunError error = Assert.Single(grown.Errors);
        Assert.Equal(series.SeriesInstanceUid, error.SeriesInstanceUid);
        Assert.Contains("now has 4 images", error.Message, StringComparison.Ordinal);
        Assert.Contains("references only 2", error.Message, StringComparison.Ordinal);
        Assert.Empty(grown.Written);
        Assert.Equal(SkipReason.PreviouslyFailed, Assert.Single(Cycle(runner).Skipped).Reason);

        File.Delete(RunnerTemplates.OutputPath(images, "Brain_Test", series.SeriesInstanceUid));
        clock.Advance(TimeSpan.FromSeconds(3));
        RunReport rewritten = Cycle(runner);
        Assert.Equal(series.SeriesInstanceUid, Assert.Single(rewritten.Written).SeriesInstanceUid);
        Assert.Empty(rewritten.Errors);
        Assert.Empty(Cycle(runner).Errors);
    }

    [Fact]
    public void A_changed_template_requirement_decides_an_unchanged_folder_again()
    {
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries series = SeriesFactory.Write(images, "ct", SeriesFactory.Descriptions("Pelvis", "Abdomen"));
        string templates = folder.Sub("Templates");
        string template = RunnerTemplates.Make(templates, "Brain_Test", new[] { images }, dicomTags: RunnerTemplates.SeriesRequirement("Brain"));
        var clock = RunnerClock.AfterFilesSettled();
        DicomTemplateRunner runner = RunnerTemplates.Runner(templates, clock);
        Assert.Equal(SkipReason.NoMatch, Assert.Single(Cycle(runner).Skipped).Reason);

        File.WriteAllLines(Path.Combine(template, "DicomTags.txt"), new[] { "Series Description\\Pelvis" });
        clock.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(series.SeriesInstanceUid, Assert.Single(Cycle(runner).Written).SeriesInstanceUid);
    }

    [Fact]
    public void A_file_in_use_defers_the_folder_with_back_off_and_it_recovers_once_released()
    {
        using var folder = new TestFolder();
        (string images, string templates) = Setup(folder, out WrittenSeries series);
        DicomTemplateRunner runner = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled());

        RunReport first, second;
        using (new FileStream(series.Files[2], FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            first = Cycle(runner);  // attempt 1 fails: retry after 1 cycle
            second = Cycle(runner); // attempt 2 fails: retry after 2 cycles
        }

        RunReport third = Cycle(runner);  // released, but still backing off
        RunReport fourth = Cycle(runner); // attempt 3 succeeds

        foreach (RunReport report in new[] { first, second, third })
        {
            Assert.Equal(1, report.BackingOffCount);
            Assert.Empty(report.Written);
            Assert.Empty(report.Errors);
        }

        Assert.StartsWith("Attempt 1 failed", first.Skipped.Single().Detail);
        Assert.StartsWith("Attempt 2 failed", second.Skipped.Single().Detail);
        Assert.StartsWith("Next attempt in 1 cycle", third.Skipped.Single().Detail);
        Assert.Equal(series.SeriesInstanceUid, Assert.Single(fourth.Written).SeriesInstanceUid);
        Assert.True(File.Exists(RunnerTemplates.OutputPath(images, "Brain_Test", series.SeriesInstanceUid)));
    }

    [Fact]
    public void After_retries_at_1_2_4_and_8_cycles_the_folder_is_given_up_until_its_files_change()
    {
        using var folder = new TestFolder();
        (_, string templates) = Setup(folder, out WrittenSeries series);
        var clock = RunnerClock.AfterFilesSettled();
        DicomTemplateRunner runner = RunnerTemplates.Runner(templates, clock);
        var attempts = new List<int>();
        var errorCycles = new List<int>();

        using (new FileStream(series.Files[0], FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            for (int cycle = 1; cycle <= 20; cycle++)
            {
                RunReport report = Cycle(runner);
                if (report.Skipped.Any(s => s.Detail.StartsWith("Attempt", StringComparison.Ordinal)) || report.HasErrors)
                {
                    attempts.Add(cycle);
                }

                if (report.HasErrors)
                {
                    errorCycles.Add(cycle);
                    Assert.Contains("Gave up after 5 attempts", report.Errors.Single().Message);
                }

                Assert.Empty(report.Written);
            }
        }

        Assert.Equal(new[] { 1, 2, 4, 8, 16 }, attempts);
        Assert.Equal(new[] { 16 }, errorCycles); // reported once, not every cycle

        // Released, but the files did not change: still given up.
        RunReport released = Cycle(runner);
        Assert.Equal(SkipReason.PreviouslyFailed, Assert.Single(released.Skipped).Reason);
        Assert.Empty(released.Errors);

        File.SetLastWriteTimeUtc(series.Files[0], DateTime.UtcNow.AddMinutes(-5));
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(SkipReason.Waiting, Assert.Single(Cycle(runner).Skipped).Reason);
        Assert.Equal(series.SeriesInstanceUid, Assert.Single(Cycle(runner).Written).SeriesInstanceUid);
    }

    [Fact]
    public void A_missing_path_is_reported_as_skipped_not_as_an_error()
    {
        using var folder = new TestFolder();
        string templates = folder.Sub("Templates");
        string missing = Path.Combine(folder.Path, "Unplugged");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { missing });

        RunReport report = Cycle(RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()));

        Assert.Empty(report.Errors);
        SkippedItem skipped = Assert.Single(report.Skipped);
        Assert.Equal(SkipReason.PathNotFound, skipped.Reason);
        Assert.Equal(missing, skipped.Directory);
    }
}
