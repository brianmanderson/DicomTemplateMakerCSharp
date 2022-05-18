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
        public AddROIRow(ROIClass roi)
        {
            Orientation = Orientation.Horizontal;
            Label roi_name_label = new Label();
            roi_name_label.Width = 200;
            roi_name_label.Content = roi.name;
            Children.Add(roi_name_label);
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
            roi_interp_combobox.Width = 200;
            Children.Add(roi_interp_combobox);
            color_button = new Button();
            Brush brush = new SolidColorBrush(Color.FromRgb(roi.R, roi.B, roi.G));
            color_button.Background = brush;
            color_button.Width = 200;
            color_button.Click += color_button_Click;
            Children.Add(color_button);
        }
        private void color_button_Click(object sender, System.EventArgs e)
        {
            System.Windows.Forms.ColorDialog MyDialog = new System.Windows.Forms.ColorDialog();
            if (MyDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var new_color = MyDialog.Color;
                Brush brush = new SolidColorBrush(Color.FromRgb(new_color.R, new_color.G, new_color.B));
                color_button.Background = brush;
            }
        }
    }
}
