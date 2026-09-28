using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using DicomTemplateMakerGUI.DicomTemplateServices;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.StackPanelClasses;
using DicomTemplateMakerGUI.Windows;
using ROIOntologyClass;
using TemplateSync.Sync;

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
        private TemplateSourceCatalog catalog;
        private ObservableCollection<TemplateSourceItem> airtables = new ObservableCollection<TemplateSourceItem>();
        private ObservableCollection<TemplateSourceItem> writeable_airtables = new ObservableCollection<TemplateSourceItem>();
        public ObservableCollection<TemplateSourceItem> AirTables
        {
            get { return airtables; }
            set
            {
                airtables = value;
                OnPropertyChanged("AirTables");
            }
        }
        public ObservableCollection<TemplateSourceItem> WriteableAirTables
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
        List<AddTemplateRow> copy_template_rows = new List<AddTemplateRow>();

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
        public void load_writeable_airtables()
        {
            TemplateSourceItem? selected = AirTableComboBox.SelectedItem as TemplateSourceItem;
            WriteableAirTables = new ObservableCollection<TemplateSourceItem>(AirTables.Where(at => at.IsWritable));
            AirTableComboBox.ItemsSource = WriteableAirTables;
            AirTableComboBox.DisplayMemberPath = "Name";
            if (selected != null && WriteableAirTables.Contains(selected))
            {
                AirTableComboBox.SelectedItem = selected;
            }
            else if (WriteableAirTables.Count > 0)
            {
                AirTableComboBox.SelectedIndex = 0;
            }
        }
        public MainWindow()
        {
            InitializeComponent();
            load_airtables();
            load_writeable_airtables();
            Loaded += MainWindow_Loaded;
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
                    update_ontology_reader(evaluator);
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
        [MemberNotNull(nameof(catalog))]
        public void load_airtables()
        {
            catalog = new TemplateSourceCatalog();
            try
            {
                catalog.Initialize(@".");
            }
            catch (Exception ex)
            {
                // Startup must never fail because of online-template settings.
                catalog.Problems.Add("Online templates could not be set up: " + ex.Message);
            }
            AirTables = catalog.Sources;
            ReadingAirTable();
        }
        public void ReadingAirTable()
        {
            // The shared TG-263 source is always present, so online templates are always offered.
            ReadAirTableButton.Content = "Load Online Templates";
            ReadAirTableButton.Background = lightgreen;
            ReadAirTableButton.IsEnabled = true;
        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (catalog.Migration.Imported.Count > 0 || catalog.Migration.Retired.Count > 0 || catalog.Migration.Problems.Count > 0 || catalog.Problems.Count > 0)
                {
                    MessageBox.Show(this, DescribeStartupNotes(), "Airtable settings", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                // Cached copies only: startup never spends Airtable API calls.
                foreach (TemplateSourceItem source in AirTables.ToList())
                {
                    await source.LoadAsync(LoadMode.CacheOnly, null, CancellationToken.None);
                }
                check_airtables(AirTableComboBox.SelectedItem as TemplateSourceItem);
            }
            catch (Exception ex)
            {
                // UI boundary: cached data is optional; report and continue.
                MessageBox.Show(this, "Could not read the saved template copies: " + ex.Message, "Online templates", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private string DescribeStartupNotes()
        {
            var text = new StringBuilder();
            if (catalog.Migration.Imported.Count > 0)
            {
                text.AppendLine("Moved these Airtable connections into encrypted storage for your Windows account: " + string.Join(", ", catalog.Migration.Imported) + ".");
            }
            if (catalog.Migration.KeptPlaintext.Count > 0)
            {
                text.AppendLine("These files still hold plain-text tokens because the folder may be shared with other users. Delete them once everyone who uses this folder has started this version: " + string.Join(", ", catalog.Migration.KeptPlaintext) + ".");
            }
            if (catalog.Migration.Retired.Count > 0)
            {
                text.AppendLine("Removed token files that shipped with older versions of this program (" + string.Join(", ", catalog.Migration.Retired) + "). Those tokens were public and must not be used. The shared TG-263 templates are now downloaded without any token.");
            }
            foreach (string problem in catalog.Migration.Problems.Concat(catalog.Problems))
            {
                text.AppendLine(problem);
            }
            return text.ToString();
        }
        public void update_ontology_reader(TemplateMaker evaluator)
        {
            evaluator.Ontologies = OntologyTools.LoadOntologiesFromFolder(onto_path);
            evaluator.Ontologies.Sort((p, q) => p.CodeMeaning.CompareTo(q.CodeMeaning));
        }
        [MemberNotNull(nameof(template_rows), nameof(visible_template_rows))]
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
                update_ontology_reader(evaluator);
                evaluator.categorize_folder();

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
            update_ontology_reader(template_maker);
            MakeTemplateWindow template_window = new MakeTemplateWindow(folder_location, template_maker, AirTables);
            template_window.ShowDialog();
            Rebuild_From_Folders();
        }

        private void ChangeTemplateClick(object sender, RoutedEventArgs e)
        {
            string? picked = FileDialogs.PickFolder(this, "Select the folder that holds your templates", folder_location);
            if (picked != null)
            {
                folder_location = picked;
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
                if (Selected_CheckBox.IsChecked == true)
                {
                    if (temp_row.SelectCheckBox.IsChecked != true)
                    {
                        continue;
                    }
                    else
                    {
                        visible_template_rows.Add(temp_row);
                    }
                }
                else if (RequireTemplateName(temp_row).ToLower().Contains(SearchBox_TextBox.Text.ToLower()))
                {
                    visible_template_rows.Add(temp_row);
                }
            }
            DisplayRows();
        }
        /// <summary>
        /// The row's template name. Rows are built for folders recognised as templates, which names them;
        /// a row without a name still fails the search here, where the name used to be dereferenced.
        /// </summary>
        private static string RequireTemplateName(AddTemplateRow row)
        {
            return row.templateMaker.TemplateName ?? throw new InvalidOperationException("A template row has no template name.");
        }
        private void SearchTextUpdate(object sender, TextChangedEventArgs e)
        {
            UpdateText();
        }

        private void Read_Airtable(object sender, RoutedEventArgs e)
        {
            AirTableWindow airtable_window = new AirTableWindow(catalog, folder_location, onto_path);
            airtable_window.Owner = this;
            airtable_window.ShowDialog();
            load_writeable_airtables();
            Rebuild_From_Folders();
            check_airtables(AirTableComboBox.SelectedItem as TemplateSourceItem);
        }

        private void CreateFolderRT_Click(object sender, RoutedEventArgs e)
        {
            string? picked = FileDialogs.PickFolder(this, "Select where to create the folder with loadable RTs", ".");
            if (picked != null)
            {
                string output_directory = Path.Combine(picked, "Template_Output");
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
                    if (template_row.SelectCheckBox.IsChecked == true)
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
                    if (template_row.SelectCheckBox.IsChecked != true)
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
                if (row.SelectCheckBox.IsChecked == true)
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
                if (row.SelectCheckBox.IsChecked == true)
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
                string template_path = row.TemplatePath;
                foreach (string dirPath in Directory.GetDirectories(template_path, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(dirPath.Replace(template_path, new_template_path));
                }

                //Copy all the files & Replaces any files with the same name
                foreach (string newPath in Directory.GetFiles(template_path, "*.*", SearchOption.AllDirectories))
                {
                    File.Copy(newPath, newPath.Replace(template_path, new_template_path), true);
                }
                TemplateMaker evaluator = new TemplateMaker();
                evaluator.set_onto_path(Path.Combine(folder_location, "Ontologies"));
                update_ontology_reader(evaluator);
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
            if (Copy_CheckBox.IsChecked == true)
            {
                Copy_Selected_Button.IsEnabled = true;
            }
            if (Delete_Checkbox.IsChecked == true)
            {
                Deleted_Selected_Button.IsEnabled = true;
            }
        }
        private void AirTableCheckBox_DataContextChanged(object sender, RoutedEventArgs e)
        {
            check_airtables(AirTableComboBox.SelectedItem as TemplateSourceItem);
        }
        private void Selected_DataContextChanged(object sender, RoutedEventArgs e)
        {
            UpdateText();
        }
        private async void WriteToAirTable_Click(object sender, RoutedEventArgs e)
        {
            TemplateSourceItem? table = AirTableComboBox.SelectedItem as TemplateSourceItem;
            List<AddTemplateRow> selected = template_rows.Where(row => row.SelectCheckBox.IsChecked == true).ToList();
            if (table == null || selected.Count == 0)
            {
                MessageBox.Show(this, "Select at least one template and an Airtable table to write to.", "Write to Airtable", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            WriteToAirTable_Button.IsEnabled = false;
            AirTableComboBox.IsEnabled = false;
            AirTableCheckbox.IsEnabled = false;
            LoadAirTables_Button.IsEnabled = false;
            ReadAirTableButton.IsEnabled = false;
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.Value = 0;
            var progress = new Progress<string>(message => WriteToAirTable_Button.Content = message);
            try
            {
                WriteToAirTable_Button.Content = "Writing " + selected.Count + " template(s)...";
                // A row without a name is rejected by the write planner as before: it treats null and empty alike.
                IReadOnlyList<WriteResult> results = await table.WriteTemplatesAsync(
                    selected.Select(row => new KeyValuePair<string, IEnumerable<ROIClass>>(row.templateMaker.TemplateName ?? string.Empty, row.templateMaker.ROIs)).ToList(),
                    progress,
                    CancellationToken.None);
                ProgressBar.Value = 100;
                WriteToAirTable_Button.Content = "Wrote to Airtable!";
                MessageBox.Show(this, DescribeWrite(table, results), "Write to Airtable", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (AirtableWriteException ex)
            {
                WriteToAirTable_Button.Content = "Failed writing to Airtable";
                string finished = ex.Completed.Count > 0 ? Environment.NewLine + Environment.NewLine + DescribeWrite(table, ex.Completed) : string.Empty;
                MessageBox.Show(this, ex.Message + finished, "Write to Airtable failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                // UI boundary: report every failure instead of letting an async void handler crash the app.
                WriteToAirTable_Button.Content = "Failed writing to Airtable";
                MessageBox.Show(this, ex.Message, "Write to Airtable failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ProgressBar.Visibility = Visibility.Hidden;
                ReadAirTableButton.IsEnabled = true;
                AirTableComboBox.IsEnabled = true;
                AirTableCheckbox.IsChecked = false;
                AirTableCheckbox.IsEnabled = true;
                LoadAirTables_Button.IsEnabled = true;
                check_airtables(table);
            }
        }
        private static string DescribeWrite(TemplateSourceItem table, IReadOnlyList<WriteResult> results)
        {
            int created = results.Sum(r => r.Created);
            int updated = results.Sum(r => r.Updated);
            int unchanged = results.Sum(r => r.Unchanged);
            int calls = results.Sum(r => r.ApiCalls);
            string summary = $"Wrote {results.Count} template(s) to {table.Name}: {created} ROI(s) created, {updated} updated, {unchanged} already up to date ({calls} API calls).";
            List<string> warnings = results.SelectMany(r => r.Warnings.Select(w => r.Site + ": " + w)).Distinct().Take(30).ToList();
            return warnings.Count == 0 ? summary : summary + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, warnings);
        }
        private void check_airtables(TemplateSourceItem? airtable)
        {
            bool canWrite = airtable != null && AirTableCheckbox.IsChecked == true;
            WriteToAirTable_Button.IsEnabled = canWrite;
            if (airtable == null)
            {
                WriteToAirTable_Button.Content = "Add an Airtable table first";
                WriteToAirTable_Button.ToolTip = "Open Load Online Templates and use Add Airtable table to connect a table you can write to.";
                LoadAirTables_Button.IsEnabled = false;
                return;
            }
            WriteToAirTable_Button.Content = "Write to AirTable";
            WriteToAirTable_Button.ToolTip = canWrite
                ? "Checks " + airtable.Name + " for recent changes, then sends only the ROIs that differ."
                : "Tick 'Airtable Write?' to enable writing.";
            LoadAirTables_Button.IsEnabled = true;
            LoadAirTables_Button.ToolTip = airtable.StatusText;
        }
        private void AirTableSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            check_airtables(AirTableComboBox.SelectedItem as TemplateSourceItem);
        }

        private void CreateVarianXml_Click(object sender, RoutedEventArgs e)
        {
            string suspected_directory = @"\\ro-ariaimg-v\va_data$\ProgramData\Vision\Templates\structure";
            string? output_directory = FileDialogs.PickFolder(this, "Select where to write the Varian XML templates", Directory.Exists(suspected_directory) ? suspected_directory : ".");
            if (output_directory != null)
            {
                if (!Directory.Exists(output_directory))
                {
                    Directory.CreateDirectory(output_directory);
                }
                bool any_select = false;
                foreach (AddTemplateRow template_row in template_rows)
                {
                    if (template_row.SelectCheckBox.IsChecked == true)
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
                    if (template_row.SelectCheckBox.IsChecked != true)
                    {
                        continue;
                    }
                    VarianXmlWriter xmlwriter = new VarianXmlWriter();
                    string template_path = template_row.TemplatePath;
                    xmlwriter.LoadROIsFromPath(template_path, template_row.templateMaker.Ontologies);
                    xmlwriter.SaveFile(Path.Combine(output_directory, $"{Path.GetFileName(template_path)}.xml"));
                }
            }
        }

        private void Load_XMLs_Click(object sender, RoutedEventArgs e)
        {
            string suspected_directory = @"\\ro-ariaimg-v\va_data$\ProgramData\Vision\Templates\structure";
            string? xml_directory = FileDialogs.PickFolder(this, "Select the folder of Varian XML templates to import", Directory.Exists(suspected_directory) ? suspected_directory : ".");
            if (xml_directory != null)
            {
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

        private async void LoadAirTables_Click(object sender, RoutedEventArgs e)
        {
            TemplateSourceItem? table = AirTableComboBox.SelectedItem as TemplateSourceItem;
            if (table == null)
            {
                return;
            }
            LoadAirTables_Button.IsEnabled = false;
            ReadAirTableButton.IsEnabled = false;
            LoadAirTables_Button.Content = "...";
            try
            {
                TableLoadResult result = await table.LoadAsync(LoadMode.Refresh, null, CancellationToken.None);
                if (result.Warning != null)
                {
                    MessageBox.Show(this, result.Warning, "Refresh " + table.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                // UI boundary.
                MessageBox.Show(this, "Could not refresh " + table.Name + ": " + ex.Message, "Refresh", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadAirTables_Button.Content = "Refresh";
                ReadAirTableButton.IsEnabled = true;
                check_airtables(table);
            }
        }

        private void Add_Ontology_Button(object sender, RoutedEventArgs e)
        {
            EditOntologyWindow ontology_window = new EditOntologyWindow(folder_location, template_rows);
            ontology_window.ShowDialog();
            Rebuild_From_Folders();
        }
    }
}
