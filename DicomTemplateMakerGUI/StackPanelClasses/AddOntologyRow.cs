using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DicomTemplateMakerGUI.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Collections.Generic;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    class OntologyClass
    {
        public string Name;
        public string CodeValue;
        public string CodingScheme;
        public OntologyClass(string name, string code_value, string coding_scheme)
        {
            this.Name = name;
            this.CodeValue = code_value;
            this.CodingScheme = coding_scheme;
        }
    }
    class AddOntologyRow : StackPanel
    {
        private OntologyClass ontology;
        private List<OntologyClass> ontology_list;
        private TextBox ontology_name_textbox, code_value_textbox, code_scheme_textbox;
        private CheckBox DeleteCheckBox;
        private Button DeleteButton;
        private string onto_path;
        public AddOntologyRow(List<OntologyClass> ontology_list, OntologyClass ontology, string onto_path)
        {
            Orientation = Orientation.Horizontal;
            this.ontology = ontology;
            this.ontology_list = ontology_list;
            this.onto_path = onto_path;
            ontology_name_textbox = new TextBox();
            ontology_name_textbox.Text = ontology.Name;
            ontology_name_textbox.TextChanged += TextValueChange;
            ontology_name_textbox.Width = 200;
            Children.Add(ontology_name_textbox);

            code_value_textbox = new TextBox();
            code_value_textbox.Text = ontology.CodeValue;
            code_value_textbox.TextChanged += TextValueChange;
            code_value_textbox.Width = 100;
            Children.Add(code_value_textbox);

            code_scheme_textbox = new TextBox();
            code_scheme_textbox.Text = ontology.CodingScheme;
            code_scheme_textbox.TextChanged += TextValueChange;
            code_scheme_textbox.Width = 100;
            Children.Add(code_scheme_textbox);

            Label DeleteLabel = new Label();
            DeleteLabel.Content = "Delete?";
            DeleteLabel.Width = 50;
            Children.Add(DeleteLabel);

            DeleteCheckBox = new CheckBox();
            DeleteCheckBox.Width = 30;
            DeleteCheckBox.Checked += CheckBox_DataContextChanged;
            DeleteCheckBox.Unchecked += CheckBox_DataContextChanged;
            Children.Add(DeleteCheckBox);

            DeleteButton = new Button();
            DeleteButton.IsEnabled = false;
            DeleteButton.Content = "Delete";
            DeleteButton.Width = 150;
            DeleteButton.Click += DeleteButton_Click;
            Children.Add(DeleteButton);
        }
        private void CheckBox_DataContextChanged(object sender, RoutedEventArgs e)
        {
            bool delete_checked = DeleteCheckBox.IsChecked ?? false;
            DeleteButton.IsEnabled = false;
            if (delete_checked)
            {
                DeleteButton.IsEnabled = true;
            }
        }
        private void DeleteButton_Click(object sender, System.EventArgs e)
        {
            Children.Clear();
            ontology_list.Remove(ontology);
            if (File.Exists(Path.Combine(Path.Combine(onto_path, $"{ontology.Name}.txt"))))
            {
                File.Delete(Path.Combine(Path.Combine(onto_path, $"{ontology.Name}.txt")));
            }
        }
        private void TextValueChange(object sender, TextChangedEventArgs e)
        {
            if (File.Exists(Path.Combine(Path.Combine(onto_path, $"{ontology.Name}.txt"))))
            {
                File.Delete(Path.Combine(Path.Combine(onto_path, $"{ontology.Name}.txt")));
            }
            ontology.Name = ontology_name_textbox.Text;
            ontology.CodeValue = code_value_textbox.Text;
            ontology.CodingScheme = code_scheme_textbox.Text;
            File.WriteAllText(Path.Combine(onto_path, $"{ontology.Name}.txt"),
                $"{ontology.CodeValue}\n{ontology.CodingScheme}");
        }
    }
}
