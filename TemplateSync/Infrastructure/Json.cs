using Newtonsoft.Json;

namespace TemplateSync.Infrastructure
{
    /// <summary>Shared JSON settings. DateParseHandling.None keeps field values exactly as Airtable sent them.</summary>
    internal static class Json
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Formatting = Formatting.None,
        };

        public static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

        public static readonly JsonSerializerSettings IndentedSettings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Formatting = Formatting.Indented,
        };
    }
}
