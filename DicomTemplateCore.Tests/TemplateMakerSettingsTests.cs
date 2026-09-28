using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;
using Xunit;
using static DicomTemplateCore.Tests.TemplateTestData;

namespace DicomTemplateCore.Tests;

/// <summary>
/// make_template and a template's monitored folders (Paths.txt) and DICOM requirements (DicomTags.txt): a rebuild
/// from an online source, a Varian XML file or an RT file keeps them; an edit of the loaded template can clear them.
/// </summary>
public class TemplateMakerSettingsTests
{
    private static readonly string[] ExistingPaths = { @"\\server\images\CT", @"D:\Pelvis" };
    private static readonly string[] ExistingTags = { "Series Description\\CT Abdomen\\CT Pelvis", "Study Description\\Pelvis" };

    /// <summary>An existing template folder with paths and DICOM requirements.</summary>
    private static string ExistingTemplate(TestFolder root)
    {
        string template = Template(root.Sub("AbdPelv_Anal"), Roi("Rectum", "14544"));
        File.WriteAllLines(Path.Combine(template, "Paths.txt"), ExistingPaths);
        File.WriteAllLines(Path.Combine(template, "DicomTags.txt"), ExistingTags);
        return template;
    }

    /// <summary>A builder as the online-template, Varian XML and default builders make one: never loaded, no paths or requirements.</summary>
    private static TemplateMaker Builder(string template)
    {
        var builder = new TemplateMaker();
        builder.define_output(template);
        builder.ROIs.Add(Roi("Bladder", "15900"));
        return builder;
    }

    [Fact]
    public void Rebuilding_a_template_keeps_its_paths_and_dicom_requirements_when_the_builder_has_none()
    {
        using var root = new TestFolder();
        string template = ExistingTemplate(root);
        byte[] paths = File.ReadAllBytes(Path.Combine(template, "Paths.txt"));
        byte[] tags = File.ReadAllBytes(Path.Combine(template, "DicomTags.txt"));
        TemplateMaker builder = Builder(template);

        builder.make_template();

        Assert.Equal(paths, File.ReadAllBytes(Path.Combine(template, "Paths.txt")));
        Assert.Equal(tags, File.ReadAllBytes(Path.Combine(template, "DicomTags.txt")));
        Assert.Equal(new[] { "Bladder" }, ROIClassTools.LoadROIsFromFolder(template, new List<OntologyCodeClass>()).Select(r => r.ROIName));
        // The builder now holds what is on disk, so a later save from it writes the same settings.
        Assert.Equal(ExistingPaths, builder.Paths);
        Assert.Equal(new[] { "CT Abdomen", "CT Pelvis" }, builder.DicomTags["Series Description"]);
        builder.make_template();
        Assert.Equal(paths, File.ReadAllBytes(Path.Combine(template, "Paths.txt")));
    }

    [Fact]
    public void Paths_and_requirements_of_the_builder_replace_the_existing_ones()
    {
        using var root = new TestFolder();
        string template = ExistingTemplate(root);
        TemplateMaker builder = Builder(template);
        builder.Paths.Add(@"E:\New");
        builder.DicomTags.Add("Series Description", new List<string> { "MR" });

        builder.make_template();

        Assert.Equal(new[] { @"E:\New" }, File.ReadAllLines(Path.Combine(template, "Paths.txt")));
        Assert.Equal(new[] { "Series Description\\MR" }, File.ReadAllLines(Path.Combine(template, "DicomTags.txt")));
    }

    [Fact]
    public void Each_file_is_kept_or_written_on_its_own()
    {
        using var root = new TestFolder();
        string template = ExistingTemplate(root);
        byte[] tags = File.ReadAllBytes(Path.Combine(template, "DicomTags.txt"));
        TemplateMaker builder = Builder(template);
        builder.Paths.Add(@"E:\New");

        builder.make_template();

        Assert.Equal(new[] { @"E:\New" }, File.ReadAllLines(Path.Combine(template, "Paths.txt")));
        Assert.Equal(tags, File.ReadAllBytes(Path.Combine(template, "DicomTags.txt")));
    }

    [Fact]
    public void Paths_and_requirements_cleared_in_a_loaded_template_are_cleared_on_disk()
    {
        using var root = new TestFolder();
        string template = ExistingTemplate(root);
        var maker = new TemplateMaker();
        maker.define_path(template);
        maker.define_output(template);
        maker.categorize_folder();
        Assert.Equal(ExistingPaths, maker.Paths);

        maker.Paths.Clear();
        maker.DicomTags.Clear();
        maker.make_template();

        Assert.Equal(0, new FileInfo(Path.Combine(template, "Paths.txt")).Length);
        Assert.Equal(0, new FileInfo(Path.Combine(template, "DicomTags.txt")).Length);
    }

