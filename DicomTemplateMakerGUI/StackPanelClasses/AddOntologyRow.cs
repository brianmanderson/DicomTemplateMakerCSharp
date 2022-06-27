﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DicomTemplateMakerGUI.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Collections.Generic;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    public class OntologyClass
    {
        public string Name;
        public string CodeValue;
        public string CodingScheme;
    }
    class AddOntologyRow : StackPanel
    {
        private OntologyClass ontology;
        private TextBox ontology_name_textbox, code_value_textbox, code_scheme_textbox;
        private CheckBox DeleteCheckBox;
        private Button DeleteButton;
        private OntologyClass onto;
        public AddOntologyRow(OntologyClass ontology)
        {
            Orientation = Orientation.Horizontal;
            this.onto = ontology;
            ontology_name_textbox = new TextBox();
            ontology_name_textbox.Text = onto.Name;
            ontology_name_textbox.TextChanged += TextValueChange;
            ontology_name_textbox.Width = 200;
            Children.Add(ontology_name_textbox);

            code_value_textbox = new TextBox();
            code_value_textbox.Text = onto.CodeValue;
            code_value_textbox.TextChanged += TextValueChange;
            code_value_textbox.Width = 200;
            Children.Add(code_value_textbox);

            code_scheme_textbox = new TextBox();
            code_scheme_textbox.Text = onto.CodingScheme;
            code_scheme_textbox.TextChanged += TextValueChange;
            code_scheme_textbox.Width = 200;
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
        }
        private void TextValueChange(object sender, TextChangedEventArgs e)
        {
            onto.Name = ontology_name_textbox.Text;
            onto.CodeValue = code_value_textbox.Text;
            onto.CodingScheme = code_scheme_textbox.Text;
        }
    }
}
