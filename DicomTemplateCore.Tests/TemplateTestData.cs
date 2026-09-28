using ROIOntologyClass;

namespace DicomTemplateCore.Tests;

/// <summary>Builders for the template-folder, ontology-library and Varian report tests.</summary>
internal static class TemplateTestData
{
    /// <summary>What an All_ROIs.json looks like after a write that stopped halfway.</summary>
    public const string TruncatedJson = "[\n  {\n    \"color_string\": \"255\\\\0\\\\0\",\n    \"ROIName\": \"Bladder\",\n    \"R\": 25";

    /// <summary>Writes a file; <paramref name="relativePath"/> uses '/', and the returned path the platform separator.</summary>
    public static string WriteFile(string folder, string relativePath, string content)
    {
        string path = Path.Combine(folder, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public static ROIClass Roi(string name, string code, string scheme = "FMA", string? type = "ORGAN")
    {
        return new ROIClass(255, 0, 0, name, type, new OntologyCodeClass(name, code, scheme));
    }

    /// <summary>A template folder whose All_ROIs.json holds <paramref name="rois"/>, as the program saves it.</summary>
    public static string Template(string folder, params ROIClass[] rois)
    {
        ROIClassTools.SaveROIsToFolder(rois.ToList(), folder);
        return folder;
    }

    public static void CopyFolder(string source, string target)
    {
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }

    /// <summary>Every file under <paramref name="folder"/>, relative and sorted, to show what was written or deleted.</summary>
    public static List<string> Files(string folder)
    {
        return Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(folder, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
    }
}