    [Fact]
    public void A_new_template_can_clear_the_paths_it_saved_earlier()
    {
        using var root = new TestFolder();
        string template = Path.Combine(root.Path, "New_Template");
        var maker = new TemplateMaker();
        maker.define_output(template);
        maker.make_template();
        maker.Paths.Add(@"C:\Images");
        maker.DicomTags.Add("Study Description", new List<string> { "Brain" });
        maker.make_template();
        Assert.Equal(new[] { @"C:\Images" }, File.ReadAllLines(Path.Combine(template, "Paths.txt")));

        maker.Paths.Clear();
        maker.DicomTags.Clear();
        maker.make_template();

        Assert.Empty(File.ReadAllLines(Path.Combine(template, "Paths.txt")));
        Assert.Empty(File.ReadAllLines(Path.Combine(template, "DicomTags.txt")));
    }

    [Fact]
    public void Settings_files_keep_their_format()
    {
        using var root = new TestFolder();
        string template = Path.Combine(root.Path, "Format");
        var maker = new TemplateMaker();
        maker.define_output(template);
        maker.Paths.AddRange(new[] { @"C:\Images", @"\\server\share" });
        maker.DicomTags.Add("Series Description", new List<string> { "CT", "CBCT" });
        maker.DicomTags.Add("Study Description", new List<string>());

        maker.make_template();

        string nl = Environment.NewLine;
        Assert.Equal(@"C:\Images" + nl + @"\\server\share" + nl, File.ReadAllText(Path.Combine(template, "Paths.txt")));
        Assert.Equal("Series Description\\CT\\CBCT" + nl + "Study Description" + nl, File.ReadAllText(Path.Combine(template, "DicomTags.txt")));
    }

    [Fact]
    public void TemplateExists_is_true_when_make_template_would_replace_something()
    {
        using var root = new TestFolder();

        Assert.False(TemplateMaker.TemplateExists(Path.Combine(root.Path, "Missing")));
        Assert.False(TemplateMaker.TemplateExists(root.Sub("Empty")));
        Assert.True(TemplateMaker.TemplateExists(Template(root.Sub("Json"), Roi("Bladder", "15900"))));
        Assert.True(TemplateMaker.TemplateExists(Path.GetDirectoryName(WriteFile(root.Path, "PathsOnly/Paths.txt", "C:\\Images"))!));
        Assert.True(TemplateMaker.TemplateExists(Path.GetDirectoryName(WriteFile(root.Path, "TagsOnly/DicomTags.txt", "Study Description\\Brain"))!));
        Assert.True(TemplateMaker.TemplateExists(Path.GetDirectoryName(Path.GetDirectoryName(WriteFile(root.Path, "Legacy/ROIs/Bladder.txt", "255\\0\\0\nBladder\\15900\\FMA\nORGAN")))!));
        Assert.True(TemplateMaker.TemplateExists(Path.GetDirectoryName(WriteFile(root.Path, "Corrupt/All_ROIs.json", TruncatedJson))!));
    }

    [Fact]
    public void Repeated_dicom_tags_keys_keep_every_value_through_a_save()
    {
        // The runner applies every line; the GUI kept only the first, so any save dropped the others.
        using var root = new TestFolder();
        string template = Template(root.Sub("Prostate"), Roi("Rectum", "14544"));
        File.WriteAllLines(Path.Combine(template, "DicomTags.txt"), new[] { "Series Description\\PELVIS", "Series Description\\PROSTATE", "Study Description\\Pelvis" });
        var maker = new TemplateMaker();
        maker.define_path(template);
        maker.define_output(template);
        maker.categorize_folder();

        Assert.Equal(new[] { "PELVIS", "PROSTATE" }, maker.DicomTags["Series Description"]);
        maker.ROIs[0].R = 12;
        maker.make_template();

        Assert.Equal(new[] { "Series Description\\PELVIS\\PROSTATE", "Study Description\\Pelvis" }, File.ReadAllLines(Path.Combine(template, "DicomTags.txt")));
        DicomTemplateMakerGUI.DicomTemplateServices.TemplateRequirements requirements =
            DicomTemplateMakerGUI.DicomTemplateServices.TemplateRequirements.Parse(File.ReadAllLines(Path.Combine(template, "DicomTags.txt")));
        Assert.Equal(new[] { "PELVIS", "PROSTATE" }, requirements.SeriesDescriptions);
    }
}
