using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TemplateSync.Airtable;
using TemplateSync.Infrastructure;
using TemplateSync.Model;

namespace TemplateSync.Storage
{
    public enum SnapshotCheckKind
    {
        /// <summary>A failure stops publication.</summary>
        Gate,

        /// <summary>Reported only (until the maintainers decide which invariants become gates).</summary>
        Warning,
    }

    /// <summary>One rule of <see cref="SnapshotCheck"/> and every finding it produced.</summary>
    public sealed class SnapshotCheckRule
    {
        public SnapshotCheckRule(string id, SnapshotCheckKind kind, string description)
        {
            Id = id;
            Kind = kind;
            Description = description;
        }

        public string Id { get; }

        public SnapshotCheckKind Kind { get; }

        public string Description { get; }

        public List<string> Findings { get; } = new List<string>();

        public bool Failed => Findings.Count > 0;
    }

    /// <summary>The outcome of checking one snapshot file.</summary>
    public sealed class SnapshotCheckReport
    {
        internal SnapshotCheckReport(IReadOnlyList<SnapshotCheckRule> rules, TableSnapshot? snapshot)
        {
            Rules = rules;
            Snapshot = snapshot;
        }

        public IReadOnlyList<SnapshotCheckRule> Rules { get; }

        /// <summary>The snapshot as written (recordCount not recomputed); null when the file did not parse.</summary>
        public TableSnapshot? Snapshot { get; }

        public bool Passed => Rules.Where(r => r.Kind == SnapshotCheckKind.Gate).All(r => !r.Failed);

        public SnapshotCheckRule Rule(string id) => Rules.First(r => r.Id == id);

        /// <summary>Markdown for a pull request body or a job summary; at most <paramref name="maxFindingsPerRule"/> findings per rule are listed.</summary>
        public string ToMarkdown(string fileLabel, string? sha256, int maxFindingsPerRule = 50)
        {
            var text = new StringBuilder();
            text.Append("## TG-263 snapshot check: ").Append(Passed ? "passed" : "FAILED").Append('\n').Append('\n');
            text.Append("File `").Append(fileLabel).Append('`');
            if (sha256 != null)
            {
                text.Append(", SHA-256 `").Append(sha256).Append('`');
            }

            if (Snapshot != null)
            {
                text.Append(", ").Append(Snapshot.Records.Count.ToString(CultureInfo.InvariantCulture)).Append(" records (recordCount ")
                    .Append(Snapshot.RecordCount.ToString(CultureInfo.InvariantCulture)).Append("), base `").Append(Snapshot.BaseId)
                    .Append("`, table `").Append(Snapshot.TableId).Append("`, generated ")
                    .Append(Snapshot.GeneratedAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            }

            text.Append(".\n\n");
            text.Append("| Kind | Check | Result |\n| --- | --- | --- |\n");
            foreach (SnapshotCheckRule rule in Rules)
            {
                string result = rule.Kind == SnapshotCheckKind.Gate
                    ? (rule.Failed ? "**FAIL** (" + rule.Findings.Count.ToString(CultureInfo.InvariantCulture) + ")" : "pass")
                    : rule.Findings.Count.ToString(CultureInfo.InvariantCulture) + (rule.Findings.Count == 1 ? " warning" : " warnings");
                text.Append("| ").Append(rule.Kind).Append(" | ").Append(rule.Description).Append(" | ").Append(result).Append(" |\n");
            }

            foreach (SnapshotCheckRule rule in Rules.Where(r => r.Failed))
            {
                text.Append('\n').Append("### ").Append(rule.Kind).Append(": ").Append(rule.Description).Append('\n').Append('\n');
                foreach (string finding in rule.Findings.Take(maxFindingsPerRule))
                {
                    text.Append("- ").Append(finding).Append('\n');
                }

                if (rule.Findings.Count > maxFindingsPerRule)
                {
                    text.Append("- … and ").Append((rule.Findings.Count - maxFindingsPerRule).ToString(CultureInfo.InvariantCulture)).Append(" more\n");
                }
            }

            return text.ToString();
        }
    }

    /// <summary>
    /// Publication gate for the TG-263 snapshot (<c>TemplateSnapshotTool check</c>). Gates stop the refresh
    /// workflow from proposing a snapshot; warnings record known invariants without blocking.
    /// </summary>
    public static class SnapshotCheck
    {
        /// <summary>Exit code of <c>TemplateSnapshotTool check</c> when a gate fails.</summary>
        public const int FailedGateExitCode = 5;

        /// <summary>The shared table holds 246 records (2026-09-30); a much smaller export means a broken read or a damaged table.</summary>
        public const int MinimumRecords = 240;

