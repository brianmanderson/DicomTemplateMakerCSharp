﻿using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using DicomTemplateMakerGUI.StackPanelClasses;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Interaction logic for EditPathsWindow.xaml
    /// </summary>
    public partial class EditPathsWindow : Window
    {
        private TemplateMaker template_maker;
        Brush lightred = new SolidColorBrush(Color.FromRgb(229, 51, 51));
        Brush lightgray = new SolidColorBrush(Color.FromRgb(221, 221, 221));
        List<string> dicom_tag_list = new List<string> {"Series Description", "Study Description"}; //, "Modality"
        public EditPathsWindow(TemplateMaker template_maker)
        {
            InitializeComponent();
            this.template_maker = template_maker;
            DicomTag_Combobox.ItemsSource = dicom_tag_list;
            DicomTag_Combobox.SelectedIndex = 0;
            write_paths();
        }
        public void write_paths()
        {
            if (template_maker.Paths.Count == 0)
            {
                Add_Path_Button.Background = lightred;
            }
            foreach (string path in template_maker.Paths)
            {
                PathsRow new_row = new PathsRow(template_maker, path);
                PathsStackPanel.Children.Add(new_row);
            }
            foreach (string key in template_maker.DicomTags.Keys)
            {
                foreach (string value in template_maker.DicomTags[key])
                {
                    DicomTagRow new_row = new DicomTagRow(template_maker, key, value);
                    RequirementStackPanel.Children.Add(new_row);
                }
            }
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = ".";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                template_maker.Paths.Add(dialog.FileName);
                PathsRow new_row = new PathsRow(template_maker, dialog.FileName);
                PathsStackPanel.Children.Add(new_row);
            }
            if (template_maker.Paths.Count != 0)
            {
                Add_Path_Button.Background = lightgray;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            template_maker.make_template();
            Close();
        }

        private void Add_Requirement(object sender, RoutedEventArgs e)
        {
            string key = DicomTag_Combobox.Text;
            string value = Dicomtag_TextBox.Text;
            if (template_maker.DicomTags.ContainsKey(key))
            {
                template_maker.DicomTags[key].Add(value);
            }
            else
            {
                template_maker.DicomTags.Add(key, new List<string> { value });
            }
            DicomTagRow new_row = new DicomTagRow(template_maker, key, value);
            RequirementStackPanel.Children.Add(new_row);
            Dicomtag_TextBox.Text = "";
        }

        private void TagText_Changed(object sender, TextChangedEventArgs e)
        {
            AddDicom_Button.IsEnabled = false;
            if (Dicomtag_TextBox.Text != "")
            {
                AddDicom_Button.IsEnabled = true;
            }
        }
    }
}
