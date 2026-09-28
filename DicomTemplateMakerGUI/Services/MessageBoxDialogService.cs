using System.Windows;
using DicomTemplateMakerGUI.ViewModels;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>Shows view-model dialogs as message boxes owned by a window.</summary>
    internal sealed class MessageBoxDialogService : IDialogService
    {
        private readonly Window owner;

        public MessageBoxDialogService(Window owner)
        {
            this.owner = owner;
        }

        /// <summary>Yes/No with No as the default button, so pressing Enter never confirms by accident.</summary>
        public bool Confirm(string title, string message)
        {
            return MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
        }

        public void ShowInfo(string title, string message)
        {
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowWarning(string title, string message)
        {
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void ShowError(string title, string message)
        {
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
