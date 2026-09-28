using Newtonsoft.Json.Linq;

namespace TemplateSync.Tests.Fakes;

/// <summary>Builds Airtable-shaped JSON bodies.</summary>
public static class AirtableJson
{
    public static JObject Record(string id, object fields, string createdTime = "2025-11-13T20:19:21.000Z")
        => new() { ["id"] = id, ["createdTime"] = createdTime, ["fields"] = JObject.FromObject(fields) };

    public static string Page(IEnumerable<JObject> records, string? offset = null)
    {
        var page = new JObject { ["records"] = new JArray(records) };
        if (offset != null)
        {
            page["offset"] = offset;
        }

        return page.ToString();
    }

    public static string Error(string type, string message) => new JObject { ["error"] = new JObject { ["type"] = type, ["message"] = message } }.ToString();

    public static IEnumerable<JObject> Records(int count, int start = 0)
        => Enumerable.Range(start, count).Select(i => Record($"rec{i:D14}", new { Structure = $"ROI_{i}", Template_Recommend = new[] { "SiteA" } }));
}
