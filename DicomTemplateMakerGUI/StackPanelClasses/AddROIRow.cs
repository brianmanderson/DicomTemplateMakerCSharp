using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DicomTemplateMakerGUI.Editors;
using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    /// <summary>
    /// One ROI in the template editor. Every change goes to the ROI in memory and is reported through the
    /// <c>changed</c> callback; the row never writes files (the window saves the template).
    /// </summary>
    internal sealed class AddROIRow : StackPanel
    {
        internal const double IncludeWidth = 50;
        internal const double NameWidth = 200;
        internal const double OntologyWidth = 175;
        internal const double TypeWidth = 150;
        internal const double ColourWidth = 75;
        internal const double LinkWidth = 75;
        internal const double LineStyleWidth = 75;
        internal const double DeleteLabelWidth = 55;
        internal const double DeleteCheckWidth = 25;
        internal const double DeleteButtonWidth = 75;

        private static readonly IReadOnlyList<string> DvhLineStyles = new[] { "solid", "-------", "*******", "-*-*-*-", "-**-**-" };

        private readonly ROIClass roi;
        private readonly List<ROIClass> roi_list;
        private readonly Action changed;
        private readonly TextBox roi_name_textbox;
        private readonly ComboBox ontology_combobox;
        private readonly Button color_button;
        private readonly Button dvh_color_button;
        private readonly CheckBox DeleteCheckBox;
        private readonly Button DeleteButton;
        private readonly TextBlock status_text;
        private string? rename_problem;

        /// <param name="roiList">The template's ROIs (the row removes its ROI from it when deleted, and checks names against it).</param>
        /// <param name="ontologies">The codes offered.</param>
        /// <param name="changed">Called after every change to the ROI.</param>
        public AddROIRow(List<ROIClass> roiList, ROIClass roi, List<OntologyCodeClass> ontologies, Action changed)
        {
            this.roi = roi;
            roi_list = roiList;
            this.changed = changed;
            Orientation = Orientation.Vertical;
            Margin = new Thickness(0, 0, 0, 2);
            StackPanel controls = new StackPanel { Orientation = Orientation.Horizontal };
            Children.Add(controls);

            CheckBox included_checkbox = new CheckBox
            {
                Width = IncludeWidth,
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = roi.Include,
                ToolTip = "Ticked: recommended ROI. Not ticked: optional ROI (Airtable's recommended and optional lists).",
            };
            included_checkbox.Click += (sender, e) =>
            {
                roi.Include = included_checkbox.IsChecked == true;
                changed();
            };
            controls.Children.Add(included_checkbox);

            // Newtonsoft leaves ROIName null for a hand-edited file that says "ROIName": null.
            roi_name_textbox = new TextBox { Width = NameWidth, Text = (string?)roi.ROIName ?? string.Empty, VerticalContentAlignment = VerticalAlignment.Center };
            roi_name_textbox.TextChanged += TextValueChange;
            controls.Children.Add(roi_name_textbox);

            ontology_combobox = new ComboBox { Width = OntologyWidth, ItemsSource = ontologies, DisplayMemberPath = nameof(OntologyCodeClass.CodeMeaning) };
            ontology_combobox.SelectedItem = FindOntology(ontologies, roi.Ontology_Class);
            ontology_combobox.ToolTip = DescribeCode(roi.Ontology_Class);
            ontology_combobox.SelectionChanged += Ontology_SelectionChanged;
            controls.Children.Add(ontology_combobox);

            // Set before the handler is attached: showing a type does not change it (a Varian file's "Organ" stays as it is).
            ComboBox roi_interp_combobox = new ComboBox { Width = TypeWidth, ItemsSource = InterpretedTypes.All };
            roi_interp_combobox.SelectedItem = InterpretedTypes.Find(roi.ROI_Interpreted_type);
            roi_interp_combobox.SelectionChanged += (sender, e) =>
            {
                if (roi_interp_combobox.SelectedItem is string type)
                {
                    roi.ROI_Interpreted_type = type;
                    changed();
                    UpdateStatus();
                }
            };
            controls.Children.Add(roi_interp_combobox);

            color_button = new Button { Width = ColourWidth, Background = RoiBrushes.Fill(roi), ToolTip = "ROI colour: click to change." };
            color_button.Click += color_button_Click;
            controls.Children.Add(color_button);

            dvh_color_button = new Button { Width = ColourWidth, Background = RoiBrushes.DvhLine(roi), ToolTip = "DVH line colour: click to change." };
            dvh_color_button.Click += dvh_color_button_Click;
            controls.Children.Add(dvh_color_button);

            Button link_button = new Button { Width = LinkWidth, Content = "Link", ToolTip = "Make the DVH line follow the ROI colour." };
            link_button.Click += link_button_Click;
            controls.Children.Add(link_button);

            ComboBox roi_dvh_line_style_combobox = new ComboBox { Width = LineStyleWidth, ItemsSource = DvhLineStyles };
            // DVHLineStyle is null for a hand-edited file that says so.
            if (DvhLineStyles.Contains((string?)roi.DVHLineStyle))
            {
                roi_dvh_line_style_combobox.SelectedItem = roi.DVHLineStyle;
            }

            roi_dvh_line_style_combobox.SelectionChanged += (sender, e) =>
            {
                if (roi_dvh_line_style_combobox.SelectedItem is string style)
                {
                    roi.DVHLineStyle = style;
                    changed();
                }
            };
            controls.Children.Add(roi_dvh_line_style_combobox);

            controls.Children.Add(new Label { Content = "Delete?", Width = DeleteLabelWidth });

            DeleteCheckBox = new CheckBox { Width = DeleteCheckWidth, VerticalAlignment = VerticalAlignment.Center };
            DeleteCheckBox.Checked += CheckBox_DataContextChanged;
            DeleteCheckBox.Unchecked += CheckBox_DataContextChanged;
            controls.Children.Add(DeleteCheckBox);

            DeleteButton = new Button { Content = "Delete", Width = DeleteButtonWidth, IsEnabled = false, ToolTip = "Removes this ROI from the template when the template is saved." };
            DeleteButton.Click += DeleteButton_Click;
            controls.Children.Add(DeleteButton);

            status_text = new TextBlock { Margin = new Thickness(IncludeWidth, 0, 0, 2), TextWrapping = TextWrapping.Wrap, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
            Children.Add(status_text);
            UpdateStatus();
        }

        /// <summary>The column headings, with the same widths as the rows.</summary>
        internal static StackPanel Header()
        {
            StackPanel header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(HeaderLabel("Include?", IncludeWidth));
            header.Children.Add(HeaderLabel("ROI name", NameWidth));
            header.Children.Add(HeaderLabel("Ontology", OntologyWidth));
            header.Children.Add(HeaderLabel("Interpreted type", TypeWidth));
            header.Children.Add(HeaderLabel("ROI colour", ColourWidth));
            header.Children.Add(HeaderLabel("DVH colour", ColourWidth));
            header.Children.Add(HeaderLabel(string.Empty, LinkWidth));
            header.Children.Add(HeaderLabel("DVH line", LineStyleWidth));
            return header;
        }

        private static Label HeaderLabel(string text, double width)
        {
            return new Label { Content = text, Width = width, FontWeight = FontWeights.SemiBold, Padding = new Thickness(2, 5, 2, 5) };
        }

        /// <summary>The list's entry for <paramref name="code"/>: the same object, else the same code value and scheme; null when none.</summary>
        private static OntologyCodeClass? FindOntology(List<OntologyCodeClass> ontologies, OntologyCodeClass? code)
        {
            if (code == null)
            {
                return null;
            }

            return ontologies.FirstOrDefault(o => ReferenceEquals(o, code))
                ?? ontologies.FirstOrDefault(o => o.CodeValue == code.CodeValue && o.Scheme == code.Scheme);
        }

        private static string DescribeCode(OntologyCodeClass? code)
        {
            return code == null
                ? "No ontology code."
                : $"{code.CodeMeaning} (code {code.CodeValue ?? "none"}, {code.Scheme ?? "no scheme"})";
        }

        private void Ontology_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ontology_combobox.SelectedItem is OntologyCodeClass code)
            {
                roi.Ontology_Class = code;
                ontology_combobox.ToolTip = DescribeCode(code);
                changed();
                UpdateStatus();
            }
        }

        private void CheckBox_DataContextChanged(object sender, RoutedEventArgs e)
        {
            DeleteButton.IsEnabled = DeleteCheckBox.IsChecked == true;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            roi_list.Remove(roi);
            if (Parent is Panel panel)
            {
                panel.Children.Remove(this);
            }

            changed();
        }

        private void color_button_Click(object sender, RoutedEventArgs e)
        {
            using System.Windows.Forms.ColorDialog dialog = new System.Windows.Forms.ColorDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                roi.update_color(dialog.Color.R, dialog.Color.G, dialog.Color.B);
                set_button_color();
                changed();
            }
        }

        private void link_button_Click(object sender, RoutedEventArgs e)
        {
            roi.DVHLineColor = "-16777216";
            roi.build_dvh_line_color();
            set_button_color();
            changed();
        }

        private void set_button_color()
        {
            color_button.Background = RoiBrushes.Fill(roi);
            dvh_color_button.Background = RoiBrushes.DvhLine(roi);
        }

        private void dvh_color_button_Click(object sender, RoutedEventArgs e)
        {
            using System.Windows.Forms.ColorDialog dialog = new System.Windows.Forms.ColorDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                roi.update_dvh_color(dialog.Color.R, dialog.Color.G, dialog.Color.B);
                set_button_color();
                changed();
            }
        }

        /// <summary>
        /// Renames the ROI when the typed name is usable (N6: the name is set before the change is reported, so a save
        /// that follows has it). A name that is empty or taken by another ROI is not applied and the reason is shown.
        /// </summary>
        private void TextValueChange(object sender, TextChangedEventArgs e)
        {
            string text = roi_name_textbox.Text;
            rename_problem = NameRules.RoiNameProblem(text, roi_list, roi);
            if (rename_problem == null && !string.Equals(text, roi.ROIName, StringComparison.Ordinal))
            {
                roi.ROIName = text;
                changed();
            }

            UpdateStatus();
        }

        private void UpdateStatus()
        {
            var problems = new List<string>();
            if (rename_problem != null)
            {
                problems.Add("Not renamed: " + rename_problem);
            }

            string? type_problem = InterpretedTypes.Problem(roi.ROI_Interpreted_type);
            if (type_problem != null)
            {
                problems.Add(type_problem);
            }

            if (roi.Ontology_Class == null)
            {
                problems.Add("No ontology code: choose one, or generated RTs leave this ROI out.");
            }

            string? warning = NameRules.RoiNameWarning((string?)roi.ROIName);
            EditorBrushes.ShowStatus(status_text, problems, warning);
            if (rename_problem != null)
            {
                roi_name_textbox.BorderBrush = EditorBrushes.Error;
            }
            else
            {
                roi_name_textbox.ClearValue(Control.BorderBrushProperty);
            }
        }
    }
}
