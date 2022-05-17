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
    class AddTemplateRow : StackPanel
    {
        public AddTemplateRow(TemplateEvaluator template_evaluator)
        {
            Label template_label = new Label();
            template_label.Content = template_evaluator.template_name;
            Children.Add(template_label);

            Label rois_present_label = new Label();
            rois_present_label.Content = $"{template_evaluator.ROIs.Count} ROIs present in template";
            Children.Add(rois_present_label);

            Button edit_rois_button = new Button();
            edit_rois_button.Content = "Edit ROIs";
            Children.Add(edit_rois_button);

            Label folder_location_label = new Label();
            folder_location_label.Content = $"{template_evaluator.path}";
            Children.Add(folder_location_label);
        }
    }
}
