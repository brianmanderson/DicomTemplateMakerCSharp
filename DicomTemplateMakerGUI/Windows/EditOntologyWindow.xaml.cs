using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using DicomTemplateMakerGUI.StackPanelClasses;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Interaction logic for EditOntologyWindow.xaml
    /// </summary>
    public partial class EditOntologyWindow : Window
    {
        private List<OntologyClass> ontology_list = new List<OntologyClass>();
        private string path;
        public EditOntologyWindow(string path)
        {
            this.path = path;
            InitializeComponent();
            OntologyStackPanel.Children.Add(TopRow());
        }
        private void check_status()
        {
            AddOntology_Button.IsEnabled = false;
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
        private StackPanel TopRow()
        {
            StackPanel top_row = new StackPanel();
            top_row.Orientation = Orientation.Horizontal;

            Label name_label = new Label();
            name_label.Width = 200;
            name_label.Content = "Name";
            top_row.Children.Add(name_label);

            Label code_value = new Label();
            code_value.Width = 100;
            code_value.Content = "Code Value";
            top_row.Children.Add(code_value);

            Label code_scheme = new Label();
            code_scheme.Width = 100;
            code_scheme.Content = "Coding Scheme";
            top_row.Children.Add(code_scheme);
            return top_row;
        }
        private void RefreshView()
        {
            OntologyStackPanel.Children.Clear();
            OntologyStackPanel.Children.Add(TopRow());
            foreach (OntologyClass onto in ontology_list)
            {
                AddOntologyRow new_row = new AddOntologyRow(ontology_list, onto);
                OntologyStackPanel.Children.Add(new_row);
            }
        }

        private void AddOntology_Click(object sender, RoutedEventArgs e)
        {
            OntologyClass onto = new OntologyClass(PreferredNameTextBox.Text, CodeValue_TextBox.Text, CodeScheme_TextBox.Text);
            ontology_list.Add(onto);
            RefreshView();
            PreferredNameTextBox.Text = "";
            CodeValue_TextBox.Text = "";
            CodeScheme_TextBox.Text = "";
        }

        private void AddOntologyFromRT_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
