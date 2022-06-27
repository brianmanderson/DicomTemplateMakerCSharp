using System;
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
    }
    class AddOntologyRow : StackPanel
    {
        private OntologyClass ontology;
        private TextBox ontology_name_textbox;
        private CheckBox DeleteCheckBox;
        private Button DeleteButton;
        private OntologyClass onto;
        public AddOntologyRow(OntologyClass ontology)
        {
            this.onto = ontology;
            ontology_name_textbox = new TextBox();
            ontology_name_textbox.Text = onto.Name;
            ontology_name_textbox.TextChanged += OntologyNameChanged;
        }
        private void OntologyNameChanged(object sender, TextChangedEventArgs e)
        {
            onto.Name = ontology_name_textbox.Text;
        }
    }
}
