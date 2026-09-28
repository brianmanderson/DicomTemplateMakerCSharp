using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using TemplateSync.Infrastructure;

namespace TemplateSync.Storage
{
    /// <summary>Reads and atomically writes <see cref="TableSnapshot"/> JSON files in one directory.</summary>
    public sealed class SnapshotStore
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public SnapshotStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A cache directory is required.", nameof(directory));
            }

            Directory = directory;
        }

        public string Directory { get; }

        /// <summary>A file name that is safe on every OS for the given cache key.</summary>
        public string PathFor(string key)
        {
            var name = new StringBuilder();
            foreach (char c in key)
            {
                name.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' ? c : '_');
            }

            return Path.Combine(Directory, name + ".json");
        }

        /// <summary>Returns null when the file is missing or unreadable, so a bad cache never blocks the app.</summary>
        public TableSnapshot? TryLoad(string key)
        {
            return TryLoadFile(PathFor(key));
        }

        public static TableSnapshot? TryLoadFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                return Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        /// <summary>Parses and validates snapshot JSON. Throws InvalidDataException or JsonException on bad input.</summary>
        public static TableSnapshot Parse(string json)
        {
            TableSnapshot? snapshot = JsonConvert.DeserializeObject<TableSnapshot>(json, Json.Settings);
            if (snapshot == null || snapshot.Records == null)
            {
                throw new InvalidDataException("The template snapshot is empty or has no records array.");
            }

            if (snapshot.SchemaVersion > TableSnapshot.CurrentSchemaVersion)
            {
                throw new InvalidDataException($"The template snapshot uses schema version {snapshot.SchemaVersion}; this program understands up to {TableSnapshot.CurrentSchemaVersion}. Update the program.");
            }

            snapshot.Records.RemoveAll(r => r == null || string.IsNullOrEmpty(r.Id));
            snapshot.RecordCount = snapshot.Records.Count;
            return snapshot;
        }

        public void Save(string key, TableSnapshot snapshot)
        {
            WriteFile(PathFor(key), snapshot);
        }

        public void Delete(string key)
        {
            string path = PathFor(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>Writes to a temporary file first and then swaps it in, so a crash never leaves a half-written cache.</summary>
        public static void WriteFile(string path, TableSnapshot snapshot)
        {
            snapshot.RecordCount = snapshot.Records.Count;
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            string json = Serialize(snapshot);
            string temp = path + ".tmp";
            File.WriteAllText(temp, json, Utf8NoBom);
            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }

        public static string Serialize(TableSnapshot snapshot)
        {
            return JsonConvert.SerializeObject(snapshot, Json.IndentedSettings) + "\n";
        }
    }
}
