using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ROIOntologyClass;
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
        private ObservableCollection<ReadAirTable> writeable_airtables = new ObservableCollection<ReadAirTable>();
        public ObservableCollection<ReadAirTable> AirTables
        {
            get { return airtables; }
            set
            {
                airtables = value;
                OnPropertyChanged("AirTables");
            }
        }
        public ObservableCollection<ReadAirTable> WriteableAirTables
        {
            get { return writeable_airtables; }
            set
            {
                writeable_airtables = value;
                OnPropertyChanged("WriteableAirTables");
            }
        }
        bool running;
        DicomRunner runner;
        List<AddTemplateRow> template_rows;
        List<AddTemplateRow> visible_template_rows;
        List<AddTemplateRow> copy_template_rows;

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
        public void load_writeable_airtables()
        {
            WriteableAirTables = new ObservableCollection<ReadAirTable>();
            foreach (ReadAirTable at in AirTables)
            {
                if (at.AirTableName != "TG263_AirTable")
                {
                    WriteableAirTables.Add(at);
                }
            }
            AirTableComboBox.ItemsSource = WriteableAirTables;
            AirTableComboBox.DisplayMemberPath = "AirTableName";
        }
        public MainWindow()
        {
            InitializeComponent();
            load_airtables();
            load_writeable_airtables();
            AirTableComboBox.ItemsSource = WriteableAirTables;
            AirTableComboBox.DisplayMemberPath = "AirTableName";
            if (AirTables.Count > 0)
            {
                AirTableComboBox.SelectedIndex = 0;
                check_airtables((ReadAirTable)AirTableComboBox.SelectedItem);
            }
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
                evaluator.Ontologies.Add(new OntologyCodeClass(ontology_file));
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
            MakeVarianXmlFolderButton.IsEnabled = false;
            template_rows = new List<AddTemplateRow>();
            visible_template_rows = new List<AddTemplateRow>();
            foreach (string directory in directories)
            {
                TemplateMaker evaluator = new TemplateMaker();
                evaluator.set_onto_path(Path.Combine(folder_location, "Ontologies"));
                evaluator.define_path(directory);
                evaluator.define_output(directory);
                evaluator.categorize_folder();
                evaluator = update_ontology_reader(evaluator);
                if (evaluator.is_template)
                {
                    AddTemplateButton.Background = lightgray;
                    if (!running)
                    {
                        RunDICOMServerButton.IsEnabled = true;
                    }
                    MakeRTFolderButton.IsEnabled = true;
                    MakeVarianXmlFolderButton.IsEnabled = true;
                    AddTemplateRow new_row = new AddTemplateRow(evaluator, AirTables);
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
            DisplayRows();
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
        private void DisplayRows()
        {
            visible_template_rows = visible_template_rows.OrderBy(x => x.template_name).ToList();
            TemplateStackPanel.Children.Clear();
            foreach (AddTemplateRow temp_row in visible_template_rows)
            {
                Border myborder = new Border();
                myborder.Background = Brushes.Black;
                myborder.BorderThickness = new Thickness(5);
                TemplateStackPanel.Children.Add(myborder);
                TemplateStackPanel.Children.Add(temp_row);
            }
        }
        private void UpdateText()
        {
            visible_template_rows = new List<AddTemplateRow>();
            foreach (AddTemplateRow temp_row in template_rows)
            {
                if ((bool)Selected_CheckBox.IsChecked)
                {
                    if (!(bool)temp_row.SelectCheckBox.IsChecked)
                    {
                        continue;
                    }
                    else
                    {
                        visible_template_rows.Add(temp_row);
                    }
                }
                else if(temp_row.templateMaker.TemplateName.ToLower().Contains(SearchBox_TextBox.Text.ToLower()))
                {
                    visible_template_rows.Add(temp_row);
                }
            }
            DisplayRows();
        }
        private void SearchTextUpdate(object sender, TextChangedEventArgs e)
        {
            UpdateText();
        }

        private void Read_Airtable(object sender, RoutedEventArgs e)
        {
            AirTableWindow airtable_window = new AirTableWindow(AirTables, folder_location, onto_path);
            airtable_window.ShowDialog();
            load_writeable_airtables();
            Rebuild_From_Folders();
            LoadAirTables_Button.Content = "Loaded";
            LoadAirTables_Button.IsEnabled = false;
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
                bool any_select = false;
                foreach (AddTemplateRow template_row in template_rows)
                {
                    if ((bool)template_row.SelectCheckBox.IsChecked)
                    {
                        any_select = true;
                    }
                }
                if (!any_select) // If none of them are selected, default to selecting all of them
                {
                    foreach (AddTemplateRow template_row in template_rows)
                    {
                        template_row.SelectCheckBox.IsChecked = true;
                    }
                }
                Selected_CheckBox.IsChecked = true;
                foreach (AddTemplateRow template_row in template_rows)
                {
                    if (!(bool)template_row.SelectCheckBox.IsChecked)
                    {
                        continue;
                    }
                    if (!template_row.templateMaker.Paths.Contains(output_directory))
                    {
                        template_row.templateMaker.Paths.Add(output_directory);
                        template_row.templateMaker.make_template();
                        template_row.CheckPaths();
                    }
                }
                //Rebuild_From_Folders();
                ClickRunDicomserver(sender, e);
            }
        }
        private void SelectAll()
        {
            foreach (AddTemplateRow row in visible_template_rows)
            {
                row.SelectCheckBox.IsChecked = true;
            }
        }
        private void UnSelectAll()
        {
            foreach (AddTemplateRow row in visible_template_rows)
            {
                row.SelectCheckBox.IsChecked = false;
            }
        }
        private void SelectAll_Button_Click(object sender, RoutedEventArgs e)
        {
            SelectAll();
        }
        private void UnselectAll_Button_Click(object sender, RoutedEventArgs e)
        {
            UnSelectAll();
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
            Delete_Checkbox.IsChecked = false;
            Deleted_Selected_Button.IsEnabled = false;
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
            copy_template_rows = new List<AddTemplateRow>();
            foreach (AddTemplateRow row in template_rows)
            {
                if ((bool)row.SelectCheckBox.IsChecked)
                {
                    copy_template_rows.Add(row);
                }
            }
            foreach (AddTemplateRow row in copy_template_rows)
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
                TemplateMaker evaluator = new TemplateMaker();
                evaluator.set_onto_path(Path.Combine(folder_location, "Ontologies"));
                evaluator = update_ontology_reader(evaluator);
                evaluator.define_path(new_template_path);
                evaluator.define_output(new_template_path);
                evaluator.categorize_folder();
                AddTemplateRow new_row = new AddTemplateRow(evaluator, AirTables);
                new_row.SelectCheckBox.IsChecked = true;
                Border myborder = new Border();
                myborder.Background = Brushes.Black;
                myborder.BorderThickness = new Thickness(5);
                TemplateStackPanel.Children.Add(myborder);
                TemplateStackPanel.Children.Add(new_row);
                template_rows.Add(new_row);
                visible_template_rows.Add(new_row);
            }
            UpdateText();
            Copy_CheckBox.IsChecked = false;
            Copy_Selected_Button.IsEnabled = false;
        }

        private void CheckBox_DataContextChanged(object sender, RoutedEventArgs e)
        {
            Copy_Selected_Button.IsEnabled = false;
            Deleted_Selected_Button.IsEnabled = false;
            WriteToAirTable_Button.IsEnabled = false;
            if ((bool)Copy_CheckBox.IsChecked)
            {
                Copy_Selected_Button.IsEnabled = true;
            }
            if ((bool)Delete_Checkbox.IsChecked)
            {
                Deleted_Selected_Button.IsEnabled = true;
            }
        }
        private async void AirTableCheckBox_DataContextChanged(object sender, RoutedEventArgs e)
        {
            if (AirTableComboBox.SelectedIndex != -1 & (bool)AirTableCheckbox.IsChecked)
            {
                ReadAirTable table = (ReadAirTable)AirTableComboBox.SelectedItem;
                await table.finished_task;
                WriteToAirTable_Button.IsEnabled = true;
            }
            else
            {
                WriteToAirTable_Button.IsEnabled = false;
            }
        }
        private void Selected_DataContextChanged(object sender, RoutedEventArgs e)
        {
            UpdateText();
        }
        private async void writeAirTable(ReadAirTable table, TemplateMaker template_maker)
        {
            table.WriteToAirTable(template_maker.TemplateName, template_maker.ROIs);
            try
            {
                await table.finished_write;
                WriteToAirTable_Button.Content = "Wrote to Airtable!";
            }
            catch
            {
                WriteToAirTable_Button.Content = "Failed writing to airtable...";
            }
        }
        private async void WriteToAirTable_Click(object sender, RoutedEventArgs e)
        {
            ReadAirTable table = (ReadAirTable)AirTableComboBox.SelectedItem;
            WriteToAirTable_Button.IsEnabled = false;
            ProgressBar.Visibility = Visibility.Hidden;
            float i = 0;
            int total = 0;
            foreach (AddTemplateRow row in template_rows)
            {
                if ((bool)row.SelectCheckBox.IsChecked)
                {
                    total++;
                    WriteToAirTable_Button.Content = "Writing to Airtable...";
                }
            }
            AirTableComboBox.IsEnabled = false;
            AirTableCheckbox.IsEnabled = false;
            foreach (AddTemplateRow row in template_rows)
            {
                if ((bool)row.SelectCheckBox.IsChecked)
                {
                    ProgressBar.Visibility = Visibility.Visible;
                    i++;
                    ProgressBar.Value = i / total * 100;
                    WriteToAirTable_Button.Content = "Writing to Airtable...";
                    try
                    {
                        table.WriteToAirTable(row.templateMaker.TemplateName, row.templateMaker.ROIs);
                        await table.finished_write;
                        WriteToAirTable_Button.Content = "Wrote to Airtable!";
                    }
                    catch
                    {
                        WriteToAirTable_Button.Content = "Failed writing to airtable...";
                        break;
                    }
                }
            }

            AirTableComboBox.IsEnabled = true;
            WriteToAirTable_Button.IsEnabled = false;
            AirTableCheckbox.IsChecked = false;
            AirTableCheckbox.IsEnabled = true;
        }
        private async void check_airtables(ReadAirTable airtable)
        {
            WriteToAirTable_Button.IsEnabled = false;
            WriteToAirTable_Button.Content = "Must load airtables...";
            try
            {
                if (airtable is null)
                {
                    return;
                }
                await airtable.finished_task;
                LoadAirTables_Button.IsEnabled = false;
                if ((bool)AirTableCheckbox.IsChecked)
                {
                    WriteToAirTable_Button.IsEnabled = true;
                }
                WriteToAirTable_Button.Content = "Write to AirTable";
                LoadAirTables_Button.Content = "Loaded";
            }
            catch
            {
                WriteToAirTable_Button.Content = "Could not load...";
            }
        }
        private void AirTableSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ReadAirTable table = (ReadAirTable)AirTableComboBox.SelectedItem;
            check_airtables(table);
        }

        private void CreateVarianXml_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog("*.dcm");
            dialog.InitialDirectory = ".";
            string suspected_directory = @"\\ro-ariaimg-v\va_data$\ProgramData\Vision\Templates\structure";
            if (Directory.Exists(suspected_directory))
            {
                dialog.InitialDirectory = suspected_directory;
                dialog.DefaultDirectory = suspected_directory;
            }
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                string output_directory = dialog.FileName;
                if (!Directory.Exists(output_directory))
                {
                    Directory.CreateDirectory(output_directory);
                }
                bool any_select = false;
                foreach (AddTemplateRow template_row in template_rows)
                {
                    if ((bool)template_row.SelectCheckBox.IsChecked)
                    {
                        any_select = true;
                    }
                }
                if (!any_select) // If none of them are selected, default to selecting all of them
                {
                    foreach (AddTemplateRow template_row in template_rows)
                    {
                        template_row.SelectCheckBox.IsChecked = true;
                    }
                }
                Selected_CheckBox.IsChecked = true;
                foreach (AddTemplateRow template_row in template_rows)
                {
                    if (!(bool)template_row.SelectCheckBox.IsChecked)
                    {
                        continue;
                    }
                    VarianXmlWriter xmlwriter = new VarianXmlWriter();
                    xmlwriter.LoadROIsFromPath(template_row.templateMaker.path);
                    xmlwriter.SaveFile(Path.Combine(output_directory,$"{Path.GetFileName(template_row.templateMaker.path)}.xml"));
                }
            }
        }

        private void Load_XMLs_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog("*.xml");
            dialog.InitialDirectory = ".";
            string suspected_directory = @"\\ro-ariaimg-v\va_data$\ProgramData\Vision\Templates\structure";
            if (Directory.Exists(suspected_directory))
            {
                dialog.InitialDirectory = suspected_directory;
                dialog.DefaultDirectory = suspected_directory;
            }
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                string xml_directory = dialog.FileName;
                foreach (string file in Directory.GetFiles(xml_directory, "*.xml"))
                {
                    //string new_file = @"K:\Template_Output_VarianXml\AbdPelv_Anal.xml";
                    try
                    {
                        VarianXmlReader reader = new VarianXmlReader(file);
                        reader.XmlToROI(folder_location);
                    }
                    catch
                    {
                        continue;
                    }
                }
                Rebuild_From_Folders();
            }
        }

        private void FMA_SNOMED_Button_Click(object sender, RoutedEventArgs e)
        {
            ChangeOntologyWindow onto_window = new ChangeOntologyWindow(template_rows, onto_path);
            onto_window.ShowDialog();
        }

        private void LoadAirTables_Click(object sender, RoutedEventArgs e)
        {
            foreach (ReadAirTable r in AirTables)
            {
                if (!r.read)
                {
                    r.read_records();
                }
            }
            LoadAirTables_Button.IsEnabled = false;
            LoadAirTables_Button.Content = "Loading...";
            load_writeable_airtables();
        }

        private void Add_Ontology_Button(object sender, RoutedEventArgs e)
        {
            EditOntologyWindow ontology_window = new EditOntologyWindow(folder_location, template_rows);
            ontology_window.ShowDialog();
            Rebuild_From_Folders();
        }
    }
}
