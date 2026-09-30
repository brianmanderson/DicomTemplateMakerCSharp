using System.Windows;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>Message boxes used by the main window. Questions default to the safe answer.</summary>
    internal static class Dialogs
    {
        /// <summary>Yes/No with No as the default button, so pressing Enter never confirms by accident.</summary>
        public static bool Confirm(Window owner, string title, string message)
        {
            return MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
        }

        /// <summary>Yes/No/Cancel with Cancel as the default button.</summary>
        public static MessageBoxResult YesNoCancel(Window owner, string title, string message)
        {
            return MessageBox.Show(owner, message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
        }

        public static void Info(Window owner, string title, string message)
        {
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void Warning(Window owner, string title, string message)
        {
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static void Error(Window owner, string title, string message)
        {
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
