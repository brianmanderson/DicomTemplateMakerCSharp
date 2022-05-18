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
using DicomTemplateMakerGUI.StackPanelClasses;
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
        TemplateMaker template_maker;
        bool file_selected;
        public MakeTemplateWindow(string folder)
        {
            out_path = folder;
            template_maker = new TemplateMaker();
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
                template_maker.interpret_RT(dicom_file);
                add_roi_rows();
            }
            check_status();
        }
        private void add_roi_rows()
        {
            ROIStackPanel.Children.Clear();
            foreach (ROIClass roi in template_maker.ROIs)
            {
                AddROIRow new_row = new AddROIRow(roi);
                ROIStackPanel.Children.Add(new_row);
            }
        }
        private void Build_Button_Click(object sender, RoutedEventArgs e)
        {
            template_maker.make_template(Path.Combine(out_path, TemplateTextBox.Text));
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
