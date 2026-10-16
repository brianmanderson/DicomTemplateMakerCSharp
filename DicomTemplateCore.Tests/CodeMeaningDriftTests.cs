using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;
using Xunit;

namespace DicomTemplateCore.Tests;

/// <summary>
/// The snapshot gives one SNOMED CT code to many target names (228793007 to every PTV-like name) and uses the name
/// as Code Meaning. Opening and saving such a template must keep each ROI's own code; matching ontologies on the
/// code value alone gave every PTV the meaning of the first one.
/// </summary>
public class CodeMeaningDriftTests
{
    private const string PtvCode = "228793007";

    /// <summary>The first snapshot site (in name order) with at least two PTV names of different meaning on 228793007.</summary>
    private static (string Site, List<ROIClass> Rois) SiteWithSharedPtvCode()
    {
        foreach (string site in SnapshotSites.SiteNames)
        {
            List<ROIClass> rois = SnapshotSites.BuildRois(site);
            int meanings = rois.Where(r => r.Ontology_Class?.CodeValue == PtvCode).Select(r => r.Ontology_Class!.CodeMeaning).Distinct(StringComparer.Ordinal).Count();
            if (meanings >= 2)
            {
                return (site, rois);
            }
        }

        throw new InvalidOperationException("No snapshot site has two PTV names sharing " + PtvCode + ".");
    }

    private static List<(string Name, string Meaning, string? Value, string? Scheme)> Codes(IEnumerable<ROIClass> rois) =>
        rois.Select(r => (r.ROIName, r.Ontology_Class!.CodeMeaning, r.Ontology_Class.CodeValue, r.Ontology_Class.Scheme)).ToList();

    [Fact]
    public void Opening_and_saving_a_snapshot_template_keeps_each_rois_code_meaning()
    {
        using var root = new TestFolder();
        (string site, List<ROIClass> rois) = SiteWithSharedPtvCode();
        var expected = Codes(rois);
        string library = root.Sub("Ontologies");
        string template = Path.Combine(root.Sub("Templates"), site);
        SnapshotSites.MakeTemplate(template, library, rois);

        // Opened as the template windows open one: the library first, then the folder; then saved unchanged.
        var editor = new TemplateMaker { Ontologies = OntologyTools.LoadOntologiesFromFolder(library) };
        editor.set_onto_path(library);
        editor.define_path(template);
        editor.define_output(template);
        editor.categorize_folder();
        editor.make_template();

        Assert.Equal(expected, Codes(editor.ROIs));
        Assert.Equal(expected, Codes(ROIClassTools.ReadRoisFile(Path.Combine(template, ROIClassTools.RoisFileName))));
        Assert.True(expected.Count(c => c.Value == PtvCode) >= 2);
    }

    [Fact]
    public void Ontologies_keep_one_entry_per_meaning_code_and_scheme()
    {
        using var root = new TestFolder();
        (string site, List<ROIClass> rois) = SiteWithSharedPtvCode();
        string template = Path.Combine(root.Path, site);
        SnapshotSites.MakeTemplate(template, root.Sub("Ontologies"), rois);

        var editor = new TemplateMaker();
        editor.define_path(template);
        editor.categorize_folder();

        var distinct = rois.Select(r => (r.Ontology_Class!.CodeMeaning, r.Ontology_Class.CodeValue, r.Ontology_Class.Scheme)).Distinct().ToList();
        Assert.Equal(distinct.Order(), editor.Ontologies.Select(o => (o.CodeMeaning, o.CodeValue, o.Scheme)).Order());
        // Each ROI shares the in-memory entry with its own meaning, code and scheme.
        foreach (ROIClass roi in editor.ROIs)
        {
            Assert.Contains(editor.Ontologies, o => ReferenceEquals(o, roi.Ontology_Class));
        }
    }

    [Fact]
    public void Two_rois_with_one_code_value_and_different_meanings_keep_their_meanings_after_a_save()
    {
        using var root = new TestFolder();
        string template = root.Sub("Prostate");
        var rois = new List<ROIClass>
        {
            new ROIClass(255, 0, 0, "PTV_High", "PTV", new OntologyCodeClass("PTV_High", PtvCode, "SCT")),
            new ROIClass(0, 255, 0, "PTV_Low", "PTV", new OntologyCodeClass("PTV_Low", PtvCode, "SCT")),
        };
        var builder = new TemplateMaker { ROIs = rois };
        builder.define_output(template);
        builder.make_template();

        Assert.Equal(2, builder.Ontologies.Count);

        var editor = new TemplateMaker();
        editor.define_path(template);
        editor.define_output(template);
        editor.categorize_folder();
        editor.make_template();

        Assert.Equal(new[] { "PTV_High", "PTV_Low" }, ROIClassTools.ReadRoisFile(Path.Combine(template, ROIClassTools.RoisFileName)).Select(r => r.Ontology_Class!.CodeMeaning));
    }
}
