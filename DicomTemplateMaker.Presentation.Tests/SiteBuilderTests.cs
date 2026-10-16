using DicomTemplateMaker.Presentation.Tests.Fakes;
using DicomTemplateMakerGUI.Services;
using Newtonsoft.Json.Linq;
using ROIOntologyClass;
using TemplateSync.Airtable;
using TemplateSync.Model;
using TemplateSync.Storage;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class SiteBuilderTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private static TemplateIndex Index(params AirtableRecord[] records) => TemplateIndex.Build(records.Select(AirTableEntry.FromRecord));

    private static AirtableRecord Record(string structure, string site, string? rgb = "255,0,0", string? code = "123", string? dvhColour = null)
    {
        var fields = new JObject { ["Structure"] = structure, ["Template_Recommend"] = new JArray(site) };
        if (rgb != null)
        {
            fields["RGB"] = rgb;
        }

        if (code != null)
        {
            fields["SchemeCode"] = code;
        }

        if (dvhColour != null)
        {
            fields["DVH_Color"] = dvhColour;
        }

        return Snapshots.Record(fields);
    }

    [Fact]
    public void Every_warning_is_returned_with_its_site_record_structure_and_kind_without_a_cap()
    {
        AirtableRecord[] noColour = Enumerable.Range(0, 40).Select(i => Record("Roi" + i, "Lung", rgb: null)).ToArray();
        AirtableRecord badDvh = Record("Heart", "Lung", dvhColour: "not a colour");
        AirtableRecord noCode = Record("Cord", "Lung", code: null);
        AirtableRecord empty = Record(" ", "Lung");
        AirtableRecord unusable = Record("<>", "Lung", code: null);
        TemplateIndex index = Index(noColour.Append(badDvh).Append(noCode).Append(empty).Append(unusable).ToArray());
        var warnings = new List<SiteBuildWarning>();

        List<SiteJob> jobs = SiteBuilder.PrepareAll(index, new[] { "Lung" }, NamingChoice.Default(index), warnings);

        Assert.Equal(42, jobs.Single().Rois.Count);
        Assert.Equal(40, warnings.Count(w => w.Kind == SiteBuildWarningKind.RedFallbackColour));
        Assert.All(warnings, w => Assert.Equal("Lung", w.Site));
        SiteBuildWarning first = warnings[0];
        Assert.Equal((noColour[0].Id, "Roi0", "Roi0: no valid colour in the table, so it is shown in red."), (first.RecordId, first.Structure, first.Message));
        Assert.Equal(badDvh.Id, warnings.Single(w => w.Kind == SiteBuildWarningKind.BadDvhColour).RecordId);
        Assert.Equal(noCode.Id, warnings.Single(w => w.Kind == SiteBuildWarningKind.IncompleteCode).RecordId);
        Assert.Equal(empty.Id, warnings.Single(w => w.Kind == SiteBuildWarningKind.EmptyName).RecordId);
        Assert.Equal(unusable.Id, warnings.Single(w => w.Kind == SiteBuildWarningKind.NoUsableCharacters).RecordId);
    }

    [Fact]
    public void A_site_name_that_is_not_a_single_folder_name_is_reported_and_left_out()
    {
        TemplateIndex index = Index(Record("Heart", "../Outside"), Record("Lung_L", "Lung"));
        var warnings = new List<SiteBuildWarning>();

        List<SiteJob> jobs = SiteBuilder.PrepareAll(index, new[] { "../Outside", "Lung" }, NamingChoice.Default(index), warnings);

        Assert.Equal(new[] { "Lung" }, jobs.Select(j => j.Site));
        SiteBuildWarning warning = Assert.Single(warnings);
        Assert.Equal(("../Outside", (string?)null, SiteBuildWarningKind.UnusableSiteName), (warning.Site, warning.RecordId, warning.Kind));
    }

    [Fact]
    public void The_default_naming_choice_offers_laterality_when_any_site_is_lateral()
    {
        var lateral = new JObject { ["Structure"] = "Breast_L", ["RGB"] = "0,255,0", ["Template_Recommend"] = new JArray("Breast"), ["TG_263R"] = "L_Breast" };
        TemplateIndex index = TemplateIndex.Build(new[] { AirTableEntry.FromRecord(Snapshots.Record(lateral)), AirTableEntry.FromRecord(Record("Heart", "Lung")) });

        NamingChoice naming = NamingChoice.Default(index);

        Assert.Equal(new NamingChoice(NamingChoice.English, false, false, true), naming);
    }

    [Fact]
    public void Build_writes_one_folder_per_job_and_merges_the_codes_into_the_library()
    {
        TemplateIndex index = Index(Record("Heart", "Lung", code: "1"), Record("Lung_L", "Lung", code: "2"), Record("Brain", "CNS", code: "3"));
        var builder = new SiteBuilder(folder.Path, Path.Combine(folder.Path, "Ontologies"));
        List<SiteJob> jobs = SiteBuilder.PrepareAll(index, new[] { "Lung", "CNS" }, NamingChoice.Default(index), new List<SiteBuildWarning>());

        int built = builder.Build(jobs, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, built);
        Assert.Equal(new[] { "Heart", "Lung_L" }, ROIClassTools.LoadROIsFromFolder(builder.TemplatePath("Lung"), new List<OntologyCodeClass>()).Select(r => r.ROIName));
        Assert.Equal(3, OntologyTools.LoadOntologiesFromFolder(builder.OntologyFolder).Count);
    }

    [Fact]
    public void A_failed_build_reports_how_many_templates_were_built()
    {
        TemplateIndex index = Index(Record("Heart", "Lung"), Record("Brain", "CNS"));
        var builder = new SiteBuilder(folder.Path, Path.Combine(folder.Path, "Ontologies"));
        File.WriteAllText(builder.TemplatePath("CNS"), "not a folder");
        List<SiteJob> jobs = SiteBuilder.PrepareAll(index, new[] { "Lung", "CNS" }, NamingChoice.Default(index), new List<SiteBuildWarning>());

        SiteBuildException failure = Assert.Throws<SiteBuildException>(() => builder.Build(jobs, null, TestContext.Current.CancellationToken));

        Assert.Equal(1, failure.Built);
        Assert.True(Directory.Exists(builder.TemplatePath("Lung")));
    }

    [Fact]
    public void The_default_naming_choice_over_the_snapshot_builds_what_the_window_builds()
    {
        TableSnapshot snapshot = SnapshotStore.Parse(File.ReadAllText(OnlineTemplatesViewModelTests.SnapshotPath()));
        TemplateIndex index = TemplateIndex.Build(snapshot.Records.Select(AirTableEntry.FromRecord));
        var builder = new SiteBuilder(folder.Path, Path.Combine(folder.Path, "Ontologies"));
        List<SiteJob> jobs = SiteBuilder.PrepareAll(index, OnlineTemplatesViewModelTests.CharacterisedSites.Keys, NamingChoice.Default(index), new List<SiteBuildWarning>());

        builder.Build(jobs, null, TestContext.Current.CancellationToken);

        var actual = jobs.ToDictionary(j => j.Site, j => OnlineTemplatesViewModelTests.RoisFileHash(builder.TemplatePath(j.Site)));
        Assert.Equal(OnlineTemplatesViewModelTests.CharacterisedSites, actual);
    }
}
