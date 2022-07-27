using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DicomTemplateMakerGUI;
using DicomTemplateMakerGUI.StackPanelClasses;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Interaction logic for Build_Default_Template_Window.xaml
    /// </summary>
    public partial class Build_Default_Template_Window : Window
    {
        string folder_location;
        string onto_path;
        List<AddDefaultTemplateRow> default_template_list;
        public Build_Default_Template_Window(string folder_location, string onto_path)
        {
            this.folder_location = folder_location;
            this.onto_path = onto_path;
            this.default_template_list = new List<AddDefaultTemplateRow>();
            InitializeComponent();
            string[] rt_files = Directory.GetFiles(folder_location, "TG263*.dcm");
            foreach (string rt_file in rt_files)
            {
                AddDefaultTemplateRow template_row = new AddDefaultTemplateRow(rt_file);
                DefaultStackPanel.Children.Add(template_row);
                Border myborder = new Border();
                myborder.Background = Brushes.Black;
                myborder.BorderThickness = new Thickness(5);
                DefaultStackPanel.Children.Add(myborder);
                default_template_list.Add(template_row);
            }
        }
        public TemplateMaker update_ontology_reader(TemplateMaker evaluator)
        {
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
                evaluator.Ontologies.Add(onto);
            }
            evaluator.Ontologies.Sort((p, q) => p.CodeMeaning.CompareTo(q.CodeMeaning));
            return evaluator;
        }
        public void write_rois(TemplateMaker evaluator, string roi_path)
        {
            if (!Directory.Exists(roi_path))
            {
                Directory.CreateDirectory(roi_path);
            }
            foreach (ROIClass roi in evaluator.ROIs)
            {
                try
                {
                    File.WriteAllText(Path.Combine(roi_path, $"{roi.ROIName}.txt"),
                        $"{roi.R}\\{roi.G}\\{roi.B}\n" +
                        $"{roi.Ontology_Class.CodeMeaning}\\{roi.Ontology_Class.CodeValue}\\{roi.Ontology_Class.Scheme}\\{roi.Ontology_Class.ContextGroupVersion}\\" +
                        $"{roi.Ontology_Class.MappingResource}\\{roi.Ontology_Class.ContextIdentifier}\\{roi.Ontology_Class.MappingResourceName}\\" +
                        $"{roi.Ontology_Class.MappingResourceUID}\\{roi.Ontology_Class.ContextUID}\n" +
                        $"{roi.ROI_Interpreted_type}");
                }
                catch
                {
                    int x = 5;
                }
            }
        }
        private void Build_button_click(object sender, RoutedEventArgs e)
        {
            foreach (AddDefaultTemplateRow template_row in default_template_list)
            {
                if ((bool)template_row.check_box.IsChecked)
                {
                    TemplateMaker evaluator = new TemplateMaker();
                    evaluator.set_onto_path(Path.Combine(folder_location, "Ontologies"));
                    evaluator = update_ontology_reader(evaluator);
                    evaluator.interpret_RT(template_row.file_path);
                    string folder_path = Path.GetFileName(template_row.file_path);
                    folder_path = folder_path.Substring(0, folder_path.Length - 4); // Chop off .dcm
                    write_rois(evaluator, Path.Combine(folder_location, folder_path, "ROIs"));
                }
            }
        }

        private void SearchTextUpdate(object sender, TextChangedEventArgs e)
        {
            DefaultStackPanel.Children.Clear();
            foreach (AddDefaultTemplateRow template_row in default_template_list)
            {
                if (template_row.file_name.Content.ToString().ToLower().Contains(SearchBox_TextBox.Text.ToLower()))
                {
                    DefaultStackPanel.Children.Add(template_row);
                    Border myborder = new Border();
                    myborder.Background = Brushes.Black;
                    myborder.BorderThickness = new Thickness(5);
                    DefaultStackPanel.Children.Add(myborder);
                }
            }
        }
    }
}
