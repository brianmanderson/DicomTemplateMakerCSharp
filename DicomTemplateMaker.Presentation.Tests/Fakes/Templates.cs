using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;

namespace DicomTemplateMaker.Presentation.Tests.Fakes;

/// <summary>Template folders on disk, written with the real TemplateMaker.</summary>
public static class Templates
{
    public static List<ROIClass> DefaultRois() => new()
    {
        new ROIClass(255, 0, 0, "Brain", "ORGAN", new OntologyCodeClass("Brain", "50801", "FMA")),
        new ROIClass(0, 255, 0, "PTV_High", "PTV", new OntologyCodeClass("PTV_High", "PTV_High", "99VMS_STRUCTCODE")),
    };

    /// <summary>Writes template <paramref name="name"/> under <paramref name="templateRoot"/> and returns its folder.</summary>
    public static string Make(string templateRoot, string name, IEnumerable<string>? paths = null, IEnumerable<ROIClass>? rois = null, bool updateLibrary = true)
    {
        var maker = new TemplateMaker();
        string folder = Path.Combine(templateRoot, name);
        maker.define_output(folder);
        if (updateLibrary)
        {
            maker.set_onto_path(Path.Combine(templateRoot, "Ontologies"));
        }

        maker.Paths.AddRange(paths ?? Array.Empty<string>());
        maker.ROIs.AddRange(rois ?? DefaultRois());
        maker.make_template();
        return folder;
    }

    /// <summary>A file of the repository (the tests read the files that ship with the program).</summary>
    public static string RepositoryFile(params string[] parts)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DicomTemplateMaker.sln")))
            {
                return Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            }
        }

        throw new InvalidOperationException("The repository root (DicomTemplateMaker.sln) was not found above " + AppContext.BaseDirectory);
    }
}
