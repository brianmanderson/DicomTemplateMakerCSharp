﻿using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System;
using System.Windows;
using System.Windows.Data;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using ROIOntologyClass;
using DicomTemplateMakerGUI.StackPanelClasses;
using DicomTemplateMakerGUI.Services;
using System.Collections.ObjectModel;

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
        Brush red = new SolidColorBrush(Color.FromRgb(255, 0, 0));
        public Brush lightred = new SolidColorBrush(Color.FromRgb(229, 51, 51));
        public TemplateMaker template_maker;
        private byte R, G, B;
        public ObservableCollection<ReadAirTable> AirTables;
        List<string> interpreters = new List<string> {"ORGAN", "PTV", "CTV", "GTV", "AVOIDANCE", "CONTROL", "BOLUS", "EXTERNAL", "ISOCENTER", "REGISTRATION", "CONTRAST_AGENT",
                "CAVITY", "BRACHY_CHANNEL", "BRACHY_ACCESSORY", "SUPPORT", "FIXATION", "DOSE_REGION", "DOSE_MEASUREMENT", "BRACHY_SRC_APP", "TREATED_VOLUME", "IRRAD_VOLUME", ""};
        public MakeTemplateWindow(string folder, TemplateMaker template_maker, ObservableCollection<ReadAirTable> airTables)
        {
            AirTables = airTables;
            out_path = folder;
            InitializeComponent();
            this.template_maker = template_maker;
            InterpComboBox.ItemsSource = interpreters;
            InterpComboBox.SelectedIndex = 0;
            
            OntologyComboBox.DisplayMemberPath = "CodeMeaning";
            OntologyComboBox.ItemsSource = template_maker.Ontologies;
            OntologyComboBox.SelectedIndex = 0;
            List<ReadAirTable> loadable_airtables = new List<ReadAirTable>();
            foreach (ReadAirTable at in AirTables)
            {
                if (at.AirTableName != "TG263_AirTable")
                {
                    loadable_airtables.Add(at);
                }
            }
            AirTableComboBox.ItemsSource = loadable_airtables;
            AirTableComboBox.DisplayMemberPath = "AirTableName";
            if (AirTables.Count > 0)
            {
                AirTableComboBox.SelectedIndex = 0;
                check_airtables((ReadAirTable)AirTableComboBox.SelectedItem);
            }
            
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
                Select_All_Button.IsEnabled = true;
                UnSelect_All_Button.IsEnabled = true;
                BuildButton.Content = "Finished Building!";
                write_path = folder;
            }
        }

        private void Select_File_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog("*.dcm");
            dialog.InitialDirectory = ".";
            dialog.IsFolderPicker = false;
            FileLocationLabel.Content = "";
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                dicom_file = dialog.FileName;
                FileLocationLabel.Content = dicom_file;
                template_maker.interpret_RT(dicom_file);
                add_roi_rows();
            }
            check_status();
        }
        private void add_roi_rows()
        {
            ROIStackPanel.Children.Clear();
            //ROIStackPanel.Children.Add(TopRow());
            List<ROIClass> PTVs = new List<ROIClass>();
            List<ROIClass> CTVs = new List<ROIClass>();
            List<ROIClass> GTVs = new List<ROIClass>();
            List<ROIClass> ROIs_list = new List<ROIClass>();
            string text = SearchBox_TextBox.Text;
            foreach (ROIClass roi in template_maker.ROIs)
            {
                bool add = false;
                if (roi.ROIName.ToLower().Contains(text))
                {
                    add = true;
                }
                else if (roi.Ontology_Class.CodeMeaning.ToLower().Contains(text))
                {
                    add = true;
                }
                else if (roi.ROI_Interpreted_type.ToLower().Contains(text))
                {
                    add = true;
                }
                if (add)
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
            }
            ROIs_list = ROIs_list.OrderBy(o => o.ROIName).ToList();
            PTVs = PTVs.OrderBy(o => o.ROIName).ToList();
            GTVs = GTVs.OrderBy(o => o.ROIName).ToList();
            CTVs = CTVs.OrderBy(o => o.ROIName).ToList();
            // First, add the ROIs that are recommended
            foreach (ROIClass roi in PTVs)
            {
                if (roi.Include)
                {
                    AddROIRow new_row = new AddROIRow(template_maker.ROIs, roi, Path.Combine(out_path, "ROIs"), template_maker.Ontologies);
                    ROIStackPanel.Children.Add(new_row);
                }
            }
            foreach (ROIClass roi in CTVs)
            {
                if (roi.Include)
                {
                    AddROIRow new_row = new AddROIRow(template_maker.ROIs, roi, Path.Combine(out_path, "ROIs"), template_maker.Ontologies);
                    ROIStackPanel.Children.Add(new_row);
                }
            }
            foreach (ROIClass roi in GTVs)
            {
                if (roi.Include)
                {
                    AddROIRow new_row = new AddROIRow(template_maker.ROIs, roi, Path.Combine(out_path, "ROIs"), template_maker.Ontologies);
                    ROIStackPanel.Children.Add(new_row);
                }
            }
            foreach (ROIClass roi in ROIs_list)
            {
                if (roi.Include)
                {
                    AddROIRow new_row = new AddROIRow(template_maker.ROIs, roi, Path.Combine(out_path, "ROIs"), template_maker.Ontologies);
                    ROIStackPanel.Children.Add(new_row);
                }
            }
            // Now add the ROIs that are only consider
            foreach (ROIClass roi in PTVs)
            {
                if (!roi.Include)
                {
                    AddROIRow new_row = new AddROIRow(template_maker.ROIs, roi, Path.Combine(out_path, "ROIs"), template_maker.Ontologies);
                    ROIStackPanel.Children.Add(new_row);
                }
            }
            foreach (ROIClass roi in CTVs)
            {
                if (!roi.Include)
                {
                    AddROIRow new_row = new AddROIRow(template_maker.ROIs, roi, Path.Combine(out_path, "ROIs"), template_maker.Ontologies);
                    ROIStackPanel.Children.Add(new_row);
                }
            }
            foreach (ROIClass roi in GTVs)
            {
                if (!roi.Include)
                {
                    AddROIRow new_row = new AddROIRow(template_maker.ROIs, roi, Path.Combine(out_path, "ROIs"), template_maker.Ontologies);
                    ROIStackPanel.Children.Add(new_row);
                }
            }
            foreach (ROIClass roi in ROIs_list)
            {
                if (!roi.Include)
                {
                    AddROIRow new_row = new AddROIRow(template_maker.ROIs, roi, Path.Combine(out_path, "ROIs"), template_maker.Ontologies);
                    ROIStackPanel.Children.Add(new_row);
                }
            }
        }
        private StackPanel TopRow()
        {
            StackPanel top_row = new StackPanel();
            top_row.Orientation = Orientation.Horizontal;

            Label include_label = new Label();
            include_label.Width = 150;
            include_label.Content = "Include?";
            top_row.Children.Add(include_label);

            Label name_label = new Label();
            name_label.Width = 200;
            name_label.Content = "ROI Name";
            top_row.Children.Add(name_label);

            Label code_value = new Label();
            code_value.Width = 250;
            code_value.Content = "Ontology";
            top_row.Children.Add(code_value);

            Label code_scheme = new Label();
            code_scheme.Width = 200;
            code_scheme.Content = "Interpreted Type";
            top_row.Children.Add(code_scheme);
            return top_row;
        }
        private void Build()
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
            out_path = Path.Combine(out_path, TemplateTextBox.Text);
            template_maker.define_output(out_path);
            template_maker.define_path(out_path);
            template_maker.make_template();
            check_status();
        }
        private void Build_Button_Click(object sender, RoutedEventArgs e)
        {
            Build();
        }
        private void check_status()
        {
            TemplateTextBox.Background = lightgreen;
            pathsButton.Background = lightgray;
            ROITextBox.Background = white;
            AddROIButton.Background = white;
            if (template_maker.Paths.Count == 0)
            {
                pathsButton.Background = lightred;
            }
            else
            {
                pathsButton.Background = lightgray;
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
                if (template_maker.ROIs.Where(p => p.ROIName == ROITextBox.Text).Any())
                {
                    ROITextBox.Background = red;
                }
                else
                {
                    if (OntologyComboBox.SelectedIndex != -1)
                    {
                        AddROIButton.IsEnabled = true;
                        AddROIButton.Background = lightgreen;
                    }
                }
            }
        }
        private void templatenameChanged()
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
        private void TemplateNameChanged(object sender, TextChangedEventArgs e)
        {
            templatenameChanged();
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
            add_roi_rows();
        }

        private void Save_and_Exit_Click(object sender, RoutedEventArgs e)
        {
            Save_Changes_Click(sender, e);
            Close();
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

        private void SearchTextUpdate(object sender, TextChangedEventArgs e)
        {
            add_roi_rows();
        }

        private void OntologyNameChanged(object sender, TextChangedEventArgs e)
        {
            string text = Ontology_TextBox.Text.ToLower();
            OntologyComboBox.ItemsSource = template_maker.Ontologies.Where(x => x.CodeMeaning.ToLower().Contains(text));
            OntologyComboBox.SelectedIndex = 0;
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (ROIClass roi in template_maker.ROIs)
            {
                roi.Include = true;
            }
            add_roi_rows();
        }

        private void UnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (ROIClass roi in template_maker.ROIs)
            {
                roi.Include = false;
            }
            add_roi_rows();
        }

        private async void WriteToAirTable_Click(object sender, RoutedEventArgs e)
        {
            ReadAirTable table = (ReadAirTable)AirTableComboBox.SelectedItem;
            table.WriteToAirTable(TemplateTextBox.Text, template_maker.ROIs);
            WriteToAirTable_Button.IsEnabled = false;
            WriteToAirTable_Button.Content = "Writing to Airtable...";
            try
            {
                await table.finished_write;
                WriteToAirTable_Button.Content = "Wrote to Airtable!";
            }
            catch
            {
                WriteToAirTable_Button.Content = "Failed writing to airtable...";
            }
        }
        private async void check_airtables(ReadAirTable airtable)
        {
            WriteToAirTable_Button.IsEnabled = false;
            WriteToAirTable_Button.Content = "Still loading airtable...";
            try
            {
                await airtable.finished_task;
                WriteToAirTable_Button.IsEnabled = true;
                WriteToAirTable_Button.Content = "Write to AirTable";
            }
            catch
            {
                WriteToAirTable_Button.Content = "Could not load...";
            }
        }

        private void AirTableSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ReadAirTable table = (ReadAirTable)AirTableComboBox.SelectedItem;
            check_airtables(table);
        }

        private void Rename_template_Click(object sender, RoutedEventArgs e)
        {
            RenameTemplateWindow rename_window = new RenameTemplateWindow(Path.GetFileName(out_path), Path.GetDirectoryName(out_path));
            rename_window.ShowDialog();
            if (rename_window.rename)
            {
                template_maker.TemplateName = rename_window.NewName_TextBox.Text;
                TemplateTextBox.Text = rename_window.NewName_TextBox.Text;
                Directory.Move(out_path, Path.Combine(Path.GetDirectoryName(out_path), rename_window.NewName_TextBox.Text));
                out_path = Path.GetDirectoryName(out_path);
                Build();
            }
        }

        private void AddROI_Click(object sender, RoutedEventArgs e)
        {
            OntologyCodeClass code_class = (OntologyCodeClass)OntologyComboBox.SelectedItem;
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
