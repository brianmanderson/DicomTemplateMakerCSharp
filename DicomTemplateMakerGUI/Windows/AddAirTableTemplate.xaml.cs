using System.Windows;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.ViewModels;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// "Add an Airtable table": connects a user's own table. The behaviour lives in
    /// <see cref="AddAirtableTableViewModel"/>; this class only hands over the masked token.
    /// </summary>
    public partial class AddAirTableTemplate : Window
    {
        private readonly AddAirtableTableViewModel viewModel;

        public AddAirTableTemplate(TemplateSourceCatalog catalog)
        {
            InitializeComponent();
            viewModel = new AddAirtableTableViewModel(catalog, new MessageBoxDialogService(this));
            viewModel.CloseRequested += (sender, e) => Close();
            DataContext = viewModel;
            Closed += (sender, e) => viewModel.Shutdown();
        }

        /// <summary>The source that was added, or null if the dialog was cancelled.</summary>
        public TemplateSourceItem? AddedSource => viewModel.AddedSource;

        /// <summary>PasswordBox.Password cannot be bound (by design), so the token is passed on here.</summary>
        private void TokenChanged(object sender, RoutedEventArgs e)
        {
            viewModel.Token = API_PasswordBox.Password;
        }
    }
}
