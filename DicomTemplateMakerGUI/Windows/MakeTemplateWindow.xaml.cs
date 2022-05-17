using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Windows;
using System.IO;
using System.Windows.Data;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Interaction logic for MakeTemplateWindow.xaml
    /// </summary>
    public partial class MakeTemplateWindow : Window
    {
        string dicom_file;
        string out_path;
        bool file_selected;
        public MakeTemplateWindow(string folder)
        {
            out_path = folder;
            InitializeComponent();
        }

        private void Select_File_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = ".";
            dialog.IsFolderPicker = false;
            file_selected = false;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                dicom_file = dialog.FileName;
                FileLocationLabel.Content = dicom_file;
                file_selected = true;
            }
            check_status();
        }

        private void Build_Button_Click(object sender, RoutedEventArgs e)
        {
            TemplateMaker template_maker = new TemplateMaker(dicom_file, Path.Combine(out_path, TemplateTextBox.Text));
            template_maker.make_template();
            TemplateTextBox.Text = "Finished!";
            file_selected = false;
            FileLocationLabel.Content = "";
            check_status();
        }
        private void check_status()
        {
            BuildButton.IsEnabled = false;
            if (file_selected & TemplateTextBox.Text != "")
            {
                BuildButton.IsEnabled = true;
            }
        }

        private void TemplateNameChanged(object sender, TextChangedEventArgs e)
        {
            check_status();
        }
    }
}
