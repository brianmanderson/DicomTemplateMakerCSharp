using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DicomTemplateMakerGUI.Editors;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.StackPanelClasses;
using Microsoft.Extensions.Logging;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// N16: edits the ontology library. Changes stay in this window until Save; closing with unsaved changes asks
    /// first. A code value is required and the reasons are shown as text; deleting an entry that templates use says
    /// which templates first.
    /// </summary>
    public partial class EditOntologyWindow : Window
    {
        private const string WindowTitle = "Edit ontologies";
        private readonly ILogger<EditOntologyWindow> logger = AppLog.For<EditOntologyWindow>();
        private readonly string onto_path;
        private readonly List<AddTemplateRow> template_rows;
        // Entries added or edited in this window: they must be valid before the library is saved. Older entries with a
        // problem are shown but do not block saving.
        private readonly HashSet<OntologyCodeClass> touched = new HashSet<OntologyCodeClass>(ReferenceEqualityComparer.Instance);
        private List<OntologyCodeClass> library = new List<OntologyCodeClass>();
        private bool dirty;

        /// <summary>Throws <see cref="TemplateLoadException"/> when the library cannot be read (the main window checks first).</summary>
        /// <param name="path">The template folder; the library is in its Ontologies folder.</param>
        public EditOntologyWindow(string path, List<AddTemplateRow> template_rows)
        {
            onto_path = Path.Combine(path, TemplateRootResolver.OntologiesFolderName);
            this.template_rows = template_rows;
            InitializeComponent();
            NoteText.Text = "The ontology library (" + Path.Combine(onto_path, OntologyTools.LibraryFileName) + ") is the list of codes offered when adding ROIs. "
                + "Changes are kept in this window until you press Save. Templates keep the codes they were saved with.";
            OntologyHeaderHost.Content = AddOntologyRow.Header();
            LoadLibrary();
            check_status();
        }

        /// <summary>Reads the library from disk, dropping any unsaved changes.</summary>
        private void LoadLibrary()
        {
            library = OntologyTools.LoadOntologiesFromFolder(onto_path);
            SortLibrary();
            touched.Clear();
            dirty = false;
            RefreshView();
            UpdateSaveState();
        }

        private void SortLibrary()
        {
            // CodeMeaning is null for a hand-edited file that says so; string.Compare orders null first.
            library.Sort((p, q) => string.Compare(p.CodeMeaning, q.CodeMeaning, StringComparison.CurrentCulture));
        }

        private void MarkChanged()
        {
            dirty = true;
            UpdateSaveState();
        }

        private void UpdateSaveState()
        {
            SaveStatusText.Text = dirty ? "Unsaved changes" : string.Empty;
            SaveStatusText.Foreground = EditorBrushes.Warning;
            Title = dirty ? WindowTitle + " (unsaved changes)" : WindowTitle;
        }

        /// <summary>The new-entry form: Add is enabled only for a valid entry, and the reason is shown as text.</summary>
        private void check_status()
        {
            string name = PreferredNameTextBox.Text;
            string code = CodeValue_TextBox.Text;
            string scheme = CodeScheme_TextBox.Text;
            if (name.Length == 0 && code.Length == 0 && scheme.Length == 0)
            {
                AddOntology_Button.IsEnabled = false;
                EditorBrushes.ShowStatus(EntryStatusText, Array.Empty<string>(), null, "Enter a common name, a code value and a coding scheme, then press Add to library.");
                return;
            }

            string? problem = OntologyEntryRules.Problem(name, code, scheme, library, null);
            AddOntology_Button.IsEnabled = problem == null;
            EditorBrushes.ShowStatus(EntryStatusText, problem == null ? Array.Empty<string>() : new[] { problem }, problem == null ? OntologyEntryRules.Warning(name, library, null) : null);
        }

        private void UpdateText(object sender, TextChangedEventArgs e)
        {
            check_status();
        }

        private void SearchTextUpdate(object sender, TextChangedEventArgs e)
        {
            RefreshView();
        }

        private void RefreshView()
        {
            OntologyStackPanel.Children.Clear();
            string query = SearchBox_TextBox.Text;
            foreach (OntologyCodeClass onto in library.Where(entry => OntologyEntryRules.Matches(entry, query)))
            {
                OntologyStackPanel.Children.Add(new AddOntologyRow(onto, library, OnEntryChanged, DeleteEntry));
            }
        }

        private void OnEntryChanged(OntologyCodeClass entry)
        {
            touched.Add(entry);
            MarkChanged();
            check_status();
        }

        /// <summary>Removes an entry from the library in this window; when templates use it, names them and asks first.</summary>
        private void DeleteEntry(OntologyCodeClass entry)
        {
            IReadOnlyList<string> users = OntologyEntryRules.TemplatesUsing(entry, template_rows.Select(row => row.templateMaker));
            if (users.Count > 0 && MessageBox.Show(this, EditorMessages.ConfirmDeleteOntology(entry, users), "Delete ontology entry",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            {
                return;
            }

            library.Remove(entry);
            touched.Remove(entry);
            MarkChanged();
            RefreshView();
            check_status();
        }

        private void AddOntology_Click(object sender, RoutedEventArgs e)
        {
            string name = PreferredNameTextBox.Text;
            string code = CodeValue_TextBox.Text;
            string scheme = CodeScheme_TextBox.Text;
            if (OntologyEntryRules.Problem(name, code, scheme, library, null) != null)
            {
                check_status();
                return;
            }

            OntologyCodeClass onto = new OntologyCodeClass(name.Trim(), code.Trim(), scheme.Trim());
            library.Add(onto);
            SortLibrary();
            touched.Add(onto);
            MarkChanged();
            PreferredNameTextBox.Text = string.Empty;
            CodeValue_TextBox.Text = string.Empty;
            CodeScheme_TextBox.Text = string.Empty;
            RefreshView();
            check_status();
        }

        private async void AddOntologyFromRT_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                const string title = "Add codes from an RT Structure file";
                string? dicom_file = await FileDialogs.PickFileAsync(this, "Select an RT Structure file", FileDialogs.DicomFilter, FolderPurpose.RtFile);
                if (dicom_file == null)
                {
                    return;
                }

                HashSet<OntologyCodeClass> before = new HashSet<OntologyCodeClass>(library, ReferenceEqualityComparer.Instance);
                TemplateMaker reader = new TemplateMaker { Ontologies = library };
                if (!RtStructureSetFile.TryInterpret(reader, dicom_file, out string? error))
                {
                    logger.LogWarning("Could not add codes from {File}: {Error}", dicom_file, error);
                    MessageBox.Show(this, error ?? "The file could not be read.", title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                List<OntologyCodeClass> added = library.Where(entry => !before.Contains(entry)).ToList();
                foreach (OntologyCodeClass entry in added)
                {
                    touched.Add(entry);
                }

                logger.LogInformation("Added {Count} code(s) from {File} to the ontology library (not saved yet).", added.Count, dicom_file);
                if (added.Count > 0)
                {
                    MarkChanged();
                }

                RefreshView();
                check_status();
                MessageBox.Show(this, added.Count == 0 ? "The file's codes are all in the library already." : (added.Count == 1 ? "1 code was added." : added.Count + " codes were added.") + " Press Save to keep them.",
                    title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // UI boundary: an async event handler must not let an exception escape.
                logger.LogError(ex, "Adding codes from an RT Structure file failed.");
                MessageBox.Show(this, "Adding codes from an RT Structure file failed: " + ex.Message, "Add codes from an RT Structure file", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Saves the library, unless an entry changed in this window still has a problem.</summary>
        private bool Save()
        {
            List<string> problems = new List<string>();
            foreach (OntologyCodeClass entry in library.Where(touched.Contains))
            {
                string? problem = OntologyEntryRules.Problem(entry.CodeMeaning, entry.CodeValue, entry.Scheme, library, entry);
                if (problem != null)
                {
                    problems.Add((string.IsNullOrWhiteSpace(entry.CodeMeaning) ? "(no name)" : entry.CodeMeaning) + ": " + problem);
                }
            }

            if (problems.Count > 0)
            {
                MessageBox.Show(this, EditorMessages.OntologyNotSaved(problems), WindowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                OntologyTools.SaveOntologiesToFolder(library, onto_path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                logger.LogError(ex, "The ontology library could not be saved in {Path}.", onto_path);
                MessageBox.Show(this, "The ontology library could not be saved in " + onto_path + ": " + ex.Message, WindowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            logger.LogInformation("Saved the ontology library in {Path} ({Count} entries).", onto_path, library.Count);
            touched.Clear();
            dirty = false;
            UpdateSaveState();
            return true;
        }

        private void Save_Changes_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }

        private void Save_and_Exit_Click(object sender, RoutedEventArgs e)
        {
            if (Save())
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
            if (!dirty)
            {
                return;
            }

            MessageBoxResult answer = MessageBox.Show(this, EditorMessages.UnsavedChanges("the ontology library"), "Unsaved changes",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (answer == MessageBoxResult.Yes)
            {
                e.Cancel = !Save();
            }
            else if (answer == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
            }
        }

        private void FMA_SNOMED_Button_Click(object sender, RoutedEventArgs e)
        {
            if (dirty)
            {
                MessageBoxResult answer = MessageBox.Show(this, "The conversion reads the ontology library from disk. Save your changes first?"
                    + Environment.NewLine + Environment.NewLine + "Yes saves them. No discards them. Cancel goes back.", "Change ontology scheme",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
                if (answer == MessageBoxResult.Cancel || (answer == MessageBoxResult.Yes && !Save()))
                {
                    return;
                }
            }

            ChangeOntologyWindow onto_window = new ChangeOntologyWindow(template_rows, onto_path) { Owner = this };
            onto_window.ShowDialog();
            try
            {
                LoadLibrary();
            }
            catch (TemplateLoadException ex)
            {
                // The editor now holds no library, so it closes rather than let a save replace the file.
                logger.LogWarning(ex, "The ontology library could not be read again after a scheme conversion.");
                MessageBox.Show(this, ex.Message + Environment.NewLine + Environment.NewLine + "The ontology library was left unchanged. Repair or remove the file, then open the editor again.",
                    WindowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                dirty = false;
                Close();
            }
        }
    }
}
