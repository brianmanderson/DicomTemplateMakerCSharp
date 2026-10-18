using DicomTemplateMakerGUI.Services;
using Newtonsoft.Json.Linq;
using ROIOntologyClass;
using TemplateSync.Airtable;
using TemplateSync.Credentials;
using TemplateSync.Infrastructure;
using TemplateSync.Model;
using TemplateSync.Storage;
using TemplateSync.Sync;

namespace TemplateEvaluationTool
{
    /// <summary>
    /// The write targets the tool accepts (plan P2.7). The public TG-263 source base and its table are refused
    /// unconditionally; any other base must be listed in <c>EVAL_TARGET_BASES</c> (comma-separated app ids). Checked
    /// before any file is written and before any HTTP request.
    /// </summary>
    internal static class WriteTargetPolicy
    {
        public const string PublicBaseId = "appYl68lenTOFqTDh";
        public const string PublicTableId = "tblex7IPsmm8hvVEc";
        public const string TargetBasesVariable = "EVAL_TARGET_BASES";

        /// <summary>Why the target is refused, or null when it may be written.</summary>
        public static string? Refusal(string baseId, string table, string? targetBases)
        {
            if (string.Equals(baseId.Trim(), PublicBaseId, StringComparison.OrdinalIgnoreCase))
            {
                return "Refused: " + PublicBaseId + " is the public TG-263 source base and is never a write target.";
            }

            if (string.Equals(table.Trim(), PublicTableId, StringComparison.OrdinalIgnoreCase))
            {
                return "Refused: " + PublicTableId + " is the public TG-263 source table and is never a write target.";
            }

            string[] allowed = (targetBases ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!allowed.Contains(baseId.Trim(), StringComparer.Ordinal))
            {
                return "Refused: base " + baseId.Trim() + " is not listed in " + TargetBasesVariable + " (comma-separated app ids of the throwaway bases this tool may write to).";
            }

            return null;
        }
    }

    /// <summary>What <see cref="WriteAirtableCommand"/> takes from its surroundings; tests replace the network and the environment.</summary>
    internal sealed class WriteAirtableHost
    {
        public Func<string, string?> GetEnvironmentVariable { get; init; } = Environment.GetEnvironmentVariable;

        /// <summary>The handler under the request log. Only called for a real write, after every check passed.</summary>
        public Func<HttpMessageHandler> CreateHandler { get; init; } = () => new SocketsHttpHandler();

        public IClock Clock { get; init; } = SystemClock.Instance;

        public RequestThrottle Throttle { get; init; } = RequestThrottle.Shared;

        public TimeSpan MaxJitter { get; init; } = TimeSpan.FromSeconds(2);
    }

    /// <summary>
    /// <c>write-airtable</c> (plan P2.7): publishes template folders to an Airtable table through the same mapping and
    /// write as the main window's "Write to Airtable" (<see cref="TemplateSourceItem.WriteTemplatesAsync"/>), with the
    /// cache in <c>--cache-dir</c>. Every request is logged to requests.jsonl (<see cref="LoggingHandler"/>); the per-site
    /// results go to write-result.json, plus the manifest. <c>--dry-run</c> plans from the cache (or an empty table)
    /// and sends nothing: it needs no token and writes dry-run.json instead.
    /// <para>Exit codes: 0 done, 1 Airtable reported an error (the completed sites are still recorded), 2 bad
    /// arguments or no token, 3 refused target or token.</para>
    /// </summary>
    internal static class WriteAirtableCommand
    {
        public const string ResultFileName = "write-result.json";
        public const string DryRunFileName = "dry-run.json";

        public static int Run(IReadOnlyList<string> args) => RunAsync(args, new WriteAirtableHost()).GetAwaiter().GetResult();

