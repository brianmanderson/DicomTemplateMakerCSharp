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
using System.Windows.Media.Imaging;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.StackPanelClasses;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Interaction logic for EditOntologyWindow.xaml
    /// </summary>
    public partial class EditOntologyWindow : Window
    {
        private string onto_path;
        public TemplateMaker template_maker;
        public EditOntologyWindow(string path, TemplateMaker template_maker)
        {
            this.onto_path = Path.Combine(path, "Ontologies");
            template_maker.set_onto_path(Path.Combine(path, "Ontologies"));
            if (!Directory.Exists(onto_path))
            {
                Directory.CreateDirectory(onto_path);
            }
            this.template_maker = template_maker;
            InitializeComponent();
            OntologyStackPanel.Children.Add(TopRow());
            BuildFromFolders();
        }
        private void check_status()
        {
            AddOntology_Button.IsEnabled = false;
            SearchBox_TextBox.IsEnabled = false;
            if (template_maker.Ontologies.Count > 0)
            {
                SearchBox_TextBox.IsEnabled = true;
            }
            if (PreferredNameTextBox.Text != "")
            {
                if (CodeValue_TextBox.Text != "")
                {
                    if (CodeScheme_TextBox.Text != "")
                    {
                        AddOntology_Button.IsEnabled = true;
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
                template_maker.write_ontology(onto);
                //File.WriteAllText(Path.Combine(onto_path, $"{onto.CodeMeaning}.txt"),
                //    $"{onto.CodeValue}\n{onto.Scheme}\n{onto.ContextGroupVersion}\n" +
                //    $"{onto.MappingResource}\n{onto.ContextIdentifier}\n" +
                //    $"{onto.MappingResourceName}\n{onto.MappingResourceUID}\n" +
                //    $"{onto.ContextUID}");
            }
        }
        private void Save_Changes_Click(object sender, RoutedEventArgs e)
        {
            Save_Changes();
        }
        private void BuildFromFolders()
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
                template_maker.Ontologies.Add(onto);
            }
            RefreshView();
        }
        private void Save_and_Exit_Click(object sender, RoutedEventArgs e)
        {
            Save_Changes_Click(sender, e);
            Close();
        }
    }
}
