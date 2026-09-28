using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;


namespace DicomTemplateMakerGUI.StackPanelClasses
{
    class AddROIRow : StackPanel
    {
        Button color_button, dvh_color_button, link_button;
        private ROIClass roi;
        private TextBox roi_name_textbox;
        private List<ROIClass> roi_list;
        private List<OntologyCodeClass> ontologies_list;
        private CheckBox DeleteCheckBox;
        private Button DeleteButton;
        private string roi_path;
        public AddROIRow(List<ROIClass> _roi_list, ROIClass _roi, string path, List<OntologyCodeClass> _ontologies_list) //, 
        {
            roi = _roi;
            roi_list = _roi_list;
            roi_path = path;
            ontologies_list = _ontologies_list;
            Orientation = Orientation.Horizontal;

            CheckBox included_checkbox = new CheckBox();
            Binding check_box_binding = new Binding("Include");
            check_box_binding.Source = roi;
            included_checkbox.SetBinding(CheckBox.IsCheckedProperty, check_box_binding);
            included_checkbox.Width = 50;
            Children.Add(included_checkbox);

            roi_name_textbox = new TextBox();
            roi_name_textbox.Text = roi.ROIName;
            roi_name_textbox.TextChanged += TextValueChange;
            roi_name_textbox.Width = 200;
            Children.Add(roi_name_textbox);

            Binding ontology_binding = new Binding("Ontology_Class");
            ontology_binding.Source = roi;
            ComboBox ontology_combobox = new ComboBox();
            ontology_combobox.SetBinding(ComboBox.SelectedItemProperty, ontology_binding);
            ontology_combobox.ItemsSource = ontologies_list;
            ontology_combobox.DisplayMemberPath = "CodeMeaning";
            ontology_combobox.Width = 175;
            Children.Add(ontology_combobox);

            List<string> interpreters = new List<string> { "ORGAN", "PTV", "CTV", "GTV", "MARKER", "AVOIDANCE", "CONTROL", "BOLUS", "EXTERNAL", "ISOCENTER", "REGISTRATION", "CONTRAST_AGENT",
                "CAVITY", "BRACHY_CHANNEL", "BRACHY_ACCESSORY", "SUPPORT", "FIXATION", "DOSE_REGION", "DOSE_MEASUREMENT", "BRACHY_SRC_APP", "TREATED_VOLUME", "IRRAD_VOLUME"};
            Binding interp_binding = new Binding("ROI_Interpreted_type");
            interp_binding.Source = roi;

            ComboBox roi_interp_combobox = new ComboBox();
            roi_interp_combobox.SetBinding(ComboBox.SelectedItemProperty, interp_binding);
            roi_interp_combobox.ItemsSource = interpreters;
            string interpreted_type = RequireInterpretedType(roi);
            if (interpreters.Contains(interpreted_type.ToUpper()))
            {
                roi_interp_combobox.SelectedItem = interpreted_type.ToUpper();
            }
            roi_interp_combobox.Width = 150;
            Children.Add(roi_interp_combobox);
            color_button = new Button();
            color_button.Background = RoiBrushes.Fill(roi);
            color_button.Width = 75;
            color_button.Click += color_button_Click;
            Children.Add(color_button);

            dvh_color_button = new Button();
            dvh_color_button.Background = RoiBrushes.DvhLine(roi);
            dvh_color_button.Width = 75;
            dvh_color_button.Click += dvh_color_button_Click;
            Children.Add(dvh_color_button);

            link_button = new Button();
            link_button.Width = 75;
            link_button.Content = "Link?";
            link_button.Click += link_button_Click;
            Children.Add(link_button);


            List<string> dvh_line_style = new List<string> { "solid", "-------", "*******", "-*-*-*-", "-**-**-" };
            Binding line_style_binding = new Binding("DVHLineStyle");
            line_style_binding.Source = roi;

            ComboBox roi_dvh_line_style_combobox = new ComboBox();
            roi_dvh_line_style_combobox.SetBinding(ComboBox.SelectedItemProperty, line_style_binding);
            roi_dvh_line_style_combobox.ItemsSource = dvh_line_style;
            roi_dvh_line_style_combobox.Width = 75;
            if (dvh_line_style.Contains(roi.DVHLineStyle))
            {
                roi_dvh_line_style_combobox.SelectedItem = roi.DVHLineStyle;
            }
            Children.Add(roi_dvh_line_style_combobox);

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
        /// <summary>
        /// The ROI's interpreted type. An ROI without one still fails where the type used to be dereferenced,
        /// now with a message that names the ROI.
        /// </summary>
        internal static string RequireInterpretedType(ROIClass roi)
        {
            return roi.ROI_Interpreted_type ?? throw new InvalidOperationException($"ROI '{roi.ROIName}' has no interpreted type.");
        }
        /// <summary>
        /// The ROI's ontology class. An ROI read from a template file whose Ontology_Class is null or missing still
        /// fails where the class used to be dereferenced, now with a message that names the ROI.
        /// </summary>
        internal static OntologyCodeClass RequireOntologyClass(ROIClass roi)
        {
            return roi.Ontology_Class ?? throw new InvalidOperationException($"ROI '{roi.ROIName}' has no ontology class.");
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
            roi_list.Remove(roi);
            Children.Clear();
            delete_previous();
        }
        private void color_button_Click(object sender, System.EventArgs e)
        {
            System.Windows.Forms.ColorDialog MyDialog = new System.Windows.Forms.ColorDialog();
            if (MyDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                roi.update_color(MyDialog.Color.R, MyDialog.Color.G, MyDialog.Color.B);
                set_button_color();
            }
        }
        private void link_button_Click(object sender, System.EventArgs e)
        {
            roi.DVHLineColor = "-16777216";
            roi.build_dvh_line_color();
            set_button_color();
        }
        private void set_button_color()
        {
            color_button.Background = RoiBrushes.Fill(roi);
            dvh_color_button.Background = RoiBrushes.DvhLine(roi);
        }
        private void dvh_color_button_Click(object sender, System.EventArgs e)
        {
            System.Windows.Forms.ColorDialog MyDialog = new System.Windows.Forms.ColorDialog();
            if (MyDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                roi.update_dvh_color(MyDialog.Color.R, MyDialog.Color.G, MyDialog.Color.B);
                set_button_color();
            }
        }
        private void delete_previous()
        {
            ROIClassTools.SaveROIsToFolder(roi_list, roi_path);
        }
        private void TextValueChange(object sender, TextChangedEventArgs e)
        {
            delete_previous();
            roi.ROIName = roi_name_textbox.Text;
        }
    }
}
