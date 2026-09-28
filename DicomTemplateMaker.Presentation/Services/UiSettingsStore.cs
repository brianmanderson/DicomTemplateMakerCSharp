using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>
    /// Loads and saves <see cref="UiSettings"/> as ui-settings.json (by default in %LOCALAPPDATA%\DicomTemplateMaker,
    /// separate from the online-template settings.json).
    /// <para>A missing file means defaults. A file that cannot be read or parsed also means defaults: the problem is
    /// logged and kept in <see cref="LoadProblem"/>, and the file is copied to <see cref="BackupPath"/> before anything
    /// replaces it; while that copy cannot be made, nothing is saved over the file. Saves replace the file in one
    /// rename. Failures to save are logged and reported by the return value; settings are a convenience and never stop
    /// the program.</para>
    /// <para>Each change reads the file again and changes only its own setting, so a second copy of the program (or a
    /// newer version, whose unknown settings are kept) does not lose what it saved in the meantime.</para>
    /// </summary>
    public sealed class UiSettingsStore
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
        };

        private readonly object gate = new object();
        private readonly ILogger logger;
        private bool backupPending;

        public UiSettingsStore(string filePath, ILogger? logger = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            FilePath = Path.GetFullPath(filePath);
            this.logger = logger ?? NullLogger.Instance;
        }

        /// <summary>A store for %LOCALAPPDATA%\DicomTemplateMaker\ui-settings.json.</summary>
        public static UiSettingsStore ForCurrentUser(ILogger? logger = null)
        {
            return new UiSettingsStore(AppPaths.UiSettingsFile, logger);
        }

        public string FilePath { get; }

        /// <summary>Where a damaged settings file is kept before it is replaced.</summary>
        public string BackupPath => FilePath + ".bak";

        /// <summary>The current settings (defaults until <see cref="Load"/> is called).</summary>
        public UiSettings Settings { get; private set; } = new UiSettings();

        /// <summary>Why the file could not be used by the last <see cref="Load"/>; null when it loaded or did not exist.</summary>
        public string? LoadProblem { get; private set; }

        /// <summary>Reads the file; see the class remarks for what happens when it is missing or damaged.</summary>
        public UiSettings Load()
        {
            lock (gate)
            {
                LoadProblem = null;
                backupPending = false;
                Settings = ReadOrDefaults();
                return Settings;
            }
        }

        /// <summary>Remembers <paramref name="root"/> as the template folder and saves.</summary>
        public bool SetTemplateRoot(string root)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(root);
            return Change(settings => settings.TemplateRoot = root);
        }

        /// <summary>Remembers <paramref name="folder"/> for <paramref name="purpose"/> and saves when it changed.</summary>
        public bool RememberFolder(FolderPurpose purpose, string folder)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folder);
            lock (gate)
            {
                if (string.Equals(Settings.GetLastFolder(purpose), folder, StringComparison.Ordinal))
                {
                    return true;
                }

                return Change(settings => settings.SetLastFolder(purpose, folder));
            }
        }

        /// <summary>
        /// Applies <paramref name="change"/> to the settings as they are on disk now (another copy of the program may have
        /// saved since this one loaded them) and saves them; <see cref="Settings"/> then holds the result. When the file
        /// cannot be read or parsed now, the change is applied to this copy's settings, after the damaged file has been
        /// backed up. Returns false (logged) when nothing could be saved; <see cref="Settings"/> is changed either way,
        /// so the choice holds for this session.
        /// </summary>
        private bool Change(Action<UiSettings> change)
        {
            lock (gate)
            {
                change(Settings);
                if (backupPending && !TryBackup())
                {
                    logger.LogError("{Path} was not saved: it could not be read at startup and no backup copy of it could be made, so it is left as it is.", FilePath);
                    return false;
                }

                UiSettings? current = ReadCurrent();
                if (current != null)
                {
                    change(current);
                }
                else if (backupPending && !TryBackup())
                {
                    logger.LogError("{Path} was not saved: it is damaged and no backup copy of it could be made, so it is left as it is.", FilePath);
                    return false;
                }

                UiSettings toSave = current ?? Settings;
                try
                {
                    AtomicTextFile.Write(FilePath, JsonSerializer.Serialize(toSave, Options));
                    Settings = toSave;
                    return true;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "Could not save the window settings to {Path}.", FilePath);
                    return false;
                }
            }
        }

        /// <summary>
        /// The settings on disk now; an empty set when there is no file; null (and a backup pending) when the file cannot
        /// be read or parsed.
        /// </summary>
        private UiSettings? ReadCurrent()
        {
            if (!File.Exists(FilePath))
            {
                return new UiSettings();
            }

            try
            {
                UiSettings? settings = JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(FilePath, Encoding.UTF8), Options);
                if (settings != null)
                {
                    settings.Normalize();
                    return settings;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                logger.LogWarning(ex, "{Path} could not be read before saving; it is backed up and replaced by this copy's settings.", FilePath);
            }

            backupPending = true;
            return null;
        }

        private UiSettings ReadOrDefaults()
        {
            if (!File.Exists(FilePath))
            {
                return new UiSettings();
            }

            string text;
            try
            {
                text = File.ReadAllText(FilePath, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return Damaged($"{FilePath} could not be read ({ex.Message}).", ex);
            }

            UiSettings? settings;
            try
            {
                settings = JsonSerializer.Deserialize<UiSettings>(text, Options);
            }
            catch (JsonException ex)
            {
                return Damaged($"{FilePath} is not a valid settings file ({ex.Message}).", ex);
            }

            if (settings == null)
            {
                return Damaged($"{FilePath} holds no settings.", null);
            }

            settings.Normalize();
            return settings;
        }

        private UiSettings Damaged(string problem, Exception? exception)
        {
            backupPending = true;
            bool kept = TryBackup();
            LoadProblem = problem + " Default window settings are used"
                + (kept ? $"; the file was copied to {BackupPath}." : "; the file is left as it is.");
            logger.LogWarning(exception, "{Problem}", LoadProblem);
            return new UiSettings();
        }

        private bool TryBackup()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Copy(FilePath, BackupPath, overwrite: true);
                }

                backupPending = false;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not copy the damaged settings file {Path} to {Backup}.", FilePath, BackupPath);
                return false;
            }
        }
    }
}
