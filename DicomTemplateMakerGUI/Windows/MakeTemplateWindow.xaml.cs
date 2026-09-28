using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DicomTemplateMakerGUI.Editors;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.StackPanelClasses;
using Microsoft.Extensions.Logging;
using ROIOntologyClass;
using TemplateSync.Sync;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Makes a new template or edits an existing one. Changes stay in memory until Save (or Save and close); closing
    /// with unsaved changes asks first. Nothing is written until the template has been built, and never into the
    /// template folder's parent. The rules and texts are in DicomTemplateMaker.Presentation (Editors).
    /// </summary>
    public partial class MakeTemplateWindow : Window
    {
        private readonly ILogger<MakeTemplateWindow> logger = AppLog.For<MakeTemplateWindow>();
        // The folder that holds the template's folder.
        private readonly string templateRoot;
        // The template's folder; null until the template exists on disk.
        private string? templateFolder;
        private bool dirty;
        private bool busy;
        private bool settingName;
        private byte R = 0, G = 255, B = 255;
        public TemplateMaker template_maker;
        public ObservableCollection<TemplateSourceItem> AirTables;

        /// <param name="folder">An existing template's folder, when <paramref name="template_maker"/> was read from it; otherwise the template folder in which Build makes the new template.</param>
        public MakeTemplateWindow(string folder, TemplateMaker template_maker, ObservableCollection<TemplateSourceItem> airTables)
        {
            AirTables = airTables;
            this.template_maker = template_maker;
            InitializeComponent();
            RoiHeaderHost.Content = AddROIRow.Header();
            InterpComboBox.ItemsSource = InterpretedTypes.All;
            InterpComboBox.SelectedItem = InterpretedTypes.Default;

            OntologyComboBox.DisplayMemberPath = nameof(OntologyCodeClass.CodeMeaning);
            OntologyComboBox.ItemsSource = template_maker.Ontologies;
            // N7: no code is preselected; the user picks one.
            OntologyComboBox.SelectedIndex = -1;

            List<TemplateSourceItem> writable_tables = AirTables.Where(t => t.IsWritable).ToList();
            AirTableComboBox.ItemsSource = writable_tables;
            AirTableComboBox.DisplayMemberPath = "Name";
            if (writable_tables.Count > 0)
            {
                AirTableComboBox.SelectedIndex = 0;
            }

            ColorButton.Background = new SolidColorBrush(Color.FromRgb(R, G, B));

            // A template read from its folder (the main window's list) is edited in place.
            if (template_maker.is_template && template_maker.path != null)
            {
                templateFolder = folder;
                templateRoot = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(folder)) ?? folder;
                SetNameText(template_maker.TemplateName ?? Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)));
                add_roi_rows();
            }
            else
            {
                templateRoot = folder;
            }

            UpdateState();
        }

        private string TemplateName => template_maker.TemplateName ?? TemplateTextBox.Text;

        // ---------------------------------------------------------------- state

        /// <summary>Enables the controls for the current state and refreshes every status line.</summary>
        private void UpdateState()
        {
            bool built = templateFolder != null;
            Title = built ? "Template editor – " + TemplateName : "New template";
            TemplateTextBox.IsReadOnly = built;
            BuildButton.IsEnabled = !built && NameRules.TemplateNameProblem(TemplateTextBox.Text) == null;
            BuildButton.Content = built ? "Template built" : "Build template";
            Rename_Button.IsEnabled = built;
            Rename_Button.ToolTip = built ? "Renames the template's folder." : "Available once the template has been built.";
            foreach (Control control in new Control[] { AddROIFromRTButton, pathsButton, Ontology_TextBox, OntologyComboBox, ROITextBox, InterpComboBox, ColorButton, RefreshButton, Select_All_Button, UnSelect_All_Button, UpdateButton, Update_and_ExitButton })
            {
                control.IsEnabled = built;
            }

            UpdateTemplateStatus();
            UpdatePathsStatus();
            UpdateAddRoiState();
            check_airtables(AirTableComboBox.SelectedItem as TemplateSourceItem);
            SaveStatusText.Text = built && dirty ? "Unsaved changes" : string.Empty;
            SaveStatusText.Foreground = EditorBrushes.Warning;
        }

        private void MarkChanged()
        {
            if (!dirty)
            {
                dirty = true;
                UpdateState();
            }
        }

        private void UpdateTemplateStatus()
        {
            string name = TemplateTextBox.Text;
            if (templateFolder != null)
            {
                EditorBrushes.ShowStatus(TemplateStatusText, Array.Empty<string>(), NameRules.TemplateNameWarning(name), "Folder: " + templateFolder);
                return;
            }

            if (name.Length == 0)
            {
                EditorBrushes.ShowStatus(TemplateStatusText, Array.Empty<string>(), null, "Enter a name for the new template, then press Build template.");
                return;
            }

            string? problem = NameRules.TemplateNameProblem(name);
            if (problem != null)
            {
                EditorBrushes.ShowStatus(TemplateStatusText, new[] { problem }, null);
                return;
            }

            string folder = Path.Combine(templateRoot, name);
            string info = "Build creates the folder " + folder + ".";
            if (TemplateExistsOrUnknown(folder))
            {
                info = "A template with this name already exists in " + folder + "; building replaces its ROIs (you will be asked first).";
            }

            EditorBrushes.ShowStatus(TemplateStatusText, Array.Empty<string>(), NameRules.TemplateNameWarning(name), info);
        }

        /// <summary>
        /// <see cref="TemplateMaker.TemplateExists"/> for the status line, which runs on every keystroke: a folder that
        /// cannot be read counts as existing (Build checks again and reports the error).
        /// </summary>
        private bool TemplateExistsOrUnknown(string folder)
        {
            try
            {
                return TemplateMaker.TemplateExists(folder);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                logger.LogDebug(ex, "Could not check whether {Folder} holds a template.", folder);
                return true;
            }
        }

        private void UpdatePathsStatus()
        {
            if (templateFolder == null)
            {
                PathsStatusText.Text = string.Empty;
                pathsButton.ClearValue(BackgroundProperty);
                return;
            }

            int paths = template_maker.Paths.Count;
            int requirements = template_maker.DicomTags.Values.Sum(values => values.Count);
            if (paths == 0)
            {
                PathsStatusText.Text = "No monitored folders: no RTs are written for this template until one is added.";
                PathsStatusText.Foreground = EditorBrushes.Error;
                pathsButton.Background = EditorBrushes.MissingBackground;
            }
            else
            {
                PathsStatusText.Text = (paths == 1 ? "1 monitored folder" : paths + " monitored folders") + ", "
                    + (requirements == 0 ? "no DICOM requirements" : requirements == 1 ? "1 DICOM requirement" : requirements + " DICOM requirements") + ".";
                PathsStatusText.Foreground = EditorBrushes.Info;
                pathsButton.ClearValue(BackgroundProperty);
            }
        }

        /// <summary>Add ROI is enabled only with a usable name, a chosen code and a chosen type; otherwise the reason is shown (see <see cref="AddRoiRules"/>).</summary>
        private void UpdateAddRoiState()
        {
            AddRoiState state = AddRoiRules.Decide(templateFolder != null, ROITextBox.Text, template_maker.ROIs, OntologyComboBox.SelectedItem != null, InterpComboBox.SelectedItem != null);
            AddROIButton.IsEnabled = state.CanAdd;
            EditorBrushes.ShowStatus(AddRoiStatusText, state.Problem == null ? Array.Empty<string>() : new[] { state.Problem }, state.Warning, state.Hint);
        }

        private void SetNameText(string name)
        {
            settingName = true;
            try
            {
                TemplateTextBox.Text = name;
            }
            finally
            {
                settingName = false;
            }
        }

        // ---------------------------------------------------------------- ROI list

        private void add_roi_rows()
        {
            ROIStackPanel.Children.Clear();
            foreach (ROIClass roi in RoiList.Arrange(template_maker.ROIs, SearchBox_TextBox.Text))
            {
                ROIStackPanel.Children.Add(new AddROIRow(template_maker.ROIs, roi, template_maker.Ontologies, OnRoiChanged));
            }
        }

        private void OnRoiChanged()
        {
            MarkChanged();
            UpdateAddRoiState();
        }

        private void SearchTextUpdate(object sender, TextChangedEventArgs e)
        {
            add_roi_rows();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            add_roi_rows();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            SetIncludeForShown(true);
        }

        private void UnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            SetIncludeForShown(false);
        }

        /// <summary>Sets Include? for the ROIs the search shows (not the hidden ones).</summary>
        private void SetIncludeForShown(bool include)
        {
            IReadOnlyList<ROIClass> shown = RoiList.Arrange(template_maker.ROIs, SearchBox_TextBox.Text);
            if (shown.Any(roi => roi.Include != include))
            {
                foreach (ROIClass roi in shown)
                {
                    roi.Include = include;
                }

                MarkChanged();
            }

            add_roi_rows();
        }

        // ---------------------------------------------------------------- building, saving, closing

        private void TemplateNameChanged(object sender, TextChangedEventArgs e)
        {
            if (!settingName)
            {
                UpdateState();
            }
        }

        /// <summary>N5: builds only a usable name, asks before replacing an existing template, and says "built" only after a save that worked.</summary>
        private void Build_Button_Click(object sender, RoutedEventArgs e)
        {
            const string title = "Build template";
            try
            {
                string name = TemplateTextBox.Text;
                string? problem = NameRules.TemplateNameProblem(name);
                if (problem != null || templateFolder != null)
                {
                    UpdateState();
                    return;
                }

                string folder = Path.Combine(templateRoot, name);
                if (TemplateMaker.TemplateExists(folder)
                    && MessageBox.Show(this, EditorMessages.ConfirmReplaceTemplate(name, folder, ExistingTemplate.CountRois(folder), template_maker.ROIs.Count), title,
                        MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                {
                    return;
                }

                template_maker.define_output(folder);
                if (!SaveTemplate())
                {
                    UpdateState();
                    return;
                }

                template_maker.define_path(folder);
                template_maker.TemplateName = name;
                templateFolder = folder;
                logger.LogInformation("Built template {Template} in {Folder}.", name, folder);
                UpdateState();
                add_roi_rows();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                logger.LogError(ex, "Building template {Template} failed.", TemplateTextBox.Text);
                MessageBox.Show(this, "The template could not be built: " + ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Saves the template, or says why it was not saved. Never writes before the template's folder is set.</summary>
        private bool SaveTemplate()
        {
            try
            {
                template_maker.make_template();
                dirty = false;
                logger.LogInformation("Saved template {Template} ({Count} ROIs).", TemplateTextBox.Text, template_maker.ROIs.Count);
                UpdateState();
                return true;
            }
            catch (TemplateLoadException ex)
            {
                logger.LogWarning(ex, "Template {Template} was not saved.", TemplateTextBox.Text);
                MessageBox.Show(this, ex.Message, "Template not saved", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                logger.LogError(ex, "Template {Template} could not be written.", TemplateTextBox.Text);
                MessageBox.Show(this, "The template could not be saved: " + ex.Message, "Template not saved", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>Puts the template back as it is on disk (the main window shares this template object).</summary>
        private void DiscardChanges()
        {
            if (templateFolder == null || !dirty)
            {
                return;
            }

            try
            {
                template_maker.define_path(templateFolder);
                template_maker.categorize_folder();
                dirty = false;
                logger.LogInformation("Discarded unsaved changes to template {Template}.", TemplateName);
            }
            catch (Exception ex) when (ex is TemplateLoadException || ex is IOException || ex is UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Template {Template} could not be read back after discarding changes.", TemplateName);
                MessageBox.Show(this, "The changes were not saved, but the template could not be read back from " + templateFolder + ": " + ex.Message
                    + Environment.NewLine + Environment.NewLine + "The main window may show the unsaved changes until the template list is read again.",
                    "Discard changes", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Save_Changes_Click(object sender, RoutedEventArgs e)
        {
            if (templateFolder != null)
            {
                SaveTemplate();
            }
        }

        private void Save_and_Exit_Click(object sender, RoutedEventArgs e)
        {
            // Stays open when the save was refused, so the edits are not lost from view.
            if (templateFolder == null || SaveTemplate())
            {
                Close();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (busy)
            {
                e.Cancel = true;
                MessageBox.Show(this, "Wait until the template has been written to Airtable.", "Template editor", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (templateFolder == null || !dirty)
            {
                return;
            }

            MessageBoxResult answer = MessageBox.Show(this, EditorMessages.UnsavedChanges("template \"" + TemplateName + "\""), "Unsaved changes",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (answer == MessageBoxResult.Yes)
            {
                e.Cancel = !SaveTemplate();
            }
            else if (answer == MessageBoxResult.No)
            {
                DiscardChanges();
            }
            else
            {
                e.Cancel = true;
            }
        }

        // ---------------------------------------------------------------- renaming

        /// <summary>N14: only for a template that exists; moves its folder, then points the template (and its rows) at the new folder.</summary>
        private void Rename_template_Click(object sender, RoutedEventArgs e)
        {
            const string title = "Rename template";
            if (templateFolder == null)
            {
                return;
            }

            string folder = templateFolder;
            string? parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(folder));
            if (parent == null)
            {
                MessageBox.Show(this, "The template's folder, " + folder + ", is the root of a drive, so it cannot be renamed.", title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (dirty && MessageBox.Show(this, "Renaming saves the template, including the changes not saved yet. Continue?", title,
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            {
                return;
            }

            RenameTemplateWindow rename_window = new RenameTemplateWindow(Path.GetFileName(folder), parent, template_maker.Paths.Count > 0) { Owner = this };
            if (rename_window.ShowDialog() != true)
            {
                return;
            }

            string newName = rename_window.NewName;
            string newFolder = Path.Combine(parent, newName);
            try
            {
                Directory.Move(folder, newFolder);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not rename template folder {Folder} to {NewName}.", folder, newName);
                MessageBox.Show(this, EditorMessages.RenameFailed(folder, newName, ex.Message), title, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            logger.LogInformation("Renamed template folder {Folder} to {NewFolder}.", folder, newFolder);
            templateFolder = newFolder;
            template_maker.TemplateName = newName;
            template_maker.define_output(newFolder);
            template_maker.define_path(newFolder);
            SetNameText(newName);
            // The files moved with the folder; saving also makes the template track its settings files there.
            SaveTemplate();
            add_roi_rows();
            UpdateState();
        }

        // ---------------------------------------------------------------- adding ROIs

        private async void Select_File_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                const string title = "Add ROIs from an RT Structure file";
                string? picked = await FileDialogs.PickFileAsync(this, "Select an RT Structure file", FileDialogs.DicomFilter, FolderPurpose.RtFile);
                if (picked == null)
                {
                    return;
                }

                int before = template_maker.ROIs.Count;
                if (!RtStructureSetFile.TryInterpret(template_maker, picked, out string? error))
                {
                    logger.LogWarning("Could not add ROIs from {File}: {Error}", picked, error);
                    MessageBox.Show(this, error ?? "The file could not be read.", title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int added = template_maker.ROIs.Count - before;
                logger.LogInformation("Added {Count} ROI(s) from {File} to template {Template}.", added, picked, TemplateName);
                if (added > 0)
                {
                    MarkChanged();
                }

                add_roi_rows();
                UpdateAddRoiState();
                MessageBox.Show(this, added == 0 ? "The file's ROIs are all in the template already." : (added == 1 ? "1 ROI was added." : added + " ROIs were added.") + " Save the template to keep them.",
                    title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // UI boundary: an async event handler must not let an exception escape.
                logger.LogError(ex, "Adding ROIs from an RT Structure file failed.");
                MessageBox.Show(this, "Adding ROIs from an RT Structure file failed: " + ex.Message, "Add ROIs from an RT Structure file", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ROINameChanged(object sender, TextChangedEventArgs e)
        {
            UpdateAddRoiState();
        }

        private void AddRoiInputChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAddRoiState();
        }

        /// <summary>N7: the search narrows the list but never picks a code.</summary>
        private void OntologyNameChanged(object sender, TextChangedEventArgs e)
        {
            string text = Ontology_TextBox.Text.Trim();
            OntologyComboBox.ItemsSource = text.Length == 0
                ? template_maker.Ontologies
                : template_maker.Ontologies.Where(x => ((string?)x.CodeMeaning ?? string.Empty).Contains(text, StringComparison.CurrentCultureIgnoreCase)).ToList();
            OntologyComboBox.SelectedIndex = -1;
            UpdateAddRoiState();
        }

        private void AddROI_Click(object sender, RoutedEventArgs e)
        {
            string name = ROITextBox.Text;
            if (templateFolder == null
                || NameRules.RoiNameProblem(name, template_maker.ROIs, null) != null
                || OntologyComboBox.SelectedItem is not OntologyCodeClass code_class
                || InterpComboBox.SelectedItem is not string type)
            {
                UpdateAddRoiState();
                return;
            }

            ROIClass roi = new ROIClass(R, G, B, name, type, code_class);
            template_maker.ROIs.Add(roi);
            MarkChanged();
            ROITextBox.Text = string.Empty;
            OntologyComboBox.SelectedIndex = -1;
            InterpComboBox.SelectedItem = InterpretedTypes.Default;
            add_roi_rows();
            UpdateAddRoiState();
        }

        private void ChangeColor_Click(object sender, RoutedEventArgs e)
        {
            using System.Windows.Forms.ColorDialog dialog = new System.Windows.Forms.ColorDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                R = dialog.Color.R;
                G = dialog.Color.G;
                B = dialog.Color.B;
                ColorButton.Background = new SolidColorBrush(Color.FromRgb(R, G, B));
            }
        }

        // ---------------------------------------------------------------- monitored folders

        private void PathsButtonClick(object sender, RoutedEventArgs e)
        {
            if (templateFolder == null)
            {
                return;
            }

            EditPathsWindow paths_window = new EditPathsWindow(template_maker, dirty) { Owner = this };
            if (paths_window.ShowDialog() == true)
            {
                // The paths editor saved the whole template.
                dirty = false;
            }

            UpdateState();
        }

        // ---------------------------------------------------------------- Airtable

        /// <summary>N19: asks first, saves the template, then sends what is saved.</summary>
        private async void WriteToAirTable_Click(object sender, RoutedEventArgs e)
        {
            const string title = "Write to Airtable";
            TemplateSourceItem? table = AirTableComboBox.SelectedItem as TemplateSourceItem;
            if (table == null || templateFolder == null)
            {
                return;
            }

            try
            {
                string name = TemplateName;
                if (MessageBox.Show(this, EditorMessages.ConfirmAirtableWrite(table.Name, name, template_maker.ROIs.Count, saveFirst: true), title,
                    MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
                {
                    return;
                }

                if (!SaveTemplate())
                {
                    return;
                }

                busy = true;
                EditorContent.IsEnabled = false;
                WriteToAirTable_Button.Content = "Writing to Airtable…";
                var progress = new Progress<string>(message => WriteToAirTable_Button.Content = message);
                logger.LogInformation("Writing template {Template} ({Count} ROIs) to Airtable table {Table}.", name, template_maker.ROIs.Count, table.Name);
                WriteResult result = await table.WriteTemplateAsync(name, template_maker.ROIs.ToList(), progress, CancellationToken.None);
                WriteToAirTable_Button.Content = "Wrote to Airtable";
                logger.LogInformation("Wrote template {Template} to {Table}: {Created} created, {Updated} updated, {Unchanged} unchanged.", name, table.Name, result.Created, result.Updated, result.Unchanged);
                string summary = $"{table.Name}: {result.Created} created, {result.Updated} updated, {result.Unchanged} already up to date ({result.ApiCalls} API calls).";
                if (result.Warnings.Count > 0)
                {
                    summary += Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, result.Warnings);
                }

                MessageBox.Show(this, summary, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // UI boundary: report every failure instead of letting an async void handler crash the app.
                logger.LogError(ex, "Writing template {Template} to Airtable table {Table} failed.", TemplateName, table.Name);
                WriteToAirTable_Button.Content = "Failed writing to Airtable";
                MessageBox.Show(this, ex.Message, "Write to Airtable failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                busy = false;
                EditorContent.IsEnabled = true;
            }
        }

        private void check_airtables(TemplateSourceItem? airtable)
        {
            if (busy)
            {
                return;
            }

            WriteToAirTable_Button.IsEnabled = airtable != null && templateFolder != null;
            WriteToAirTable_Button.Content = airtable == null ? "Add an Airtable table to write" : "Write to Airtable…";
            WriteToAirTable_Button.ToolTip = airtable == null
                ? "Use Load Online Templates > Add Airtable to connect a table you can write to."
                : templateFolder == null
                    ? "Build the template first."
                    : "Asks first, saves the template, checks the table for recent changes, then sends only the ROIs that differ.";
        }

        private void AirTableSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            check_airtables(AirTableComboBox.SelectedItem as TemplateSourceItem);
        }
    }
}
