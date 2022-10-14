using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Interaction logic for AddAirTableTemplate.xaml
    /// </summary>
    public partial class AddAirTableTemplate : Window
    {
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
        public AddAirTableTemplate(List<ReadAirTable> ats)
        {
            InitializeComponent();
            AirTables = ats;
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
        private void AddAirTableButton_Click(object sender, RoutedEventArgs e)
        {
            string airtable_directory = Path.Combine(@".", "AirTables");
            if (!Directory.Exists(airtable_directory))
            {
                Directory.CreateDirectory(airtable_directory);
            }
            File.WriteAllText(Path.Combine(airtable_directory, $"{TableName_TextBox.Text}.txt"),
                $"{API_TextBox.Text}\n{Base_TextBox.Text}\n{Table_TextBox.Text}");
            ReadAirTable ratb = new ReadAirTable(TableName_TextBox.Text, API_TextBox.Text, Base_TextBox.Text, Table_TextBox.Text);
            ratb.read_records();
            AirTables.Add(ratb);
            Close();
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
