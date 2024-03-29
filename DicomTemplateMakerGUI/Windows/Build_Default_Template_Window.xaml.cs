﻿using System;
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
using ROIOntologyClass;

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
            if (!Directory.Exists(Path.Combine(folder_location, "Template_Dicoms")))
            {
                Directory.CreateDirectory(Path.Combine(folder_location, "Template_Dicoms"));
            }
            string[] rt_files = Directory.GetFiles(Path.Combine(folder_location, "Template_Dicoms"), "*.dcm");
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
                OntologyCodeClass onto = new OntologyCodeClass(ontology_file);
                evaluator.Ontologies.Add(onto);
            }
            evaluator.Ontologies.Sort((p, q) => p.CodeMeaning.CompareTo(q.CodeMeaning));
            return evaluator;
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
                    evaluator.define_output(Path.Combine(folder_location, folder_path));
                    evaluator.make_template();
                }
            }
            Close();
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
