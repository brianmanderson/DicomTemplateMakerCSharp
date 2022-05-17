using System;
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
using System.Windows.Shapes;
using DicomTemplateMakerGUI.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace DicomTemplateMakerGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool folder_selected;
        string folder_location;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Click_Build(object sender, RoutedEventArgs e)
        {
            MakeTemplateWindow template_window = new MakeTemplateWindow(folder_location);
            template_window.ShowDialog();
        }

        private void Select_Folder_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = ".";
            dialog.IsFolderPicker = true;
            folder_selected = false;
            BuildFromRTButton.IsEnabled = false;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                folder_location = dialog.FileName;
                FolderLocationLabel.Content = folder_location;
                BuildFromRTButton.IsEnabled = true;
                folder_selected = true;
            }
        }
    }
}