        public const string ParsesRule = "parses";
        public const string RecordCountRule = "record-count";
        public const string RequiredFieldsRule = "required-fields";
        public const string ColoursRule = "colours";
        public const string SiteNamesRule = "site-folder-names";
        public const string UniqueStructureRule = "unique-structure";
        public const string InterpretedTypeRule = "interpreted-type";
        public const string SchemeCodeRule = "scheme-code";

        /// <summary>The 25 defined terms of RT ROI Interpreted Type (3006,00A4), DICOM PS3.3 C.8.8.8.</summary>
        public static readonly IReadOnlyList<string> InterpretedTypes = new[]
        {
            "EXTERNAL", "PTV", "CTV", "GTV", "TREATED_VOLUME", "IRRAD_VOLUME", "OAR", "BOLUS", "AVOIDANCE", "ORGAN",
            "MARKER", "REGISTRATION", "ISOCENTER", "CONTRAST_AGENT", "CAVITY", "BRACHY_CHANNEL", "BRACHY_ACCESSORY",
            "BRACHY_SRC_APP", "BRACHY_CHNL_SHLD", "SUPPORT", "FIXATION", "DOSE_REGION", "CONTROL", "DOSE_MEASUREMENT", "DEVICE",
        };

        private static readonly HashSet<string> InterpretedTypeSet = new HashSet<string>(InterpretedTypes, StringComparer.Ordinal);

        // Windows rules regardless of the OS running the check, because the program builds site folders on Windows.
        private static readonly char[] InvalidFolderNameChars = "<>:\"/\\|?*".ToCharArray()
            .Concat(Enumerable.Range(0, 32).Select(i => (char)i)).ToArray();

        private static readonly HashSet<string> ReservedDeviceNames = new HashSet<string>(
            new[] { "CON", "PRN", "AUX", "NUL" }
                .Concat(Enumerable.Range(1, 9).Select(i => "COM" + i.ToString(CultureInfo.InvariantCulture)))
                .Concat(Enumerable.Range(1, 9).Select(i => "LPT" + i.ToString(CultureInfo.InvariantCulture))),
            StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Reads, checks and summarises a snapshot file. Returns 0 when every gate passes, 5 when one fails
        /// and 2 when the file cannot be read. The Markdown summary goes to <paramref name="output"/> and,
        /// when given, to <paramref name="summaryPath"/>.
        /// </summary>
        public static int RunCommand(string path, string? summaryPath, TextWriter output)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                output.WriteLine($"Cannot read {path}: {ex.Message}");
                return 2;
            }

            string json;
            using (var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                json = reader.ReadToEnd();
            }

