using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using DicomTemplateMakerGUI.Editors;
using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    /// <summary>
    /// One entry of the ontology library in the ontology editor. Edits change the entry in memory and are reported
    /// through <c>changed</c>; the editor saves the library when the user presses Save. The row shows, as text, why
    /// the entry cannot be saved.
    /// </summary>
    internal sealed class AddOntologyRow : StackPanel
    {
        internal const double NameWidth = 240;
        internal const double CodeWidth = 170;
        internal const double SchemeWidth = 150;

        private readonly OntologyCodeClass ontology;
        private readonly List<OntologyCodeClass> ontology_list;
        private readonly Action<OntologyCodeClass> changed;
        private readonly TextBox ontology_name_textbox;
        private readonly TextBox code_value_textbox;
        private readonly TextBox code_scheme_textbox;
        private readonly CheckBox DeleteCheckBox;
        private readonly Button DeleteButton;
        private readonly TextBlock status_text;

        /// <param name="ontologyList">The whole library (for the duplicate checks).</param>
        /// <param name="changed">Called after every edit of the entry.</param>
        /// <param name="delete">Asked to delete the entry (it may ask the user first).</param>
        public AddOntologyRow(OntologyCodeClass ontology, List<OntologyCodeClass> ontologyList, Action<OntologyCodeClass> changed, Action<OntologyCodeClass> delete)
        {
            this.ontology = ontology;
            ontology_list = ontologyList;
            this.changed = changed;
            Orientation = Orientation.Vertical;
            Margin = new Thickness(0, 0, 0, 2);
            StackPanel controls = new StackPanel { Orientation = Orientation.Horizontal };
            Children.Add(controls);

            // CodeMeaning is null for a hand-edited file that says so.
            ontology_name_textbox = new TextBox { Width = NameWidth, Text = (string?)ontology.CodeMeaning ?? string.Empty, VerticalContentAlignment = VerticalAlignment.Center };
            ontology_name_textbox.TextChanged += (sender, e) => Edited(() => ontology.CodeMeaning = ontology_name_textbox.Text);
            controls.Children.Add(ontology_name_textbox);

            code_value_textbox = new TextBox { Width = CodeWidth, Text = ontology.CodeValue ?? string.Empty, VerticalContentAlignment = VerticalAlignment.Center };
            code_value_textbox.TextChanged += (sender, e) => Edited(() => ontology.CodeValue = code_value_textbox.Text);
            controls.Children.Add(code_value_textbox);

            code_scheme_textbox = new TextBox { Width = SchemeWidth, Text = ontology.Scheme ?? string.Empty, VerticalContentAlignment = VerticalAlignment.Center };
            code_scheme_textbox.TextChanged += (sender, e) => Edited(() => ontology.Scheme = code_scheme_textbox.Text);
            controls.Children.Add(code_scheme_textbox);

            controls.Children.Add(new Label { Content = "Delete?", Width = 55, Margin = new Thickness(8, 0, 0, 0) });

            DeleteCheckBox = new CheckBox { Width = 25, VerticalAlignment = VerticalAlignment.Center };
            DeleteCheckBox.Checked += CheckBox_DataContextChanged;
            DeleteCheckBox.Unchecked += CheckBox_DataContextChanged;
            controls.Children.Add(DeleteCheckBox);

            DeleteButton = new Button { Content = "Delete", Width = 75, IsEnabled = false, ToolTip = "Removes this entry from the library when you press Save." };
            DeleteButton.Click += (sender, e) => delete(ontology);
            controls.Children.Add(DeleteButton);

            status_text = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(2, 0, 0, 2) };
            Children.Add(status_text);
            UpdateStatus();
        }

        /// <summary>The column headings, with the same widths as the rows.</summary>
        internal static StackPanel Header()
        {
            StackPanel header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new Label { Content = "Common name", Width = NameWidth, FontWeight = FontWeights.SemiBold });
            header.Children.Add(new Label { Content = "Code value", Width = CodeWidth, FontWeight = FontWeights.SemiBold });
            header.Children.Add(new Label { Content = "Coding scheme", Width = SchemeWidth, FontWeight = FontWeights.SemiBold });
            return header;
        }

        private void Edited(Action apply)
        {
            apply();
            changed(ontology);
            UpdateStatus();
        }

        private void CheckBox_DataContextChanged(object sender, RoutedEventArgs e)
        {
            DeleteButton.IsEnabled = DeleteCheckBox.IsChecked == true;
        }

        private void UpdateStatus()
        {
            string? problem = OntologyEntryRules.Problem(ontology.CodeMeaning, ontology.CodeValue, ontology.Scheme, ontology_list, ontology);
            string? warning = problem == null ? OntologyEntryRules.Warning(ontology.CodeMeaning, ontology_list, ontology) : null;
            EditorBrushes.ShowStatus(status_text, problem == null ? Array.Empty<string>() : new[] { problem }, warning);
            if (problem != null && string.IsNullOrWhiteSpace(ontology.CodeValue))
            {
                code_value_textbox.BorderBrush = EditorBrushes.Error;
            }
            else
            {
                code_value_textbox.ClearValue(Control.BorderBrushProperty);
            }
        }
    }
}
