using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ROIOntologyClass;
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
        private TemplateMaker template_maker;
        private string onto_path;
        private List<AddTemplateRow> template_rows;
        List<string> default_ontology_list = new List<string> { "FMA", "SNOMEDCT" };
        public ChangeOntologyWindow(List<AddTemplateRow> template_rows, string onto_path)
        {
            InitializeComponent();
            From_ComboBox.ItemsSource = default_ontology_list;
            To_ComboBox.ItemsSource = default_ontology_list;
            this.template_rows = template_rows;
            this.onto_path = onto_path;
        }
        private void remake_onto()
        {
            template_maker = new TemplateMaker();
            template_maker.set_onto_path(onto_path);
            string[] roi_files = Directory.GetFiles(onto_path, "*.txt");
            foreach (string ontology_file in roi_files)
            {
                string onto_name = Path.GetFileName(ontology_file).Replace(".txt", "");
                string[] instructions = File.ReadAllLines(ontology_file);
                string code_value = instructions[0];
                string coding_scheme = instructions[1];
                string context_group_version = instructions[2];
                string mapping_resource = instructions[3];
                string context_identifier = instructions[4];
                string mapping_resource_name = instructions[5];
                string mapping_resource_uid = instructions[6];
                string context_uid = instructions[7];
                OntologyCodeClass onto = new OntologyCodeClass(onto_name, code_value, coding_scheme, context_group_version, mapping_resource,
                    context_identifier, mapping_resource_name, mapping_resource_uid, context_uid);
                template_maker.Ontologies.Add(onto);
            }
            template_maker.Ontologies.Sort((p, q) => p.CodeMeaning.CompareTo(q.CodeMeaning));
        }
        private void UpdateTemplateROIs(string from_onto, string to_onto)
        {
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
        }
        private void UpdateOntologyFiles(string from_onto, string to_onto)
        {
            Dictionary<string, string> convert_dict;
            remake_onto();
            if (from_onto == "FMA")
            {
                convert_dict = converter.FMA_to_SNOMED;
            }
            else
            {
                convert_dict = converter.SNOMED_To_FMA;
            }
            bool rewrite = false;
            foreach (OntologyCodeClass onto in template_maker.Ontologies)
            {
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
                template_maker.write_ontologies();
            }
        }
        private void ConverterButton_Click(object sender, RoutedEventArgs e)
        {
            ///
            /// This will remake the ontology codes inside of the ROIs present in our templates
            ///
            string from_onto = (string)From_ComboBox.SelectedItem;
            string to_onto = (string)To_ComboBox.SelectedItem;
            if (from_onto == "SNOMEDCT")
            {
                from_onto = "SCT";
            }
            else if (to_onto == "SNOMEDCT")
            {
                to_onto = "SCT";
            }
            UpdateTemplateROIs(from_onto, to_onto);
            UpdateOntologyFiles(from_onto, to_onto);
            From_ComboBox.SelectedIndex = -1;
            To_ComboBox.SelectedIndex = -1;
            ConverterButton.IsEnabled = false;
        }

        private void ToFromComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ConverterButton.IsEnabled = false;
            if (From_ComboBox.SelectedIndex != -1)
            {
                if (To_ComboBox.SelectedIndex != -1)
                {
                    ConverterButton.IsEnabled = true;
                }
            }
        }
    }
}
