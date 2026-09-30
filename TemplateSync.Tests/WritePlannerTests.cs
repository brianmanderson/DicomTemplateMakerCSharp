using Newtonsoft.Json.Linq;
using TemplateSync.Airtable;
using TemplateSync.Model;
using TemplateSync.Tests.Fakes;
using Xunit;

namespace TemplateSync.Tests;

public class WritePlannerTests
{
    private static AirtableRecord Remote(string id, object fields) => AirtableJson.Record(id, fields).ToObject<AirtableRecord>()!;

    private static LocalRoi Roi(string name, bool include = true, string rgb = "1,2,3") =>
        new(new AirTableEntry { Structure = name, Type = "ORGAN", RGB = rgb, Colors_RGB = new List<string> { "Auto:" + rgb } }, include);

    [Fact]
    public void New_roi_is_created_with_the_site_in_the_right_list_and_no_id_column()
    {
        WritePlan plan = WritePlanner.Plan("SiteA", new[] { Roi("Brain", include: false) }, Array.Empty<AirtableRecord>());

        JObject fields = Assert.Single(plan.Creates);
        Assert.Equal(new[] { "SiteA" }, fields["Template_Consider"]!.Values<string>());
        Assert.Null(fields["Template_Recommend"]);
        Assert.Null(fields["Id"]);
        Assert.Equal("Brain", (string?)fields["Structure"]);
        Assert.Equal("FMA", (string?)fields["Scheme"]);
    }

    [Fact]
    public void Including_an_roi_adds_the_site_to_recommend_and_removes_it_from_consider_keeping_other_sites()
    {
        var remote = Remote("rec1", new { Structure = "Brain", Type = "ORGAN", RGB = "1,2,3", Colors_RGB = new[] { "Auto:1,2,3" }, Template_Recommend = new[] { "SiteB" }, Template_Consider = new[] { "SiteA", "SiteC" } });

        WritePlan plan = WritePlanner.Plan("SiteA", new[] { Roi("Brain", include: true) }, new[] { remote });

        RecordUpdate update = Assert.Single(plan.Updates);
        Assert.Equal("rec1", update.Id);
        Assert.Equal(new[] { "SiteA", "SiteB" }, update.Fields["Template_Recommend"]!.Values<string>());
        Assert.Equal(new[] { "SiteC" }, update.Fields["Template_Consider"]!.Values<string>());
    }

    [Fact]
    public void Optional_roi_moves_the_site_from_recommend_to_consider()
    {
        var remote = Remote("rec1", new { Structure = "Brain", Template_Recommend = new[] { "SiteA", "SiteB" } });

        RecordUpdate update = Assert.Single(WritePlanner.Plan("SiteA", new[] { Roi("Brain", include: false) }, new[] { remote }).Updates);

        Assert.Equal(new[] { "SiteB" }, update.Fields["Template_Recommend"]!.Values<string>());
        Assert.Equal(new[] { "SiteA" }, update.Fields["Template_Consider"]!.Values<string>());
    }

    [Fact]
    public void Colors_are_the_union_of_local_then_remote_so_the_current_colour_comes_first()
    {
        var remote = Remote("rec1", new { Structure = "Brain", Colors_RGB = new[] { "Auto:9,9,9" }, RGB = "9,9,9", Template_Recommend = new[] { "SiteA" } });

        RecordUpdate update = Assert.Single(WritePlanner.Plan("SiteA", new[] { Roi("Brain", rgb: "1,2,3") }, new[] { remote }).Updates);

        Assert.Equal(new[] { "Auto:1,2,3", "Auto:9,9,9" }, update.Fields["Colors_RGB"]!.Values<string>());
        Assert.Equal("1,2,3", (string?)update.Fields["RGB"]);
    }

    [Fact]
    public void Only_changed_fields_are_sent_and_null_local_values_never_erase_remote_ones()
    {
        var remote = Remote("rec1", new { Structure = "Brain", Type = "ORGAN", RGB = "1,2,3", Colors_RGB = new[] { "Auto:1,2,3" }, TG_263Spanish = "Cerebro", Template_Recommend = new[] { "SiteA" }, Scheme = "FMA", ContextGroupVersion = "20161209", MappingResource = "99VMS", ContextIdentifier = "VMS011", MappingResourceName = "Varian Medical Systems", MappingResourceUID = "1.2.246.352.7.1.1", ContextUID = "1.2.246.352.7.2.11" });
        LocalRoi roi = Roi("Brain");
        roi.Desired.Type = "AVOIDANCE";

        RecordUpdate update = Assert.Single(WritePlanner.Plan("SiteA", new[] { roi }, new[] { remote }).Updates);

        Assert.Equal(new[] { "Type" }, update.Fields.Properties().Select(p => p.Name));
    }

