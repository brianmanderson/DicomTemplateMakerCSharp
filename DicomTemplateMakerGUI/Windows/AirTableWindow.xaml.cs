﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.StackPanelClasses;
using AirtableApiClient;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Interaction logic for AirTableWindow.xaml
    /// </summary>
    public partial class AirTableWindow : Window
    {
        public ReadAirTable airtable;
        public List<ReadAirTable> airtables;
        string folder_location;
        string onto_path;
        bool finished = false;
        List<AddAirTableRow> default_airtable_list = new List<AddAirTableRow>();
        List<string> airtable_names;
        Brush lightgreen = new SolidColorBrush(Color.FromRgb(144, 238, 144));
        Brush lightgray = new SolidColorBrush(Color.FromRgb(221, 221, 221));
        Brush yellow = new SolidColorBrush(Color.FromRgb(255, 255, 0));
        Brush red = new SolidColorBrush(Color.FromRgb(255, 0, 0));
        public AirTableWindow(List<ReadAirTable> ats, string folder_location, string onto_path)
        {
            InitializeComponent();
            this.folder_location = folder_location;
            this.onto_path = onto_path;
            airtables = ats;
            build_combobox();
        }
        private void build_combobox()
        {
            airtable_names = new List<string>();
            foreach (ReadAirTable r in airtables)
            {
                airtable_names.Add(r.AirTableName);
            }
            //Template_ComboBox.DisplayMemberPath = "Test";
            Template_ComboBox.ItemsSource = airtable_names;
            if (airtable_names.Count > 0)
            {
                Template_ComboBox.SelectedIndex = 0;
            }
        }
        private StackPanel TopRow()
        {
            StackPanel top_row = new StackPanel();
            top_row.Orientation = Orientation.Horizontal;

            Label name_label = new Label();
            name_label.Width = 200;
            name_label.Content = "Template Name";
            top_row.Children.Add(name_label);

            Label code_value = new Label();
            code_value.Width = 200;
            code_value.Content = "Include in build?";
            top_row.Children.Add(code_value);

            Label code_scheme = new Label();
            code_scheme.Width = 150;
            code_scheme.Content = "Check box";
            top_row.Children.Add(code_scheme);
            return top_row;
        }
        private void BuildTables()
        {
            BuildButton.IsEnabled = false;
            StackDefaultAirtablePanel.Children.Clear();
            default_airtable_list = new List<AddAirTableRow>();
            foreach (ReadAirTable airtable in airtables)
            {
                if (airtable.AirTableName == (string)Template_ComboBox.SelectedItem)
                {
                    BuildTable(airtable);
                }
            }
        }
        public async void BuildTable(ReadAirTable airtable)
        {
            Status_Label.Content = "Status: Loading from online...Please wait";
            Status_Label.Background = yellow;
            Status_Label.Visibility = Visibility.Visible;
            CheckBoxLabel.Visibility = Visibility.Hidden;
            IncludeLabel.Visibility = Visibility.Hidden;
            TemplateNameLabel.Visibility = Visibility.Hidden;
            try
            {
                await airtable.finished_task;
            }
            catch
            {
                Status_Label.Content = "Status: Could not load from online =(";
                Status_Label.Background = red;
                BuildButton.Background = red;
                Status_Label.Visibility = Visibility.Visible;
                return;
            }
            //StackDefaultAirtablePanel.Children.Add(TopRow());
            foreach (string site in airtable.template_dictionary.Keys)
            {
                AddAirTableRow atrow = new AddAirTableRow(site, airtable);
                Border myborder = new Border();
                myborder.Background = Brushes.Black;
                myborder.BorderThickness = new Thickness(5);
                StackDefaultAirtablePanel.Children.Add(myborder);
                StackDefaultAirtablePanel.Children.Add(atrow);
                default_airtable_list.Add(atrow);
            }
            if (airtable.template_dictionary.Keys.Count > 0)
            {
                BuildButton.IsEnabled = true;
                SelectAllButton.IsEnabled = true;
                Status_Label.Content = "Ready!";
                BuildButton.Background = lightgreen;
                Status_Label.Visibility = Visibility.Hidden;
                CheckBoxLabel.Visibility = Visibility.Visible;
                IncludeLabel.Visibility = Visibility.Visible;
                TemplateNameLabel.Visibility = Visibility.Visible;
            }
            else
            {
                Status_Label.Content = "No records found!";
                BuildButton.IsEnabled = false;
                SelectAllButton.IsEnabled = false;
                Status_Label.Background = red;
                Status_Label.Visibility = Visibility.Visible;
            }
            
        }
        public async Task Main(ReadAirTable airTable)
        {
            await airTable.finished_task;
            finished = true;
        }
        private void Build_button_click(object sender, RoutedEventArgs e)
        {
            foreach (AddAirTableRow row in default_airtable_list)
            {
                if ((bool) row.check_box.IsChecked)
                {
                    TemplateMaker evaluator = new TemplateMaker();
                    evaluator.set_onto_path(Path.Combine(folder_location, "Ontologies"));
                    evaluator.define_output(Path.Combine(folder_location, row.site_name));
                    evaluator.ROIs = row.airtable.roi_dictionary[row.site_name];
                    evaluator.make_template();
                }
            }
            Close();
        }
        private void SearchTextUpdate(object sender, TextChangedEventArgs e)
        {
            StackDefaultAirtablePanel.Children.Clear();
            foreach (AddAirTableRow template_row in default_airtable_list)
            {
                if (template_row.site_label.Content.ToString().ToLower().Contains(SearchBox_TextBox.Text))
                {
                    StackDefaultAirtablePanel.Children.Add(template_row);
                    Border myborder = new Border();
                    myborder.Background = Brushes.Black;
                    myborder.BorderThickness = new Thickness(5);
                    StackDefaultAirtablePanel.Children.Add(myborder);
                }
            }
        }
        private void Template_ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Delete_CheckBox.IsChecked = false;
            DeleteButton.IsEnabled = false;
            BuildTables();
        }

        private void DeleteTemplate_Click(object sender, RoutedEventArgs e)
        {
            DeleteButton.IsEnabled = false;
            Delete_CheckBox.IsChecked = false;
            foreach (ReadAirTable airtable in airtables)
            {
                if (airtable.AirTableName == (string)Template_ComboBox.SelectedItem)
                {
                    airtable.Delete();
                    airtables.Remove(airtable);
                    build_combobox();
                    break;
                }
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
                DeleteButton.IsEnabled = true;
        }
        private void CheckBox_UnChecked(object sender, RoutedEventArgs e)
        {
            DeleteButton.IsEnabled = false;
        }

        private void AddAirTable_Click(object sender, RoutedEventArgs e)
        {
            AddAirTableTemplate at_window = new AddAirTableTemplate(airtables);
            at_window.ShowDialog();
            build_combobox();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (AddAirTableRow row in default_airtable_list)
            {
                row.check_box.IsChecked = true;
            }
        }
    }
}
