using System.Text;
using System.Text.RegularExpressions;
using DicomTemplateMakerGUI.DicomTemplateServices;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.Shell;
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
    /// (as in the inventories, numbered within this file), StartedAt and Duration are masked.
    /// </summary>
    internal static class RunReportMasking
    {
        private static readonly Regex Uid = new Regex(@"(?<![0-9]|[0-9]\.)[0-2](\.(0|[1-9][0-9]*)){3,}(?![0-9]|\.[0-9])", RegexOptions.CultureInvariant);

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
}
