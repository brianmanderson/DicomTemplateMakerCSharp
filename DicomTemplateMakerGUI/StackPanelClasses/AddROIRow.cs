using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ROIOntologyClass;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;
using System.Security.Policy;

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
        public AddROIRow(List<ROIClass> roi_list, ROIClass roi, string path, List<OntologyCodeClass> ontologies_list) //, 
        {
            this.roi = roi;
            this.roi_list = roi_list;
            this.roi_path = path;
            this.ontologies_list = ontologies_list;
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
            ontology_combobox.SelectionChanged += SelectionChangedEvent;
            ontology_combobox.Width = 175;
            Children.Add(ontology_combobox);

            List<string> interpreters = new List<string> { "ORGAN", "PTV", "CTV", "GTV", "MARKER", "AVOIDANCE", "CONTROL", "BOLUS", "EXTERNAL", "ISOCENTER", "REGISTRATION", "CONTRAST_AGENT",
                "CAVITY", "BRACHY_CHANNEL", "BRACHY_ACCESSORY", "SUPPORT", "FIXATION", "DOSE_REGION", "DOSE_MEASUREMENT", "BRACHY_SRC_APP", "TREATED_VOLUME", "IRRAD_VOLUME"};
            Binding interp_binding = new Binding("ROI_Interpreted_type");
            interp_binding.Source = roi;

            ComboBox roi_interp_combobox = new ComboBox();
            roi_interp_combobox.SetBinding(ComboBox.SelectedItemProperty, interp_binding);
            roi_interp_combobox.ItemsSource = interpreters;
            roi_interp_combobox.SelectionChanged += SelectionChangedEvent;
            if (interpreters.Contains(roi.ROI_Interpreted_type.ToUpper()))
            {
                roi_interp_combobox.SelectedItem = roi.ROI_Interpreted_type.ToUpper();
            }
            roi_interp_combobox.Width = 150;
            Children.Add(roi_interp_combobox);
            color_button = new Button();
            color_button.Background = roi.ROI_Brush;
            color_button.Width = 75;
            color_button.Click += color_button_Click;
            Children.Add(color_button);

            dvh_color_button = new Button();
            dvh_color_button.Background = roi.DVH_Brush;
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
            roi_dvh_line_style_combobox.SelectionChanged += SelectionChangedEvent;
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
            rebuild_text();
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
                rebuild_text();
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
            color_button.Background = roi.ROI_Brush;
            dvh_color_button.Background = roi.DVH_Brush;
        }
        private void dvh_color_button_Click(object sender, System.EventArgs e)
        {
            System.Windows.Forms.ColorDialog MyDialog = new System.Windows.Forms.ColorDialog();
            if (MyDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                roi.update_dvh_color(MyDialog.Color.R, MyDialog.Color.G, MyDialog.Color.B);
                set_button_color();
                rebuild_text();
            }
        }
        private void SelectionChangedEvent(object sender, SelectionChangedEventArgs args)
        {
            rebuild_text();
        }
        private void delete_previous()
        {
            if (File.Exists(Path.Combine(Path.Combine(roi_path, $"{roi.ROIName}.txt"))))
            {
                File.Delete(Path.Combine(Path.Combine(roi_path, $"{roi.ROIName}.txt")));
            }
        }
        private void rebuild_text()
        {
            if (!Directory.Exists(roi_path))
            {
                Directory.CreateDirectory(roi_path);
            }
            try
            {
                roi.write_roi(roi_path);
            }
            catch
            {
            }

        }
        private void TextValueChange(object sender, TextChangedEventArgs e)
        {
            delete_previous();
            roi.ROIName = roi_name_textbox.Text;
            rebuild_text();
        }
    }
}
