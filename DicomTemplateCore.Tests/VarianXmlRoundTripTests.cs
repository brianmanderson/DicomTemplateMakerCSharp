using System.Xml.Linq;
using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;
using Xunit;

namespace DicomTemplateCore.Tests;

/// <summary>
/// Characterisation tests for Varian Eclipse structure-template import/export. The golden files were
/// captured from the code as it was moved into DicomTemplateCore, before any other change, so any
/// later change in output shows up here.
/// </summary>
public class VarianXmlRoundTripTests
{
    public static TheoryData<string, string> Samples => new()
    {
        { "Structure Template.xml", "StructureTemplate" },
        { "Abdomen.xml", "Abdomen" },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void Importing_a_structure_template_produces_the_same_template_folder(string sample, string golden)
    {
        using var folder = new TestFolder();

        new VarianXmlReader(TestFolder.Data(sample)).XmlToROI(folder.Path);

        string templateDir = Directory.GetDirectories(folder.Path).Single(d => System.IO.Path.GetFileName(d) != "Ontologies");
        Golden.AssertMatches(golden + ".import.golden.json", NormalizeJson(File.ReadAllText(System.IO.Path.Combine(templateDir, "All_ROIs.json"))));
        Assert.True(File.Exists(System.IO.Path.Combine(folder.Path, "Ontologies", "All_Ontologies.json")));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Round_trip_back_to_varian_xml_produces_the_same_structures(string sample, string golden)
    {
        using var folder = new TestFolder();
        new VarianXmlReader(TestFolder.Data(sample)).XmlToROI(folder.Path);
        string templateDir = Directory.GetDirectories(folder.Path).Single(d => System.IO.Path.GetFileName(d) != "Ontologies");
        List<OntologyCodeClass> ontologies = OntologyTools.LoadOntologiesFromFolder(System.IO.Path.Combine(folder.Path, "Ontologies"));

        var writer = new VarianXmlWriter();
        writer.LoadROIsFromPath(templateDir, ontologies);
        string output = System.IO.Path.Combine(folder.Path, "out.xml");
        writer.SaveFile(output);

        Golden.AssertMatches(golden + ".roundtrip.golden.xml", NormalizeXml(output));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Round_trip_keeps_every_structure_id_from_the_original(string sample, string golden)
    {
        _ = golden;
        using var folder = new TestFolder();
        new VarianXmlReader(TestFolder.Data(sample)).XmlToROI(folder.Path);
        string templateDir = Directory.GetDirectories(folder.Path).Single(d => System.IO.Path.GetFileName(d) != "Ontologies");
        var writer = new VarianXmlWriter();
        writer.LoadROIsFromPath(templateDir, OntologyTools.LoadOntologiesFromFolder(System.IO.Path.Combine(folder.Path, "Ontologies")));
        string output = System.IO.Path.Combine(folder.Path, "out.xml");
        writer.SaveFile(output);

        IEnumerable<string> Ids(string path) => XDocument.Load(path).Root!.Element("Structures")!.Elements().Select(e => (string)e.Attribute("ID")!).Where(id => id.Length > 0).OrderBy(id => id, StringComparer.Ordinal);
        Assert.Equal(Ids(TestFolder.Data(sample)), Ids(output));
    }

    /// <summary>Removes the parts that legitimately differ per run (user name and timestamps in Preview).</summary>
    private static string NormalizeXml(string path)
    {
        XDocument doc = XDocument.Load(path);
        XElement? preview = doc.Root!.Element("Preview");
        if (preview != null)
        {
            foreach (string attribute in new[] { "ApprovalHistory", "AssignedUsers", "LastModified" })
            {
                preview.SetAttributeValue(attribute, "normalized");
            }
        }

        return doc.ToString() + "\n";
    }

    private static string NormalizeJson(string json) => json.ReplaceLineEndings("\n").TrimEnd() + "\n";
}