        public static async Task<int> RunAsync(IReadOnlyList<string> args, WriteAirtableHost host)
        {
            var options = new Options(args, new[] { "dry-run" }, "sites", "base", "table", "cache-dir", "out", "site");
            string baseId = options.Required("base").Trim();
            string table = options.Required("table").Trim();
            string? refusal = WriteTargetPolicy.Refusal(baseId, table, host.GetEnvironmentVariable(WriteTargetPolicy.TargetBasesVariable));
            if (refusal != null)
            {
                Console.Error.WriteLine(refusal);
                return 3;
            }

            if (!AirtableIds.IsBaseId(baseId))
            {
                throw new UsageException("--base should be app followed by 14 letters or digits.");
            }

            bool dryRun = options.Flag("dry-run");
            string root = RunContext.RepositoryRoot();
            string sitesDir = RunContext.SafeFolder(options.Required("sites"), "sites");
            string cacheDir = RunContext.SafeFolder(options.Required("cache-dir"), "cache-dir");
            List<KeyValuePair<string, IEnumerable<ROIClass>>> templates = LoadSites(sitesDir, options.All("site"));

            string? token = null;
            if (!dryRun)
            {
                token = host.GetEnvironmentVariable(AirtableConnectionStore.TokenEnvironmentVariable)?.Trim();
                if (string.IsNullOrEmpty(token))
                {
                    Console.Error.WriteLine("Set " + AirtableConnectionStore.TokenEnvironmentVariable + " to a token with data.records:read and data.records:write on the base (schema.bases:read is optional).");
                    return 2;
                }

                if (LeakedTokens.IsKnownLeaked(token))
                {
                    Console.Error.WriteLine("Refused: the token in " + AirtableConnectionStore.TokenEnvironmentVariable + " was published in this repository's history and must be revoked, not used.");
                    return 3;
                }
            }

            string outDir = RunContext.EmptyOutputFolder(options.Required("out"), "out");
            var store = new SnapshotStore(cacheDir);
            var definition = new AirtableSourceDefinition("Airtable " + baseId + "/" + table, baseId, table);
            var inputs = new List<JObject> { RunContext.Input("sites", sitesDir, root) };
            string cacheFile = store.PathFor(definition.CacheKey);
            if (File.Exists(cacheFile))
            {
                inputs.Add(RunContext.Input("cache", cacheFile, root));
            }

            int exitCode = dryRun
                ? await DryRunAsync(definition, store, templates, outDir, host).ConfigureAwait(false)
                : await WriteAsync(definition, store, templates, outDir, token!, host).ConfigureAwait(false);
            RunContext.WriteManifest(outDir, "write-airtable", args, inputs, root);
            return exitCode;
        }

        /// <summary>The template folders (those with All_ROIs.json) in name order, only the <c>--site</c> ones when any are given.</summary>
        internal static List<KeyValuePair<string, IEnumerable<ROIClass>>> LoadSites(string sitesDir, IReadOnlyList<string> only)
        {
            List<string> folders = Directory.GetDirectories(sitesDir)
                .Where(d => File.Exists(Path.Combine(d, ROIClassTools.RoisFileName)))
                .OrderBy(d => Path.GetFileName(d), StringComparer.Ordinal)
                .ToList();
            foreach (string site in only)
            {
                if (!folders.Any(d => string.Equals(Path.GetFileName(d), site, StringComparison.Ordinal)))
                {
                    throw new UsageException("--site " + site + ": " + sitesDir + " has no template folder of that name.");
                }
            }

            if (only.Count > 0)
            {
                folders = folders.Where(d => only.Contains(Path.GetFileName(d), StringComparer.Ordinal)).ToList();
            }

            if (folders.Count == 0)
            {
                throw new UsageException("--sites " + sitesDir + " holds no template folder (a folder with " + ROIClassTools.RoisFileName + ").");
            }

            var templates = new List<KeyValuePair<string, IEnumerable<ROIClass>>>();
            foreach (string folder in folders)
            {
                var warnings = new List<string>();
                List<ROIClass> rois;
                try
                {
                    rois = ROIClassTools.LoadROIsFromFolder(folder, new List<OntologyCodeClass>(), warnings);
                }
                catch (TemplateLoadException ex)
                {
                    throw new UsageException("Could not read the template in " + folder + ": " + ex.Message);
                }

                foreach (string warning in warnings)
                {
                    Console.WriteLine(Path.GetFileName(folder) + ": " + warning);
                }

                templates.Add(new KeyValuePair<string, IEnumerable<ROIClass>>(Path.GetFileName(folder), rois));
            }

            return templates;
        }

        private static async Task<int> DryRunAsync(AirtableSourceDefinition definition, SnapshotStore store, List<KeyValuePair<string, IEnumerable<ROIClass>>> templates, string outDir, WriteAirtableHost host)
        {
            var source = new AirtableTableSource(definition, new NoRequestsApi(), store, null, host.Clock);
            var item = new TemplateSourceItem(source);
            IReadOnlyList<WritePreview> previews;
            try
            {
                previews = await item.PreviewWritesAsync(templates, CancellationToken.None).ConfigureAwait(false);
            }
            catch (AirtableException ex)
            {
                Console.Error.WriteLine(ex.Message);
                RunContext.WriteJson(Path.Combine(outDir, DryRunFileName), new JObject { ["base"] = definition.BaseId, ["table"] = definition.TableId, ["error"] = ex.Message });
                return 1;
            }

            bool fromCache = previews.Count > 0 && previews[0].FromCache;
            Console.WriteLine("Dry run against " + (fromCache ? "the cache in " + store.Directory : "an empty table (no cache in " + store.Directory + ")") + "; nothing is sent.");
            var sites = new JArray();
            foreach (WritePreview preview in previews)
            {
                WritePlan plan = preview.Plan;
                Console.WriteLine($"{preview.Site}: {plan.Creates.Count} create(s), {plan.Updates.Count} update(s), {plan.Unchanged} unchanged, {plan.RequestCount} request(s).");
                foreach (string warning in preview.Warnings)
                {
                    Console.WriteLine("  " + warning);
                }

                sites.Add(new JObject
                {
                    ["site"] = preview.Site,
                    ["creates"] = plan.Creates.Count,
                    ["updates"] = plan.Updates.Count,
                    ["unchanged"] = plan.Unchanged,
                    ["requestCount"] = plan.RequestCount,
                    ["warnings"] = new JArray(preview.Warnings.Cast<object>().ToArray()),
                    ["createFields"] = new JArray(plan.Creates.Cast<object>().ToArray()),
                    ["updateFields"] = new JArray(plan.Updates.Select(u => (object)new JObject { ["id"] = u.Id, ["fields"] = u.Fields }).ToArray()),
                });
            }

            int requests = previews.Sum(p => p.Plan.RequestCount);
            Console.WriteLine($"{requests} write request(s) for {previews.Count} site(s), plus the refresh a real write makes first.");
            RunContext.WriteJson(Path.Combine(outDir, DryRunFileName), new JObject
            {
                ["base"] = definition.BaseId,
                ["table"] = definition.TableId,
                ["dryRun"] = true,
                ["fromCache"] = fromCache,
                ["requestCount"] = requests,
                ["sites"] = sites,
            });
            return 0;
        }

