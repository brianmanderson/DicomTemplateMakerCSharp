using System;
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
using System.Windows.Shapes;
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
        bool file_selected, folder_selected;
        public MakeTemplateWindow()
        {
            InitializeComponent();
        }

        private void Select_File_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = ".";
            dialog.IsFolderPicker = false;
            file_selected = false;
            OutPath_Button.IsEnabled = false;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                dicom_file = dialog.FileName;
                FileLocationLabel.Content = dicom_file;
                OutPath_Button.IsEnabled = true;
                file_selected = true;
            }
            check_status();
        }

        private void Build_Button_Click(object sender, RoutedEventArgs e)
        {
            TemplateMaker template_maker = new TemplateMaker(dicom_file, out_path);
            template_maker.make_template();
        }
        private void check_status()
        {
            BuildButton.IsEnabled = false;
            if (file_selected & folder_selected)
            {
                BuildButton.IsEnabled = true;
            }
        }
        private void Select_Folder_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = ".";
            dialog.IsFolderPicker = true;
            folder_selected = false;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                out_path = dialog.FileName;
                FolderLocationLabel.Content = out_path;
                BuildButton.IsEnabled = true;
                folder_selected = true;
            }
            check_status();
        }
    }
}
