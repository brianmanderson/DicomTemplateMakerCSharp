using DicomTemplateMaker.Presentation.Tests.Fakes;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.Shell;
using ROIOntologyClass;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class TemplateLibraryScannerTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Root => folder.Sub("templates");

    [Fact]
    public void Readable_templates_are_listed_by_name_without_problems()
    {
        Templates.Make(Root, "Prostate", new[] { @"D:\Incoming" });
        Templates.Make(Root, "brain");

        TemplateScanResult result = TemplateLibraryScanner.Scan(Root, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "brain", "Prostate" }, result.Templates.Select(t => t.TemplateName));
        Assert.Empty(result.Problems);
        TemplateMaker prostate = result.Templates[1];
        Assert.Equal(new[] { @"D:\Incoming" }, prostate.Paths);
        Assert.Equal(Path.Combine(Root, "Ontologies"), prostate.onto_path);
        Assert.Equal(2, prostate.ROIs.Count);
    }

    [Fact]
    public void Each_template_gets_its_own_copy_of_the_library()
    {
        Templates.Make(Root, "A");
        Templates.Make(Root, "B");

        TemplateScanResult result = TemplateLibraryScanner.Scan(Root, cancellationToken: TestContext.Current.CancellationToken);

        OntologyCodeClass brainA = result.Templates[0].Ontologies.Single(o => o.CodeValue == "50801");
        OntologyCodeClass brainB = result.Templates[1].Ontologies.Single(o => o.CodeValue == "50801");
        Assert.NotSame(brainA, brainB);
        Assert.NotSame(result.Templates[0].Ontologies, result.Templates[1].Ontologies);
        // Within one template the ROI shares its template's instance, as the editors expect.
        Assert.Same(brainA, result.Templates[0].ROIs.Single(r => r.ROIName == "Brain").Ontology_Class);
    }

    [Fact]
    public void An_unreadable_template_is_reported_and_not_listed()
    {
        Templates.Make(Root, "Good");
        string bad = Path.Combine(Root, "Bad");
        Directory.CreateDirectory(bad);
        File.WriteAllText(Path.Combine(bad, "All_ROIs.json"), "{ broken");

        TemplateScanResult result = TemplateLibraryScanner.Scan(Root, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Good" }, result.Templates.Select(t => t.TemplateName));
        TemplateProblem problem = Assert.Single(result.Problems);
        Assert.Equal(TemplateProblemKind.TemplateUnreadable, problem.Kind);
        Assert.Equal("Bad", problem.Subject);
        Assert.Equal("{ broken", File.ReadAllText(Path.Combine(bad, "All_ROIs.json")));
    }

    [Fact]
    public void An_unreadable_library_lists_no_template()
    {
        Templates.Make(Root, "Good");
        File.WriteAllText(Path.Combine(Root, "Ontologies", OntologyTools.LibraryFileName), "not json");

        TemplateScanResult result = TemplateLibraryScanner.Scan(Root, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result.Templates);
        TemplateProblem problem = Assert.Single(result.Problems);
        Assert.Equal(TemplateProblemKind.LibraryUnreadable, problem.Kind);
        Assert.Equal("not json", File.ReadAllText(Path.Combine(Root, "Ontologies", OntologyTools.LibraryFileName)));
    }

    [Fact]
    public void Legacy_files_that_could_not_be_migrated_are_reported_and_kept()
    {
        string legacy = Path.Combine(Root, "Legacy", "ROIs");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "Bladder.txt"), "255\\255\\0\nBladder\\15900\\FMA\\20161209\\99VMS\\VMS011\\Varian Medical Systems\\1.2.246.352.7.1.1\\1.2.246.352.7.2.11\nORGAN");
        File.WriteAllText(Path.Combine(legacy, "Broken.txt"), "red\\0\\0\nBroken\\1\\FMA\nORGAN");

        TemplateScanResult result = TemplateLibraryScanner.Scan(Root, cancellationToken: TestContext.Current.CancellationToken);

        TemplateMaker template = Assert.Single(result.Templates);
        Assert.NotEmpty(template.LoadWarnings);
        Assert.Contains(result.Problems, p => p.Kind == TemplateProblemKind.LegacyFilesKept && p.Subject == "Legacy");
        Assert.True(File.Exists(Path.Combine(legacy, "Broken.txt")));
    }

    [Fact]
    public void Legacy_files_kept_next_to_all_rois_json_are_reported_on_every_scan()
    {
        // Once the migration had written All_ROIs.json, later scans said nothing about the kept files, and the row
        // showed the ROIs that could be read with no mention of the rest.
        string legacy = Path.Combine(Root, "Legacy", "ROIs");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "Bladder.txt"), "255\\255\\0\nBladder\\15900\\FMA\nORGAN");
        File.WriteAllText(Path.Combine(legacy, "CTV.txt"), "300\\0\\0\nCTV\\1\\FMA\nCTV");
        TemplateLibraryScanner.Scan(Root, cancellationToken: TestContext.Current.CancellationToken);

        TemplateScanResult later = TemplateLibraryScanner.Scan(Root, cancellationToken: TestContext.Current.CancellationToken);

        TemplateProblem kept = Assert.Single(later.Problems, p => p.Kind == TemplateProblemKind.LegacyFilesKept);
        Assert.Equal("Legacy", kept.Subject);
        Assert.Contains("CTV.txt", kept.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { "Bladder" }, Assert.Single(later.Templates).ROIs.Select(r => r.ROIName));
    }

    [Fact]
    public void Codes_used_by_templates_are_added_to_the_library_once()
    {
        Templates.Make(Root, "A", rois: new[] { new ROIClass(1, 2, 3, "Liver", "ORGAN", new OntologyCodeClass("Liver", "7197", "FMA")) }, updateLibrary: false);
        Templates.Make(Root, "B", rois: new[] { new ROIClass(1, 2, 3, "Heart", "ORGAN", new OntologyCodeClass("Heart", "7088", "FMA")) }, updateLibrary: false);

        TemplateScanResult result = TemplateLibraryScanner.Scan(Root, cancellationToken: TestContext.Current.CancellationToken);

        List<OntologyCodeClass> library = OntologyTools.LoadOntologiesFromFolder(Path.Combine(Root, "Ontologies"));
        Assert.Equal(new[] { "Heart", "Liver" }, library.Select(o => o.CodeMeaning).OrderBy(n => n, StringComparer.Ordinal));
        // Each template also offers the code the other one brought in.
        Assert.All(result.Templates, t => Assert.Equal(2, t.Ontologies.Count));
    }

    [Fact]
    public void An_empty_folder_creates_no_library()
    {
        TemplateScanResult result = TemplateLibraryScanner.Scan(Root, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result.Templates);
        Assert.Empty(result.Problems);
        Assert.False(Directory.Exists(Path.Combine(Root, "Ontologies")));
    }

    [Fact]
    public void A_missing_template_folder_is_a_problem_not_an_exception()
    {
        TemplateScanResult result = TemplateLibraryScanner.Scan(Path.Combine(folder.Path, "missing"), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TemplateProblemKind.FolderUnreadable, Assert.Single(result.Problems).Kind);
    }

    [Fact]
    public void Each_distinct_problem_is_new_once_per_session()
    {
        var tracker = new ProblemTracker();
        var a = new TemplateProblem(TemplateProblemKind.TemplateUnreadable, "A", "broken");
        var b = new TemplateProblem(TemplateProblemKind.TemplateUnreadable, "B", "broken");

        Assert.Equal(new[] { a }, tracker.TakeNew(new[] { a }));
        Assert.Empty(tracker.TakeNew(new[] { a with { } }));
        Assert.Equal(new[] { b }, tracker.TakeNew(new[] { a, b }));
        Assert.Single(tracker.TakeNew(new[] { a with { Message = "broken differently" } }));
    }
}
