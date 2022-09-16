using System;
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
            BuildButton.Background = yellow;
            Status_Label.Visibility = Visibility.Visible;
            await airtable.finished_task;
            StackDefaultAirtablePanel.Children.Add(TopRow());
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
            Status_Label.Content = "Ready!";
            BuildButton.Background = lightgreen;
            Status_Label.Visibility = Visibility.Hidden;
            BuildButton.IsEnabled = true;
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
                if (template_row.site_label.Content.ToString().ToLower().Contains(SearchBox_TextBox.Text.ToLower()))
                {
                    StackDefaultAirtablePanel.Children.Add(template_row);
                    Border myborder = new Border();
                    myborder.Background = Brushes.Black;
                    myborder.BorderThickness = new Thickness(5);
                    StackDefaultAirtablePanel.Children.Add(myborder);
                }
            }
        }
        private void AddAirTableTextUpdate(object sender, TextChangedEventArgs e)
        {
            AddAirTableButton.IsEnabled = false;
            if (TableName_TextBox.Text != "")
            {
                if (API_TextBox.Text != "")
                {
                    if (Base_TextBox.Text != "")
                    {
                        if (Table_TextBox.Text != "")
                        {
                            AddAirTableButton.IsEnabled = true;
                        }
                    }
                }
            }

        }
        private void Template_ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BuildTables();
        }

        private void AddAirTableButton_Click(object sender, RoutedEventArgs e)
        {
            ReadAirTable ratb = new ReadAirTable(TableName_TextBox.Text, API_TextBox.Text, Base_TextBox.Text, Table_TextBox.Text);
            ratb.read_records();
            airtables.Add(ratb);
            build_combobox();
        }
    }
}
