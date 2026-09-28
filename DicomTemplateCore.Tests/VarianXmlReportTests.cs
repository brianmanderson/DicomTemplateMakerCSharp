using System.Xml.Linq;
using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;
using Xunit;
using static DicomTemplateCore.Tests.TemplateTestData;

namespace DicomTemplateCore.Tests;

/// <summary>
/// Import and export reports for Varian structure templates: every structure is either converted or listed with
/// the reason it was left out, and a file that cannot be converted at all fails without writing anything.
/// </summary>
public class VarianXmlReportTests
{
    /// <param name="code">The structure code; null for a structure without a StructureCode element.</param>
    private static string Structure(string id, string? typeIndex = "2", string color = "RGB255  0  0", string? code = "15900")
    {
        string type = typeIndex == null ? string.Empty : $"<TypeIndex>{typeIndex}</TypeIndex>";
        string structureCode = code == null ? string.Empty : $"<StructureCode Code=\"{code}\" CodeScheme=\"FMA\" CodeSchemeVersion=\"3.2\" />";
        return $"<Structure ID=\"{id}\" Name=\"{id}\"><Identification><VolumeID /><VolumeCode /><VolumeType>Organ</VolumeType><VolumeCodeTable />"
            + $"{structureCode}</Identification>{type}<ColorAndStyle>{color}</ColorAndStyle>"
            + "<SearchCTLow xsi:nil=\"true\" /><SearchCTHigh xsi:nil=\"true\" /><DVHLineStyle>0</DVHLineStyle><DVHLineColor>-16777216</DVHLineColor>"
            + "<DVHLineWidth>1</DVHLineWidth></Structure>";
    }

    private static string StructureTemplate(TestFolder folder, string previewId, params string[] structures)
    {
        return WriteFile(folder.Path, "input/template.xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><StructureTemplate Version=\"1.2\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
            + $"<Preview ID=\"{previewId}\" Type=\"Structure\" /><Structures>{string.Concat(structures)}</Structures></StructureTemplate>");
    }

    [Fact]
    public void Import_report_lists_each_structure_and_why_it_was_skipped()
    {
        using var folder = new TestFolder();
        string output = folder.Sub("templates");
        string xml = StructureTemplate(folder, "Report Test",
            Structure("Bladder"), Structure(""), Structure("NoTypeIndex", typeIndex: null), Structure("ShortColour", color: "RGB12"), Structure("Rectum", code: "14544"));

        VarianXmlReport report = new VarianXmlReader(xml).XmlToROI(output);

        Assert.False(report.Failed);
        Assert.Equal(Path.Combine(output, "Report_Test"), report.Target);
        Assert.Equal(new[] { "Bladder", "structure #2 (no ID)", "NoTypeIndex", "ShortColour", "Rectum" }, report.Structures.Select(s => s.Structure));
        Assert.Equal(new[] { false, true, true, true, false }, report.Structures.Select(s => s.Skipped));
        Assert.Contains("empty ID", report.Structures[1].SkipReason);
        Assert.Contains("TypeIndex", report.Structures[2].SkipReason);
        Assert.False(string.IsNullOrWhiteSpace(report.Structures[3].SkipReason));
        Assert.Equal(2, report.SucceededCount);
        Assert.Equal(new[] { "Bladder", "Rectum" }, ROIClassTools.LoadROIsFromFolder(Path.Combine(output, "Report_Test"), new List<OntologyCodeClass>()).Select(r => r.ROIName));
        List<string> lines = report.Describe().ToList();
        Assert.Contains("2 structure(s) done, 3 skipped", lines[0]);
        Assert.Contains(lines, l => l.Contains("'NoTypeIndex'") && l.Contains("TypeIndex"));
    }