    [Fact]
    public void Default_ontology_values_are_written_when_the_remote_cell_is_empty()
    {
        var remote = Remote("rec1", new { Structure = "Brain", Type = "ORGAN", RGB = "1,2,3", Colors_RGB = new[] { "Auto:1,2,3" }, Template_Recommend = new[] { "SiteA" } });

        RecordUpdate update = Assert.Single(WritePlanner.Plan("SiteA", new[] { Roi("Brain") }, new[] { remote }).Updates);

        Assert.Equal("FMA", (string?)update.Fields["Scheme"]);
        Assert.Equal("1.2.246.352.7.2.11", (string?)update.Fields["ContextUID"]);
    }

    [Fact]
    public void Identical_record_produces_no_request()
    {
        var remote = Remote("rec1", new { Structure = "Brain", Type = "ORGAN", RGB = "1,2,3", Colors_RGB = new[] { "Auto:1,2,3" }, Template_Recommend = new[] { "SiteA" }, Scheme = "FMA", ContextGroupVersion = "20161209", MappingResource = "99VMS", ContextIdentifier = "VMS011", MappingResourceName = "Varian Medical Systems", MappingResourceUID = "1.2.246.352.7.1.1", ContextUID = "1.2.246.352.7.2.11" });

        WritePlan plan = WritePlanner.Plan("SiteA", new[] { Roi("Brain") }, new[] { remote });

        Assert.True(plan.IsEmpty);
        Assert.Equal(1, plan.Unchanged);
        Assert.Equal(0, plan.RequestCount);
    }

    [Fact]
    public void Site_order_within_a_list_does_not_count_as_a_change()
    {
        var remote = Remote("rec1", new { Structure = "Brain", Type = "ORGAN", RGB = "1,2,3", Colors_RGB = new[] { "Auto:1,2,3" }, Template_Recommend = new[] { "SiteB", "SiteA" }, Scheme = "FMA", ContextGroupVersion = "20161209", MappingResource = "99VMS", ContextIdentifier = "VMS011", MappingResourceName = "Varian Medical Systems", MappingResourceUID = "1.2.246.352.7.1.1", ContextUID = "1.2.246.352.7.2.11" });

        Assert.True(WritePlanner.Plan("SiteA", new[] { Roi("Brain") }, new[] { remote }).IsEmpty);
    }

    [Fact]
    public void Matching_is_by_exact_structure_name_and_the_first_record_wins()
    {
        var first = Remote("recFirst", new { Structure = "Brain", Template_Recommend = new[] { "SiteB" } });
        var second = Remote("recSecond", new { Structure = "Brain", Template_Recommend = new[] { "SiteC" } });
        var otherCase = Remote("recCase", new { Structure = "brain" });

        WritePlan plan = WritePlanner.Plan("SiteA", new[] { Roi("Brain") }, new[] { otherCase, first, second });

        Assert.Equal("recFirst", Assert.Single(plan.Updates).Id);
        Assert.Empty(plan.Creates);
    }

    [Fact]
    public void Duplicate_and_unnamed_local_rois_are_skipped_with_warnings()
    {
        WritePlan plan = WritePlanner.Plan("SiteA", new[] { Roi("Brain"), Roi("Brain", rgb: "4,5,6"), new LocalRoi(new AirTableEntry { Structure = "" }, true) }, Array.Empty<AirtableRecord>());

        Assert.Single(plan.Creates);
        Assert.Equal(2, plan.Warnings.Count);
    }

    [Fact]
    public void Excluded_columns_are_never_sent()
    {
        WritePlan plan = WritePlanner.Plan("SiteA", new[] { Roi("Brain") }, Array.Empty<AirtableRecord>(), new HashSet<string> { "RGB", "CommonName" });

        Assert.Null(plan.Creates[0]["RGB"]);
        Assert.NotNull(plan.Creates[0]["Colors_RGB"]);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(10, 0, 1)]
    [InlineData(11, 0, 2)]
    [InlineData(21, 9, 4)]
    public void Request_count_is_ten_records_per_request(int creates, int updates, int expected)
    {
        WritePlan plan = WritePlanner.Plan("S", Enumerable.Range(0, creates).Select(i => Roi($"N{i}")).Concat(Enumerable.Range(0, updates).Select(i => Roi($"U{i}"))),
            Enumerable.Range(0, updates).Select(i => Remote($"rec{i}", new { Structure = $"U{i}" })));

        Assert.Equal(expected, plan.RequestCount);
    }

    [Fact]
    public void Blank_site_name_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => WritePlanner.Plan(" ", new[] { Roi("Brain") }, Array.Empty<AirtableRecord>()));
    }
}
