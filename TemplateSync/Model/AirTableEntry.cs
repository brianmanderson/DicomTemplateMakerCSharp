using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TemplateSync.Airtable;
using TemplateSync.Infrastructure;

namespace TemplateSync.Model
{
    /// <summary>
    /// Default ontology values. These mirror the field initialisers of
    /// ROIOntologyClass.OntologyCodeClass and are applied to any field an Airtable record omits
    /// (Airtable leaves empty cells out of API responses).
    /// </summary>
    public static class OntologyDefaults
    {
        public const string Scheme = "FMA";
        public const string ContextGroupVersion = "20161209";
        public const string MappingResource = "99VMS";
        public const string ContextIdentifier = "VMS011";
        public const string MappingResourceName = "Varian Medical Systems";
        public const string MappingResourceUID = "1.2.246.352.7.1.1";
        public const string ContextUID = "1.2.246.352.7.2.11";
    }

    /// <summary>
    /// One ROI row of a template table. Property names are the Airtable column names.
    /// </summary>
    public sealed class AirTableEntry
    {
        /// <summary>Every column this class maps, in a stable order. "Id" is not a column we read or write.</summary>
        public static readonly IReadOnlyList<string> FieldNames = new[]
        {
            nameof(Structure), nameof(CommonName), nameof(Type),
            nameof(Colors_RGB), nameof(RGB),
            nameof(Template_Recommend), nameof(Template_Consider),
            nameof(SchemeCode), nameof(Scheme), nameof(ContextGroupVersion), nameof(MappingResource),
            nameof(ContextIdentifier), nameof(MappingResourceName), nameof(MappingResourceUID), nameof(ContextUID),
            nameof(TG_263), nameof(TG_263R), nameof(TG_263Spanish), nameof(TG_263SpanishR), nameof(TG_263French), nameof(TG_263FrenchR),
            nameof(DVH_Color), nameof(DVH_Style), nameof(DVH_Width), nameof(DVH_Type_Index), nameof(DVH_ContourStyle),
        };

        /// <summary>Columns holding lists (Airtable multiple-select fields).</summary>
        public static readonly IReadOnlyCollection<string> ListFieldNames = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(Colors_RGB), nameof(Template_Recommend), nameof(Template_Consider),
        };

        public string? Structure { get; set; }
        public string? CommonName { get; set; }
        public string? Type { get; set; }

        public List<string>? Colors_RGB { get; set; }
        public string? RGB { get; set; }

        public List<string> Template_Recommend { get; set; } = new List<string>();
        public List<string> Template_Consider { get; set; } = new List<string>();

        public string? SchemeCode { get; set; }
        public string? Scheme { get; set; } = OntologyDefaults.Scheme;
        public string? ContextGroupVersion { get; set; } = OntologyDefaults.ContextGroupVersion;
        public string? MappingResource { get; set; } = OntologyDefaults.MappingResource;
        public string? ContextIdentifier { get; set; } = OntologyDefaults.ContextIdentifier;
        public string? MappingResourceName { get; set; } = OntologyDefaults.MappingResourceName;
        public string? MappingResourceUID { get; set; } = OntologyDefaults.MappingResourceUID;
        public string? ContextUID { get; set; } = OntologyDefaults.ContextUID;

        /// <summary>The Airtable record id (rec...). Never sent as a field.</summary>
        [JsonIgnore]
        public string? Id { get; set; }

        public string? TG_263 { get; set; }
        public string? TG_263R { get; set; }
        public string? TG_263Spanish { get; set; }
        public string? TG_263SpanishR { get; set; }
        public string? TG_263French { get; set; }
        public string? TG_263FrenchR { get; set; }

        public string? DVH_Color { get; set; }
        public string? DVH_Style { get; set; }
        public string? DVH_Width { get; set; }
        public string? DVH_Type_Index { get; set; }
        public string? DVH_ContourStyle { get; set; }

