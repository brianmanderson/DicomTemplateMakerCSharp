using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>Opens folders in Explorer.</summary>
    internal static class ShellLauncher
    {
        private static readonly ILogger Logger = AppLog.For("DicomTemplateMakerGUI.Services.ShellLauncher");

        /// <summary>Opens <paramref name="folder"/> in Explorer, creating it first when missing; failures are shown and logged.</summary>
        public static void OpenFolder(Window owner, string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                using (Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true }))
                {
                }
            }
            catch (Exception ex) when (ex is Win32Exception || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is PlatformNotSupportedException)
            {
                Logger.LogWarning(ex, "Could not open {Folder} in Explorer.", folder);
                MessageBox.Show(owner, $"Could not open {folder}: {ex.Message}", "Open folder", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
