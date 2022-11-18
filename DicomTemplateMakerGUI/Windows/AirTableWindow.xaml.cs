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
using System.ComponentModel;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.StackPanelClasses;
using AirtableApiClient;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Interaction logic for AirTableWindow.xaml
    /// </summary>
    public partial class AirTableWindow : Window, INotifyPropertyChanged
    {
        public ReadAirTable airtable;
        private List<ReadAirTable> airtables;
        public List<ReadAirTable> AirTables
        {
            get { return airtables; }
            set
            {
                airtables = value;
                OnPropertyChanged("AirTables");
            }
        }
        string folder_location;
        string onto_path;
        bool finished = false;
        List<AddAirTableRow> default_airtable_list = new List<AddAirTableRow>();
        Brush lightgreen = new SolidColorBrush(Color.FromRgb(144, 238, 144));
        Brush lightgray = new SolidColorBrush(Color.FromRgb(221, 221, 221));
        Brush yellow = new SolidColorBrush(Color.FromRgb(255, 255, 0));
        Brush red = new SolidColorBrush(Color.FromRgb(255, 0, 0));
        public AirTableWindow(List<ReadAirTable> ats, string folder_location, string onto_path)
        {
            InitializeComponent();
            this.folder_location = folder_location;
            this.onto_path = onto_path;
            AirTables = ats;
            build_combobox();
        }
        private void build_combobox()
        {
            Template_ComboBox.DisplayMemberPath = "AirTableName";
            Binding source_binding = new Binding("AirTables");
            source_binding.Source = this;
            Template_ComboBox.SetBinding(ComboBox.ItemsSourceProperty, source_binding);
            Template_ComboBox.SelectedIndex = -1;
            if (AirTables.Count > 0)
            {
                Template_ComboBox.SelectedIndex = 0;
            }
        }
        private void BuildTables()
        {
            BuildButton.IsEnabled = false;
            StackDefaultAirtablePanel.Children.Clear();
            default_airtable_list = new List<AddAirTableRow>();
            BuildTable((ReadAirTable)Template_ComboBox.SelectedItem);
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
            if (airtable.AirTableName == ((ReadAirTable)Template_ComboBox.SelectedItem).AirTableName)
            {
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
            if (Template_ComboBox.SelectedIndex != -1)
            {
                BuildTables();
            }

        }

        private void DeleteTemplate_Click(object sender, RoutedEventArgs e)
        {
            DeleteButton.IsEnabled = false;
            Delete_CheckBox.IsChecked = false;
            ReadAirTable airtable = (ReadAirTable)Template_ComboBox.SelectedItem;
            airtable.Delete();
            AirTables.Remove(airtable);
            build_combobox();
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
            AddAirTableTemplate at_window = new AddAirTableTemplate(AirTables);
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
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string info)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(info));
            }
        }
    }
}
