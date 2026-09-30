using DicomTemplateMaker.Presentation.Tests.Fakes;
using DicomTemplateMakerGUI.Services;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class TemplateRootResolverTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Program => folder.Sub("program");

    private string Working => folder.Sub("working");

    [Fact]
    public void An_existing_saved_folder_wins()
    {
        string saved = folder.Sub("saved");
        Directory.CreateDirectory(Path.Combine(Working, "Ontologies"));

        TemplateRootResolution resolution = TemplateRootResolver.Resolve(saved + Path.DirectorySeparatorChar, Working, Program);

        Assert.Equal(saved, resolution.Root);
        Assert.Equal(TemplateRootSource.Saved, resolution.Source);
        Assert.False(resolution.ShouldSave);
        Assert.Null(resolution.Note);
    }

    [Fact]
    public void Without_a_saved_folder_the_program_folder_is_used()
    {
        TemplateRootResolution resolution = TemplateRootResolver.Resolve(null, Working, Program);

        Assert.Equal(Program, resolution.Root);
        Assert.Equal(TemplateRootSource.ProgramFolder, resolution.Source);
        Assert.False(resolution.ShouldSave);
    }

    [Fact]
    public void A_working_directory_with_an_ontologies_folder_is_used_and_saved()
    {
        Directory.CreateDirectory(Path.Combine(Working, "Ontologies"));

        TemplateRootResolution resolution = TemplateRootResolver.Resolve(null, Working, Program);

        Assert.Equal(Working, resolution.Root);
        Assert.Equal(TemplateRootSource.WorkingDirectory, resolution.Source);
        Assert.True(resolution.ShouldSave);
        Assert.Null(resolution.Note);
    }

    [Theory]
    [InlineData("All_ROIs.json", false)]
    [InlineData("Paths.txt", false)]
    [InlineData("ROIs", true)]
    public void A_working_directory_with_a_template_subfolder_is_used(string entry, bool isFolder)
    {
        string template = Path.Combine(Working, "Brain");
        Directory.CreateDirectory(template);
        if (isFolder)
        {
            Directory.CreateDirectory(Path.Combine(template, entry));
        }
        else
        {
            File.WriteAllText(Path.Combine(template, entry), "[]");
        }

        TemplateRootResolution resolution = TemplateRootResolver.Resolve(null, Working, Program);

        Assert.Equal(Working, resolution.Root);
        Assert.True(resolution.ShouldSave);
    }

    [Fact]
    public void A_working_directory_with_unrelated_folders_is_not_used()
    {
        Directory.CreateDirectory(Path.Combine(Working, "Photos"));
        File.WriteAllText(Path.Combine(Working, "Photos", "readme.txt"), "x");

        Assert.Equal(Program, TemplateRootResolver.Resolve(null, Working, Program).Root);
    }

    [Fact]
    public void The_program_folder_wins_when_both_hold_templates()
    {
        Directory.CreateDirectory(Path.Combine(Working, "Ontologies"));
        Directory.CreateDirectory(Path.Combine(Program, "Ontologies"));

        TemplateRootResolution resolution = TemplateRootResolver.Resolve(null, Working, Program);

        Assert.Equal(Program, resolution.Root);
        Assert.False(resolution.ShouldSave);
    }

    [Fact]
    public void The_same_folder_as_working_directory_and_program_folder_is_the_program_folder()
    {
        Directory.CreateDirectory(Path.Combine(Program, "Ontologies"));

        TemplateRootResolution resolution = TemplateRootResolver.Resolve(null, Program + Path.DirectorySeparatorChar, Program);

        Assert.Equal(Program, resolution.Root);
        Assert.Equal(TemplateRootSource.ProgramFolder, resolution.Source);
    }

    [Fact]
    public void A_missing_saved_folder_is_reported_and_not_replaced()
    {
        string missing = Path.Combine(folder.Path, "disconnected");
        Directory.CreateDirectory(Path.Combine(Working, "Ontologies"));

        TemplateRootResolution resolution = TemplateRootResolver.Resolve(missing, Working, Program);

        Assert.Equal(Working, resolution.Root);
        Assert.False(resolution.ShouldSave);
        Assert.NotNull(resolution.Note);
        Assert.Contains(missing, resolution.Note, StringComparison.Ordinal);
        Assert.Contains(Working, resolution.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void A_saved_folder_that_is_not_a_valid_path_falls_back()
    {
        TemplateRootResolution resolution = TemplateRootResolver.Resolve("bad\0path", Working, Program);

        Assert.Equal(Program, resolution.Root);
        Assert.NotNull(resolution.Note);
    }

    [Fact]
    public void Layout_detection_is_false_for_a_missing_folder()
    {
        Assert.False(TemplateRootResolver.HasTemplateLayout(Path.Combine(folder.Path, "nothing")));
    }
}
