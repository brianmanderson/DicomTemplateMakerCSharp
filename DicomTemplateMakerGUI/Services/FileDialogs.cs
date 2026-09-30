using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>
    /// Built-in .NET folder and file pickers. The overloads that take a <see cref="FolderPurpose"/> start in the folder
    /// last used for that purpose and remember the new choice in the window settings (see <see cref="Configure"/>).
    /// The start folder is checked off the UI thread with a short timeout (see <see cref="DialogStartFolder"/>): a
    /// remembered folder on a share that does not answer must not freeze the window.
    /// </summary>
    internal static class FileDialogs
    {
        public const string DicomFilter = "DICOM files (*.dcm)|*.dcm|All files (*.*)|*.*";

        private static UiSettingsStore? settings;

        /// <summary>Where remembered folders are kept; set once at startup.</summary>
        public static void Configure(UiSettingsStore store)
        {
            settings = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>
        /// Picks a folder, starting in <paramref name="preferredStart"/> when given and existing, else in the folder last
        /// used for <paramref name="purpose"/> (unless it lies in <paramref name="unreachableShare"/>, a share that was just
        /// found not to answer); the chosen folder is remembered for that purpose.
        /// </summary>
        public static async Task<string?> PickFolderAsync(Window? owner, string title, FolderPurpose purpose, string? preferredStart = null, string? unreachableShare = null)
        {
            string? start = await StartFolderAsync(DialogStartFolder.Candidates(preferredStart, settings?.Settings.GetLastFolder(purpose), unreachableShare));
            string? picked = ShowFolderDialog(owner, title, start);
            if (picked != null)
            {
                settings?.RememberFolder(purpose, picked);
            }

            return picked;
        }

        /// <summary>Picks a folder, starting in <paramref name="initialDirectory"/> when it exists.</summary>
        public static async Task<string?> PickFolderAsync(Window? owner, string title, string? initialDirectory)
        {
            string? start = await StartFolderAsync(DialogStartFolder.Candidates(initialDirectory, null));
            return ShowFolderDialog(owner, title, start);
        }

        /// <summary>Picks a file, starting in the folder last used for <paramref name="purpose"/>; the file's folder is remembered.</summary>
        public static async Task<string?> PickFileAsync(Window? owner, string title, string filter, FolderPurpose purpose)
        {
            string? start = await StartFolderAsync(DialogStartFolder.Candidates(null, settings?.Settings.GetLastFolder(purpose)));
            var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true, Multiselect = false };
            if (start != null)
            {
                dialog.InitialDirectory = start;
            }

            string? picked = Show(dialog, owner) ? dialog.FileName : null;
            string? folder = picked == null ? null : Path.GetDirectoryName(picked);
            if (folder != null)
            {
                settings?.RememberFolder(purpose, folder);
            }

            return picked;
        }

        /// <summary>The first candidate that exists, as a full path; null (the dialog's default folder) when none does.</summary>
        private static async Task<string?> StartFolderAsync(IReadOnlyList<string> candidates)
        {
            var full = new List<string>();
            foreach (string candidate in candidates)
            {
                try
                {
                    full.Add(Path.GetFullPath(candidate));
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
                {
                    // Not a usable path; the dialog starts elsewhere.
                }
            }

            return await DialogStartFolder.ChooseAsync(full, DialogStartFolder.DefaultTimeout);
        }

        /// <summary>Shows the folder dialog in <paramref name="start"/>, which the caller has already checked.</summary>
        private static string? ShowFolderDialog(Window? owner, string title, string? start)
        {
            var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
            if (start != null)
            {
                dialog.InitialDirectory = start;
            }

            return Show(dialog, owner) ? dialog.FolderName : null;
        }

        private static bool Show(CommonDialog dialog, Window? owner)
        {
            return (owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) == true;
        }
    }
}
