using System.Windows;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.ViewModels;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// "Online Templates": browses the templates offered by the shared TG-263 snapshot and the user's
    /// Airtable tables. The behaviour lives in <see cref="OnlineTemplatesViewModel"/>.
    /// </summary>
    public partial class AirTableWindow : Window
    {
        private readonly TemplateSourceCatalog catalog;
        private readonly OnlineTemplatesViewModel viewModel;

        public AirTableWindow(TemplateSourceCatalog catalog, string folder_location, string onto_path)
        {
            InitializeComponent();
            this.catalog = catalog;
            viewModel = new OnlineTemplatesViewModel(catalog, new MessageBoxDialogService(this), folder_location, onto_path, ShowAddTable);
            viewModel.CloseRequested += (sender, e) => Close();
            DataContext = viewModel;

            // The load never throws (failures are shown in the window), so the task can be left running.
            Loaded += (sender, e) => _ = viewModel.StartAsync();
            Closing += (sender, e) =>
            {
                // Closing mid-build would leave some templates written and others not, without a report.
                if (viewModel.IsBuilding)
                {
                    e.Cancel = true;
                    MessageBox.Show(this, OnlineTemplatesViewModel.WaitForBuildMessage, OnlineTemplatesViewModel.BuildTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };
            Closed += (sender, e) => viewModel.Shutdown();
        }

        private TemplateSourceItem? ShowAddTable()
        {
            var window = new AddAirTableTemplate(catalog) { Owner = this };
            window.ShowDialog();
            return window.AddedSource;
        }
    }
}
