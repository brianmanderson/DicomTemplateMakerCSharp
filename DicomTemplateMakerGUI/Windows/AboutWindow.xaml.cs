using System.Windows;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            VersionText.Text = "DICOM RT Template Maker, version " + App.Version;
        }

        private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            ShellLauncher.OpenFolder(this, AppPaths.LogsDirectory);
        }
    }
}
