using DicomTemplateMakerGUI.DicomTemplateServices;
using DicomTemplateMakerGUI.Services;
using Xunit;

namespace DicomTemplateCore.Tests;

/// <summary>Data files are found in the program folder, not the working directory.</summary>
public class RunnerProgramFolderTests
{
    [Fact]
    public void The_default_template_rt_is_in_the_program_folder()
    {
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "template_RS.dcm"), DicomTemplateRunner.DefaultTemplateRsPath);
        Assert.True(File.Exists(DicomTemplateRunner.DefaultTemplateRsPath));
    }

    [Fact]
    public void The_fma_snomed_key_is_read_from_the_program_folder()
    {
        // It was ".\FMA_SNOMEDCT_Key.txt": the working directory on Windows, a file named with a backslash elsewhere.
        string path = Path.Combine(AppContext.BaseDirectory, "FMA_SNOMEDCT_Key.txt");
        bool created = !File.Exists(path);
        if (created)
        {
            File.WriteAllLines(path, new[] { "FMAID,SNOMED", "50801,12738006", "missing,123456" });
        }

        try
        {
            var key = new FMAID_SNOMED_OntologyClass();

            Assert.Equal(path, key.path);
            Assert.Equal(path, FMAID_SNOMED_OntologyClass.DefaultPath);
            Assert.NotEmpty(key.FMA_to_SNOMED);
            if (created)
            {
                Assert.Equal("12738006", key.FMA_to_SNOMED["50801"]);
                Assert.Equal("50801", key.SNOMED_To_FMA["12738006"]);
                Assert.False(key.SNOMED_To_FMA.ContainsKey("123456"));
            }
        }
        finally
        {
            if (created)
            {
                File.Delete(path);
            }
        }
    }
}
