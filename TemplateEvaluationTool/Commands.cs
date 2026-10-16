using System.Text;
using System.Text.RegularExpressions;
using DicomTemplateMakerGUI.DicomTemplateServices;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.Shell;
using DicomTemplateMakerGUI.Validation;
using FellowOakDicom;
using Newtonsoft.Json.Linq;
using TemplateSync.Model;
using TemplateSync.Storage;

namespace TemplateEvaluationTool
{
    /// <summary>
    /// <c>build-sites</c>: builds every site of a snapshot as a template folder with the window's default naming
    /// choice (<see cref="NamingChoice.Default"/>), and writes build-warnings.json and sites.json.
    /// </summary>
    internal static class BuildSitesCommand
    {
        public const string WarningsFileName = "build-warnings.json";
        public const string SitesFileName = "sites.json";

        public static int Run(IReadOnlyList<string> args)
        {
            var options = new Options(args, "snapshot", "naming", "data-dir", "out");
            string root = RunContext.RepositoryRoot();
            string snapshotPath = Path.GetFullPath(options.Required("snapshot"));
            if (options.Required("naming") != "default")
            {
                throw new UsageException("--naming supports only 'default' (English, side last, as the window opens).");
            }

            string dataDir = RunContext.SafeFolder(options.Required("data-dir"), "data-dir");
            string outDir = RunContext.EmptyOutputFolder(options.Required("out"), "out");
            JObject snapshotInput = RunContext.Input("snapshot", snapshotPath, root);

            TableSnapshot snapshot = SnapshotStore.Parse(File.ReadAllText(snapshotPath, Encoding.UTF8));
            TemplateIndex index = TemplateIndex.Build(snapshot.Records.Select(AirTableEntry.FromRecord));
            NamingChoice naming = NamingChoice.Default(index);
            List<string> sites = index.SiteNames.OrderBy(s => s, StringComparer.Ordinal).ToList();
            var warnings = new List<SiteBuildWarning>();
            List<SiteJob> jobs = SiteBuilder.PrepareAll(index, sites, naming, warnings);
            var builder = new SiteBuilder(outDir, Path.Combine(dataDir, "Ontologies"));

            int exitCode = 0;
            try
            {
                builder.Build(jobs, new ConsoleProgress(), CancellationToken.None);
            }
            catch (SiteBuildException ex)
            {
                Console.Error.WriteLine("The build stopped after " + ex.Built + " of " + jobs.Count + " sites: " + (ex.InnerException ?? ex).Message);
                exitCode = 1;
            }

            RunContext.WriteJson(Path.Combine(outDir, WarningsFileName), JArray.FromObject(warnings, RunContext.Serializer()));
            var siteRows = new JArray();
            foreach (string site in sites)
            {
                IReadOnlyList<SiteRoi> rois = index.GetSite(site);
                SiteJob? job = jobs.FirstOrDefault(j => j.Site == site);
                siteRows.Add(new JObject
                {
                    ["site"] = site,
                    ["built"] = job != null && File.Exists(Path.Combine(builder.TemplatePath(site), "All_ROIs.json")),
                    ["indexRois"] = rois.Count,
                    ["recommendRois"] = rois.Count(r => r.Include),
                    ["considerRois"] = rois.Count(r => !r.Include),
                    ["builtRois"] = job?.Rois.Count ?? 0,
                    ["warnings"] = warnings.Count(w => w.Site == site),
                    ["incompleteCodes"] = warnings.Count(w => w.Site == site && w.Kind == SiteBuildWarningKind.IncompleteCode),
                });
            }

            RunContext.WriteJson(Path.Combine(outDir, SitesFileName), new JObject
            {
                ["naming"] = JObject.FromObject(naming, RunContext.Serializer()),
                ["siteCount"] = sites.Count,
                ["builtCount"] = siteRows.Count(r => (bool)r["built"]!),
                ["sites"] = siteRows,
            });
            RunContext.WriteManifest(outDir, "build-sites", args, new[] { snapshotInput }, root);
            Console.WriteLine($"{siteRows.Count(r => (bool)r["built"]!)} of {sites.Count} sites built into {outDir}; {warnings.Count} warning(s).");
            return exitCode;
        }
    }

