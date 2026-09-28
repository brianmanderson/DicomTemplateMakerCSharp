using DicomTemplateMaker.Presentation.Tests.Fakes;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.Shell;
using ROIOntologyClass;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class VarianXmlBatchTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Root => folder.Sub("templates");

    private string XmlFolder => folder.Sub("xml");

    private VarianExportItem Item(string name) => new(name, Path.Combine(Root, name), OntologyTools.LoadOntologiesFromFolder(Path.Combine(Root, "Ontologies")));

    [Fact]
    public void Export_writes_one_file_per_template()
    {
        Templates.Make(Root, "Brain");
        Templates.Make(Root, "Lung");

        VarianExportResult result = VarianXmlBatch.Export(new[] { Item("Brain"), Item("Lung") }, XmlFolder, replaceExisting: false, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Written.Count);
        Assert.Empty(result.Failed);
        Assert.Equal(new[] { "Brain.xml", "Lung.xml" }, Directory.GetFiles(XmlFolder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(2, new VarianXmlReader(Path.Combine(XmlFolder, "Brain.xml")).root.Element("Structures")!.Elements().Count());
    }

    [Fact]
    public void Existing_files_are_listed_and_kept_unless_replacing()
    {
        Templates.Make(Root, "Brain");
        Templates.Make(Root, "Lung");
        File.WriteAllText(Path.Combine(XmlFolder, "Brain.xml"), "keep me");

        Assert.Equal(new[] { "Brain.xml" }, VarianXmlBatch.ExistingExportFiles(new[] { "Brain", "Lung" }, XmlFolder));
        VarianExportResult kept = VarianXmlBatch.Export(new[] { Item("Brain"), Item("Lung") }, XmlFolder, replaceExisting: false, null, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Brain.xml" }, kept.SkippedExisting);
        Assert.Single(kept.Written);
        Assert.Equal("keep me", File.ReadAllText(Path.Combine(XmlFolder, "Brain.xml")));

        VarianExportResult replaced = VarianXmlBatch.Export(new[] { Item("Brain") }, XmlFolder, replaceExisting: true, null, TestContext.Current.CancellationToken);

        Assert.Single(replaced.Written);
        Assert.StartsWith("<?xml", File.ReadAllText(Path.Combine(XmlFolder, "Brain.xml")), StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(XmlFolder, "*.tmp"));
    }

    [Fact]
    public void An_unreadable_template_fails_alone()
    {
        Templates.Make(Root, "Good");
        string bad = Path.Combine(Root, "Bad");
        Directory.CreateDirectory(bad);
        File.WriteAllText(Path.Combine(bad, "All_ROIs.json"), "{ broken");

        VarianExportResult result = VarianXmlBatch.Export(new[] { Item("Bad"), Item("Good") }, XmlFolder, replaceExisting: true, null, TestContext.Current.CancellationToken);

        Assert.Equal("Bad", Assert.Single(result.Failed).Template);
        Assert.Single(result.Written);
        Assert.False(File.Exists(Path.Combine(XmlFolder, "Bad.xml")));
    }

    [Fact]
    public void The_import_plan_marks_existing_templates_duplicates_and_unusable_names()
    {
        Templates.Make(Root, "Brain");
        Templates.Make(Root, "Lung");
        VarianXmlBatch.Export(new[] { Item("Brain"), Item("Lung") }, XmlFolder, replaceExisting: true, null, TestContext.Current.CancellationToken);
        Directory.Delete(Path.Combine(Root, "Lung"), recursive: true);
        File.Copy(Path.Combine(XmlFolder, "Brain.xml"), Path.Combine(XmlFolder, "Brain copy.xml"));
        File.WriteAllText(Path.Combine(XmlFolder, "Evil.xml"), "<StructureTemplate><Preview ID=\"../evil\" /><Structures /></StructureTemplate>");
        File.WriteAllText(Path.Combine(XmlFolder, "Broken.xml"), "<not xml");

        IReadOnlyList<VarianImportCandidate> plan = VarianXmlBatch.PlanImport(XmlFolder, Root);

        VarianImportCandidate brain = plan.Single(c => Path.GetFileName(c.File) == "Brain copy.xml");
        Assert.Equal("Brain", brain.TemplateName);
        Assert.True(brain.Exists);
        Assert.Null(brain.Problem);
        Assert.Contains("only that file is imported", plan.Single(c => Path.GetFileName(c.File) == "Brain.xml").Problem, StringComparison.Ordinal);
        VarianImportCandidate lung = plan.Single(c => c.TemplateName == "Lung");
        Assert.False(lung.Exists);
        Assert.Contains("cannot be used", plan.Single(c => Path.GetFileName(c.File) == "Evil.xml").Problem, StringComparison.Ordinal);
        VarianImportCandidate broken = plan.Single(c => Path.GetFileName(c.File) == "Broken.xml");
        Assert.Null(broken.TemplateName);
        Assert.StartsWith("it could not be read", broken.Problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Ontologies")]
    [InlineData("ontologies")]
    [InlineData("CON")]
    [InlineData("Lung.")]
    [InlineData("..\\evil")]
    [InlineData("a:b")]
    public void Template_names_the_editors_refuse_are_not_imported(string previewId)
    {
        // "Ontologies" was imported into the ontology library folder, where the template is never listed.
        Templates.Make(Root, "Brain");
        File.WriteAllText(Path.Combine(XmlFolder, "Bad.xml"), $"<StructureTemplate><Preview ID=\"{previewId}\" /><Structures /></StructureTemplate>");

        IReadOnlyList<VarianImportCandidate> plan = VarianXmlBatch.PlanImport(XmlFolder, Root);
        VarianImportResult result = VarianXmlBatch.Import(plan, Root, replaceExisting: true, null, TestContext.Current.CancellationToken);

        Assert.Contains("cannot be used", Assert.Single(plan).Problem, StringComparison.Ordinal);
        Assert.Single(result.NotImported);
        Assert.Empty(result.Reports);
        Assert.False(File.Exists(Path.Combine(Root, "Ontologies", "All_ROIs.json")));
    }

    [Fact]
    public void A_file_that_cannot_be_read_while_planning_is_not_imported_even_if_it_becomes_readable()
    {
        // It used to be listed without a name and imported later without the existence check, so it could replace an
        // existing template the user had chosen to keep.
        Templates.Make(Root, "Brain");
        VarianXmlBatch.Export(new[] { Item("Brain") }, XmlFolder, replaceExisting: true, null, TestContext.Current.CancellationToken);
        string xml = Path.Combine(XmlFolder, "Brain.xml");
        string content = File.ReadAllText(xml);
        File.WriteAllText(xml, "<still being copied");
        IReadOnlyList<VarianImportCandidate> plan = VarianXmlBatch.PlanImport(XmlFolder, Root);
        File.WriteAllText(xml, content);
        string roisBefore = File.ReadAllText(Path.Combine(Root, "Brain", "All_ROIs.json"));

        VarianImportResult result = VarianXmlBatch.Import(plan, Root, replaceExisting: false, null, TestContext.Current.CancellationToken);

        Assert.Empty(result.Reports);
        Assert.Contains("could not be read", Assert.Single(result.NotImported).Reason, StringComparison.Ordinal);
        Assert.Equal(roisBefore, File.ReadAllText(Path.Combine(Root, "Brain", "All_ROIs.json")));
    }

    [Fact]
    public void A_template_created_after_the_plan_is_kept_when_not_replacing()
    {
        Templates.Make(Root, "Lung");
        VarianXmlBatch.Export(new[] { Item("Lung") }, XmlFolder, replaceExisting: true, null, TestContext.Current.CancellationToken);
        Directory.Delete(Path.Combine(Root, "Lung"), recursive: true);
        IReadOnlyList<VarianImportCandidate> plan = VarianXmlBatch.PlanImport(XmlFolder, Root);
        Assert.False(Assert.Single(plan).Exists);
        string lung = Templates.Make(Root, "Lung", rois: new List<ROIClass> { new(1, 2, 3, "Made_meanwhile", "ORGAN", new OntologyCodeClass("Lung", "7195", "FMA")) });
        string roisBefore = File.ReadAllText(Path.Combine(lung, "All_ROIs.json"));

        VarianImportResult result = VarianXmlBatch.Import(plan, Root, replaceExisting: false, null, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Lung" }, result.SkippedExisting);
        Assert.Equal(roisBefore, File.ReadAllText(Path.Combine(lung, "All_ROIs.json")));
    }

    [Fact]
    public void Import_keeps_existing_templates_unless_replacing_and_reports_every_file()
    {
        string brain = Templates.Make(Root, "Brain", new[] { "/monitored" });
        Templates.Make(Root, "Lung");
        VarianXmlBatch.Export(new[] { Item("Brain"), Item("Lung") }, XmlFolder, replaceExisting: true, null, TestContext.Current.CancellationToken);
        Directory.Delete(Path.Combine(Root, "Lung"), recursive: true);
        File.WriteAllText(Path.Combine(XmlFolder, "Broken.xml"), "<not xml");
        string roisBefore = File.ReadAllText(Path.Combine(brain, "All_ROIs.json"));
        IReadOnlyList<VarianImportCandidate> plan = VarianXmlBatch.PlanImport(XmlFolder, Root);

        VarianImportResult kept = VarianXmlBatch.Import(plan, Root, replaceExisting: false, null, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Brain" }, kept.SkippedExisting);
        Assert.Equal(new[] { "Lung" }, kept.Imported.Select(r => Path.GetFileName(r.Target!)));
        Assert.Equal("Broken.xml", Path.GetFileName(Assert.Single(kept.NotImported).File));
        Assert.Equal(roisBefore, File.ReadAllText(Path.Combine(brain, "All_ROIs.json")));

        VarianImportResult replaced = VarianXmlBatch.Import(plan.Where(c => c.TemplateName == "Brain").ToList(), Root, replaceExisting: true, null, TestContext.Current.CancellationToken);

        Assert.Single(replaced.Imported);
        Assert.Equal("/monitored", File.ReadAllText(Path.Combine(brain, "Paths.txt")).Trim());
        string summary = ShellMessages.DescribeImport(kept);
        Assert.Contains("Imported 1 template into", summary, StringComparison.Ordinal);
        Assert.Contains("Kept 1 existing template unchanged: Brain.", summary, StringComparison.Ordinal);
        Assert.Contains("Broken.xml:", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_does_not_import_candidates_with_a_problem()
    {
        File.WriteAllText(Path.Combine(XmlFolder, "Evil.xml"), "<StructureTemplate><Preview ID=\"../evil\" /><Structures /></StructureTemplate>");

        VarianImportResult result = VarianXmlBatch.Import(VarianXmlBatch.PlanImport(XmlFolder, Root), Root, replaceExisting: true, null, TestContext.Current.CancellationToken);

        Assert.Single(result.NotImported);
        Assert.Empty(result.Reports);
        Assert.False(Directory.Exists(Path.Combine(folder.Path, "evil")));
    }
}
