using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using TemplateSync.Infrastructure;

namespace TemplateSync.Credentials
{
    /// <summary>Encrypts tokens at rest. The Windows implementation uses DPAPI (current user).</summary>
    public interface ITokenProtector
    {
        string Protect(string plaintext);

        string Unprotect(string protectedValue);
    }

    public sealed class AirtableConnection
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("baseId")]
        public string BaseId { get; set; } = string.Empty;

        [JsonProperty("tableId")]
        public string TableId { get; set; } = string.Empty;

        /// <summary>The token encrypted by <see cref="ITokenProtector"/>; never plain text.</summary>
        [JsonProperty("protectedToken")]
        public string ProtectedToken { get; set; } = string.Empty;
    }

    public sealed class LegacyMigrationResult
    {
        public List<string> Imported { get; } = new List<string>();

        /// <summary>Imported files left in place because the folder may be shared with other Windows users.</summary>
        public List<string> KeptPlaintext { get; } = new List<string>();

        public List<string> Retired { get; } = new List<string>();

        public List<string> Problems { get; } = new List<string>();
    }

    /// <summary>
    /// The user's Airtable connections, stored as JSON with encrypted tokens in a per-user folder.
    /// The AIRTABLE_PAT environment variable, when set, overrides every stored token.
    /// </summary>
    public sealed class AirtableConnectionStore
    {
        public const string TokenEnvironmentVariable = "AIRTABLE_PAT";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly string _path;
        private readonly ITokenProtector _protector;
        private readonly Func<string, string?> _environment;

        public AirtableConnectionStore(string path, ITokenProtector protector, Func<string, string?>? environment = null)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _protector = protector ?? throw new ArgumentNullException(nameof(protector));
            _environment = environment ?? Environment.GetEnvironmentVariable;
        }

        public string FilePath => _path;

        public List<AirtableConnection> Load()
        {
            if (!File.Exists(_path))
            {
                return new List<AirtableConnection>();
            }

            string json = File.ReadAllText(_path, Encoding.UTF8);
            return JsonConvert.DeserializeObject<List<AirtableConnection>>(json, Json.Settings) ?? new List<AirtableConnection>();
        }

        public void Add(string name, string baseId, string tableId, string token)
        {
            string? problem = AirtableIds.Validate(name, baseId, tableId, token);
            if (problem != null)
            {
                throw new ArgumentException(problem);
            }

            List<AirtableConnection> connections = Load();
            connections.RemoveAll(c => string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
            connections.Add(new AirtableConnection
            {
                Name = name.Trim(),
                BaseId = baseId.Trim(),
                TableId = tableId.Trim(),
                ProtectedToken = ProtectVerified(token.Trim()),
            });
            Save(connections);
        }

        public bool Remove(string name)
        {
            List<AirtableConnection> connections = Load();
            int removed = connections.RemoveAll(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                Save(connections);
            }

            return removed > 0;
        }

        /// <summary>The token to use: AIRTABLE_PAT if set, otherwise the stored one.</summary>
        public string GetToken(AirtableConnection connection)
        {
            string? fromEnvironment = _environment(TokenEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                return fromEnvironment!.Trim();
            }

            return _protector.Unprotect(connection.ProtectedToken);
        }

        /// <summary>
        /// Moves the old plain-text "AirTables/*.txt" files (token, base id, table id on three lines)
        /// into this store. Files holding a token that was published with an old release
        /// (<see cref="LeakedTokens"/>) are deleted without importing; this retires the formerly bundled
        /// tokens whatever the file is called. Other files are imported, and deleted once their token is
        /// encrypted and saved when <paramref name="deletePlaintext"/> is true (a per-user folder); in a
        /// folder other Windows users may share, they are kept so those users can import them too. A file
        /// whose name matches an existing connection with different settings is left in place and
        /// reported, so no token is ever lost.
        /// </summary>
        public LegacyMigrationResult MigrateLegacyFiles(string legacyDirectory, bool deletePlaintext = true, Func<string, bool>? isRetiredToken = null)
        {
            Func<string, bool> retiredToken = isRetiredToken ?? LeakedTokens.IsKnownLeaked;
            var result = new LegacyMigrationResult();
            if (!Directory.Exists(legacyDirectory))
            {
                return result;
            }

            List<AirtableConnection> connections = Load();
            foreach (string file in Directory.GetFiles(legacyDirectory, "*.txt"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                try
                {
                    string[] lines = File.ReadAllLines(file).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
                    if (lines.Length > 0 && retiredToken(lines[0]))
                    {
                        try
                        {
                            File.Delete(file);
                            result.Retired.Add(name);
                        }
                        catch (Exception ex) when (!deletePlaintext && (ex is IOException || ex is UnauthorizedAccessException))
                        {
                            // A read-only install folder: the published token is never imported, so do not
                            // report the same undeletable file on every start.
                        }

                        continue;
                    }

                    if (lines.Length < 3)
                    {
                        result.Problems.Add($"{name}: expected a token, base id and table id on three lines; left in place.");
                        continue;
                    }

                    AirtableConnection? existing = connections.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        bool same = string.Equals(existing.BaseId, lines[1], StringComparison.Ordinal)
                            && string.Equals(existing.TableId, lines[2], StringComparison.Ordinal)
                            && string.Equals(_protector.Unprotect(existing.ProtectedToken), lines[0], StringComparison.Ordinal);
                        if (!same)
                        {
                            result.Problems.Add($"{name}: a connection with this name already exists with different settings; the file was left in place. Remove one of them, then restart.");
                            continue;
                        }

                        if (!deletePlaintext)
                        {
                            // Already imported on an earlier start; nothing new to report.
                            continue;
                        }
                    }
                    else
                    {
                        connections.Add(new AirtableConnection
                        {
                            Name = name,
                            BaseId = lines[1],
                            TableId = lines[2],
                            ProtectedToken = ProtectVerified(lines[0]),
                        });

                        // Persist before deleting the only other copy of the token.
                        Save(connections);
                    }

                    result.Imported.Add(name);
                    if (deletePlaintext)
                    {
                        File.Delete(file);
                    }
                    else
                    {
                        result.KeptPlaintext.Add(file);
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.Cryptography.CryptographicException || ex is FormatException)
                {
                    result.Problems.Add($"{name}: {ex.Message}");
                }
            }

            return result;
        }

        private string ProtectVerified(string token)
        {
            string protectedToken = _protector.Protect(token);
            if (!string.Equals(_protector.Unprotect(protectedToken), token, StringComparison.Ordinal))
            {
                throw new System.Security.Cryptography.CryptographicException("The token could not be stored securely (encryption round-trip failed).");
            }

            return protectedToken;
        }

        private void Save(List<AirtableConnection> connections)
        {
            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temp = _path + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(connections, Json.IndentedSettings), Utf8NoBom);
            if (File.Exists(_path))
            {
                File.Replace(temp, _path, null);
            }
            else
            {
                File.Move(temp, _path);
            }
        }
    }
}