    /// <summary>
    /// <c>generate-rts</c>: copies the sample CT into <c>--out/series</c> and writes one RT per site there with
    /// <see cref="TestRtGenerator.Run"/>; serialises the run reports to run-report.json.
    /// </summary>
    internal static class GenerateRtsCommand
    {
        public const string ReportFileName = "run-report.json";

        public static int Run(IReadOnlyList<string> args)
        {
            var options = new Options(args, "sites", "images", "out", "template-rs");
            string root = RunContext.RepositoryRoot();
            string sitesDir = RunContext.SafeFolder(options.Required("sites"), "sites");
            string images = Path.GetFullPath(options.Required("images"));
            string templateRs = Path.GetFullPath(options.Optional("template-rs") ?? Path.Combine(root, "DicomTemplateMakerGUI", "template_RS.dcm"));
            string outDir = RunContext.EmptyOutputFolder(options.Required("out"), "out");
            string seriesDir = Path.Combine(outDir, "series");
            var inputs = new[]
            {
                RunContext.Input("sites", sitesDir, root),
                RunContext.Input("images", images, root),
                RunContext.Input("template-rs", templateRs, root),
            };

            List<string> names = Directory.GetDirectories(sitesDir)
                .Where(d => File.Exists(Path.Combine(d, "All_ROIs.json")))
                .Select(d => Path.GetFileName(d))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            var runner = new DicomTemplateRunner(sitesDir, templateRs, null, TimeProvider.System);
            TestRtResult result = TestRtGenerator.Run(runner, names, images, seriesDir, new ConsoleProgress(), CancellationToken.None);

            var report = new JObject
            {
                ["sampleFilesCopied"] = result.SampleFilesCopied,
                ["writtenCount"] = result.WrittenCount,
                ["hasFailures"] = result.HasFailures,
                ["errors"] = new JArray(result.Errors.Cast<object>().ToArray()),
                ["reports"] = new JArray(result.Reports.Select(r => (object)new JObject
                {
                    ["template"] = r.Template,
                    ["report"] = JObject.FromObject(r.Report, RunContext.Serializer()),
                }).ToArray()),
            };
            RunContext.WriteJson(Path.Combine(outDir, ReportFileName), RunReportMasking.Mask(report, outDir));
            RunContext.WriteManifest(outDir, "generate-rts", args, inputs, root);
            Console.WriteLine($"{result.WrittenCount} RT(s) for {names.Count} site(s) in {seriesDir}; failures: {result.HasFailures}.");
            return result.Errors.Count > 0 ? 3 : 0;
        }
    }

    /// <summary>
    /// Makes run-report.json reproducible: the output folder becomes <c>&lt;out&gt;</c>, UIDs become <c>UID#n</c>
    /// (as in the inventories, numbered within this file), StartedAt and Duration are masked. A UID needs four or
    /// more components, so version numbers such as 1.1.0 stay visible; UUID-derived UIDs (<c>2.25.</c> and one
    /// integer) have three and are matched by their root.
    /// </summary>
    internal static class RunReportMasking
    {
        private static readonly Regex Uid = new Regex(@"(?<![0-9]|[0-9]\.)([0-2](\.(0|[1-9][0-9]*)){3,}|2\.25\.(0|[1-9][0-9]*))(?![0-9]|\.[0-9])", RegexOptions.CultureInvariant);

        public static JToken Mask(JToken report, string outDir)
        {
            var masker = new UidMasker();
            string full = Path.GetFullPath(outDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Walk(report, masker, full);
            return report;
        }

        private static void Walk(JToken token, UidMasker masker, string outDir)
        {
            if (token is JObject obj)
            {
                foreach (JProperty property in obj.Properties().ToList())
                {
                    if (property.Name == "StartedAt")
                    {
                        property.Value = "<DATETIME>";
                    }
                    else if (property.Name == "Duration")
                    {
                        property.Value = "<DURATION>";
                    }
                    else
                    {
                        Walk(property.Value, masker, outDir);
                    }
                }
            }
            else if (token is JArray array)
            {
                foreach (JToken child in array)
                {
                    Walk(child, masker, outDir);
                }
            }
            else if (token is JValue value && value.Type == JTokenType.String)
            {
                string text = ((string)value.Value!).Replace(outDir, "<out>", StringComparison.OrdinalIgnoreCase).Replace('\\', '/');
                value.Value = Uid.Replace(text, m => masker.Mask(m.Value));
            }
        }
    }

