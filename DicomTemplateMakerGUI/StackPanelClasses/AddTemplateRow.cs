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
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    public class AddTemplateRow : StackPanel
    {
        private Label rois_present_label;
        public TemplateMaker templateMaker;
        private Button DeleteButton;
        public CheckBox DeleteCheckBox;
        private Button edit_rois_button;
        public List<ReadAirTable> AirTables;
        Brush lightred = new SolidColorBrush(Color.FromRgb(229, 51, 51));
        Brush lightgray = new SolidColorBrush(Color.FromRgb(221, 221, 221));
        public AddTemplateRow(TemplateMaker tm, List<ReadAirTable> airTables)
        {
            AirTables = airTables;
            templateMaker = tm;
            Label template_label = new Label();
            Binding template_name_binding = new Binding("TemplateName");
            template_name_binding.Source = templateMaker;
            template_label.SetBinding(Label.ContentProperty, template_name_binding);
            Children.Add(template_label);

            rois_present_label = new Label();
            rois_present_label.Content = $"{templateMaker.ROIs.Count} ROIs present in template";
            Children.Add(rois_present_label);

            edit_rois_button = new Button();
            if (templateMaker.Paths.Count == 0)
            {
                edit_rois_button.Background = lightred;
            }
            else
            {
                edit_rois_button.Background = lightgray;
            }
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
            MakeTemplateWindow template_window = new MakeTemplateWindow(templateMaker.path, templateMaker, AirTables);
            template_window.ShowDialog();
            if (templateMaker.Paths.Count != 0)
            {
                edit_rois_button.Background = lightgray;
            }
            rois_present_label.Content = $"{templateMaker.ROIs.Count} ROIs present in template";
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
        public void Delete()
        {
            templateMaker.define_output(templateMaker.path);
            templateMaker.define_path(templateMaker.path);
            templateMaker.clear_folder();
            foreach (string path in Directory.GetFiles(templateMaker.path))
            {
                File.Delete(path);
            }
            if (Directory.Exists(Path.Combine(templateMaker.path, "ROIs")))
            {
                Directory.Delete(Path.Combine(templateMaker.path, "ROIs"));
            }
            Directory.Delete(templateMaker.path);
            Children.Clear();
        }
        private void DeleteButton_Click(object sender, System.EventArgs e)
        {
            Delete();
        }
    }
}
