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
using DicomTemplateMakerGUI.StackPanelClasses;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Interaction logic for AddAirTableTemplate.xaml
    /// </summary>
    public partial class ChangeOntologyWindow : Window
    {
        public FMAID_SNOMED_OntologyClass converter = new FMAID_SNOMED_OntologyClass();
        private List<AddTemplateRow> template_rows;
        List<string> default_ontology_list = new List<string> { "Default ontology scheme?", "FMA", "SNOMED-CT" };
        public ChangeOntologyWindow(List<AddTemplateRow> template_rows)
        {
            InitializeComponent();
            From_ComboBox.ItemsSource = default_ontology_list;
            From_ComboBox.SelectedIndex = 0;
            To_ComboBox.ItemsSource = default_ontology_list;
            To_ComboBox.SelectedIndex = 0;
            this.template_rows = template_rows;
        }

        private void ConverterButton_Click(object sender, RoutedEventArgs e)
        {
            string from_onto = (string)From_ComboBox.SelectedItem;
            string to_onto = (string)To_ComboBox.SelectedItem;
            Dictionary<string, string> convert_dict;
            if (from_onto == "FMA")
            {
                convert_dict = converter.FMA_to_SNOMED;
            }
            else
            {
                convert_dict = converter.SNOMED_To_FMA;
            }
            foreach (AddTemplateRow template_row in template_rows)
            {
                TemplateMaker template_maker = template_row.templateMaker;
                bool rewrite = false;
                foreach (ROIClass roi in template_maker.ROIs)
                {
                    OntologyCodeClass onto = roi.Ontology_Class;
                    if (onto.Scheme == from_onto)
                    {
                        if (convert_dict.ContainsKey(onto.CodeValue))
                        {
                            onto.CodeValue = convert_dict[onto.CodeValue];
                            onto.Scheme = to_onto;
                            rewrite = true;
                        }
                    }
                }
                if (rewrite)
                {
                    template_maker.make_template();
                    template_maker.write_ontologies();
                }
            }
            From_ComboBox.SelectedIndex = 0;
            To_ComboBox.SelectedIndex = 0;
            ConverterButton.IsEnabled = false;
        }

        private void ToFromComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ConverterButton.IsEnabled = false;
            if (From_ComboBox.SelectedIndex != 0)
            {
                if (To_ComboBox.SelectedIndex != 0)
                {
                    ConverterButton.IsEnabled = true;
                }
            }
        }
    }
}
