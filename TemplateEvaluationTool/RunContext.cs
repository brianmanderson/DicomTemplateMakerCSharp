using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace TemplateEvaluationTool
{
    /// <summary>A command-line mistake: the usage is printed and the tool exits with 2.</summary>
    internal sealed class UsageException : Exception
    {
        public UsageException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Parses <c>--name value</c> pairs and value-less <c>--flag</c>s, and refuses names the command does not know. A
    /// name given more than once keeps its last value for <see cref="Required"/> and <see cref="Optional"/>;
    /// <see cref="All"/> returns every value.
    /// </summary>
    internal sealed class Options
    {
        private readonly Dictionary<string, List<string>> values = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly HashSet<string> flagsSet = new HashSet<string>(StringComparer.Ordinal);

        public Options(IReadOnlyList<string> args, params string[] known)
            : this(args, Array.Empty<string>(), known)
        {
        }

        public Options(IReadOnlyList<string> args, IReadOnlyCollection<string> flags, params string[] known)
        {
            for (int i = 0; i < args.Count; i++)
            {
                string name = args[i];
                string key = name.StartsWith("--", StringComparison.Ordinal) ? name.Substring(2) : string.Empty;
                if (flags.Contains(key))
                {
                    flagsSet.Add(key);
                    continue;
                }

                if (!known.Contains(key))
                {
                    throw new UsageException("Unknown option '" + name + "'.");
                }

                if (i + 1 >= args.Count)
                {
                    throw new UsageException("Option '" + name + "' needs a value.");
                }

                if (!values.TryGetValue(key, out List<string>? list))
                {
                    values[key] = list = new List<string>();
                }

                list.Add(args[++i]);
            }
        }

        public string Required(string name)
            => Optional(name) is string value && !string.IsNullOrWhiteSpace(value) ? value : throw new UsageException("--" + name + " is required.");

        public string? Optional(string name) => values.TryGetValue(name, out List<string>? list) ? list[list.Count - 1] : null;

        public IReadOnlyList<string> All(string name) => values.TryGetValue(name, out List<string>? list) ? list : Array.Empty<string>();

        public bool Flag(string name) => flagsSet.Contains(name);
    }

    /// <summary>Reports on the calling thread, so console lines keep their order.</summary>
    internal sealed class ConsoleProgress : IProgress<string>
    {
        public void Report(string value) => Console.WriteLine(value);
    }

    /// <summary>
    /// What every command shares: the repository root, safe output folders, JSON with fixed formatting, and the
    /// manifest (git SHA, tool version, inputs with SHA-256, UTC time) written next to the outputs.
    /// </summary>
    internal static class RunContext
    {
        public const string ManifestFileName = "manifest.json";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>The folder holding DicomTemplateMaker.sln, above the current folder or the tool.</summary>
        public static string RepositoryRoot()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (DirectoryInfo? directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "DicomTemplateMaker.sln")))
                    {
                        return directory.FullName;
                    }
                }
            }

            throw new UsageException("Run the tool inside the repository (DicomTemplateMaker.sln was not found).");
        }

        /// <summary>The program's own data folder (%LOCALAPPDATA%\DicomTemplateMaker); the tool never writes there.</summary>
        public static string ProgramDataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DicomTemplateMaker");

        /// <summary>Refuses a folder inside the program's data folder, so a run never changes a user's settings or caches.</summary>
        public static string SafeFolder(string path, string option)
        {
            string full = Path.GetFullPath(path);
            string data = Path.GetFullPath(ProgramDataFolder);
            if (IsInside(full, data))
            {
                throw new UsageException("--" + option + " must not be inside " + data + ".");
            }

            return full;
        }

        /// <summary>An output folder that is new or empty, so a run never mixes with an earlier one.</summary>
        public static string EmptyOutputFolder(string path, string option)
        {
            string full = SafeFolder(path, option);
            if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any())
            {
                throw new UsageException("--" + option + " " + full + " is not empty.");
            }

            Directory.CreateDirectory(full);
            return full;
        }

        public static JsonSerializer Serializer()
        {
            var serializer = JsonSerializer.Create(new JsonSerializerSettings { Formatting = Formatting.Indented });
            serializer.Converters.Add(new StringEnumConverter());
            return serializer;
        }

        /// <summary>Indented JSON with LF line endings on every platform.</summary>
        public static void WriteJson(string path, JToken value) => WriteText(path, value.ToString(Formatting.Indented).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");

        public static void WriteText(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text, Utf8NoBom);
        }

        public static string Sha256(string file)
        {
            using FileStream stream = File.OpenRead(file);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }

        /// <summary>
        /// A path as the manifest records it: relative to the repository when inside it (forward slashes), otherwise
        /// with the home folder shortened to <c>~</c>.
        /// </summary>
        public static string DisplayPath(string path, string repositoryRoot)
        {
            string full = Path.GetFullPath(path);
            if (IsInside(full, repositoryRoot))
            {
                return Path.GetRelativePath(repositoryRoot, full).Replace('\\', '/');
            }

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return !string.IsNullOrEmpty(home) && IsInside(full, home) ? "~/" + Path.GetRelativePath(home, full).Replace('\\', '/') : full.Replace('\\', '/');
        }

        /// <summary>One input: a file, or every file of a folder (sorted, relative to it), each with its SHA-256.</summary>
        public static JObject Input(string role, string path, string repositoryRoot)
        {
            var input = new JObject { ["role"] = role, ["path"] = DisplayPath(path, repositoryRoot) };
            if (File.Exists(path))
            {
                input["sha256"] = Sha256(path);
                return input;
            }

            var files = new JArray();
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Where(f => !string.Equals(Path.GetFileName(f), ManifestFileName, StringComparison.Ordinal))
                .OrderBy(f => Path.GetRelativePath(path, f).Replace('\\', '/'), StringComparer.Ordinal))
            {
                files.Add(new JObject { ["path"] = Path.GetRelativePath(path, file).Replace('\\', '/'), ["sha256"] = Sha256(file) });
            }

            input["files"] = files;
            return input;
        }

        public static void WriteManifest(string outputFolder, string command, IReadOnlyList<string> args, IEnumerable<JObject> inputs, string repositoryRoot)
        {
            (string sha, bool? dirty) = Git(repositoryRoot);
            var manifest = new JObject
            {
                ["tool"] = "TemplateEvaluationTool",
                ["toolVersion"] = typeof(RunContext).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown",
                ["command"] = command,
                ["arguments"] = new JArray(args.Select(a => (object)(!a.StartsWith("--", StringComparison.Ordinal) && (File.Exists(a) || Directory.Exists(a)) ? DisplayPath(a, repositoryRoot) : a)).ToArray()),
                ["gitSha"] = sha,
                ["gitDirty"] = dirty,
                ["inputs"] = new JArray(inputs.Cast<object>().ToArray()),
                ["createdUtc"] = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            };
            WriteJson(Path.Combine(outputFolder, ManifestFileName), manifest);
        }

        private static bool IsInside(string path, string folder)
        {
            string relative = Path.GetRelativePath(Path.GetFullPath(folder), Path.GetFullPath(path));
            return relative == "." || (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative));
        }

        private static (string Sha, bool? Dirty) Git(string repositoryRoot)
        {
            string? sha = RunGit(repositoryRoot, "rev-parse HEAD")?.Trim();
            string? status = RunGit(repositoryRoot, "status --porcelain");
            return (string.IsNullOrEmpty(sha) ? Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "unknown" : sha, status == null ? null : status.Trim().Length > 0);
        }

        private static string? RunGit(string repositoryRoot, string arguments)
        {
            try
            {
                var start = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = repositoryRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                using Process? process = Process.Start(start);
                if (process == null)
                {
                    return null;
                }

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0 ? output : null;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return null;
            }
        }
    }
}
