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
using FellowOakDicom;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    class AddROIRow : StackPanel
    {
        Button color_button;
        private ROIClass roi;
        private int index;
        private TextBox roi_name_textbox;
        private List<ROIClass> roi_list;
        private CheckBox DeleteCheckBox;
        private Button DeleteButton;
        public AddROIRow(List<ROIClass> roi_list, int index)
        {
            this.roi = roi_list[index];
            this.roi_list = roi_list;
            this.index = index;
            Orientation = Orientation.Horizontal;
            roi_name_textbox = new TextBox();
            roi_name_textbox.Text = roi.name;
            roi_name_textbox.Width = 200;
            Children.Add(roi_name_textbox);
            List<string> interpreters = new List<string> { "ORGAN", "PTV", "CTV", "GTV", "AVOIDANCE", "CONTROL", "BOLUS", "EXTERNAL", "ISOCENTER", "REGISTRATION", "CONTRAST_AGENT",
                "CAVITY", "BRACHY_CHANNEL", "BRACHY_ACCESSORY", "SUPPORT", "FIXATION", "DOSE_REGION", "DOSE_MEASUREMENT", "BRACHY_SRC_APP", "TREATED_VOLUME", "IRRAD_VOLUME", "", "PLEASE SELECT"};
            ComboBox roi_interp_combobox = new ComboBox();
            roi_interp_combobox.ItemsSource = interpreters;
            if (interpreters.Contains(roi.roi_interpreted_type))
            {
                roi_interp_combobox.SelectedItem = roi.roi_interpreted_type;
            }
            else
            {
                roi_interp_combobox.SelectedItem = "PLEASE SELECT";
            }
            roi_interp_combobox.Width = 150;
            Children.Add(roi_interp_combobox);
            color_button = new Button();
            Brush brush = new SolidColorBrush(Color.FromRgb(roi.RGB[0], roi.RGB[1], roi.RGB[2]));
            color_button.Background = brush;
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
            roi_list.RemoveAt(index);
            Children.Clear();
        }
        private void color_button_Click(object sender, System.EventArgs e)
        {
            System.Windows.Forms.ColorDialog MyDialog = new System.Windows.Forms.ColorDialog();
            if (MyDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var new_color = MyDialog.Color;
                Brush brush = new SolidColorBrush(Color.FromRgb(new_color.R, new_color.G, new_color.B));
                roi.R = new_color.R;
                roi.G = new_color.G;
                roi.B = new_color.B;
                color_button.Background = brush;
            }
        }
        private void ROINameChanged(object sender, TextChangedEventArgs e)
        {
            roi.name = roi_name_textbox.Text;
        }
    }
}
