using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.Windows;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    /// <summary>
    /// Interaction logic for TemplateWindow.xaml
    /// </summary>
    public partial class TemplateWindow : Window
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
        public TemplateWindow(TemplateMaker tm, ObservableCollection<ReadAirTable> airTables)
        {
            InitializeComponent();
            template_name = tm.TemplateName;
            AirTables = airTables;
            templateMaker = tm;
            Binding template_name_binding = new Binding("TemplateName");
            template_name_binding.Source = templateMaker;
            TemplateNameLabel.SetBinding(Label.ContentProperty, template_name_binding);
            RoisPresentLabel.Content = $"{templateMaker.ROIs.Count} ROIs present in template";
            CheckPaths();
            EditROIsButton.Click += EditROIsButton_Click;
        }
        public StackPanel return_panel()
        {
            return TemplateStackPanelRow;
        }

        public void CheckPaths()
        {
            if (templateMaker.Paths.Count == 0)
            {
                EditROIsButton.Background = lightred;
            }
            else
            {
                EditROIsButton.Background = lightgray;
            }
        }
        private void EditROIsButton_Click(object sender, System.EventArgs e)
        {
            MakeTemplateWindow template_window = new MakeTemplateWindow(templateMaker.path, templateMaker, AirTables);
            template_window.ShowDialog();
            if (templateMaker.Paths.Count != 0)
            {
                EditROIsButton.Background = lightgray;
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
            TemplateStackPanelRow.Children.Clear();
        }
        private void DeleteButton_Click(object sender, System.EventArgs e)
        {
            Delete();
        }
    }
}
