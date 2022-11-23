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
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Interaction logic for AddAirTableTemplate.xaml
    /// </summary>
    public partial class AddAirTableTemplate : Window, INotifyPropertyChanged
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
        public AddAirTableTemplate(ObservableCollection<ReadAirTable> ats)
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
    }
}