    /// <summary><c>inventory</c>: one text file per RT Structure Set under <c>--in</c> (see <see cref="DicomInventory"/>).</summary>
    internal static class InventoryCommand
    {
        public static int Run(IReadOnlyList<string> args)
        {
            var options = new Options(args, "in", "out");
            string root = RunContext.RepositoryRoot();
            string inDir = RunContext.SafeFolder(options.Required("in"), "in");
            string outDir = RunContext.EmptyOutputFolder(options.Required("out"), "out");
            var rtFiles = new JArray();
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.EnumerateFiles(inDir, "*.dcm", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                DicomFile file = DicomFile.Open(path, FileReadOption.ReadAll);
                if (file.Dataset.GetSingleValueOrDefault(DicomTag.Modality, string.Empty) != "RTSTRUCT")
                {
                    continue;
                }

                string name = Unique(SiteName(Path.GetFileNameWithoutExtension(path)), written);
                RunContext.WriteText(Path.Combine(outDir, name + ".txt"), DicomInventory.Write(file));
                rtFiles.Add(new JObject { ["path"] = Path.GetRelativePath(inDir, path).Replace('\\', '/'), ["sha256"] = RunContext.Sha256(path), ["inventory"] = name + ".txt" });
            }

            var input = new JObject { ["role"] = "rt", ["path"] = RunContext.DisplayPath(inDir, root), ["files"] = rtFiles };
            RunContext.WriteManifest(outDir, "inventory", args, new[] { input }, root);
            Console.WriteLine($"{rtFiles.Count} inventories written to {outDir}.");
            return 0;
        }

        /// <summary>The template name of a generated RT, <c>{template}_UID{series}</c>; other names are kept.</summary>
        internal static string SiteName(string fileStem)
        {
            int marker = fileStem.LastIndexOf("_UID", StringComparison.Ordinal);
            return marker > 0 ? fileStem.Substring(0, marker) : fileStem;
        }

        private static string Unique(string name, HashSet<string> written)
        {
            string candidate = name;
            for (int i = 2; !written.Add(candidate); i++)
            {
                candidate = name + "-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return candidate;
        }
    }

    /// <summary>
    /// <c>realism</c>: runs the TPS-realism rules (<see cref="RtRealismChecker"/>) on every RT Structure Set under
    /// <c>--in</c>, the cross-file rules over all of them together, and writes realism.json (every finding) and
    /// realism.md. Errors must be covered by a <c>"tool": "realism"</c> entry of <c>--allowlist</c>; warnings are
    /// reported only. Exits 1 when an error is not allowlisted or no RT was found.
    /// </summary>
    internal static class RealismCommand
    {
        public const string JsonFileName = "realism.json";
        public const string MarkdownFileName = "realism.md";

