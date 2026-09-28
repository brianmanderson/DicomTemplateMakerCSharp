using TemplateSync.Airtable;
using TemplateSync.Model;
using TemplateSync.Tests.Fakes;
using Xunit;

namespace TemplateSync.Tests;

public class TemplateModelTests
{
    private static AirTableEntry Entry(object fields, string id = "rec1") => AirTableEntry.FromRecord(AirtableJson.Record(id, fields).ToObject<AirtableRecord>()!);

    [Fact]
    public void Missing_cells_get_the_ontology_defaults_like_the_old_deserialiser()
    {
        AirTableEntry entry = Entry(new { Structure = "Brain" });

        Assert.Equal("FMA", entry.Scheme);
        Assert.Equal("20161209", entry.ContextGroupVersion);
        Assert.Equal("99VMS", entry.MappingResource);
        Assert.Equal("VMS011", entry.ContextIdentifier);
        Assert.Equal("Varian Medical Systems", entry.MappingResourceName);
        Assert.Equal("1.2.246.352.7.1.1", entry.MappingResourceUID);
        Assert.Equal("1.2.246.352.7.2.11", entry.ContextUID);
        Assert.Null(entry.CommonName);
        Assert.Empty(entry.Template_Recommend);
        Assert.Equal("rec1", entry.Id);
    }

    [Fact]
    public void Numbers_and_lists_are_read_as_text_where_the_model_expects_text()
    {
        AirTableEntry entry = Entry(new { Structure = "Brain", DVH_Width = 2, SchemeCode = new[] { "12345" } });

        Assert.Equal("2", entry.DVH_Width);
        Assert.Equal("12345", entry.SchemeCode);
    }

    [Theory]
    [InlineData("10,20,30", null, true, 10, 20, 30)]
    [InlineData(" 10 , 20 ,30", null, true, 10, 20, 30)]
    [InlineData(null, "Auto:1,2,3", true, 1, 2, 3)]
    [InlineData("bad", "Red:255,0,0", true, 255, 0, 0)]
    [InlineData("300,0,0", null, false, 0, 0, 0)]
    [InlineData(null, "NoColon", false, 0, 0, 0)]
    [InlineData(null, null, false, 0, 0, 0)]
    public void Colour_parsing_prefers_rgb_then_the_first_colour_option(string? rgb, string? option, bool ok, byte r, byte g, byte b)
    {
        var entry = new AirTableEntry { RGB = rgb, Colors_RGB = option == null ? null : new List<string> { option } };

        Assert.Equal(ok, entry.TryGetRgb(out byte rr, out byte gg, out byte bb));
        Assert.Equal((r, g, b), (rr, gg, bb));
    }

    [Fact]
    public void Index_groups_by_site_recommend_before_consider_and_first_structure_wins()
    {
        var entries = new[]
        {
            Entry(new { Structure = "Brain", Template_Consider = new[] { "SiteA" }, Template_Recommend = new[] { "SiteB" } }, "rec1"),
            Entry(new { Structure = "Brain", Template_Recommend = new[] { "SiteA" } }, "rec2"),
            Entry(new { Structure = "Lens", Template_Recommend = new[] { "SiteA" }, Template_Consider = new[] { "SiteA" } }, "rec3"),
        };

        TemplateIndex index = TemplateIndex.Build(entries);

        Assert.Equal(new[] { "SiteB", "SiteA" }, index.SiteNames);
        IReadOnlyList<SiteRoi> siteA = index.GetSite("SiteA");
        Assert.Equal(new[] { "rec1", "rec3" }, siteA.Select(s => s.Entry.Id));
        Assert.False(siteA[0].Include); // rec1 listed Brain under Consider for SiteA and came first
        Assert.True(siteA[1].Include);  // Lens: Recommend processed before Consider
        Assert.Empty(index.GetSite("Unknown"));
    }

    [Fact]
    public void Field_name_list_matches_the_properties_and_excludes_id()
    {
        var properties = typeof(AirTableEntry).GetProperties().Select(p => p.Name).Where(n => n != "Id").ToHashSet();

        Assert.Equal(properties.OrderBy(n => n), AirTableEntry.FieldNames.OrderBy(n => n));
        foreach (string name in AirTableEntry.FieldNames)
        {
            _ = new AirTableEntry().GetField(name);
        }
    }
}