            SnapshotCheckReport report = Check(json);
            string sha256 = string.Concat(SHA256.HashData(bytes).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
            string markdown = report.ToMarkdown(Path.GetFileName(path), sha256);
            if (!string.IsNullOrEmpty(summaryPath))
            {
                string? directory = Path.GetDirectoryName(summaryPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(summaryPath, markdown, new UTF8Encoding(false));
            }

            output.Write(markdown);
            return report.Passed ? 0 : FailedGateExitCode;
        }

        public static SnapshotCheckReport Check(string json)
        {
            var parses = new SnapshotCheckRule(ParsesRule, SnapshotCheckKind.Gate, "File parses as a template snapshot");
            var count = new SnapshotCheckRule(RecordCountRule, SnapshotCheckKind.Gate, $"`recordCount` equals the number of records, at least {MinimumRecords}");
            var required = new SnapshotCheckRule(RequiredFieldsRule, SnapshotCheckKind.Gate, "Every record has `Structure`, `SchemeCode` and `Scheme`");
            var colours = new SnapshotCheckRule(ColoursRule, SnapshotCheckKind.Gate, "Every `Colors_RGB` option is `name:r,g,b` with 0-255");
            var sites = new SnapshotCheckRule(SiteNamesRule, SnapshotCheckKind.Warning, "Site names usable as folder names");
            var unique = new SnapshotCheckRule(UniqueStructureRule, SnapshotCheckKind.Warning, "`Structure` unique (exact, then ignoring case)");
            var types = new SnapshotCheckRule(InterpretedTypeRule, SnapshotCheckKind.Warning, "`Type` is one of the 25 PS3.3 RT ROI Interpreted Types");
            var codes = new SnapshotCheckRule(SchemeCodeRule, SnapshotCheckKind.Warning, "`SchemeCode` valid for `Scheme` (SCT: SCTID; FMA: integer)");
            var rules = new[] { parses, count, required, colours, sites, unique, types, codes };

            TableSnapshot? snapshot;
            try
            {
                snapshot = JsonConvert.DeserializeObject<TableSnapshot>(json, Json.Settings);
            }
            catch (JsonException ex)
            {
                parses.Findings.Add("Not valid snapshot JSON: " + ex.Message);
                return new SnapshotCheckReport(rules, null);
            }

            if (snapshot == null || snapshot.Records == null)
            {
                parses.Findings.Add("The file holds no snapshot object or no records array.");
                return new SnapshotCheckReport(rules, null);
            }

            if (snapshot.SchemaVersion != TableSnapshot.CurrentSchemaVersion)
            {
                parses.Findings.Add($"schemaVersion {snapshot.SchemaVersion}; this tool checks version {TableSnapshot.CurrentSchemaVersion}.");
            }

            List<AirtableRecord?> records = snapshot.Records.Cast<AirtableRecord?>().ToList();
            if (records.Any(r => r == null || string.IsNullOrEmpty(r.Id)))
            {
                parses.Findings.Add($"{records.Count(r => r == null || string.IsNullOrEmpty(r.Id))} record(s) without an id.");
            }

            if (snapshot.RecordCount != records.Count)
            {
                count.Findings.Add($"recordCount is {snapshot.RecordCount} but the file holds {records.Count} records.");
            }

            if (records.Count < MinimumRecords)
            {
                count.Findings.Add($"{records.Count} records; at least {MinimumRecords} are required.");
            }

            var siteNames = new List<string>();
            var seenSites = new HashSet<string>(StringComparer.Ordinal);
            var byStructure = new List<(string Structure, string Id)>();
            foreach (AirtableRecord? record in records)
            {
                if (record == null)
                {
                    continue;
                }

                JObject fields = record.Fields ?? new JObject();
                string? structure = AirTableEntry.Scalar(fields, nameof(AirTableEntry.Structure), null);
                string label = Label(record.Id, structure);

                string[] missing = new[] { nameof(AirTableEntry.Structure), nameof(AirTableEntry.SchemeCode), nameof(AirTableEntry.Scheme) }
                    .Where(name => string.IsNullOrWhiteSpace(AirTableEntry.Scalar(fields, name, null)))
                    .ToArray();
                if (missing.Length > 0)
                {
                    required.Findings.Add($"{label}: no {string.Join(", ", missing)}.");
                }

                foreach (string option in AirTableEntry.List(fields, nameof(AirTableEntry.Colors_RGB)) ?? new List<string>())
                {
                    if (!TryParseColourOption(option, out _, out _, out _, out _))
                    {
                        colours.Findings.Add($"{label}: `{option}` is not name:r,g,b with 0-255.");
                    }
                }

                foreach (string site in (AirTableEntry.List(fields, nameof(AirTableEntry.Template_Recommend)) ?? new List<string>())
                    .Concat(AirTableEntry.List(fields, nameof(AirTableEntry.Template_Consider)) ?? new List<string>()))
                {
                    if (seenSites.Add(site))
                    {
                        siteNames.Add(site);
                    }
                }

                if (!string.IsNullOrWhiteSpace(structure))
                {
                    byStructure.Add((structure!, record.Id));
                }

                string? type = AirTableEntry.Scalar(fields, nameof(AirTableEntry.Type), null);
                if (string.IsNullOrWhiteSpace(type))
                {
                    types.Findings.Add($"{label}: no Type.");
                }
                else if (!InterpretedTypeSet.Contains(type!))
                {
                    types.Findings.Add($"{label}: Type `{type}` is not a defined term.");
                }

                string? scheme = AirTableEntry.Scalar(fields, nameof(AirTableEntry.Scheme), null);
                string? code = AirTableEntry.Scalar(fields, nameof(AirTableEntry.SchemeCode), null);
                string? problem = SchemeCodeProblem(scheme, code);
                if (problem != null)
                {
                    codes.Findings.Add($"{label}: {scheme} code `{code}` {problem}.");
                }
            }

            foreach (string site in siteNames.Where(s => !IsUsableSiteName(s)))
            {
                sites.Findings.Add($"`{site}` cannot be a folder name.");
            }

            foreach (IGrouping<string, (string Structure, string Id)> group in byStructure.GroupBy(s => s.Structure, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                unique.Findings.Add($"`{group.Key}` appears {group.Count()} times: {string.Join(", ", group.Select(s => s.Id))}.");
            }

            foreach (IGrouping<string, (string Structure, string Id)> group in byStructure.GroupBy(s => s.Structure, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Select(s => s.Structure).Distinct(StringComparer.Ordinal).Count() > 1))
            {
                unique.Findings.Add($"Differ only in case: {string.Join(", ", group.Select(s => $"`{s.Structure}` ({s.Id})"))}.");
            }

            return new SnapshotCheckReport(rules, snapshot);
        }

        /// <summary>
        /// Whether a site name can be one folder directly inside the template folder on Windows (the program's
        /// rule, with the Windows character set on every OS, plus names Windows reserves or silently alters).
        /// </summary>
        public static bool IsUsableSiteName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." || name!.IndexOfAny(InvalidFolderNameChars) >= 0)
            {
                return false;
            }

            if (name.EndsWith(".", StringComparison.Ordinal) || name.EndsWith(" ", StringComparison.Ordinal) || name.StartsWith(" ", StringComparison.Ordinal))
            {
                return false;
            }

            int dot = name.IndexOf('.');
            return !ReservedDeviceNames.Contains(dot < 0 ? name : name.Substring(0, dot));
        }

        /// <summary>Parses a <c>Colors_RGB</c> option such as <c>Red:255,000,000</c>.</summary>
        public static bool TryParseColourOption(string? option, out string name, out byte r, out byte g, out byte b)
        {
            name = string.Empty;
            r = g = b = 0;
            int colon = option == null ? -1 : option.IndexOf(':');
            if (colon <= 0 || string.IsNullOrWhiteSpace(option!.Substring(0, colon)))
            {
                return false;
            }

            string[] parts = option.Substring(colon + 1).Split(',');
            if (parts.Length != 3
                || !TryParseByte(parts[0], out r)
                || !TryParseByte(parts[1], out g)
                || !TryParseByte(parts[2], out b))
            {
                return false;
            }

            name = option.Substring(0, colon).Trim();
            return true;
        }

        /// <summary>Why <paramref name="code"/> is not a valid code of <paramref name="scheme"/>; null when it is, or when the scheme is not checked.</summary>
        public static string? SchemeCodeProblem(string? scheme, string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null; // reported by the required-fields gate
            }

            switch (scheme)
            {
                case "SCT":
                    return SctIdProblem(code!);
                case "FMA":
                    return code!.Length > 0 && code.All(IsAsciiDigit) && code.Any(c => c != '0') ? null : "is not an integer";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Why <paramref name="code"/> is not a SNOMED CT concept identifier: 6-18 digits without a leading zero,
        /// a concept partition identifier (00, or 10 for an extension) and a valid Verhoeff check digit.
        /// </summary>
        public static string? SctIdProblem(string code)
        {
            if (code.Length < 6 || code.Length > 18 || !code.All(IsAsciiDigit))
            {
                return "is not 6-18 digits";
            }

            if (code[0] == '0')
            {
                return "starts with 0";
            }

            string partition = code.Substring(code.Length - 3, 2);
            if (partition != "00" && partition != "10")
            {
                return $"has partition {partition}, not a concept (00 or 10)";
            }

            return IsValidVerhoeff(code) ? null : "fails the Verhoeff check digit";
        }

        public static bool IsValidVerhoeff(string digits)
        {
            int check = 0;
            for (int i = 0; i < digits.Length; i++)
            {
                char c = digits[digits.Length - 1 - i];
                if (!IsAsciiDigit(c))
                {
                    return false;
                }

                check = VerhoeffD[check, VerhoeffP[i % 8, c - '0']];
            }

            return check == 0;
        }

        private static readonly int[,] VerhoeffD =
        {
            { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
            { 1, 2, 3, 4, 0, 6, 7, 8, 9, 5 },
            { 2, 3, 4, 0, 1, 7, 8, 9, 5, 6 },
            { 3, 4, 0, 1, 2, 8, 9, 5, 6, 7 },
            { 4, 0, 1, 2, 3, 9, 5, 6, 7, 8 },
            { 5, 9, 8, 7, 6, 0, 4, 3, 2, 1 },
            { 6, 5, 9, 8, 7, 1, 0, 4, 3, 2 },
            { 7, 6, 5, 9, 8, 2, 1, 0, 4, 3 },
            { 8, 7, 6, 5, 9, 3, 2, 1, 0, 4 },
            { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 },
        };

        private static readonly int[,] VerhoeffP =
        {
            { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
            { 1, 5, 7, 6, 2, 8, 3, 0, 9, 4 },
            { 5, 8, 0, 3, 7, 9, 6, 1, 4, 2 },
            { 8, 9, 1, 6, 0, 4, 3, 5, 2, 7 },
            { 9, 4, 5, 3, 1, 2, 6, 8, 7, 0 },
            { 4, 2, 8, 6, 5, 7, 3, 9, 0, 1 },
            { 2, 7, 9, 3, 8, 0, 6, 4, 1, 5 },
            { 7, 0, 4, 6, 9, 1, 3, 2, 5, 8 },
        };

        private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

        private static bool TryParseByte(string text, out byte value)
        {
            string trimmed = text.Trim();
            value = 0;
            return trimmed.Length > 0 && trimmed.All(IsAsciiDigit) && byte.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        private static string Label(string? id, string? structure)
            => string.IsNullOrWhiteSpace(structure) ? (id ?? "(no id)") : $"{id} `{structure}`";
    }
}
