using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;
using Xunit;
using static DicomTemplateCore.Tests.TemplateTestData;

namespace DicomTemplateCore.Tests;

/// <summary>
/// Migration of legacy text-file templates (one ROIs/*.txt file per ROI) to All_ROIs.json, on verbatim copies of
/// real legacy files. The ROIs folder is only deleted when every file in it was migrated.
/// </summary>
public class TemplateLegacyMigrationTests
{
    private static readonly string[] RealRois = { "BODY", "Bladder", "CTV_High", "GTVp" };

    private static string LegacyTemplate(TestFolder folder)
    {
        string template = Path.Combine(folder.Path, "AbdPelv_Anal");
        CopyFolder(TestFolder.Data("Legacy", "AbdPelv_Anal"), template);
        return template;
    }

    [Fact]
    public void Real_legacy_template_is_migrated_and_its_rois_folder_removed()
    {
        using var folder = new TestFolder();
        string template = LegacyTemplate(folder);
        var warnings = new List<string>();

        List<ROIClass> rois = ROIClassTools.LoadROIsFromFolder(template, new List<OntologyCodeClass>(), warnings);

        Assert.Equal(RealRois, rois.Select(r => r.ROIName).OrderBy(n => n, StringComparer.Ordinal));
        ROIClass bladder = rois.Single(r => r.ROIName == "Bladder");
        Assert.Equal(((byte)255, (byte)255, (byte)0), (bladder.R, bladder.G, bladder.B));
        Assert.Equal("ORGAN", bladder.ROI_Interpreted_type);
        Assert.True(bladder.Include);
        Assert.NotNull(bladder.Ontology_Class);
        Assert.Equal(("Bladder", "15900", "FMA", "VMS011"), (bladder.Ontology_Class.CodeMeaning, bladder.Ontology_Class.CodeValue, bladder.Ontology_Class.Scheme, bladder.Ontology_Class.ContextIdentifier));
        Assert.Equal("99VMS_STRUCTCODE", rois.Single(r => r.ROIName == "GTVp").Ontology_Class!.Scheme);
        Assert.Empty(warnings);
        Assert.Equal(new[] { "All_ROIs.json", "Paths.txt" }, Files(template));
        Assert.Equal(RealRois, ROIClassTools.LoadROIsFromFolder(template, new List<OntologyCodeClass>()).Select(r => r.ROIName).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Legacy_file_with_include_flag_and_eclipse_settings_is_read()
    {
        using var folder = new TestFolder();
        WriteFile(folder.Path, "ROIs/Rectum.txt", "139\\69\\19\nRectum\\14544\\FMA\nORGAN\nFalse\n3\\Segment\\-------\\255\\2");

        ROIClass rectum = Assert.Single(ROIClassTools.LoadROIsFromFolder(folder.Path, new List<OntologyCodeClass>()));

        Assert.False(rectum.Include);
        Assert.Equal(("3", "Segment", "-------", "255", "2"), (rectum.TypeIndex, rectum.ContourStyle, rectum.DVHLineStyle, rectum.DVHLineColor, rectum.DVHLineWidth));
        Assert.Equal(((byte)255, (byte)0, (byte)0), (rectum.R_DVH, rectum.G_DVH, rectum.B_DVH));
    }

    [Fact]
    public void Migration_with_unreadable_files_keeps_the_rois_folder_and_reports_them()
    {
        using var folder = new TestFolder();
        string template = LegacyTemplate(folder);
        WriteFile(template, "ROIs/Broken.txt", "red\\0\\0\nBroken\\1\\FMA\nORGAN");
        WriteFile(template, "ROIs/Short.txt", "255\\0\\0\nShort\\2\\FMA");
        var warnings = new List<string>();

        List<ROIClass> rois = ROIClassTools.LoadROIsFromFolder(template, new List<OntologyCodeClass>(), warnings);

        Assert.Equal(RealRois, rois.Select(r => r.ROIName).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(RealRois, ROIClassTools.LoadROIsFromFolder(template, new List<OntologyCodeClass>()).Select(r => r.ROIName).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(new[] { "All_ROIs.json", "Paths.txt", "ROIs/BODY.txt", "ROIs/Bladder.txt", "ROIs/Broken.txt", "ROIs/CTV_High.txt", "ROIs/GTVp.txt", "ROIs/Short.txt" }, Files(template));
        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, w => w.Contains("Broken.txt") && w.Contains("kept"));
        Assert.Contains(warnings, w => w.Contains("Short.txt"));
    }

    [Fact]
    public void Saving_after_a_partial_migration_keeps_the_legacy_folder()
    {
        using var folder = new TestFolder();
        string template = LegacyTemplate(folder);
        WriteFile(template, "ROIs/Broken.txt", "red\\0\\0\nBroken\\1\\FMA\nORGAN");
        List<ROIClass> rois = ROIClassTools.LoadROIsFromFolder(template, new List<OntologyCodeClass>());

        rois.RemoveAt(0);
        ROIClassTools.SaveROIsToFolder(rois, template);

        Assert.True(File.Exists(Path.Combine(template, "ROIs", "Broken.txt")));
        Assert.Equal(5, Directory.GetFiles(Path.Combine(template, "ROIs")).Length);
    }

    [Fact]
    public void Template_whose_legacy_files_are_all_unreadable_throws_and_writes_nothing()
    {
        using var folder = new TestFolder();
        WriteFile(folder.Path, "ROIs/Broken.txt", "red\\0\\0\nBroken\\1\\FMA\nORGAN");
        WriteFile(folder.Path, "ROIs/Short.txt", "255\\0\\0");

        var ex = Assert.Throws<TemplateLoadException>(() => ROIClassTools.LoadROIsFromFolder(folder.Path, new List<OntologyCodeClass>()));

        Assert.Equal(Path.Combine(folder.Path, "ROIs"), ex.FilePath);
        Assert.Contains("none of its 2", ex.Message);
        Assert.Equal(new[] { "ROIs/Broken.txt", "ROIs/Short.txt" }, Files(folder.Path));
    }

    [Fact]
    public void Categorize_folder_lists_files_it_could_not_migrate()
    {
        using var folder = new TestFolder();
        string template = LegacyTemplate(folder);
        WriteFile(template, "ROIs/Broken.txt", "red\\0\\0\nBroken\\1\\FMA\nORGAN");
        var maker = new TemplateMaker();
        maker.define_path(template);
        maker.define_output(template);

        maker.categorize_folder();

        Assert.True(maker.is_template);
        Assert.Equal(4, maker.ROIs.Count);
        Assert.Equal(new[] { "O:\\DICOM\\BMA_Export\\Single_Image" }, maker.Paths);
        Assert.Contains("Broken.txt", Assert.Single(maker.LoadWarnings));
        Assert.True(File.Exists(Path.Combine(template, "ROIs", "Broken.txt")));
    }

    [Fact]
    public void A_template_whose_legacy_files_were_kept_reports_them_on_every_later_load()
    {
        // Once All_ROIs.json exists, the kept files used to be reported by the migrating read only.
        using var folder = new TestFolder();
        string template = LegacyTemplate(folder);
        WriteFile(template, "ROIs/Broken.txt", "red\\0\\0\nBroken\\1\\FMA\nORGAN");
        ROIClassTools.LoadROIsFromFolder(template, new List<OntologyCodeClass>(), new List<string>());
        var later = new List<string>();

        List<ROIClass> rois = ROIClassTools.LoadROIsFromFolder(template, new List<OntologyCodeClass>(), later);

        Assert.Equal(4, rois.Count);
        string warning = Assert.Single(later);
        Assert.Contains("5 legacy ROI file(s)", warning, StringComparison.Ordinal);
        Assert.Contains("Broken.txt", warning, StringComparison.Ordinal);
        Assert.Contains("remove the ROIs folder", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Two_readers_migrating_the_same_template_at_once_never_lose_rois()
    {
        // The GUI's template scan and the RT generator read templates at the same time. A reader that had read only some
        // files when the other deleted them used to save its partial list over the complete All_ROIs.json.
        const int roiCount = 60;
        var random = new Random(20260928);
        for (int run = 0; run < 100; run++)
        {
            using var folder = new TestFolder();
            for (int i = 0; i < roiCount; i++)
            {
                WriteFile(folder.Path, $"ROIs/ROI_{i:00}.txt", $"255\\0\\0\nROI_{i:00}\\{1000 + i}\\FMA\nORGAN");
            }

            // The second reader starts a little later each time, so that some runs catch the first one deleting.
            int stagger = random.Next(0, 400_000);
            using var start = new Barrier(2);
            List<ROIClass> Read(int spin)
            {
                start.SignalAndWait(TestContext.Current.CancellationToken);
                Thread.SpinWait(spin);
                return ROIClassTools.LoadROIsFromFolder(folder.Path, new List<OntologyCodeClass>());
            }

            List<ROIClass>[] results = await Task.WhenAll(
                Task.Run(() => Read(0), TestContext.Current.CancellationToken),
                Task.Run(() => Read(stagger), TestContext.Current.CancellationToken));

            Assert.All(results, r => Assert.Equal(roiCount, r.Count));
            Assert.Equal(roiCount, ROIClassTools.LoadROIsFromFolder(folder.Path, new List<OntologyCodeClass>()).Count);
            Assert.DoesNotContain(Files(folder.Path), f => f.EndsWith(".tmp", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Two_readers_migrating_the_same_ontology_library_at_once_never_lose_entries()
    {
        const int entryCount = 60;
        var random = new Random(20260928);
        for (int run = 0; run < 100; run++)
        {
            using var folder = new TestFolder();
            for (int i = 0; i < entryCount; i++)
            {
                WriteFile(folder.Path, $"Structure_{i:00}.txt", $"{2000 + i}\nFMA");
            }

            int stagger = random.Next(0, 400_000);
            using var start = new Barrier(2);
            List<OntologyCodeClass> Read(int spin)
            {
                start.SignalAndWait(TestContext.Current.CancellationToken);
                Thread.SpinWait(spin);
                return OntologyTools.LoadOntologiesFromFolder(folder.Path);
            }

            List<OntologyCodeClass>[] results = await Task.WhenAll(
                Task.Run(() => Read(0), TestContext.Current.CancellationToken),
                Task.Run(() => Read(stagger), TestContext.Current.CancellationToken));

            Assert.All(results, r => Assert.Equal(entryCount, r.Count));
            Assert.Equal(entryCount, OntologyTools.LoadOntologiesFromFolder(folder.Path).Count);
        }
    }
}