        private static async Task<int> WriteAsync(AirtableSourceDefinition definition, SnapshotStore store, List<KeyValuePair<string, IEnumerable<ROIClass>>> templates, string outDir, string token, WriteAirtableHost host)
        {
            using var http = new HttpClient(new LoggingHandler(Path.Combine(outDir, LoggingHandler.FileName), host.CreateHandler(), new[] { token })) { Timeout = TimeSpan.FromSeconds(60) };
            var api = new AirtableHttpClient(http, token, host.Clock, new AirtableClientOptions
            {
                UserAgent = "DicomTemplateMaker-EvaluationTool",
                MaxJitter = host.MaxJitter,
                Notify = Console.WriteLine,
            }, host.Throttle);
            var item = new TemplateSourceItem(new AirtableTableSource(definition, api, store, null, host.Clock));

            IReadOnlyList<WriteResult> results;
            string? error = null;
            string? failedSite = null;
            try
            {
                results = await item.WriteTemplatesAsync(templates, new ConsoleProgress(), CancellationToken.None).ConfigureAwait(false);
            }
            catch (AirtableWriteException ex)
            {
                results = ex.Completed;
                error = ex.Message;
                failedSite = ex.FailedSite;
            }
            catch (AirtableException ex)
            {
                results = Array.Empty<WriteResult>();
                error = ex.Message;
                failedSite = templates.Count == 1 ? templates[0].Key : null;
            }

            var sites = new JArray();
            foreach (WriteResult result in results)
            {
                Console.WriteLine($"{result.Site}: {result.Created} created, {result.Updated} updated, {result.Unchanged} unchanged, {result.ApiCalls} API call(s).");
                foreach (string warning in result.Warnings)
                {
                    Console.WriteLine("  " + warning);
                }

                sites.Add(new JObject
                {
                    ["site"] = result.Site,
                    ["apiCalls"] = result.ApiCalls,
                    ["created"] = result.Created,
                    ["updated"] = result.Updated,
                    ["unchanged"] = result.Unchanged,
                    ["warnings"] = new JArray(result.Warnings.Cast<object>().ToArray()),
                });
            }

            if (error != null)
            {
                Console.Error.WriteLine(error);
            }

            RunContext.WriteJson(Path.Combine(outDir, ResultFileName), new JObject
            {
                ["base"] = definition.BaseId,
                ["table"] = definition.TableId,
                ["dryRun"] = false,
                ["sitesRequested"] = templates.Count,
                ["sitesCompleted"] = results.Count,
                ["apiCalls"] = results.Sum(r => r.ApiCalls),
                ["failedSite"] = failedSite,
                ["error"] = error,
                ["requestLog"] = LoggingHandler.FileName,
                ["sites"] = sites,
            });
            return error == null ? 0 : 1;
        }

        /// <summary>The API of a dry run: every call is a bug, so it throws instead of reaching the network.</summary>
        private sealed class NoRequestsApi : IAirtableApi
        {
            public Task<ListRecordsPage> ListRecordsPageAsync(ListRecordsRequest request, CancellationToken cancellationToken) => throw Refused();

            public Task<IReadOnlyList<AirtableRecord>> CreateRecordsAsync(string baseId, string table, IReadOnlyList<JObject> records, bool typecast, CancellationToken cancellationToken) => throw Refused();

            public Task<IReadOnlyList<AirtableRecord>> UpdateRecordsAsync(string baseId, string table, IReadOnlyList<RecordUpdate> updates, bool typecast, CancellationToken cancellationToken) => throw Refused();

            public Task<IReadOnlyList<AirtableTableSchema>> ListTablesAsync(string baseId, CancellationToken cancellationToken) => throw Refused();

            private static InvalidOperationException Refused() => new InvalidOperationException("A dry run sends no request.");
        }
    }
}
