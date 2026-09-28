using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DicomTemplateMakerGUI.Editors;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Asks for a template's new name with the same rules as "Build template" (the reason is shown as text). The caller
    /// moves the folder when <see cref="Window.ShowDialog"/> returns true.
    /// </summary>
    public partial class RenameTemplateWindow : Window
    {
        private readonly string parentFolder;
        private readonly string previousName;

        /// <param name="previous_name">The template's current folder name.</param>
        /// <param name="parentFolder">The folder that holds the template's folder.</param>
        /// <param name="hasMonitoredFolders">True when the template has monitored folders, so its generated RTs are affected.</param>
        public RenameTemplateWindow(string previous_name, string parentFolder, bool hasMonitoredFolders)
        {
            InitializeComponent();
            previousName = previous_name;
            this.parentFolder = parentFolder;
            PreviousName_Textbox.Text = previous_name;
            if (hasMonitoredFolders)
            {
                Consequences_Text.Text = EditorMessages.RenameConsequences(previous_name);
                Consequences_Text.Visibility = Visibility.Visible;
            }

            NewName_TextBox.Text = previous_name;
            NewName_TextBox.SelectAll();
        }

        /// <summary>The name typed; valid when the dialog returned true.</summary>
        public string NewName => NewName_TextBox.Text;

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                DialogResult = false;
            }
        }

        private void Rename_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Rename_Button.IsEnabled)
            {
                DialogResult = true;
            }
        }

        private void NewName_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string name = NewName_TextBox.Text;
            string? problem = NameRules.TemplateNameProblem(name);
            if (problem == null && string.Equals(name, previousName, StringComparison.Ordinal))
            {
                problem = "Type the new name.";
            }

            // A change of case only is a rename of the same folder, which Windows reports as existing.
            bool caseOnly = string.Equals(name, previousName, StringComparison.OrdinalIgnoreCase);
            if (problem == null && !caseOnly && Directory.Exists(Path.Combine(parentFolder, name)))
            {
                problem = "A template or folder named \"" + name + "\" already exists in " + parentFolder + ".";
            }

            Rename_Button.IsEnabled = problem == null;
            EditorBrushes.ShowStatus(Status_Label, problem == null ? Array.Empty<string>() : new[] { problem }, problem == null ? NameRules.TemplateNameWarning(name) : null,
                problem == null ? "The folder becomes " + Path.Combine(parentFolder, name) + "." : null);
        }
    }
}
