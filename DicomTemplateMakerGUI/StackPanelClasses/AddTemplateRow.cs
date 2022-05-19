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
using DicomTemplateMakerGUI.Windows;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    class AddTemplateRow : StackPanel
    {
        private string folder_location;
        private Label rois_present_label;
        private TemplateMaker template_maker;
        public AddTemplateRow(TemplateMaker template_maker)
        {
            this.template_maker = template_maker;
            Label template_label = new Label();
            template_label.Content = template_maker.template_name;
            Children.Add(template_label);

            rois_present_label = new Label();
            rois_present_label.Content = $"{template_maker.ROIs.Count} ROIs present in template";
            Children.Add(rois_present_label);

            Button edit_rois_button = new Button();
            edit_rois_button.Content = "Edit ROIs";
            edit_rois_button.Click += EditROIButton_Click;
            Children.Add(edit_rois_button);
        }
        private void EditROIButton_Click(object sender, System.EventArgs e)
        {
            MakeTemplateWindow template_window = new MakeTemplateWindow(template_maker.path, template_maker);
            template_window.ShowDialog();
            rois_present_label.Content = $"{template_maker.ROIs.Count} ROIs present in template";
        }
    }
}
