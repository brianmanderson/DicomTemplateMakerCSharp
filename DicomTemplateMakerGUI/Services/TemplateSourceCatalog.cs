using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using TemplateSync.Airtable;
using TemplateSync.Credentials;
using TemplateSync.Storage;
using TemplateSync.Sync;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>
    /// Owns every template source for the session: the shared TG-263 snapshot (no token) and the
    /// user's own Airtable tables (encrypted tokens). Nothing here contacts the network; loading
    /// is always an explicit call.
    /// </summary>
    public sealed class TemplateSourceCatalog
    {
        public const string SharedSourceName = "TG-263 (shared)";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        private readonly string userAgent;

        public TemplateSourceCatalog()
        {
            try
            {
                Directory.CreateDirectory(AppDataDirectory);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Problems.Add("Could not create " + AppDataDirectory + ": " + ex.Message + " Online templates will not be saved between sessions.");
            }

            SettingsFile = SyncSettingsFile.Load(Path.Combine(AppDataDirectory, "settings.json"), p => Problems.Add(p));
            Settings = SettingsFile.ToSyncSettings();
            Cache = new SnapshotStore(Path.Combine(AppDataDirectory, "cache"));
            Connections = new AirtableConnectionStore(Path.Combine(AppDataDirectory, "airtable-connections.json"), new DpapiTokenProtector());
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            userAgent = "DicomTemplateMaker/" + (version == null ? "0" : version.ToString());
        }

        /// <summary>%LOCALAPPDATA%\DicomTemplateMaker: caches, settings and encrypted connections.</summary>
        public static string AppDataDirectory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DicomTemplateMaker"); }
        }

        public ObservableCollection<TemplateSourceItem> Sources { get; } = new ObservableCollection<TemplateSourceItem>();

        public List<string> Problems { get; } = new List<string>();

        public LegacyMigrationResult Migration { get; private set; } = new LegacyMigrationResult();

        public SyncSettings Settings { get; }

        public SyncSettingsFile SettingsFile { get; }

        public SnapshotStore Cache { get; }

        public AirtableConnectionStore Connections { get; }

        public IEnumerable<TemplateSourceItem> WritableSources
        {
            get { return Sources.Where(s => s.IsWritable); }
        }

        /// <summary>
        /// Migrates old plain-text token files and builds the source list. Looks for the old
        /// "AirTables" folder both in the working directory (where the old program looked) and next
        /// to the executable (where the old build copied the bundled TG-263 token).
        /// </summary>
        public void Initialize(string workingDirectory)
        {
            var legacyFolders = new[]
            {
                Path.GetFullPath(Path.Combine(workingDirectory, "AirTables")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AirTables")),
            }.Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in legacyFolders)
            {
                try
                {
                    // Plain-text files are deleted only in the user's own folders; in a shared or install folder
                    // other Windows users still need them to import their copy.
                    LegacyMigrationResult result = Connections.MigrateLegacyFiles(folder, deletePlaintext: IsInUserProfile(folder));
                    Migration.Imported.AddRange(result.Imported);
                    Migration.KeptPlaintext.AddRange(result.KeptPlaintext);
                    Migration.Retired.AddRange(result.Retired);
                    Migration.Problems.AddRange(result.Problems);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is Newtonsoft.Json.JsonException)
                {
                    Problems.Add("Could not migrate Airtable settings from " + folder + ": " + ex.Message);
                }
            }

            string bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TemplateSnapshots", "TG263.json");
            var shared = new SnapshotTableSource(
                new SnapshotSourceDefinition(SharedSourceName, SettingsFile.GetSnapshotUrl(), bundled),
                Http,
                Cache,
                Settings);
            Sources.Add(new TemplateSourceItem(shared));

            List<AirtableConnection> connections;
            try
            {
                connections = Connections.Load();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is Newtonsoft.Json.JsonException)
            {
                Problems.Add("Could not read your Airtable connections: " + ex.Message);
                connections = new List<AirtableConnection>();
            }

            foreach (AirtableConnection connection in connections)
            {
                TemplateSourceItem item = TryCreateAirtableItem(connection);
                if (item != null)
                {
                    Sources.Add(item);
                }
            }
        }

        /// <summary>One cheap request (a single record) to prove the ids and token work before saving.</summary>
        public async Task<string> TestConnectionAsync(string baseId, string table, string token, CancellationToken cancellationToken)
        {
            try
            {
                var api = new AirtableHttpClient(Http, token, options: new AirtableClientOptions { UserAgent = userAgent, MaxAttempts = 1 });
                await api.ListRecordsPageAsync(new ListRecordsRequest(baseId.Trim(), table.Trim()) { PageSize = 1 }, cancellationToken);
                return null;
            }
            catch (AirtableException ex)
            {
                return ex.Message;
            }
        }

        public TemplateSourceItem AddConnection(string name, string baseId, string table, string token)
        {
            TemplateSourceItem existing = Sources.FirstOrDefault(s => s.IsWritable && string.Equals(s.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
            Connections.Add(name, baseId, table, token);
            if (existing != null)
            {
                Sources.Remove(existing);
            }

            AirtableConnection connection = Connections.Load().First(c => string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
            TemplateSourceItem item = TryCreateAirtableItem(connection);
            Sources.Add(item);
            return item;
        }

        public void RemoveConnection(TemplateSourceItem item)
        {
            if (item == null || !item.IsWritable)
            {
                return;
            }

            Connections.Remove(item.Name);
            item.Source.DeleteCache();
            Sources.Remove(item);
        }

        private TemplateSourceItem TryCreateAirtableItem(AirtableConnection connection)
        {
            var definition = new AirtableSourceDefinition(connection.Name, connection.BaseId, connection.TableId);
            string token;
            try
            {
                token = Connections.GetToken(connection);
            }
            catch (Exception ex) when (ex is CryptographicException || ex is FormatException)
            {
                string problem = "The saved token for '" + connection.Name + "' cannot be decrypted by this Windows account. Open Load Online Templates, remove that table and add it again.";
                Problems.Add(problem);
                // Keep it listed so it can be removed.
                return new TemplateSourceItem(new UnavailableAirtableSource(definition, Cache, problem));
            }

            var api = new AirtableHttpClient(Http, token, options: new AirtableClientOptions { UserAgent = userAgent });
            return new TemplateSourceItem(new AirtableTableSource(definition, api, Cache, Settings));
        }

        private static bool IsInUserProfile(string folder)
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(profile))
            {
                return false;
            }

            string full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string root = Path.GetFullPath(profile).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
    }
}
