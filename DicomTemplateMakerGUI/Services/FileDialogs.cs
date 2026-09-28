using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>Built-in .NET folder and file pickers (replace the unmaintained WindowsAPICodePack).</summary>
    internal static class FileDialogs
    {
        public const string DicomFilter = "DICOM files (*.dcm)|*.dcm|All files (*.*)|*.*";

        public static string? PickFolder(Window? owner, string title, string? initialDirectory = null)
        {
            var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
            string? start = ExistingDirectory(initialDirectory);
            if (start != null)
            {
                dialog.InitialDirectory = start;
            }

            return Show(dialog, owner) ? dialog.FolderName : null;
        }

        public static string? PickFile(Window? owner, string title, string filter, string? initialDirectory = null)
        {
            var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true, Multiselect = false };
            string? start = ExistingDirectory(initialDirectory);
            if (start != null)
            {
                dialog.InitialDirectory = start;
            }

            return Show(dialog, owner) ? dialog.FileName : null;
        }

        private static bool Show(CommonDialog dialog, Window? owner)
        {
            return (owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) == true;
        }

        private static string? ExistingDirectory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                string full = Path.GetFullPath(path);
                return Directory.Exists(full) ? full : null;
            }
            catch (System.Exception ex) when (ex is IOException || ex is System.ArgumentException || ex is System.NotSupportedException || ex is System.UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
