namespace DicomTemplateMakerGUI.ViewModels
{
    /// <summary>
    /// Message boxes for view models, so they can ask and tell the user without referencing WPF.
    /// The GUI implements this with MessageBox; tests use a scripted fake.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>Asks a yes/no question. Returns true only when the user answers yes.</summary>
        bool Confirm(string title, string message);

        void ShowInfo(string title, string message);

        void ShowWarning(string title, string message);

        void ShowError(string title, string message);
    }
}
