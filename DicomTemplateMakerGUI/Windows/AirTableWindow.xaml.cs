using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Interaction logic for AirTableWindow.xaml
    /// </summary>
    public partial class AirTableWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }
        public ReadAirTable airtable;
        private ObservableCollection<ReadAirTable> airtables;
        public ObservableCollection<ReadAirTable> AirTables
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
        List<string> languages = new List<string> {"English/Ingles/Anglais", "Spanish/Espanol/Espagnol", "French/Fracnes/Francais"};
        public AirTableWindow(ObservableCollection<ReadAirTable> ats, string folder_location, string onto_path)
        {
            InitializeComponent();
            Language_ComboBox.Visibility = Visibility.Hidden;
            Laterality_CheckBox.Visibility = Visibility.Hidden;
            this.folder_location = folder_location;
            this.onto_path = onto_path;
            AirTables = ats;
            Template_ComboBox.DisplayMemberPath = "AirTableName";
            Binding source_binding = new Binding("AirTables");
            source_binding.Source = this;
            Template_ComboBox.SetBinding(ComboBox.ItemsSourceProperty, source_binding);
            Language_ComboBox.ItemsSource = languages;
            Language_ComboBox.SelectedIndex = 0;
            build_combobox();
        }
        private void build_combobox()
        {
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
            Laterality_CheckBox.Visibility = Visibility.Hidden;
            Language_ComboBox.Visibility = Visibility.Hidden;
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
                    Laterality_CheckBox.Visibility = Visibility.Hidden;
                    if (atrow.airtable.roi_dictionary[site].FindAll(x => x.has_lateral).Count > 0)
                    {
                        Laterality_CheckBox.Visibility = Visibility.Visible;
                    }
                    Language_ComboBox.Visibility = Visibility.Hidden;
                    if (atrow.airtable.roi_dictionary[site].FindAll(x => x.has_other_lanuages).Count > 0)
                    {
                        Language_ComboBox.Visibility = Visibility.Visible;
                    }
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
                    // We only have to check for French or Spanish, it defaults to english and closes after this
                    if (Language_ComboBox.Visibility == Visibility.Visible)
                    {
                        if (Laterality_CheckBox.Visibility == Visibility.Visible)
                        {
                            foreach (ROIWrapper r_wrapper in row.airtable.roi_dictionary[row.site_name])
                            {
                                if (((string)Language_ComboBox.SelectedItem).Contains("French"))
                                {
                                    r_wrapper.Set_French((bool)Laterality_CheckBox.IsChecked);
                                }
                                else if (((string)Language_ComboBox.SelectedItem).Contains("Spanish"))
                                {
                                    r_wrapper.Set_Spanish((bool)Laterality_CheckBox.IsChecked);
                                }
                                else if (((string)Language_ComboBox.SelectedItem).Contains("English"))
                                {
                                    r_wrapper.Set_English((bool)Laterality_CheckBox.IsChecked);
                                }
                            }
                        }
                        else
                        {
                            foreach (ROIWrapper r_wrapper in row.airtable.roi_dictionary[row.site_name])
                            {
                                if (((string)Language_ComboBox.SelectedItem).Contains("French"))
                                {
                                    r_wrapper.Set_French();
                                }
                                else if (((string)Language_ComboBox.SelectedItem).Contains("Spanish"))
                                {
                                    r_wrapper.Set_Spanish();
                                }
                                else if (((string)Language_ComboBox.SelectedItem).Contains("English"))
                                {
                                    r_wrapper.Set_English();
                                }
                            }
                        }
                    }
                    else if (Laterality_CheckBox.Visibility == Visibility.Visible)
                    {
                        foreach (ROIWrapper r_wrapper in row.airtable.roi_dictionary[row.site_name])
                        {
                            r_wrapper.Set_English((bool)Laterality_CheckBox.IsChecked);
                        }
                    }
                    evaluator.ROIs = row.airtable.roi_dictionary[row.site_name].Select(x => x.roi).ToList();
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
            ReadAirTable at = (ReadAirTable)Template_ComboBox.SelectedItem;
            at.Delete();
            AirTables.Remove(at);
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
    }
}
