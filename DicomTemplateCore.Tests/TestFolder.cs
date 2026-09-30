namespace DicomTemplateCore.Tests;

public sealed class TestFolder : IDisposable
{
    public TestFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DicomTemplateCoreTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Sub(string name)
    {
        string path = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    public static string Data(params string[] parts) => System.IO.Path.Combine(new[] { AppContext.BaseDirectory, "TestData" }.Concat(parts).ToArray());
}

/// <summary>Golden-file helper. Set UPDATE_GOLDEN=1 to (re)write the expected files from the current output.</summary>
public static class Golden
{
    public static void AssertMatches(string name, string actual)
    {
        string sourcePath = System.IO.Path.Combine(ProjectDirectory(), "TestData", name);
        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            File.WriteAllText(sourcePath, actual);
            return;
        }

        string expected = File.ReadAllText(TestFolder.Data(name));
        Xunit.Assert.Equal(expected.ReplaceLineEndings("\n"), actual.ReplaceLineEndings("\n"));
    }

    private static string ProjectDirectory()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(System.IO.Path.Combine(dir, "DicomTemplateCore.Tests.csproj")))
        {
            dir = System.IO.Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Test project folder not found.");
    }
}
