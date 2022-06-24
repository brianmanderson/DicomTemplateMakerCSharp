﻿using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System;
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
        private string write_path;
        Brush lightgreen = new SolidColorBrush(Color.FromRgb(144, 238, 144));
        Brush lightgray = new SolidColorBrush(Color.FromRgb(221, 221, 221));
        Brush white = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        public TemplateMaker template_maker;
        private byte R, G, B;
        bool file_selected;
        List<string> interpreters = new List<string> {"ORGAN", "PTV", "CTV", "GTV", "AVOIDANCE", "CONTROL", "BOLUS", "EXTERNAL", "ISOCENTER", "REGISTRATION", "CONTRAST_AGENT",
                "CAVITY", "BRACHY_CHANNEL", "BRACHY_ACCESSORY", "SUPPORT", "FIXATION", "DOSE_REGION", "DOSE_MEASUREMENT", "BRACHY_SRC_APP", "TREATED_VOLUME", "IRRAD_VOLUME", ""};
        public MakeTemplateWindow(string folder, TemplateMaker template_maker)
        {
            out_path = folder;
            InitializeComponent();
            this.template_maker = template_maker;
            InterpComboBox.ItemsSource = interpreters;
            InterpComboBox.SelectedIndex = 0;
            R = byte.Parse("0");
            G = byte.Parse("255");
            B = byte.Parse("255");
            Brush brush = new SolidColorBrush(Color.FromRgb(R, G, B));
            ColorButton.Background = brush;
            if (Directory.Exists(Path.Combine(folder, "ROIs")))
            {
                // This means we are editing a folder, not making a new one
                TemplateTextBox.Text = Path.GetFileName(folder);
                template_maker.define_output(folder);
                TemplateTextBox.IsEnabled = false;
                add_roi_rows();
                UpdateButton.IsEnabled = true;
                RefreshButton.IsEnabled = true;
                pathsButton.IsEnabled = true;
                Update_and_ExitButton.IsEnabled = true;
                ROITextBox.IsEnabled = true;
                InterpComboBox.IsEnabled = true;
                ColorButton.IsEnabled = true;
                AddROIFromRTButton.IsEnabled = true;
                BuildButton.IsEnabled = false;
                BuildButton.Content = "Finished Building!";
                write_path = folder;
            }
        }

        private void Select_File_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog("*.dcm");
            dialog.InitialDirectory = ".";
            dialog.IsFolderPicker = false;
            file_selected = false;
            FileLocationLabel.Content = "";
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                dicom_file = dialog.FileName;
                FileLocationLabel.Content = dicom_file;
                file_selected = true;
                template_maker.interpret_RT(dicom_file);
                add_roi_rows();
                file_selected = true;
            }
            check_status();
        }
        private void add_roi_rows()
        {
            ROIStackPanel.Children.Clear();
            List<ROIClass> PTVs = new List<ROIClass>();
            List<ROIClass> CTVs = new List<ROIClass>();
            List<ROIClass> GTVs = new List<ROIClass>();
            List<ROIClass> ROIs_list = new List<ROIClass>();
            foreach (ROIClass roi in template_maker.ROIs)
            {
                if (roi.ROI_Interpreted_type == "PTV")
                {
                    PTVs.Add(roi);
                }
                else if (roi.ROI_Interpreted_type == "CTV")
                {
                    CTVs.Add(roi);
                }
                else if (roi.ROI_Interpreted_type == "GTV")
                {
                    GTVs.Add(roi);
                }
                else
                {
                    ROIs_list.Add(roi);
                }
            }
            ROIs_list = ROIs_list.OrderBy(o => o.ROIName).ToList();
            PTVs = PTVs.OrderBy(o => o.ROIName).ToList();
            GTVs = GTVs.OrderBy(o => o.ROIName).ToList();
            CTVs = CTVs.OrderBy(o => o.ROIName).ToList();
            foreach (ROIClass roi in PTVs)
            {
                AddROIRow new_row = new AddROIRow(template_maker.ROIs, roi);
                ROIStackPanel.Children.Add(new_row);
            }
            foreach (ROIClass roi in CTVs)
            {
                AddROIRow new_row = new AddROIRow(template_maker.ROIs, roi);
                ROIStackPanel.Children.Add(new_row);
            }
            foreach (ROIClass roi in GTVs)
            {
                AddROIRow new_row = new AddROIRow(template_maker.ROIs, roi);
                ROIStackPanel.Children.Add(new_row);
            }
            foreach (ROIClass roi in ROIs_list)
            {
                AddROIRow new_row = new AddROIRow(template_maker.ROIs, roi);
                ROIStackPanel.Children.Add(new_row);
            }
        }
        private void Build_Button_Click(object sender, RoutedEventArgs e)
        {
            UpdateButton.IsEnabled = true;
            pathsButton.IsEnabled = true;
            TemplateTextBox.IsEnabled = false;
            AddROIFromRTButton.IsEnabled = true;
            ROITextBox.IsEnabled = true;
            InterpComboBox.IsEnabled = true;
            ColorButton.IsEnabled = true;
            BuildButton.Content = "Finished Building!";
            BuildButton.IsEnabled = false;
            Update_and_ExitButton.IsEnabled = true;
            template_maker.define_output(Path.Combine(out_path, TemplateTextBox.Text));
            template_maker.make_template();
            check_status();
        }
        private void check_status()
        {
            TemplateTextBox.Background = lightgreen;
            pathsButton.Background = lightgray;
            if (template_maker.Paths.Count == 0)
            {
                pathsButton.Background = lightgreen;
            }
            BuildButton.IsEnabled = false;
            if (TemplateTextBox.Text != "")
            {
                TemplateTextBox.Background = white;
                if (TemplateTextBox.IsEnabled)
                {
                    BuildButton.IsEnabled = true;
                }    
            }
            
            AddROIButton.IsEnabled = false;
            if (ROITextBox.Text != "")
            {
                AddROIButton.IsEnabled = true;
            }
        }

        private void TemplateNameChanged(object sender, TextChangedEventArgs e)
        {
            check_status();
            write_path = Path.Combine(out_path, TemplateTextBox.Text);
            template_maker.define_output(write_path);
            UpdateButton.IsEnabled = false;
            RefreshButton.IsEnabled = false;
            pathsButton.IsEnabled = false;
            Update_and_ExitButton.IsEnabled = false;
            check_status();
        }

        private void Save_Changes_Click(object sender, RoutedEventArgs e)
        {
            UpdateButton.Content = "Saving...";
            template_maker.make_template();
            check_status();
            UpdateButton.Content = "Save Changes";
        }

        private void ROINameChanged(object sender, TextChangedEventArgs e)
        {
            check_status();
            UpdateButton.IsEnabled = true;
            RefreshButton.IsEnabled = true;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            check_status();
        }

        private void Save_and_Exit_Click(object sender, RoutedEventArgs e)
        {
            Save_Changes_Click(sender, e);
            this.Close();
        }

        private void PathsButtonClick(object sender, RoutedEventArgs e)
        {
            EditPathsWindow paths_window = new EditPathsWindow(template_maker);
            paths_window.ShowDialog();
            check_status();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            add_roi_rows();
        }

        private void AddROI_Click(object sender, RoutedEventArgs e)
        {
            IdentificationCodeClass code_class = new IdentificationCodeClass("Test", "Test", "test");
            ROIClass roi = new ROIClass(R, G, B, ROITextBox.Text, InterpComboBox.SelectedItem.ToString(), code_class);
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