    [Fact]
    public void A_structure_without_a_complete_code_is_imported_with_a_note_that_rts_leave_it_out()
    {
        // It used to be reported as done, and every generated RT then left it out without the import saying so.
        using var folder = new TestFolder();
        string output = folder.Sub("templates");
        string xml = StructureTemplate(folder, "Helpers", Structure("z_Ring", code: null), Structure("Opt_PTV", code: ""), Structure("Bladder"));

        VarianXmlReport report = VarianXmlReader.Import(xml, output);

        Assert.False(report.Failed, report.Error);
        Assert.Equal(3, report.SucceededCount);
        Assert.Equal(new[] { "z_Ring", "Opt_PTV" }, report.NotedStructures.Select(s => s.Structure));
        Assert.Contains("no structure code", report.Structures[0].Note);
        Assert.Contains("no code value", report.Structures[1].Note);
        Assert.All(report.NotedStructures, s => Assert.Contains("generated RTs leave it out", s.Note));
        Assert.Null(report.Structures[2].Note);
        List<string> lines = report.Describe().ToList();
        Assert.Contains("3 structure(s) done, 0 skipped", lines[0]);
        Assert.Contains(lines, l => l.Contains("note 'z_Ring'") && l.Contains("no structure code"));
        // Kept in the template, so a Varian export gives them back.
        Assert.Equal(new[] { "z_Ring", "Opt_PTV", "Bladder" }, ROIClassTools.LoadROIsFromFolder(Path.Combine(output, "Helpers"), new List<OntologyCodeClass>()).Select(r => r.ROIName));
    }

    [Fact]
    public void File_in_which_no_structure_can_be_imported_fails_and_writes_nothing()
    {
        using var folder = new TestFolder();
        string output = folder.Sub("templates");
        string xml = StructureTemplate(folder, "Nothing Usable", Structure("NoTypeIndex", typeIndex: null), Structure(""));

        VarianXmlReport report = VarianXmlReader.Import(xml, output);

        Assert.True(report.Failed);
        Assert.Contains("none of its 2", report.Error);
        Assert.Null(report.Target);
        Assert.Equal(2, report.SkippedStructures.Count());
        Assert.Empty(Directory.GetFileSystemEntries(output));
    }

    [Fact]
    public void File_without_structures_fails_and_writes_nothing()
    {
        using var folder = new TestFolder();
        string output = folder.Sub("templates");

        VarianXmlReport report = VarianXmlReader.Import(StructureTemplate(folder, "Empty"), output);

        Assert.True(report.Failed);
        Assert.Contains("no structures", report.Error);
        Assert.Empty(Directory.GetFileSystemEntries(output));
    }

    [Theory]
    [InlineData("<StructureTemplate><Preview ID=\"Broken\"")]
    [InlineData("<StructureTemplate><Structures /></StructureTemplate>")]
    [InlineData("<StructureTemplate><Preview ID=\"No Structures\" /></StructureTemplate>")]
    public void Import_of_a_file_that_cannot_be_read_returns_a_failed_report(string content)
    {
        using var folder = new TestFolder();
        string output = folder.Sub("templates");
        string xml = WriteFile(folder.Path, "input/bad.xml", content);

        VarianXmlReport report = VarianXmlReader.Import(xml, output);

        Assert.True(report.Failed);
        Assert.Equal(xml, report.Source);
        Assert.False(string.IsNullOrWhiteSpace(report.Error));
        Assert.Empty(Directory.GetFileSystemEntries(output));
    }

    [Fact]
    public void Preview_with_an_empty_id_fails_instead_of_writing_into_the_output_folder()
    {
        using var folder = new TestFolder();
        string output = folder.Sub("templates");

        VarianXmlReport report = VarianXmlReader.Import(StructureTemplate(folder, " ", Structure("Bladder")), output);

        Assert.True(report.Failed);
        Assert.Contains("ID is empty", report.Error);
        Assert.Empty(Directory.GetFileSystemEntries(output));
    }

