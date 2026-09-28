using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using TemplateSync.Infrastructure;

namespace TemplateSync.Sync
{
    /// <summary>
    /// Optional per-user overrides, read from settings.json in the app-data folder. Missing or
    /// invalid values fall back to the defaults, so a bad file can never stop the program.
    /// </summary>
    public sealed class SyncSettingsFile
    {
        public const string DefaultSnapshotUrl = "https://raw.githubusercontent.com/brianmanderson/DicomTemplateMakerCSharp/main/TemplateSnapshots/TG263.json";

        [JsonProperty("cacheTimeToLiveHours")]
        public double? CacheTimeToLiveHours { get; set; }

        [JsonProperty("fullRefreshIntervalDays")]
        public double? FullRefreshIntervalDays { get; set; }

        [JsonProperty("snapshotCheckIntervalHours")]
        public double? SnapshotCheckIntervalHours { get; set; }

        [JsonProperty("sharedSnapshotUrl")]
        public string? SharedSnapshotUrl { get; set; }

        /// <summary>Ten years; larger values are clamped so a typo cannot overflow TimeSpan.</summary>
        private const double MaxHours = 24 * 3650;

        public static SyncSettingsFile Load(string path, Action<string>? onProblem = null)
        {
            try
            {
                if (File.Exists(path))
                {
                    return JsonConvert.DeserializeObject<SyncSettingsFile>(File.ReadAllText(path, Encoding.UTF8), Json.Settings) ?? new SyncSettingsFile();
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                onProblem?.Invoke($"Ignoring {path}: {ex.Message}");
            }

            return new SyncSettingsFile();
        }

        public SyncSettings ToSyncSettings()
        {
            var settings = new SyncSettings();
            if (IsPositive(CacheTimeToLiveHours))
            {
                settings.CacheTimeToLive = TimeSpan.FromHours(Math.Min(CacheTimeToLiveHours!.Value, MaxHours));
            }

            if (IsPositive(FullRefreshIntervalDays))
            {
                settings.FullRefreshInterval = TimeSpan.FromDays(Math.Min(FullRefreshIntervalDays!.Value, MaxHours / 24));
            }

            if (IsPositive(SnapshotCheckIntervalHours))
            {
                settings.SnapshotCheckInterval = TimeSpan.FromHours(Math.Min(SnapshotCheckIntervalHours!.Value, MaxHours));
            }

            return settings;
        }

        public Uri GetSnapshotUrl()
        {
            if (!string.IsNullOrWhiteSpace(SharedSnapshotUrl)
                && Uri.TryCreate(SharedSnapshotUrl, UriKind.Absolute, out Uri? uri)
                && uri.Scheme == Uri.UriSchemeHttps)
            {
                return uri;
            }

            return new Uri(DefaultSnapshotUrl);
        }

        private static bool IsPositive(double? value)
        {
            return value.HasValue && value.Value > 0 && !double.IsInfinity(value.Value) && !double.IsNaN(value.Value);
        }
    }
}
