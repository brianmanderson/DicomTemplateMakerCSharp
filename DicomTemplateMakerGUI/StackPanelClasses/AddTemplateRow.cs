using System.IO;
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
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.Windows;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    class AddTemplateRow : StackPanel
    {
        private Label rois_present_label;
        private TemplateMaker template_maker;
        private Button DeleteButton;
        private CheckBox DeleteCheckBox;
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
            edit_rois_button.Content = "Edit ROIs and monitored DICOM paths";
            edit_rois_button.Click += EditROIButton_Click;
            Children.Add(edit_rois_button);

            StackPanel delete_panel = new StackPanel();
            delete_panel.Orientation = Orientation.Horizontal;
            Label delete_label = new Label();
            delete_label.Content = "Delete?";
            delete_label.Width = 100;
            delete_panel.Children.Add(delete_label);

            DeleteCheckBox = new CheckBox();
            DeleteCheckBox.Checked += CheckBox_DataContextChanged;
            DeleteCheckBox.Unchecked += CheckBox_DataContextChanged;
            delete_panel.Children.Add(DeleteCheckBox);

            Label padding_label = new Label();
            delete_label.Width = 100;
            delete_panel.Children.Add(padding_label);

            DeleteButton = new Button();
            DeleteButton.IsEnabled = false;
            DeleteButton.Width = 100;
            DeleteButton.Click += DeleteButton_Click;
            DeleteButton.Content = "Delete";
            delete_panel.Children.Add(DeleteButton);
            Children.Add(delete_panel);
        }
        private void EditROIButton_Click(object sender, System.EventArgs e)
        {
            MakeTemplateWindow template_window = new MakeTemplateWindow(template_maker.path, template_maker);
            template_window.ShowDialog();
            rois_present_label.Content = $"{template_maker.ROIs.Count} ROIs present in template";
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
            template_maker.define_output(template_maker.path);
            template_maker.clear_folder();
            foreach (string path in Directory.GetFiles(template_maker.path))
            {
                File.Delete(path);
            }
            if (Directory.Exists(Path.Combine(template_maker.path, "ROIs")))
            {
                Directory.Delete(Path.Combine(template_maker.path, "ROIs"));
            }
            Directory.Delete(template_maker.path);
            Children.Clear();
        }
    }
}