        public static int Run(IReadOnlyList<string> args)
        {
            var options = new Options(args, "in", "out", "allowlist");
            string root = RunContext.RepositoryRoot();
            string inDir = RunContext.SafeFolder(options.Required("in"), "in");
            string? allowlistPath = options.Optional("allowlist") is string a ? Path.GetFullPath(a) : null;
            if (allowlistPath != null && !File.Exists(allowlistPath))
            {
                throw new UsageException("--allowlist " + allowlistPath + " does not exist.");
            }

            string outDir = RunContext.EmptyOutputFolder(options.Required("out"), "out");
            IReadOnlyList<RealismAllowEntry> allowlist = allowlistPath == null ? Array.Empty<RealismAllowEntry>() : RealismAllowEntry.Load(allowlistPath);

            var rts = new List<(string Name, DicomDataset Rt)>();
            var rtFiles = new JArray();
            foreach (string path in Directory.EnumerateFiles(inDir, "*.dcm", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                DicomFile file = DicomFile.Open(path, FileReadOption.ReadAll);
                if (file.Dataset.GetSingleValueOrDefault(DicomTag.Modality, string.Empty) != "RTSTRUCT")
                {
                    continue;
                }

                string name = Path.GetRelativePath(inDir, path).Replace('\\', '/');
                rts.Add((name, file.Dataset));
                rtFiles.Add(new JObject { ["path"] = name, ["sha256"] = RunContext.Sha256(path) });
            }

            var findings = new List<(string File, RealismFinding Finding)>();
            foreach ((string name, DicomDataset rt) in rts)
            {
                findings.AddRange(RtRealismChecker.Check(rt).Select(f => (name, f)));
            }

            findings.AddRange(RtRealismChecker.CheckSet(rts));
            findings = findings.OrderBy(f => f.File, StringComparer.Ordinal).ThenBy(f => RuleOrder(f.Finding.RuleId)).ToList();
            var rows = findings.Select(f => (f.File, f.Finding, Allowed: f.Finding.Severity == RealismSeverity.Error ? allowlist.FirstOrDefault(e => e.Matches(f.Finding)) : null)).ToList();
            int unallowed = rows.Count(r => r.Finding.Severity == RealismSeverity.Error && r.Allowed == null);
            bool passed = rts.Count > 0 && unallowed == 0;

            RunContext.WriteJson(Path.Combine(outDir, JsonFileName), new JObject
            {
                ["files"] = rts.Count,
                ["allowlist"] = allowlistPath == null ? null : RunContext.DisplayPath(allowlistPath, root),
                ["errors"] = rows.Count(r => r.Finding.Severity == RealismSeverity.Error),
                ["allowlistedErrors"] = rows.Count(r => r.Allowed != null),
                ["unallowedErrors"] = unallowed,
                ["warnings"] = rows.Count(r => r.Finding.Severity == RealismSeverity.Warning),
                ["passed"] = passed,
                ["findings"] = new JArray(rows.Select(r =>
                {
                    var row = new JObject
                    {
                        ["file"] = r.File,
                        ["rule"] = r.Finding.RuleId,
                        ["severity"] = r.Finding.Severity.ToString().ToLowerInvariant(),
                        ["message"] = r.Finding.Message,
                    };
                    if (r.Allowed != null)
                    {
                        row["allowlistedAs"] = r.Allowed.Id;
                    }

                    return (object)row;
                }).ToArray()),
            });

            string markdown = Markdown(rts.Count, allowlistPath == null ? null : RunContext.DisplayPath(allowlistPath, root), rows, passed);
            RunContext.WriteText(Path.Combine(outDir, MarkdownFileName), markdown);
            var inputs = new List<JObject> { new JObject { ["role"] = "rt", ["path"] = RunContext.DisplayPath(inDir, root), ["files"] = rtFiles } };
            if (allowlistPath != null)
            {
                inputs.Add(RunContext.Input("allowlist", allowlistPath, root));
            }

            RunContext.WriteManifest(outDir, "realism", args, inputs, root);
            Console.Write(markdown);
            if (rts.Count == 0)
            {
                Console.Error.WriteLine("No RT Structure Set files under " + inDir + ".");
            }

            return passed ? 0 : 1;
        }

        private static string Markdown(int fileCount, string? allowlist, IReadOnlyList<(string File, RealismFinding Finding, RealismAllowEntry? Allowed)> rows, bool passed)
        {
            var text = new StringBuilder();
            text.Append("# TPS-realism report\n\n");
            text.Append($"Files: {fileCount} RT Structure Set(s). Allowlist: {(allowlist == null ? "none" : "`" + allowlist + "`")}. ");
            text.Append($"Gate: every error must be allowlisted; warnings are reported only. Result: **{(passed ? "PASS" : "FAIL")}**.\n\n");
            text.Append("| Rule | Checks | Errors | Allowlisted | Warnings |\n|---|---|---|---|---|\n");
            foreach ((string rule, string checks) in RtRealismChecker.Rules.OrderBy(r => RuleOrder(r.Key)))
            {
                var ofRule = rows.Where(r => r.Finding.RuleId == rule).ToList();
                text.Append($"| {rule} | {checks} | {ofRule.Count(r => r.Finding.Severity == RealismSeverity.Error)} | {ofRule.Count(r => r.Allowed != null)} | {ofRule.Count(r => r.Finding.Severity == RealismSeverity.Warning)} |\n");
            }

            AppendSection(text, "Errors not in the allowlist", rows.Where(r => r.Finding.Severity == RealismSeverity.Error && r.Allowed == null).Select(r => $"`{r.Finding.RuleId}` {r.File}: {r.Finding.Message}"));
            AppendSection(text, "Allowlisted errors", Grouped(rows.Where(r => r.Allowed != null), r => $"`{r.Finding.RuleId}` {r.Allowed!.Id}"));
            AppendSection(text, "Warnings (not gated)", Grouped(rows.Where(r => r.Finding.Severity == RealismSeverity.Warning), r => $"`{r.Finding.RuleId}`"));
            return text.ToString();
        }

        /// <summary>One line per distinct message: how often it occurs and in how many files.</summary>
        private static IEnumerable<string> Grouped(IEnumerable<(string File, RealismFinding Finding, RealismAllowEntry? Allowed)> rows, Func<(string File, RealismFinding Finding, RealismAllowEntry? Allowed), string> prefix)
            => rows.GroupBy(r => (Prefix: prefix(r), r.Finding.RuleId, r.Finding.Message))
                .OrderBy(g => RuleOrder(g.Key.RuleId)).ThenBy(g => g.Key.Message, StringComparer.Ordinal)
                .Select(g => $"{g.Key.Prefix} x{g.Count()} in {g.Select(r => r.File).Distinct(StringComparer.Ordinal).Count()} file(s): {g.Key.Message}");

        private static void AppendSection(StringBuilder text, string title, IEnumerable<string> lines)
        {
            text.Append("\n## ").Append(title).Append("\n\n");
            bool any = false;
            foreach (string line in lines)
            {
                text.Append("- ").Append(line).Append('\n');
                any = true;
            }

            if (!any)
            {
                text.Append("none\n");
            }
        }

        /// <summary>RR2 before RR10.</summary>
        private static int RuleOrder(string rule)
            => rule.StartsWith("RR", StringComparison.Ordinal) && int.TryParse(rule.AsSpan(2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int n) ? n : int.MaxValue;
    }

    /// <summary>
    /// A <c>"tool": "realism"</c> entry of docs/evidence/validator-allowlist.json: an error of rule <c>rule</c> whose
    /// message matches the regular expression <c>pattern</c> is accepted, for the reason the gap <c>id</c> names.
    /// Entries for other tools (the standard validators) are skipped.
    /// </summary>
    internal sealed class RealismAllowEntry
    {
        public const string Tool = "realism";

        private readonly Regex pattern;

        private RealismAllowEntry(string rule, string pattern, string id)
        {
            Rule = rule;
            Id = id;
            this.pattern = new Regex(pattern, RegexOptions.CultureInvariant);
        }

        public string Rule { get; }

        public string Id { get; }

        public bool Matches(RealismFinding finding) => finding.RuleId == Rule && pattern.IsMatch(finding.Message);

        public static IReadOnlyList<RealismAllowEntry> Load(string path)
        {
            JToken data = JToken.Parse(File.ReadAllText(path, Encoding.UTF8));
            JToken entries = data is JObject obj ? obj["entries"] ?? new JArray() : data;
            var result = new List<RealismAllowEntry>();
            foreach (JObject entry in entries.OfType<JObject>().Where(e => (string?)e["tool"] == Tool))
            {
                string rule = (string?)entry["rule"] ?? throw new UsageException("A realism allowlist entry has no \"rule\".");
                string pattern = (string?)entry["pattern"] ?? throw new UsageException("A realism allowlist entry has no \"pattern\".");
                string id = (string?)entry["id"] ?? throw new UsageException("A realism allowlist entry has no \"id\".");
                result.Add(new RealismAllowEntry(rule, pattern, id));
            }

            return result;
        }
    }
}
