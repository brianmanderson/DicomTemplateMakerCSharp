using Newtonsoft.Json.Linq;
using TemplateSync.Airtable;
using TemplateSync.Storage;

namespace DicomTemplateMaker.Presentation.Tests.Fakes;

/// <summary>Builds template tables: one record per ROI, listing the sites that recommend it.</summary>
public static class Snapshots
{
    private static int nextId;

    public static TableSnapshot Of(DateTimeOffset generatedAtUtc, params AirtableRecord[] records)
        => new() { GeneratedAtUtc = generatedAtUtc, Records = records.ToList(), RecordCount = records.Length };

    public static AirtableRecord Roi(string structure, params string[] sites)
        => Record(new JObject { ["Structure"] = structure, ["RGB"] = "255,0,0", ["Template_Recommend"] = new JArray(sites) });

    public static AirtableRecord Record(JObject fields)
        => new() { Id = "rec" + Interlocked.Increment(ref nextId).ToString("D14", System.Globalization.CultureInfo.InvariantCulture), Fields = fields };
}