    [Fact]
    public void Import_does_not_replace_an_unreadable_template_of_the_same_name()
    {
        using var folder = new TestFolder();
        string output = folder.Sub("templates");
        string existing = WriteFile(output, "Report_Test/All_ROIs.json", TruncatedJson);
        byte[] before = File.ReadAllBytes(existing);

        VarianXmlReport report = VarianXmlReader.Import(StructureTemplate(folder, "Report Test", Structure("Bladder")), output);

        Assert.True(report.Failed);
        Assert.Contains(existing, report.Error);
        Assert.Equal(before, File.ReadAllBytes(existing));
        Assert.Equal(new[] { "Report_Test/All_ROIs.json" }, Files(output));
    }

    [Theory]
    [InlineData("Structure Template.xml", 1)]
    [InlineData("Abdomen.xml", 17)]
    public void Import_report_of_the_samples_has_every_structure(string sample, int count)
    {
        using var folder = new TestFolder();

        VarianXmlReport report = VarianXmlReader.Import(TestFolder.Data(sample), folder.Path);

        Assert.False(report.Failed, report.Error);
        Assert.Equal(count, report.SucceededCount);
        Assert.Empty(report.SkippedStructures);
    }

    private static List<string> ExportedIds(VarianXmlWriter writer)
    {
        return writer.base_struct.Elements("Structure").Select(e => (string)e.Attribute("ID")!).ToList();
    }

    [Fact]
    public void Export_skips_an_roi_without_an_interpreted_type_and_reports_it()
    {
        using var folder = new TestFolder();
        string template = Template(folder.Sub("Pelvis"), Roi("Bladder", "15900"), Roi("Rectum", "14544", type: null), Roi("Prostate", "9600"));
        var writer = new VarianXmlWriter();

        VarianXmlReport report = writer.LoadROIsFromPath(template, new List<OntologyCodeClass>());

        Assert.False(report.Failed);
        Assert.Equal(new[] { "Bladder", "Prostate" }, ExportedIds(writer));
        VarianStructureResult skipped = Assert.Single(report.SkippedStructures);
        Assert.Equal("Rectum", skipped.Structure);
        Assert.Contains("interpreted type", skipped.SkipReason);
    }

    [Fact]
    public void Export_skips_an_roi_without_an_ontology_and_reports_it()
    {
        using var folder = new TestFolder();
        ROIClass mystery = Roi("Mystery", "1");
        mystery.Ontology_Class = null;
        string template = Template(folder.Sub("Pelvis"), mystery, Roi("Bladder", "15900"));
        var writer = new VarianXmlWriter();

        VarianXmlReport report = writer.LoadROIsFromPath(template, new List<OntologyCodeClass>());

        Assert.Equal(new[] { "Bladder" }, ExportedIds(writer));
        VarianStructureResult skipped = Assert.Single(report.SkippedStructures);
        Assert.Equal("Mystery", skipped.Structure);
        Assert.Contains("ontology", skipped.SkipReason);
        string output = Path.Combine(folder.Path, "Pelvis.xml");
        writer.SaveFile(output);
        Assert.Single(XDocument.Load(output).Root!.Element("Structures")!.Elements());
    }

    [Fact]
    public void Export_of_an_unreadable_template_throws_instead_of_writing_an_empty_one()
    {
        using var folder = new TestFolder();
        string template = folder.Sub("Pelvis");
        WriteFile(template, "All_ROIs.json", TruncatedJson);

        Assert.Throws<TemplateLoadException>(() => new VarianXmlWriter().LoadROIsFromPath(template, new List<OntologyCodeClass>()));
    }

    [Fact]
    public void Export_of_a_folder_without_a_template_reports_it()
    {
        using var folder = new TestFolder();
        var writer = new VarianXmlWriter();

        VarianXmlReport report = writer.LoadROIsFromPath(folder.Sub("Empty"), new List<OntologyCodeClass>());

        Assert.True(report.Failed);
        Assert.Empty(ExportedIds(writer));
    }
}
