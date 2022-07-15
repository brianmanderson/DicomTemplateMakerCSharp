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
            name_label.Content = "Name";
            top_row.Children.Add(name_label);

            Label code_value = new Label();
            code_value.Width = 200;
            code_value.Content = "Code Value";
            top_row.Children.Add(code_value);

            Label code_scheme = new Label();
            code_scheme.Width = 200;
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
                if (onto.CommonName.ToLower().Contains(text))
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
            }
        }
        private void Save_Changes_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(onto_path))
            {
                Directory.CreateDirectory(onto_path);
            }
            foreach (OntologyCodeClass onto in template_maker.Ontologies)
            {
                File.WriteAllText(Path.Combine(onto_path, $"{onto.CommonName}.txt"),
                    $"{onto.CodeValue}\n{onto.Scheme}");
            }
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
                OntologyCodeClass onto = new OntologyCodeClass(onto_name, code_value, coding_scheme);
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