        /// <summary>
        /// Maps an API record to an entry. Omitted cells keep the defaults above, exactly as the
        /// previous RetrieveRecord&lt;AirTableEntry&gt; deserialisation did.
        /// </summary>
        public static AirTableEntry FromRecord(AirtableRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            var entry = new AirTableEntry();
            JObject fields = record.Fields ?? new JObject();
            entry.Structure = Scalar(fields, nameof(Structure), entry.Structure);
            entry.CommonName = Scalar(fields, nameof(CommonName), entry.CommonName);
            entry.Type = Scalar(fields, nameof(Type), entry.Type);
            entry.Colors_RGB = List(fields, nameof(Colors_RGB)) ?? entry.Colors_RGB;
            entry.RGB = Scalar(fields, nameof(RGB), entry.RGB);
            entry.Template_Recommend = List(fields, nameof(Template_Recommend)) ?? entry.Template_Recommend;
            entry.Template_Consider = List(fields, nameof(Template_Consider)) ?? entry.Template_Consider;
            entry.SchemeCode = Scalar(fields, nameof(SchemeCode), entry.SchemeCode);
            entry.Scheme = Scalar(fields, nameof(Scheme), entry.Scheme);
            entry.ContextGroupVersion = Scalar(fields, nameof(ContextGroupVersion), entry.ContextGroupVersion);
            entry.MappingResource = Scalar(fields, nameof(MappingResource), entry.MappingResource);
            entry.ContextIdentifier = Scalar(fields, nameof(ContextIdentifier), entry.ContextIdentifier);
            entry.MappingResourceName = Scalar(fields, nameof(MappingResourceName), entry.MappingResourceName);
            entry.MappingResourceUID = Scalar(fields, nameof(MappingResourceUID), entry.MappingResourceUID);
            entry.ContextUID = Scalar(fields, nameof(ContextUID), entry.ContextUID);
            entry.TG_263 = Scalar(fields, nameof(TG_263), entry.TG_263);
            entry.TG_263R = Scalar(fields, nameof(TG_263R), entry.TG_263R);
            entry.TG_263Spanish = Scalar(fields, nameof(TG_263Spanish), entry.TG_263Spanish);
            entry.TG_263SpanishR = Scalar(fields, nameof(TG_263SpanishR), entry.TG_263SpanishR);
            entry.TG_263French = Scalar(fields, nameof(TG_263French), entry.TG_263French);
            entry.TG_263FrenchR = Scalar(fields, nameof(TG_263FrenchR), entry.TG_263FrenchR);
            entry.DVH_Color = Scalar(fields, nameof(DVH_Color), entry.DVH_Color);
            entry.DVH_Style = Scalar(fields, nameof(DVH_Style), entry.DVH_Style);
            entry.DVH_Width = Scalar(fields, nameof(DVH_Width), entry.DVH_Width);
            entry.DVH_Type_Index = Scalar(fields, nameof(DVH_Type_Index), entry.DVH_Type_Index);
            entry.DVH_ContourStyle = Scalar(fields, nameof(DVH_ContourStyle), entry.DVH_ContourStyle);
            entry.Id = record.Id;
            return entry;
        }

        /// <summary>Value of a mapped column by name (lists are returned as IReadOnlyList&lt;string&gt;).</summary>
        public object? GetField(string name)
        {
            switch (name)
            {
                case nameof(Structure): return Structure;
                case nameof(CommonName): return CommonName;
                case nameof(Type): return Type;
                case nameof(Colors_RGB): return Colors_RGB;
                case nameof(RGB): return RGB;
                case nameof(Template_Recommend): return Template_Recommend;
                case nameof(Template_Consider): return Template_Consider;
                case nameof(SchemeCode): return SchemeCode;
                case nameof(Scheme): return Scheme;
                case nameof(ContextGroupVersion): return ContextGroupVersion;
                case nameof(MappingResource): return MappingResource;
                case nameof(ContextIdentifier): return ContextIdentifier;
                case nameof(MappingResourceName): return MappingResourceName;
                case nameof(MappingResourceUID): return MappingResourceUID;
                case nameof(ContextUID): return ContextUID;
                case nameof(TG_263): return TG_263;
                case nameof(TG_263R): return TG_263R;
                case nameof(TG_263Spanish): return TG_263Spanish;
                case nameof(TG_263SpanishR): return TG_263SpanishR;
                case nameof(TG_263French): return TG_263French;
                case nameof(TG_263FrenchR): return TG_263FrenchR;
                case nameof(DVH_Color): return DVH_Color;
                case nameof(DVH_Style): return DVH_Style;
                case nameof(DVH_Width): return DVH_Width;
                case nameof(DVH_Type_Index): return DVH_Type_Index;
                case nameof(DVH_ContourStyle): return DVH_ContourStyle;
                default: throw new ArgumentException("Unknown template field " + name, nameof(name));
            }
        }

        /// <summary>
        /// Parses the display colour. "RGB" ("r,g,b") wins; otherwise the first "Colors_RGB" option
        /// ("Name:r,g,b"). Returns false when neither parses.
        /// </summary>
        public bool TryGetRgb(out byte r, out byte g, out byte b)
        {
            if (TryParseTriplet(RGB, out r, out g, out b))
            {
                return true;
            }

            if (Colors_RGB != null && Colors_RGB.Count > 0 && Colors_RGB[0] != null)
            {
                string first = Colors_RGB[0];
                int colon = first.IndexOf(':');
                if (colon >= 0 && TryParseTriplet(first.Substring(colon + 1), out r, out g, out b))
                {
                    return true;
                }
            }

            r = g = b = 0;
            return false;
        }

        private static bool TryParseTriplet(string? text, out byte r, out byte g, out byte b)
        {
            r = g = b = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] parts = text!.Split(',');
            return parts.Length >= 3
                && byte.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out r)
                && byte.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out g)
                && byte.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out b);
        }

        internal static string? Scalar(JObject fields, string name, string? fallback)
        {
            if (!fields.TryGetValue(name, StringComparison.Ordinal, out JToken? token) || token == null || token.Type == JTokenType.Null)
            {
                return fallback;
            }

            return TokenToString(token);
        }

        internal static string? TokenToString(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.String:
                    return (string?)token;
                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.Boolean:
                    return Convert.ToString(((JValue)token).Value, CultureInfo.InvariantCulture);
                case JTokenType.Array:
                    // A column we treat as text but the table stores as a list; keep the first value.
                    JToken? first = token.First;
                    return first == null ? null : TokenToString(first);
                default:
                    return token.ToString(Formatting.None);
            }
        }

        internal static List<string>? List(JObject fields, string name)
        {
            if (!fields.TryGetValue(name, StringComparison.Ordinal, out JToken? token) || token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            if (token is JArray array)
            {
                return array.Select(TokenToString).Where(s => s != null).Select(s => s!).ToList();
            }

            string? single = TokenToString(token);
            return single == null ? new List<string>() : new List<string> { single };
        }
    }
}
