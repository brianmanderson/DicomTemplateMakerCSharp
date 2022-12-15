﻿using System;
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
using System.Windows.Navigation;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DicomTemplateMakerGUI.Windows;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.StackPanelClasses;
using Microsoft.WindowsAPICodePack.Dialogs;
using DicomTemplateMakerGUI.DicomTemplateServices;
using System.Threading;
using System.Collections.ObjectModel;

namespace DicomTemplateMakerGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public class DicomRunner
    {
        private Task t;
        public string folder_location;
        bool running;
        public DicomRunner(string folder_location)
        {
            this.folder_location = Path.GetFullPath(folder_location);
            t = new Task(() => RunTemplateRunner());
            running = false;
        }
        private async void RunTemplateRunner()
        {
            DicomTemplateRunner runner = new DicomTemplateRunner(Path.GetFullPath(folder_location));
            runner.run();
        }
        public void run()
        {
            if (!running)
            {
                t = new Task(() => RunTemplateRunner());
                t.Start();
                //t.Dispose();
            }
            running = true;
        }
    }
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        string folder_location, onto_path;
        Brush lightgreen = new SolidColorBrush(Color.FromRgb(144, 238, 144));
        Brush lightgray = new SolidColorBrush(Color.FromRgb(221, 221, 221));
        private ObservableCollection<ReadAirTable> airtables = new ObservableCollection<ReadAirTable>();
        public ObservableCollection<ReadAirTable> AirTables
        {
            get { return airtables; }
            set
            {
                airtables = value;
                OnPropertyChanged("AirTables");
            }
        }
        bool running;
        DicomRunner runner;
        List<AddTemplateRow> template_rows;
        List<AddTemplateRow> visible_template_rows;

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
        public MainWindow()
        {
            InitializeComponent();
            load_airtables();
            folder_location = @".";
            int month = DateTime.Now.Month;
            int year = DateTime.Now.Year;
            if (!File.Exists(Path.Combine(folder_location, "Running.txt")))
            {
                if (year > 2022)
                {
                    //Window warning = new OutDatedWindow();
                    //warning.ShowDialog();
                    //Close();
                }
                else if (month > 8)
                {
                    //Window warning = new OutDatedWindow();
                    //warning.ShowDialog();
                }
            }
            running = false;
            onto_path = Path.Combine(folder_location, "Ontologies");
            if (!Directory.Exists(onto_path))
            {
                Directory.CreateDirectory(onto_path);
            }
            bool build = false;
            if (!File.Exists(Path.Combine(folder_location, "Built_from_RTs.txt")) & (build))
            {
                string[] rt_files = Directory.GetFiles(folder_location, "TG263*.dcm");
                foreach (string rt_file in rt_files)
                {
                    TemplateMaker evaluator = new TemplateMaker();
                    evaluator.set_onto_path(Path.Combine(folder_location, "Ontologies"));
                    evaluator = update_ontology_reader(evaluator);
                    evaluator.interpret_RT(rt_file);
                    string folder_path = Path.GetFileName(rt_file);
                    folder_path = folder_path.Substring(0, folder_path.Length - 4); // Chop off .dcm
                    evaluator.define_output(Path.Combine(folder_location, folder_path));
                    evaluator.make_template();
                }
                File.CreateText(Path.Combine(folder_location, "Built_from_RTs.txt"));
            }
            TemplateBaseLabel.Content = Path.GetFullPath(folder_location);
            AddTemplateButton.IsEnabled = true;
            Rebuild_From_Folders();
            running = false;
            runner = new DicomRunner(Path.GetFullPath(folder_location));
        }
        public void load_airtables()
        {
            string airtable_directory = Path.Combine(@".", "AirTables");
            if (Directory.Exists(airtable_directory))
            {
                foreach (string file in Directory.EnumerateFiles(airtable_directory, "*.txt"))
                {
                    ReadAirTable airtable = new ReadAirTable(file);
                    airtable.read_records();
                    AirTables.Add(airtable);
                }
            }
            ReadingAirTable();
        }
        public async void ReadingAirTable()
        {
            if (AirTables.Count > 0)
            {
                ReadAirTableButton.Content = "Load Online Templates";
                ReadAirTableButton.Background = lightgreen;
            }
            else
            {
                ReadAirTableButton.Content = "Online Templates Found...";
                ReadAirTableButton.Background = lightgray;
                ReadAirTableButton.IsEnabled = false;
            }
        }
        public TemplateMaker update_ontology_reader(TemplateMaker evaluator)
        {
            string[] roi_files = Directory.GetFiles(onto_path, "*.txt");
            foreach (string ontology_file in roi_files)
            {
                string onto_name = Path.GetFileName(ontology_file).Replace(".txt", "");
                string[] instructions = File.ReadAllLines(ontology_file);
                string code_value = instructions[0];
                string coding_scheme = instructions[1];
                string context_group_version = instructions[2];
                string mapping_resource = instructions[3];
                string context_identifier = instructions[4];
                string mapping_resource_name = instructions[5];
                string mapping_resource_uid = instructions[6];
                string context_uid = instructions[7];
                OntologyCodeClass onto = new OntologyCodeClass(onto_name, code_value, coding_scheme, context_group_version, mapping_resource,
                    context_identifier, mapping_resource_name, mapping_resource_uid, context_uid);
                evaluator.Ontologies.Add(onto);
            }
            evaluator.Ontologies.Sort((p, q) => p.CodeMeaning.CompareTo(q.CodeMeaning));
            return evaluator;
        }
        public void Rebuild_From_Folders()
        {
            TemplateStackPanel.Children.Clear();
            string[] directories = Directory.GetDirectories(folder_location);
            AddTemplateButton.Background = lightgreen;
            RunDICOMServerButton.IsEnabled = false;
            MakeRTFolderButton.IsEnabled = false;
            template_rows = new List<AddTemplateRow>();
            visible_template_rows = new List<AddTemplateRow>();
            foreach (string directory in directories)
            {
                TemplateMaker evaluator = new TemplateMaker();
                evaluator.set_onto_path(Path.Combine(folder_location, "Ontologies"));
                evaluator = update_ontology_reader(evaluator);
                evaluator.define_path(directory);
                evaluator.define_output(directory);
                evaluator.categorize_folder();
                if (evaluator.is_template)
                {
                    AddTemplateButton.Background = lightgray;
                    if (!running)
                    {
                        RunDICOMServerButton.IsEnabled = true;
                    }
                    MakeRTFolderButton.IsEnabled = true;
                    AddTemplateRow new_row = new AddTemplateRow(evaluator, AirTables);
                    Border myborder = new Border();
                    myborder.Background = Brushes.Black;
                    myborder.BorderThickness = new Thickness(5);
                    TemplateStackPanel.Children.Add(myborder);
                    TemplateStackPanel.Children.Add(new_row);
                    template_rows.Add(new_row);
                    visible_template_rows.Add(new_row);
                }
            }
            if (template_rows.Count == 0)
            {
                BuildDefault_Button.Background = lightgreen;
            }
            else
            {
                BuildDefault_Button.Background = lightgray;
            }
        }
        private void Click_Build(object sender, RoutedEventArgs e)
        {
            TemplateMaker template_maker = new TemplateMaker();
            template_maker.set_onto_path(Path.Combine(folder_location, "Ontologies"));
            template_maker = update_ontology_reader(template_maker);
            MakeTemplateWindow template_window = new MakeTemplateWindow(folder_location, template_maker, AirTables);
            template_window.ShowDialog();
            Rebuild_From_Folders();
        }

        private void ChangeTemplateClick(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = Path.GetFullPath(folder_location);
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                folder_location = dialog.FileName;
                TemplateBaseLabel.Content = folder_location;
                Rebuild_From_Folders();
            }
        }

        private void ClickRunDicomserver(object sender, RoutedEventArgs e)
        {
            runner.run();
            running = true;
            RunDICOMServerButton.IsEnabled = false;
            ChangeTemplateButton.IsEnabled = false;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow about_window = new AboutWindow();
            about_window.Show();
        }

        private void MakeDefault_Button(object sender, RoutedEventArgs e)
        {
            Build_Default_Template_Window window = new Build_Default_Template_Window(folder_location, onto_path);
            window.ShowDialog();
            Rebuild_From_Folders();
        }
        private void UpdateText()
        {
            TemplateStackPanel.Children.Clear();
            visible_template_rows = new List<AddTemplateRow>();
            foreach (AddTemplateRow temp_row in template_rows)
            {
                if ((bool)Selected_CheckBox.IsChecked)
                {
                    if (!(bool)temp_row.SelectCheckBox.IsChecked)
                    {
                        continue;
                    }

                }
                if (temp_row.templateMaker.TemplateName.ToLower().Contains(SearchBox_TextBox.Text.ToLower()))
                {
                    visible_template_rows.Add(temp_row);
                    Border myborder = new Border();
                    myborder.Background = Brushes.Black;
                    myborder.BorderThickness = new Thickness(5);
                    TemplateStackPanel.Children.Add(myborder);
                    TemplateStackPanel.Children.Add(temp_row);
                }
            }
        }
        private void SearchTextUpdate(object sender, TextChangedEventArgs e)
        {
            UpdateText();
        }

        private void Read_Airtable(object sender, RoutedEventArgs e)
        {
            AirTableWindow airtable_window = new AirTableWindow(AirTables, folder_location, onto_path);
            airtable_window.ShowDialog();
            Rebuild_From_Folders();
        }

        private void CreateFolderRT_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog("*.dcm");
            dialog.InitialDirectory = ".";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                string output_directory = Path.Combine(dialog.FileName, "Template_Output");
                if (!Directory.Exists(output_directory))
                {
                    Directory.CreateDirectory(output_directory);
                }
                foreach (string dicom_file in Directory.GetFiles(Path.Combine(@".", "SmallCT")))
                {
                    string out_file = Path.Combine(output_directory, Path.GetFileName(dicom_file));
                    if (!File.Exists(out_file))
                    {
                        File.Copy(dicom_file, out_file);
                    }
                }
                foreach (AddTemplateRow template_row in template_rows)
                {
                    if (!template_row.templateMaker.Paths.Contains(output_directory))
                    {
                        template_row.templateMaker.Paths.Add(output_directory);
                        template_row.templateMaker.make_template();
                    }
                }
                Rebuild_From_Folders();
                ClickRunDicomserver(sender, e);

            }
        }

        private void SelectAll_Button_Click(object sender, RoutedEventArgs e)
        {
            foreach (AddTemplateRow row in visible_template_rows)
            {
                row.SelectCheckBox.IsChecked = true;
            }
        }
        private void UnselectAll_Button_Click(object sender, RoutedEventArgs e)
        {
            foreach (AddTemplateRow row in visible_template_rows)
            {
                row.SelectCheckBox.IsChecked = false;
            }
        }
        private void Deleted_Selected_Button_Click(object sender, RoutedEventArgs e)
        {
            foreach (AddTemplateRow row in template_rows)
            {
                if ((bool)row.SelectCheckBox.IsChecked)
                {
                    row.Delete();
                }
            }
            Rebuild_From_Folders();
            UpdateText();
        }

        private void DeleteROIs_Button_Click(object sender, RoutedEventArgs e)
        {
            Deleted_Selected_Button.Content = "Deleting ROIs...";
            Deleted_Selected_Button.IsEnabled = false;
            DicomTemplateRunner runner = new DicomTemplateRunner(Path.GetFullPath(folder_location));
            runner.delete_rts();
            Deleted_Selected_Button.Content = "Delete previously generated RTs";
            Deleted_Selected_Button.IsEnabled = true;
        }

        private void Copy_Selected_Button_Click(object sender, RoutedEventArgs e)
        {
            foreach (AddTemplateRow row in template_rows)
            {
                if ((bool)row.SelectCheckBox.IsChecked)
                {
                    int copy_number = 0;
                    string new_template_name = $"{row.templateMaker.TemplateName}_Copy{copy_number}";
                    while (Directory.Exists(Path.Combine(folder_location, new_template_name)))
                    {
                        copy_number++;
                        new_template_name = $"{row.templateMaker.TemplateName}_Copy{copy_number}";
                    }
                    string new_template_path = Path.Combine(folder_location, new_template_name);
                    try
                    {
                        Directory.CreateDirectory(new_template_path);
                    }
                    catch
                    {
                        continue;
                    }
                    foreach (string dirPath in Directory.GetDirectories(row.templateMaker.path, "*", SearchOption.AllDirectories))
                    {
                        Directory.CreateDirectory(dirPath.Replace(row.templateMaker.path, new_template_path));
                    }

                    //Copy all the files & Replaces any files with the same name
                    foreach (string newPath in Directory.GetFiles(row.templateMaker.path, "*.*", SearchOption.AllDirectories))
                    {
                        File.Copy(newPath, newPath.Replace(row.templateMaker.path, new_template_path), true);
                    }
                }
            }
            Rebuild_From_Folders();
            UpdateText();
        }

        private void CheckBox_DataContextChanged(object sender, RoutedEventArgs e)
        {
            Copy_Selected_Button.IsEnabled = false;
            Deleted_Selected_Button.IsEnabled = false;
            if ((bool)Copy_CheckBox.IsChecked)
            {
                Copy_Selected_Button.IsEnabled = true;
            }
            if ((bool)Delete_Checkbox.IsChecked)
            {
                Deleted_Selected_Button.IsEnabled = true;
            }
        }

        private void Selected_DataContextChanged(object sender, RoutedEventArgs e)
        {
            UpdateText();
        }

        private void Add_Ontology_Button(object sender, RoutedEventArgs e)
        {
            TemplateMaker template_maker = new TemplateMaker();
            EditOntologyWindow ontology_window = new EditOntologyWindow(folder_location, template_maker);
            ontology_window.ShowDialog();
            Rebuild_From_Folders();
        }
    }
}
