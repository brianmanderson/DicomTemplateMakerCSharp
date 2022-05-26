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
using System.Windows.Navigation;
using System.Windows.Shapes;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    class AddROIRow : StackPanel
    {
        Button color_button;
        private ROIClass roi;
        private TextBox roi_name_textbox;
        private List<ROIClass> roi_list;
        private CheckBox DeleteCheckBox;
        private Button DeleteButton;
        public AddROIRow(List<ROIClass> roi_list, ROIClass roi)
        {
            this.roi = roi;
            this.roi_list = roi_list;
            Orientation = Orientation.Horizontal;
            roi_name_textbox = new TextBox();
            roi_name_textbox.Text = roi.name;
            roi_name_textbox.TextChanged += ROINameChanged;
            roi_name_textbox.Width = 200;
            Children.Add(roi_name_textbox);
            List<string> interpreters = new List<string> { "ORGAN", "PTV", "CTV", "GTV", "AVOIDANCE", "CONTROL", "BOLUS", "EXTERNAL", "ISOCENTER", "REGISTRATION", "CONTRAST_AGENT",
                "CAVITY", "BRACHY_CHANNEL", "BRACHY_ACCESSORY", "SUPPORT", "FIXATION", "DOSE_REGION", "DOSE_MEASUREMENT", "BRACHY_SRC_APP", "TREATED_VOLUME", "IRRAD_VOLUME"};
            Binding interp_binding = new Binding("ROI_Interpreted_type");
            interp_binding.Source = roi;

            ComboBox roi_interp_combobox = new ComboBox();
            roi_interp_combobox.SetBinding(ComboBox.SelectedItemProperty, interp_binding);
            roi_interp_combobox.ItemsSource = interpreters;
            if (interpreters.Contains(roi.ROI_Interpreted_type))
            {
                roi_interp_combobox.SelectedItem = roi.ROI_Interpreted_type;
            }
            roi_interp_combobox.Width = 150;
            Children.Add(roi_interp_combobox);
            color_button = new Button();
            Binding color_binding = new Binding("ROI_Brush");
            color_binding.Source = roi;
            color_button.SetBinding(Button.BackgroundProperty, color_binding);
            color_button.Width = 100;
            color_button.Click += color_button_Click;
            Children.Add(color_button);
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
            roi_list.Remove(roi);
            Children.Clear();
        }
        private void color_button_Click(object sender, System.EventArgs e)
        {
            System.Windows.Forms.ColorDialog MyDialog = new System.Windows.Forms.ColorDialog();
            if (MyDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var new_color = MyDialog.Color;
                roi.update_color(new_color.R, new_color.G, new_color.B);
            }
        }
        private void ROINameChanged(object sender, TextChangedEventArgs e)
        {
            roi.name = roi_name_textbox.Text;
        }
    }
}
