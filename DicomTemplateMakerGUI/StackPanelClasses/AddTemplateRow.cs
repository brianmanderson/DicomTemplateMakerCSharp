using System.IO;
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
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    public class AddTemplateRow : StackPanel
    {
        private Label rois_present_label;
        public string template_name;
        public TemplateMaker templateMaker;
        private CheckBox selectCheckBox;
        public CheckBox SelectCheckBox
        {
            get { return selectCheckBox; }
            set
            {
                selectCheckBox = value;
                OnPropertyChanged("SelectCheckBox");
            }
        }
        private Button edit_rois_button;
        public ObservableCollection<ReadAirTable> AirTables;
        Brush lightred = new SolidColorBrush(Color.FromRgb(229, 51, 51));
        Brush lightgray = new SolidColorBrush(Color.FromRgb(221, 221, 221));
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }
        public AddTemplateRow(TemplateMaker tm, ObservableCollection<ReadAirTable> airTables)
        {
            this.Orientation = Orientation.Horizontal;
            StackPanel left_panel = new StackPanel();
            left_panel.Orientation = Orientation.Vertical;
            template_name = tm.TemplateName;
            AirTables = airTables;
            templateMaker = tm;
            Label template_label = new Label();
            template_label.Width = 250;
            Binding template_name_binding = new Binding("TemplateName");
            template_name_binding.Source = templateMaker;
            template_label.SetBinding(Label.ContentProperty, template_name_binding);
            left_panel.Children.Add(template_label);

            rois_present_label = new Label();
            rois_present_label.Content = $"{templateMaker.ROIs.Count} ROIs present in template";
            left_panel.Children.Add(rois_present_label);


            Label padding_label = new Label();
            padding_label.Width = 100;

            selectCheckBox = new CheckBox();
            selectCheckBox.Content = "Select?";
            padding_label = new Label();
            padding_label.Width = 100;

            left_panel.Children.Add(selectCheckBox);

            Children.Add(left_panel);

            StackPanel right_panel = new StackPanel();
            right_panel.Orientation = Orientation.Horizontal;

            edit_rois_button = new Button();
            edit_rois_button.Width = 250;
            CheckPaths();
            edit_rois_button.Content = "Edit ROIs and monitored DICOM paths";
            edit_rois_button.Click += EditROIButton_Click;
            right_panel.Children.Add(edit_rois_button);
            Children.Add(right_panel);

        }
        public void CheckPaths()
        {
            if (templateMaker.Paths.Count == 0)
            {
                edit_rois_button.Background = lightred;
            }
            else
            {
                edit_rois_button.Background = lightgray;
            }
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
