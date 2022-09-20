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

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Interaction logic for AddAirTableTemplate.xaml
    /// </summary>
    public partial class AddAirTableTemplate : Window
    {
        public List<ReadAirTable> airtables;
        public AddAirTableTemplate(List<ReadAirTable> ats)
        {
            InitializeComponent();
            airtables = ats;
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
            ReadAirTable ratb = new ReadAirTable(TableName_TextBox.Text, API_TextBox.Text, Base_TextBox.Text, Table_TextBox.Text);
            ratb.read_records();
            airtables.Add(ratb);
            Close();
        }
    }
}
