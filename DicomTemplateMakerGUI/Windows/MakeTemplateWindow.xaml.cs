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
        private byte R, G, B;
        bool file_selected;
        List<string> interpreters = new List<string> {"PLEASE SELECT", "ORGAN", "PTV", "CTV", "GTV", "AVOIDANCE", "CONTROL", "BOLUS", "EXTERNAL", "ISOCENTER", "REGISTRATION", "CONTRAST_AGENT",
                "CAVITY", "BRACHY_CHANNEL", "BRACHY_ACCESSORY", "SUPPORT", "FIXATION", "DOSE_REGION", "DOSE_MEASUREMENT", "BRACHY_SRC_APP", "TREATED_VOLUME", "IRRAD_VOLUME", ""};
        public MakeTemplateWindow(string folder)
        {
            out_path = folder;
            InitializeComponent();
            template_maker = new TemplateMaker();
            InterpComboBox.ItemsSource = interpreters;
            InterpComboBox.SelectedIndex = 0;
            R = byte.Parse("0");
            G = byte.Parse("255");
            B = byte.Parse("255");
            Brush brush = new SolidColorBrush(Color.FromRgb(R, G, B));
            ColorButton.Background = brush;
        }

        private void Select_File_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = ".";
            dialog.IsFolderPicker = false;
            file_selected = false;
            FileLocationLabel.Content = "";
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                BuildButton.Content = "Bulid!";
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
                AddROIRow new_row = new AddROIRow(template_maker.ROIs, template_maker.ROIs.IndexOf(roi));
                ROIStackPanel.Children.Add(new_row);
            }
        }
        private void Build_Button_Click(object sender, RoutedEventArgs e)
        {
            UpdateButton.IsEnabled = true;
            template_maker.make_template(Path.Combine(out_path, TemplateTextBox.Text));
            BuildButton.Content = "Finished!";
            check_status();
        }
        private void check_status()
        {
            BuildButton.IsEnabled = false;
            if (file_selected & TemplateTextBox.Text != "")
            {
                BuildButton.IsEnabled = true;
            }
            AddROIButton.IsEnabled = false;
            if (ROITextBox.Text != "" & InterpComboBox.SelectedIndex != 0)
            {
                AddROIButton.IsEnabled = true;
            }
        }

        private void TemplateNameChanged(object sender, TextChangedEventArgs e)
        {
            BuildButton.Content = "Build!";
            UpdateButton.IsEnabled = false;
            check_status();
        }

        private void Save_Changes_Click(object sender, RoutedEventArgs e)
        {
            template_maker.make_template(Path.Combine(out_path, TemplateTextBox.Text));
            check_status();
        }

        private void ROINameChanged(object sender, TextChangedEventArgs e)
        {
            check_status();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            check_status();
        }

        private void AddROI_Click(object sender, RoutedEventArgs e)
        {
            ROIClass roi = new ROIClass(R, G, B, ROITextBox.Text, InterpComboBox.SelectedItem.ToString());
            template_maker.ROIs.Add(roi);
            add_roi_rows();
            ROITextBox.Text = "";
            InterpComboBox.SelectedIndex = 0;
            check_status();
        }

        private void ChangeColor_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.ColorDialog MyDialog = new System.Windows.Forms.ColorDialog();
            if (MyDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var new_color = MyDialog.Color;
                R = new_color.R;
                G = new_color.G;
                B = new_color.B;
                Brush brush = new SolidColorBrush(Color.FromRgb(new_color.R, new_color.G, new_color.B));
                ColorButton.Background = brush;
            }
        }
    }
}
