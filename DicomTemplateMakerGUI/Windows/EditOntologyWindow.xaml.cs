﻿using System;
using System.Collections.Generic;
using System.IO;
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
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.StackPanelClasses;
using Microsoft.WindowsAPICodePack.Dialogs;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Interaction logic for EditOntologyWindow.xaml
    /// </summary>
    public partial class EditOntologyWindow : Window
    {
        Brush lightgreen = new SolidColorBrush(Color.FromRgb(144, 238, 144));
        Brush white = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        Brush red = new SolidColorBrush(Color.FromRgb(255, 0, 0));
        Brush yellow = new SolidColorBrush(Color.FromRgb(255, 255, 0));
        private string onto_path;
        public TemplateMaker template_maker;
        private List<AddTemplateRow> template_rows;
        public EditOntologyWindow(string path, List<AddTemplateRow> template_rows)
        {
            onto_path = Path.Combine(path, "Ontologies");
            this.template_rows = template_rows;
            if (!Directory.Exists(onto_path))
            {
                Directory.CreateDirectory(onto_path);
            }
            InitializeComponent();
            OntologyStackPanel.Children.Add(TopRow());
            BuildFromFolders();
        }
        private void check_status()
        {
            AddOntology_Button.IsEnabled = false;
            SearchBox_TextBox.IsEnabled = false;
            PreferredNameTextBox.Background = white;
            CodeValue_TextBox.Background = white;
            AddOntology_Button.Background = white;
            if (template_maker.Ontologies.Count > 0)
            {
                SearchBox_TextBox.IsEnabled = true;
            }
            if (template_maker.Ontologies.Where(p => p.CodeValue == CodeValue_TextBox.Text).Any())
            {
                CodeValue_TextBox.Background = yellow;
            }
            if (template_maker.Ontologies.Where(p => p.CodeMeaning.ToLower() == PreferredNameTextBox.Text.ToLower()).Any())
            {
                PreferredNameTextBox.Background = yellow;
            }
            if (CodeValue_TextBox.Background == yellow & PreferredNameTextBox.Background == yellow)
            {
                CodeValue_TextBox.Background = red;
                PreferredNameTextBox.Background = red;
            }
            if (PreferredNameTextBox.Background != red & CodeValue_TextBox.Background != red)
            {
                if (PreferredNameTextBox.Text != "")
                {
                    if (CodeScheme_TextBox.Text != "")
                    {
                        {
                            AddOntology_Button.IsEnabled = true;
                            AddOntology_Button.Background = lightgreen;
                        }
                    }
                }
            }
        }
        private void UpdateText(object sender, TextChangedEventArgs e)
        {
            check_status();
        }
        private void SearchTextUpdate(object sender, TextChangedEventArgs e)
        {
            RefreshView();
        }
        private StackPanel TopRow()
        {
            StackPanel top_row = new StackPanel();
            top_row.Orientation = Orientation.Horizontal;

            Label name_label = new Label();
            name_label.Width = 200;
            name_label.Content = "Common Name";
            top_row.Children.Add(name_label);

            Label code_value = new Label();
            code_value.Width = 200;
            code_value.Content = "Code Value";
            top_row.Children.Add(code_value);

            Label code_scheme = new Label();
            code_scheme.Width = 150;
            code_scheme.Content = "Coding Scheme";
            top_row.Children.Add(code_scheme);
            return top_row;
        }
        private void RefreshView()
        {
            OntologyStackPanel.Children.Clear();
            OntologyStackPanel.Children.Add(TopRow());
            string text = SearchBox_TextBox.Text.ToLower();
            foreach (OntologyCodeClass onto in template_maker.Ontologies)
            {
                bool add_onto = false;
                if (onto.CodeMeaning.ToLower().Contains(text))
                {
                    add_onto = true;
                }
                else if (onto.CodeValue.ToLower().Contains(text))
                {
                    add_onto = true;
                }
                else if (onto.Scheme.ToLower().Contains(text))
                {
                    add_onto = true;
                }
                if (add_onto)
                {
                    AddOntologyRow new_row = new AddOntologyRow(template_maker.Ontologies, onto, onto_path);
                    OntologyStackPanel.Children.Add(new_row);
                }
            }
        }

        private void AddOntology_Click(object sender, RoutedEventArgs e)
        {
            OntologyCodeClass onto = new OntologyCodeClass(PreferredNameTextBox.Text, CodeValue_TextBox.Text, CodeScheme_TextBox.Text);
            template_maker.Ontologies.Add(onto);
            template_maker.Ontologies.Sort((p, q) => p.CodeMeaning.CompareTo(q.CodeMeaning));
            PreferredNameTextBox.Text = "";
            CodeValue_TextBox.Text = "";
            CodeScheme_TextBox.Text = "";
            Save_Changes_Click(sender, e);
            RefreshView();
            check_status();
        }

        private void AddOntologyFromRT_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog("*.dcm");
            dialog.InitialDirectory = ".";
            dialog.IsFolderPicker = false;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                string dicom_file = dialog.FileName;
                template_maker.interpret_RT(dicom_file);
                RefreshView();
                check_status();
                Save_Changes();
            }
        }
        private void Save_Changes()
        {
            if (!Directory.Exists(onto_path))
            {
                Directory.CreateDirectory(onto_path);
            }
            foreach (OntologyCodeClass onto in template_maker.Ontologies)
            {
                onto.write_ontology(template_maker.onto_path);
            }
        }
        private void Save_Changes_Click(object sender, RoutedEventArgs e)
        {
            Save_Changes();
        }
        private void remake_onto()
        {
            template_maker = new TemplateMaker();
            template_maker.set_onto_path(onto_path);
            string[] roi_files = Directory.GetFiles(onto_path, "*.txt");
            foreach (string ontology_file in roi_files)
            {
                OntologyCodeClass onto = new OntologyCodeClass(ontology_file);
                template_maker.Ontologies.Add(onto);
            }
            template_maker.Ontologies.Sort((p, q) => p.CodeMeaning.CompareTo(q.CodeMeaning));
        }
        private void BuildFromFolders()
        {
            remake_onto();
            RefreshView();
        }
        private void Save_and_Exit_Click(object sender, RoutedEventArgs e)
        {
            Save_Changes_Click(sender, e);
            Close();
        }

        private void FMA_SNOMED_Button_Click(object sender, RoutedEventArgs e)
        {
            ChangeOntologyWindow onto_window = new ChangeOntologyWindow(template_rows, onto_path);
            onto_window.ShowDialog();
            BuildFromFolders();
        }
    }
}
