using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.Windows;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    /// <summary>
    /// Interaction logic for TemplateWindow.xaml
    /// </summary>
    public partial class TemplateWindow : Window
    {
        public string? template_name;
        public TemplateMaker templateMaker;
        // Nothing in this window assigns it; the check box in the XAML has no name.
        private CheckBox? selectCheckBox;
        public CheckBox? SelectCheckBox
        {
            get { return selectCheckBox; }
            set
            {
                selectCheckBox = value;
                OnPropertyChanged("SelectCheckBox");
            }
        }
        public ObservableCollection<TemplateSourceItem> AirTables;
        Brush lightred = new SolidColorBrush(Color.FromRgb(229, 51, 51));
        Brush lightgray = new SolidColorBrush(Color.FromRgb(221, 221, 221));
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChangedEventHandler? handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }
        public TemplateWindow(TemplateMaker tm, ObservableCollection<TemplateSourceItem> airTables)
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

        /// <summary>
        /// The template's folder. When define_path has not been called on the TemplateMaker, this fails where
        /// the null path used to reach the file APIs.
        /// </summary>
        private string TemplatePath
        {
            get { return templateMaker.path ?? throw new InvalidOperationException("define_path has not been called on this template's TemplateMaker."); }
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
            MakeTemplateWindow template_window = new MakeTemplateWindow(TemplatePath, templateMaker, AirTables);
            template_window.ShowDialog();
            if (templateMaker.Paths.Count != 0)
            {
                EditROIsButton.Background = lightgray;
            }
            // This line used a label field that nothing assigned, so it always threw NullReferenceException here.
            // It now updates the label the constructor fills. Nothing creates a TemplateWindow (plan item N26: delete it).
            RoisPresentLabel.Content = $"{templateMaker.ROIs.Count} ROIs present in template";
        }
        public void Delete()
        {
            string template_path = TemplatePath;
            templateMaker.define_output(template_path);
            templateMaker.define_path(template_path);
            templateMaker.clear_folder();
            foreach (string path in Directory.GetFiles(template_path))
            {
                File.Delete(path);
            }
            if (Directory.Exists(Path.Combine(template_path, "ROIs")))
            {
                Directory.Delete(Path.Combine(template_path, "ROIs"));
            }
            Directory.Delete(template_path);
            TemplateStackPanelRow.Children.Clear();
        }
        private void DeleteButton_Click(object sender, System.EventArgs e)
        {
            Delete();
        }
    }
}
