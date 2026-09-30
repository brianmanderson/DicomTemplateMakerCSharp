using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>What a folder picker is for; each purpose remembers its own last folder.</summary>
    public enum FolderPurpose
    {
        /// <summary>RT Structure Set files read into a template.</summary>
        RtFile,

        /// <summary>Where "Create folder with loadable RTs" writes its folder.</summary>
        Output,

        /// <summary>Varian XML structure templates (import and export).</summary>
        VarianXml,

        /// <summary>Folders a template monitors for new images.</summary>
        MonitoredPath,
    }

    /// <summary>
    /// The window settings saved in ui-settings.json (see <see cref="UiSettingsStore"/>). Every value is optional;
    /// a missing, blank or unusable value means "no preference".
    /// </summary>
    public sealed class UiSettings
    {
        /// <summary>The folder that holds the templates; null to decide at startup (see <see cref="TemplateRootResolver"/>).</summary>
        public string? TemplateRoot { get; set; }

        /// <summary>
        /// A site share offered first by the Varian XML import and export (for example the Eclipse structure template
        /// folder). Empty by default; it is only probed, off the UI thread, when set.
        /// </summary>
        public string? VarianXmlShare { get; set; }

        public string? LastRtFileFolder { get; set; }

        public string? LastOutputFolder { get; set; }

        public string? LastVarianXmlFolder { get; set; }

        public string? LastMonitoredPathFolder { get; set; }

        /// <summary>
        /// Settings this version does not know (written by a newer one); kept so that saving does not drop them.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        public string? GetLastFolder(FolderPurpose purpose)
        {
            return purpose switch
            {
                FolderPurpose.RtFile => LastRtFileFolder,
                FolderPurpose.Output => LastOutputFolder,
                FolderPurpose.VarianXml => LastVarianXmlFolder,
                FolderPurpose.MonitoredPath => LastMonitoredPathFolder,
                _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null),
            };
        }

        public void SetLastFolder(FolderPurpose purpose, string? folder)
        {
            switch (purpose)
            {
                case FolderPurpose.RtFile:
                    LastRtFileFolder = folder;
                    break;
                case FolderPurpose.Output:
                    LastOutputFolder = folder;
                    break;
                case FolderPurpose.VarianXml:
                    LastVarianXmlFolder = folder;
                    break;
                case FolderPurpose.MonitoredPath:
                    LastMonitoredPathFolder = folder;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null);
            }
        }

        /// <summary>Blank values become null and the rest are trimmed.</summary>
        internal void Normalize()
        {
            TemplateRoot = Clean(TemplateRoot);
            VarianXmlShare = Clean(VarianXmlShare);
            LastRtFileFolder = Clean(LastRtFileFolder);
            LastOutputFolder = Clean(LastOutputFolder);
            LastVarianXmlFolder = Clean(LastVarianXmlFolder);
            LastMonitoredPathFolder = Clean(LastMonitoredPathFolder);
        }

        private static string? Clean(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
