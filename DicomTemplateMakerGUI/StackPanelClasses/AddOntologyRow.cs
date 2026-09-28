using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    class AddOntologyRow : StackPanel
    {
        private OntologyCodeClass ontology;
        private List<OntologyCodeClass> ontology_list;
        private TextBox ontology_name_textbox, code_value_textbox, code_scheme_textbox;
        private CheckBox DeleteCheckBox;
        private Button DeleteButton;
        private string onto_path;
        private string originalCodeMeaning;

        public AddOntologyRow(List<OntologyCodeClass> ontology_list, OntologyCodeClass ontology, string onto_path)
        {
            Orientation = Orientation.Horizontal;
            this.ontology = ontology;
            this.ontology_list = ontology_list;
            this.onto_path = onto_path;
            this.originalCodeMeaning = ontology.CodeMeaning;

            ontology_name_textbox = new TextBox();
            ontology_name_textbox.Text = ontology.CodeMeaning;
            ontology_name_textbox.TextChanged += TextValueChange;
            ontology_name_textbox.Width = 200;
            Children.Add(ontology_name_textbox);

            code_value_textbox = new TextBox();
            code_value_textbox.Text = ontology.CodeValue;
            code_value_textbox.TextChanged += TextValueChange;
            code_value_textbox.Width = 200;
            Children.Add(code_value_textbox);

            code_scheme_textbox = new TextBox();
            code_scheme_textbox.Text = ontology.Scheme;
            code_scheme_textbox.TextChanged += TextValueChange;
            code_scheme_textbox.Width = 150;
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
            DeleteButton.Width = 75;
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

            // Remove from the ontology list
            ontology_list.Remove(ontology);

            // Save the updated list to JSON (this also cleans up any legacy text files)
            OntologyTools.SaveOntologiesToFolder(ontology_list, onto_path);
        }

        private void TextValueChange(object sender, TextChangedEventArgs e)
        {
            // Update the ontology object with new values
            ontology.CodeMeaning = ontology_name_textbox.Text;
            ontology.CodeValue = code_value_textbox.Text;
            ontology.Scheme = code_scheme_textbox.Text;

            // Save the updated list to JSON (this also cleans up any legacy text files)
            OntologyTools.SaveOntologiesToFolder(ontology_list, onto_path);
        }
    }
}
