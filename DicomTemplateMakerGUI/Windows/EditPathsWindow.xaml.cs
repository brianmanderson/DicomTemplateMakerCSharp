using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
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
    /// Edits a template's monitored folders and DICOM requirements. Edits go to a working copy
    /// (<see cref="MonitoredSettingsDraft"/>); Save puts them into the template and saves it, Cancel or the window's close
    /// button discards them. <see cref="Window.ShowDialog"/> returns true when the template was saved.
    /// </summary>
    public partial class EditPathsWindow : Window
    {
        private readonly ILogger<EditPathsWindow> logger = AppLog.For<EditPathsWindow>();
        private readonly TemplateMaker template_maker;
        private readonly MonitoredSettingsDraft draft;
        private bool saved;

        /// <param name="savesOtherChanges">True when the template editor has unsaved changes, which saving here also saves.</param>
        public EditPathsWindow(TemplateMaker template_maker, bool savesOtherChanges)
        {
            InitializeComponent();
            this.template_maker = template_maker;
            draft = MonitoredSettingsDraft.From(template_maker);
            Title = "Monitored folders and DICOM requirements – " + (template_maker.TemplateName ?? "template");
            DicomTag_Combobox.ItemsSource = MonitoredSettingsDraft.RequirementKeys;
            DicomTag_Combobox.SelectedIndex = 0;
            if (savesOtherChanges)
            {
                EditorBrushes.ShowStatus(SaveNoteText, Array.Empty<string>(), "Saving also saves the changes not yet saved in the template editor (the whole template is saved).");
            }

            write_paths();
        }

        /// <summary>Shows the draft's folders and requirements.</summary>
        public void write_paths()
        {
            PathsStackPanel.Children.Clear();
            foreach (string path in draft.Paths)
            {
                AddPathRow(path);
            }

            RequirementStackPanel.Children.Clear();
            foreach ((string key, string value) in draft.Requirements)
            {
                RequirementStackPanel.Children.Add(new DicomTagRow(key, value, RemoveRequirement));
            }

            UpdatePathsHeader();
        }

        private void AddPathRow(string path)
        {
            PathsRow row = new PathsRow(path, RemovePath);
            PathsStackPanel.Children.Add(row);
            // Not awaited: the row shows its warning when the check finishes. The check catches its own failures.
            _ = CheckFolderAsync(row);
        }

        /// <summary>Warns next to a folder that does not exist; checked off the UI thread, since a network share may not answer.</summary>
        private async Task CheckFolderAsync(PathsRow row)
        {
            try
            {
                FolderState state = await FolderProbe.CheckAsync(row.Path, FolderProbe.DefaultTimeout);
                row.ShowWarning(FolderProbe.Describe(state, FolderProbe.DefaultTimeout));
            }
            catch (Exception ex)
            {
                // UI boundary: the check is only advice.
                logger.LogWarning(ex, "Could not check whether {Path} exists.", row.Path);
            }
        }

        private void UpdatePathsHeader()
        {
            int count = draft.Paths.Count;
            PathsHeader.Text = count == 0
                ? "Monitored folders: none. No RTs are written for this template until a folder is added."
                : count == 1 ? "Monitored folders (1)" : "Monitored folders (" + count + ")";
            PathsHeader.Foreground = count == 0 ? EditorBrushes.Error : SystemColors.ControlTextBrush;
            if (count == 0)
            {
                Add_Path_Button.Background = EditorBrushes.MissingBackground;
            }
            else
            {
                Add_Path_Button.ClearValue(BackgroundProperty);
            }
        }

        private void RemovePath(PathsRow row)
        {
            if (draft.RemovePath(row.Path))
            {
                PathsStackPanel.Children.Remove(row);
                UpdatePathsHeader();
            }
        }

        private void RemoveRequirement(DicomTagRow row)
        {
            if (draft.RemoveRequirement(row.Key, row.Value))
            {
                RequirementStackPanel.Children.Remove(row);
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? folder = await FileDialogs.PickFolderAsync(this, "Select a folder that receives DICOM images", FolderPurpose.MonitoredPath);
                if (folder == null)
                {
                    return;
                }

                string? problem = draft.AddPath(folder);
                if (problem != null)
                {
                    MessageBox.Show(this, problem, "Add folder", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                AddPathRow(folder.Trim());
                UpdatePathsHeader();
            }
            catch (Exception ex)
            {
                // UI boundary: an async event handler must not let an exception escape.
                logger.LogError(ex, "Adding a monitored folder failed.");
                MessageBox.Show(this, "Adding a monitored folder failed: " + ex.Message, "Add folder", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Puts the edits into the template and saves it. A refused save puts the template back and keeps the window open.</summary>
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            MonitoredSettingsDraft original = MonitoredSettingsDraft.From(template_maker);
            draft.ApplyTo(template_maker);
            try
            {
                template_maker.make_template();
            }
            catch (Exception ex) when (ex is TemplateLoadException || ex is IOException || ex is UnauthorizedAccessException)
            {
                original.ApplyTo(template_maker);
                logger.LogWarning(ex, "The monitored folders of template {Template} were not saved.", template_maker.TemplateName);
                MessageBox.Show(this, ex.Message + Environment.NewLine + Environment.NewLine + "Nothing was saved; your changes are still in this window.",
                    "Template not saved", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            logger.LogInformation("Saved template {Template}: {Paths} monitored folder(s), {Requirements} DICOM requirement(s).",
                template_maker.TemplateName, template_maker.Paths.Count, draft.Requirements.Count);
            saved = true;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>Closing without saving (Cancel or the close button) discards the changes, after asking when there are any.</summary>
        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (saved || !draft.IsDirty)
            {
                return;
            }

            e.Cancel = MessageBox.Show(this, "Discard the changes to the monitored folders and DICOM requirements?", "Discard changes",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes;
        }

        private void Add_Requirement(object sender, RoutedEventArgs e)
        {
            string key = DicomTag_Combobox.SelectedItem as string ?? string.Empty;
            string value = Dicomtag_TextBox.Text;
            string? problem = draft.AddRequirement(key, value);
            if (problem != null)
            {
                EditorBrushes.ShowStatus(RequirementStatusText, new[] { problem }, null);
                return;
            }

            RequirementStackPanel.Children.Add(new DicomTagRow(key, value.Trim(), RemoveRequirement));
            Dicomtag_TextBox.Text = string.Empty;
        }

        private void TagText_Changed(object sender, TextChangedEventArgs e)
        {
            UpdateRequirementState();
        }

        private void Tag_Changed(object sender, SelectionChangedEventArgs e)
        {
            UpdateRequirementState();
        }

        private void UpdateRequirementState()
        {
            string key = DicomTag_Combobox.SelectedItem as string ?? string.Empty;
            string value = Dicomtag_TextBox.Text;
            string? problem = value.Length == 0 ? null : MonitoredSettingsDraft.RequirementProblem(draft.Requirements, key, value);
            AddDicom_Button.IsEnabled = value.Length > 0 && problem == null;
            EditorBrushes.ShowStatus(RequirementStatusText, problem == null ? Array.Empty<string>() : new[] { problem }, null);
        }
    }
}
