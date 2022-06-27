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
        public EditOntologyWindow()
        {
            InitializeComponent();
        }
        private void RefreshView()
        {
            OntologyStackPanel.Children.Clear();
            foreach (OntologyClass onto in ontology_list)
            {
                AddOntologyRow new_row = new AddOntologyRow(ontology_list, onto);
                OntologyStackPanel.Children.Add(new_row);
            }
        }

        private void AddOntology_Button(object sender, RoutedEventArgs e)
        {

        }
    }
}
