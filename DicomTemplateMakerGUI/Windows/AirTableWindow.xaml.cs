using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.StackPanelClasses;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Interaction logic for AirTableWindow.xaml
    /// </summary>
    public partial class AirTableWindow : Window
    {
        public ReadAirTable airtable;
        string folder_location;
        string onto_path;
        List<AddAirTableRow> default_airtable_list = new List<AddAirTableRow>();
        public AirTableWindow(ReadAirTable airtable, string folder_location, string onto_path)
        {
            InitializeComponent();
            airtable.read_records();
            this.folder_location = folder_location;
            this.onto_path = onto_path;
            this.airtable = airtable;
            foreach (string site in airtable.template_dictionary.Keys)
            {
                AddAirTableRow atrow = new AddAirTableRow(site);
                Border myborder = new Border();
                myborder.Background = Brushes.Black;
                myborder.BorderThickness = new Thickness(5);
                StackDefaultAirtablePanel.Children.Add(myborder);
                StackDefaultAirtablePanel.Children.Add(atrow);
                default_airtable_list.Add(atrow);
            }
        }

        private void Build_button_click(object sender, RoutedEventArgs e)
        {
            foreach (AddAirTableRow row in default_airtable_list)
            {
                if ((bool) row.check_box.IsChecked)
                {

                }
            }
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
    }
}
