using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;
using TemplateSync.Model;
using TemplateSync.Storage;

namespace DicomTemplateCore.Tests;

/// <summary>
/// The sites of the published template snapshot (TemplateSnapshots/TG263.json), built into ROIs the way the online
/// templates window builds them: snapshot records, <see cref="TemplateIndex"/>, <see cref="TemplateEntryMapper"/>.
/// </summary>
internal static class SnapshotSites
{
    private static readonly Lazy<TemplateIndex> LazyIndex = new Lazy<TemplateIndex>(() =>
    {
        string path = TestFolder.Data("TG263.json");
        TableSnapshot snapshot = SnapshotStore.TryLoadFile(path) ?? throw new InvalidOperationException(path + " could not be read.");
        return TemplateIndex.Build(snapshot.Records.Select(AirTableEntry.FromRecord));
    });

    public static TemplateIndex Index => LazyIndex.Value;

    public static IEnumerable<string> SiteNames => Index.SiteNames.Order(StringComparer.Ordinal);

    /// <summary>Fresh ROIs of <paramref name="site"/>, in table order; entries the mapper skips are left out.</summary>
    public static List<ROIClass> BuildRois(string site)
    {
        var warnings = new List<string>();
        return Index.GetSite(site)
            .Select(siteRoi => TemplateEntryMapper.ToRoiWrapper(siteRoi, warnings))
            .OfType<ROIWrapper>()
            .Select(wrapper => wrapper.roi)
            .ToList();
    }

    /// <summary>Saves <paramref name="rois"/> as template <paramref name="folder"/> with make_template, as the online templates window does.</summary>
    public static void MakeTemplate(string folder, string ontologyLibrary, List<ROIClass> rois)
    {
        var builder = new TemplateMaker { ROIs = rois };
        builder.set_onto_path(ontologyLibrary);
        builder.define_output(folder);
        builder.make_template();
    }
}
