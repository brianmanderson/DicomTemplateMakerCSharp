using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.Shell;
using DicomTemplateMakerGUI.Windows;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    /// <summary>One template in the main window's list. Deleting templates is the main window's job (Recycle Bin, with confirmation).</summary>
    public class AddTemplateRow : StackPanel
    {
        private Label rois_present_label;
        public string? template_name;
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
        public ObservableCollection<TemplateSourceItem> AirTables;
        Brush lightred = new SolidColorBrush(Color.FromRgb(229, 51, 51));
        Brush lightgray = new SolidColorBrush(Color.FromRgb(221, 221, 221));
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public AddTemplateRow(TemplateMaker tm, ObservableCollection<TemplateSourceItem> airTables)
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
            left_panel.Children.Add(rois_present_label);

            selectCheckBox = new CheckBox();
            selectCheckBox.Content = "Select";
            left_panel.Children.Add(selectCheckBox);

            Children.Add(left_panel);

            StackPanel right_panel = new StackPanel();
            right_panel.Orientation = Orientation.Horizontal;

            edit_rois_button = new Button();
            edit_rois_button.Width = 250;
            edit_rois_button.Content = "Edit ROIs and monitored DICOM paths";
            edit_rois_button.Click += EditROIButton_Click;
            right_panel.Children.Add(edit_rois_button);
            Children.Add(right_panel);

            ToolTipService.SetShowDuration(this, 60000);
            Refresh();
        }
        /// <summary>
        /// The template's folder. Rows are only built for a TemplateMaker whose define_path has been called;
        /// if that ever does not hold, this fails where the null path used to reach the file APIs.
        /// </summary>
        internal string TemplatePath
        {
            get { return templateMaker.path ?? throw new InvalidOperationException("define_path has not been called on this template's TemplateMaker."); }
        }
        /// <summary>Colours the edit button red while the template has no monitored folders.</summary>
        public void CheckPaths()
        {
            if (templateMaker.Paths.Count == 0)
            {
                edit_rois_button.Background = lightred;
                edit_rois_button.ToolTip = "No monitored folders: no RTs are written for this template until one is added.";
            }
            else
            {
                edit_rois_button.Background = lightgray;
                edit_rois_button.ToolTip = null;
            }
        }
        /// <summary>Updates the ROI count, the edit button and the tooltip from the template.</summary>
        private void Refresh()
        {
            rois_present_label.Content = $"{templateMaker.ROIs.Count} ROIs present in template";
            CheckPaths();
            ToolTip = TemplateRowText.Tooltip(templateMaker);
        }
        private void EditROIButton_Click(object sender, RoutedEventArgs e)
        {
            MakeTemplateWindow template_window = new MakeTemplateWindow(TemplatePath, templateMaker, AirTables);
            template_window.Owner = Window.GetWindow(this);
            template_window.ShowDialog();
            Refresh();
        }
    }
}
