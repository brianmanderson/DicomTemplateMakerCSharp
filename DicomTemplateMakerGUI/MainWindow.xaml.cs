using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DicomTemplateMakerGUI.Windows;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.StackPanelClasses;
using Microsoft.WindowsAPICodePack.Dialogs;
using DicomUtilitiesTemplateRunner;

namespace DicomTemplateMakerGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        string folder_location;
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }
        public MainWindow()
        {
            InitializeComponent();
            folder_location = @".";
            TemplateBaseLabel.Content = Path.GetFullPath(folder_location);
            BuildFromRTButton.IsEnabled = true;
            Rebuild_From_Folders();
        }
        public void Rebuild_From_Folders()
        {
            TemplateStackPanel.Children.Clear();
            string[] directories = Directory.GetDirectories(folder_location);
            foreach (string directory in directories)
            {
                TemplateMaker evaluator = new TemplateMaker();
                evaluator.categorize_folder(directory);
                if (evaluator.is_template)
                {
                    AddTemplateRow new_row = new AddTemplateRow(evaluator);
                    Border myborder = new Border();
                    myborder.Background = Brushes.Black;
                    myborder.BorderThickness = new Thickness(5);
                    TemplateStackPanel.Children.Add(myborder);
                    TemplateStackPanel.Children.Add(new_row);
                }
            }
        }
        private void Click_Build(object sender, RoutedEventArgs e)
        {
            TemplateMaker template_maker = new TemplateMaker();
            MakeTemplateWindow template_window = new MakeTemplateWindow(folder_location, template_maker);
            template_window.ShowDialog();
            Rebuild_From_Folders();
        }

        private void ChangeTemplateClick(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = Path.GetFullPath(folder_location);
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                folder_location = dialog.FileName;
                TemplateBaseLabel.Content = folder_location;
                Rebuild_From_Folders();
            }
        }

        private void ClickRunDicomserver(object sender, RoutedEventArgs e)
        {
            DicomTemplateRunner runner = new DicomTemplateRunner(Path.GetFullPath(folder_location));
            runner.run();
        }
    }
}
