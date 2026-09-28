using DicomTemplateMakerGUI.DicomTemplateServices;
using FellowOakDicom;
using Xunit;

namespace DicomTemplateCore.Tests;

/// <summary>Per-series decisions of <see cref="DicomTemplateRunner.RunOnce"/>, delete mode and error isolation.</summary>
public class RunnerSeriesTests
{
    [Fact]
    public void Every_series_in_a_folder_gets_its_own_rt()
    {
        // The old runner wrote an RT for the first series only (KeyNotFoundException after the first save).
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries a = SeriesFactory.Write(images, "a");
        WrittenSeries b = SeriesFactory.Write(images, "b");
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images });

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        Assert.Empty(report.Errors);
        Assert.Equal(2, report.WrittenCount);
        foreach (WrittenSeries series in new[] { a, b })
        {
            DicomDataset rt = RunnerTemplates.ReadOutput(images, "Brain_Test", series.SeriesInstanceUid).Dataset;
            Assert.Equal(series.StudyInstanceUid, rt.GetString(DicomTag.StudyInstanceUID));
            Assert.Equal(series.FrameOfReferenceUid, rt.GetString(DicomTag.FrameOfReferenceUID));
            Assert.Equal(new[] { "Brain", "PTV_High" }, rt.GetSequence(DicomTag.StructureSetROISequence).Items.Select(i => i.GetString(DicomTag.ROIName)));
        }
    }

    [Fact]
    public void Localizers_secondary_captures_and_non_planning_modalities_next_to_a_ct_get_no_rt()
    {
        // A template without requirements used to write an RT for the topogram (which often shares the CT's frame of
        // reference) and one for the scanner's dose report, so the planner saw several structure sets for one CT.
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries ct = SeriesFactory.Write(images, "ct", SeriesFactory.Descriptions("Pelvis 3mm", "Pelvis"));
        WrittenSeries scout = SeriesFactory.Write(images, "scout", dataset =>
        {
            dataset.AddOrUpdate(DicomTag.ImageType, "ORIGINAL", "PRIMARY", "LOCALIZER");
            dataset.AddOrUpdate(DicomTag.SeriesDescription, "Topogram 0.6 T20s");
        });
        WrittenSeries doseReport = SeriesFactory.Write(images, "dose", dataset =>
        {
            dataset.AddOrUpdate(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage);
            dataset.AddOrUpdate(DicomTag.Modality, "CT");
            dataset.AddOrUpdate(DicomTag.SeriesDescription, "Dose Report");
        });
        WrittenSeries ultrasound = SeriesFactory.Write(images, "us", dataset => dataset.AddOrUpdate(DicomTag.Modality, "US"));
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images });

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        Assert.Empty(report.Errors);
        Assert.Equal(ct.SeriesInstanceUid, Assert.Single(report.Written).SeriesInstanceUid);
        Dictionary<string, SkippedItem> skipped = report.Skipped.Where(i => i.SeriesInstanceUid != null).ToDictionary(i => i.SeriesInstanceUid!);
        Assert.All(skipped.Values, i => Assert.Equal(SkipReason.NoMatch, i.Reason));
        Assert.Contains("localizer", skipped[scout.SeriesInstanceUid].Detail, StringComparison.Ordinal);
        Assert.Contains("Secondary Capture", skipped[doseReport.SeriesInstanceUid].Detail, StringComparison.Ordinal);
        Assert.Contains("modality is US", skipped[ultrasound.SeriesInstanceUid].Detail, StringComparison.Ordinal);
        Assert.Single(Directory.GetFiles(images, "Brain_Test_UID*"));
    }

    [Theory]
    [InlineData("1.2.826.0.1.3680043.2.1125.1", "1.2.826.0.1.3680043.2.1125.2")] // matching series listed first
    [InlineData("1.2.826.0.1.3680043.2.1125.2", "1.2.826.0.1.3680043.2.1125.1")] // matching series listed last
    public void Each_series_is_matched_on_its_own_whatever_the_order(string matchingUid, string otherUid)
    {
        // The old runner did not reset its decision per series: a non-matching series blocked the later ones.
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        SeriesFactory.Write(images, "brain", SeriesFactory.Descriptions("Brain MRI fusion", "Head"), seriesUid: matchingUid);
        SeriesFactory.Write(images, "pelvis", SeriesFactory.Descriptions("Pelvis", "Abdomen"), seriesUid: otherUid);
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images }, dicomTags: RunnerTemplates.SeriesRequirement("brain"));

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        Assert.Empty(report.Errors);
        Assert.Equal(matchingUid, Assert.Single(report.Written).SeriesInstanceUid);
        Assert.True(File.Exists(RunnerTemplates.OutputPath(images, "Brain_Test", matchingUid)));
        Assert.False(File.Exists(RunnerTemplates.OutputPath(images, "Brain_Test", otherUid)));
        SkippedItem noMatch = Assert.Single(report.Skipped, s => s.Reason == SkipReason.NoMatch);
        Assert.Equal(otherUid, noMatch.SeriesInstanceUid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_or_missing_description_does_not_match_a_template_with_requirements(string? description)
    {
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries series = SeriesFactory.Write(images, "ct", SeriesFactory.Descriptions(description, description));
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images }, dicomTags: RunnerTemplates.SeriesRequirement("Brain"));

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        Assert.Empty(report.Written);
        Assert.Equal(series.SeriesInstanceUid, Assert.Single(report.Skipped, s => s.Reason == SkipReason.NoMatch).SeriesInstanceUid);
        Assert.Empty(Directory.GetFiles(images, "Brain_Test_UID*"));
    }

    [Fact]
    public void A_template_without_requirements_or_with_only_blank_ones_matches_a_series_without_descriptions()
    {
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries series = SeriesFactory.Write(images, "ct", SeriesFactory.Descriptions(null, null));
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "No_Tags", new[] { images });
        // DicomTags.txt holds "Series Description\" and "Study Description\": blank requirements, ignored.
        RunnerTemplates.Make(templates, "Blank_Tags", new[] { images }, dicomTags: new Dictionary<string, List<string>>
        {
            { "Series Description", new List<string> { string.Empty } },
            { "Study Description", new List<string>() },
        });
        Assert.Contains("Series Description\\", File.ReadAllLines(Path.Combine(templates, "Blank_Tags", "DicomTags.txt")));

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        Assert.Empty(report.Errors);
        Assert.Equal(new[] { "Blank_Tags", "No_Tags" }, report.Written.Select(w => w.TemplateName).OrderBy(n => n, StringComparer.Ordinal));
        Assert.True(File.Exists(RunnerTemplates.OutputPath(images, "No_Tags", series.SeriesInstanceUid)));
        Assert.True(File.Exists(RunnerTemplates.OutputPath(images, "Blank_Tags", series.SeriesInstanceUid)));
    }

    [Fact]
    public void A_series_whose_rt_exists_is_skipped_while_a_new_series_in_the_same_folder_is_processed()
    {
        // The old runner skipped the whole directory as soon as any "{template}_UID*" file was in it.
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries first = SeriesFactory.Write(images, "a");
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images });
        RunReport initial = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);
        string firstOutput = Assert.Single(initial.Written).Path;
        byte[] firstBytes = File.ReadAllBytes(firstOutput);
        WrittenSeries second = SeriesFactory.Write(images, "b");

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        Assert.Equal(second.SeriesInstanceUid, Assert.Single(report.Written).SeriesInstanceUid);
        Assert.Equal(first.SeriesInstanceUid, Assert.Single(report.Skipped, s => s.Reason == SkipReason.AlreadyDone).SeriesInstanceUid);
        Assert.Equal(firstBytes, File.ReadAllBytes(firstOutput));
    }

    [Fact]
    public void A_template_that_cannot_be_loaded_is_reported_and_the_others_still_run()
    {
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries series = SeriesFactory.Write(images, "ct");
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "A_Broken", new[] { images });
        RunnerTemplates.Make(templates, "B_Good", new[] { images });

        RunReport report;
        using (new FileStream(Path.Combine(templates, "A_Broken", "Paths.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);
        }

        RunError error = Assert.Single(report.Errors);
        Assert.Equal("A_Broken", error.TemplateName);
        Assert.Contains("A_Broken", error.Context);
        Assert.Equal("B_Good", Assert.Single(report.Written).TemplateName);
        Assert.True(File.Exists(RunnerTemplates.OutputPath(images, "B_Good", series.SeriesInstanceUid)));
    }

    [Fact]
    public void A_template_with_a_corrupt_roi_file_is_an_error_every_cycle_but_logged_once()
    {
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        SeriesFactory.Write(images, "ct");
        string templates = folder.Sub("Templates");
        string broken = RunnerTemplates.Make(templates, "A_Broken", new[] { images });
        File.WriteAllText(Path.Combine(broken, "All_ROIs.json"), "{ this is not JSON");
        RunnerTemplates.Make(templates, "B_Good", new[] { images });
        var logger = new CapturingLogger();
        var runner = new DicomTemplateRunner(templates, DicomTemplateRunner.DefaultTemplateRsPath, logger, RunnerClock.AfterFilesSettled());

        RunReport first = runner.RunOnce(TestContext.Current.CancellationToken);
        RunReport second = runner.RunOnce(TestContext.Current.CancellationToken);

        Assert.Equal("A_Broken", Assert.Single(first.Errors).TemplateName);
        Assert.Equal("A_Broken", Assert.Single(second.Errors).TemplateName);
        Assert.Equal("B_Good", Assert.Single(first.Written).TemplateName);
        Assert.Empty(Directory.GetFiles(images, "A_Broken_UID*"));
        Assert.Single(logger.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Error && e.Message.Contains("A_Broken", StringComparison.Ordinal));
    }

    [Fact]
    public void Legacy_roi_files_that_were_not_migrated_are_an_error_every_cycle_but_logged_once()
    {
        // The runner, usually first to see a legacy template, migrated it without a word: an unreadable ROI file was
        // simply missing from every RT.
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        SeriesFactory.Write(images, "ct");
        string templates = folder.Sub("Templates");
        string prostate = Path.Combine(templates, "Prostate");
        Directory.CreateDirectory(Path.Combine(prostate, "ROIs"));
        File.WriteAllText(Path.Combine(prostate, "ROIs", "Bladder.txt"), "255\\255\\0\nBladder\\15900\\FMA\nORGAN");
        File.WriteAllText(Path.Combine(prostate, "ROIs", "CTV.txt"), "300\\0\\0\nCTV\\1\\FMA\nCTV"); // 300 is not a colour value
        File.WriteAllLines(Path.Combine(prostate, "Paths.txt"), new[] { images });
        var logger = new CapturingLogger();
        var runner = new DicomTemplateRunner(templates, DicomTemplateRunner.DefaultTemplateRsPath, logger, RunnerClock.AfterFilesSettled());

        RunReport first = runner.RunOnce(TestContext.Current.CancellationToken);
        RunReport second = runner.RunOnce(TestContext.Current.CancellationToken);

        RunError migrating = Assert.Single(first.Errors);
        Assert.Equal("Prostate", migrating.TemplateName);
        Assert.Contains("CTV.txt", migrating.Message, StringComparison.Ordinal);
        RunError later = Assert.Single(second.Errors);
        Assert.Contains("legacy ROI file(s)", later.Message, StringComparison.Ordinal);
        Assert.Equal(1, first.WrittenCount); // the template is still used, with the ROIs that could be read
        Assert.Equal(2, logger.Entries.Count(e => e.Level == Microsoft.Extensions.Logging.LogLevel.Error && e.Message.Contains("Prostate", StringComparison.Ordinal)));

        RunReport third = runner.RunOnce(TestContext.Current.CancellationToken);
        Assert.Single(third.Errors);
        Assert.Equal(2, logger.Entries.Count(e => e.Level == Microsoft.Extensions.Logging.LogLevel.Error && e.Message.Contains("Prostate", StringComparison.Ordinal)));

        Directory.Delete(Path.Combine(prostate, "ROIs"), recursive: true);
        Assert.Empty(runner.RunOnce(TestContext.Current.CancellationToken).Errors);
    }

    [Fact]
    public void A_corrupt_dicom_file_is_reported_once_and_blocks_the_folder_until_it_changes()
    {
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries series = SeriesFactory.Write(images, "ct");
        string corrupt = Path.Combine(images, "broken.dcm");
        File.WriteAllText(corrupt, "not a DICOM file");
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images });
        var clock = RunnerClock.AfterFilesSettled();
        DicomTemplateRunner runner = RunnerTemplates.Runner(templates, clock);

        RunReport first = runner.RunOnce(TestContext.Current.CancellationToken);
        RunReport second = runner.RunOnce(TestContext.Current.CancellationToken);

        Assert.Contains("broken.dcm", Assert.Single(first.Errors).Message);
        Assert.Empty(first.Written);
        Assert.Empty(second.Errors);
        Assert.Equal(SkipReason.PreviouslyFailed, Assert.Single(second.Skipped).Reason);

        File.Delete(corrupt);
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(SkipReason.Waiting, Assert.Single(runner.RunOnce(TestContext.Current.CancellationToken).Skipped).Reason);
        RunReport fixedRun = runner.RunOnce(TestContext.Current.CancellationToken);
        Assert.Equal(series.SeriesInstanceUid, Assert.Single(fixedRun.Written).SeriesInstanceUid);
    }

    [Fact]
    public void Delete_mode_deletes_only_generated_rts_and_writes_nothing()
    {
        using var folder = new TestFolder();
        string done = folder.Sub("Done");
        string nested = Directory.CreateDirectory(Path.Combine(done, "Nested")).FullName;
        string untouched = folder.Sub("NoRtYet");
        WrittenSeries doneSeries = SeriesFactory.Write(done, "a");
        WrittenSeries nestedSeries = SeriesFactory.Write(nested, "n");
        SeriesFactory.Write(untouched, "u");
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { done });
        RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);
        string doneRt = RunnerTemplates.OutputPath(done, "Brain_Test", doneSeries.SeriesInstanceUid);
        string nestedRt = RunnerTemplates.OutputPath(nested, "Brain_Test", nestedSeries.SeriesInstanceUid);
        Assert.True(File.Exists(doneRt) && File.Exists(nestedRt));
        // Now point the template at a folder without RTs as well; delete mode must not generate there.
        File.AppendAllLines(Path.Combine(templates, "Brain_Test", "Paths.txt"), new[] { untouched });
        // Files that only look similar are kept: another template's RT, a user file, a non-DICOM extension.
        string[] keep =
        {
            Path.Combine(done, "Other_UID1.2.3.dcm"),
            Path.Combine(done, "Brain_Test_UID_notes.dcm"),
            Path.Combine(done, "Brain_Test_UID1.2.3.txt"),
        };
        foreach (string file in keep)
        {
            File.WriteAllText(file, "keep");
        }

        string[] before = Directory.GetFiles(folder.Path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        DeleteReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).DeleteGenerated(TestContext.Current.CancellationToken);
        string[] after = Directory.GetFiles(folder.Path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(new[] { doneRt, nestedRt }.Order(StringComparer.Ordinal), report.Deleted.Order(StringComparer.Ordinal));
        Assert.Empty(report.Failed);
        Assert.Empty(report.Errors);
        Assert.Equal(before.Except(new[] { doneRt, nestedRt }), after);
        Assert.All(keep, file => Assert.True(File.Exists(file)));
    }

    [Fact]
    public void Delete_mode_can_be_limited_to_named_templates_and_reports_unknown_names()
    {
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        WrittenSeries series = SeriesFactory.Write(images, "a");
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "One", new[] { images });
        RunnerTemplates.Make(templates, "Two", new[] { images });
        RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunOnce(TestContext.Current.CancellationToken);

        DeleteReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled())
            .DeleteGenerated(new[] { "One", "Missing" }, TestContext.Current.CancellationToken);

        Assert.Equal(RunnerTemplates.OutputPath(images, "One", series.SeriesInstanceUid), Assert.Single(report.Deleted));
        Assert.Equal("Missing", Assert.Single(report.Errors).TemplateName);
        Assert.True(File.Exists(RunnerTemplates.OutputPath(images, "Two", series.SeriesInstanceUid)));
    }

    [Fact]
    public void Legacy_delete_rts_deletes_without_generating()
    {
        // delete_rts used to call walk_down_folders(true), which generated RTs in every folder without one.
        using var folder = new TestFolder();
        string images = folder.Sub("Images");
        SeriesFactory.Write(images, "a");
        string templates = folder.Sub("Templates");
        RunnerTemplates.Make(templates, "Brain_Test", new[] { images });

        new DicomTemplateRunner(templates).delete_rts();

        Assert.Empty(Directory.GetFiles(images, "Brain_Test_UID*"));
    }

    [Fact]
    public void Run_for_folder_processes_a_folder_that_was_just_written_without_touching_paths_txt()
    {
        using var folder = new TestFolder();
        string images = folder.Sub("Output");
        WrittenSeries series = SeriesFactory.Write(images, "ct");
        string templates = folder.Sub("Templates");
        string template = RunnerTemplates.Make(templates, "Brain_Test", Array.Empty<string>());
        string pathsBefore = File.ReadAllText(Path.Combine(template, "Paths.txt"));
        // The clock says the files were written this instant: RunForFolder does not wait for them to settle.
        var clock = new RunnerClock(DateTimeOffset.UtcNow);

        RunReport report = RunnerTemplates.Runner(templates, clock).RunForFolder("Brain_Test", images, TestContext.Current.CancellationToken);

        Assert.Empty(report.Errors);
        Assert.Equal(series.SeriesInstanceUid, Assert.Single(report.Written).SeriesInstanceUid);
        Assert.Equal(pathsBefore, File.ReadAllText(Path.Combine(template, "Paths.txt")));
    }

    [Fact]
    public void Run_for_folder_reports_an_unknown_template()
    {
        using var folder = new TestFolder();
        string templates = folder.Sub("Templates");

        RunReport report = RunnerTemplates.Runner(templates, RunnerClock.AfterFilesSettled()).RunForFolder("Nope", folder.Sub("Images"), TestContext.Current.CancellationToken);

        Assert.Equal("Nope", Assert.Single(report.Errors).TemplateName);
    }
}
