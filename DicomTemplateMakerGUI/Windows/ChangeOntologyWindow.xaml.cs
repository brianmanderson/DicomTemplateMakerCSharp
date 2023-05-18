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
    public partial class ChangeOntologyWindow : Window
    {
        public FMAID_SNOMED_OntologyClass converter = new FMAID_SNOMED_OntologyClass();
        List<string> default_ontology_list = new List<string> { "Default ontology scheme?", "FMA", "SNOMED-CT" };
        public ChangeOntologyWindow()
        {
            InitializeComponent();
            FMA_SNOMED_ComboBox.ItemsSource = default_ontology_list;
            FMA_SNOMED_ComboBox.SelectedIndex = 0;
        }

        private void ConverterButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
