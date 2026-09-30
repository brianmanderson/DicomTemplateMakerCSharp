using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
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
    /// N15: converts codes between FMA and SNOMED CT in the listed templates and the ontology library. Convert is enabled
    /// only for two different schemes with something to convert; the confirmation gives the counts and the report says
    /// what changed. The work runs off the UI thread (<see cref="OntologySchemeConverter.Apply"/>).
    /// </summary>
    public partial class ChangeOntologyWindow : Window
    {
        private const string WindowTitle = "Change ontology scheme";
        private readonly ILogger<ChangeOntologyWindow> logger = AppLog.For<ChangeOntologyWindow>();
        private readonly string onto_path;
        private readonly List<AddTemplateRow> template_rows;
        private FMAID_SNOMED_OntologyClass? table;
        private bool busy;

        public ChangeOntologyWindow(List<AddTemplateRow> template_rows, string onto_path)
        {
            InitializeComponent();
            this.template_rows = template_rows;
            this.onto_path = onto_path;
            From_ComboBox.ItemsSource = OntologySchemeConverter.Choices;
            To_ComboBox.ItemsSource = OntologySchemeConverter.Choices;
            UpdateState();
        }

        /// <summary>True once a conversion ran; the caller should read the templates again.</summary>
        public bool Converted { get; private set; }

        private List<TemplateMaker> Templates => template_rows.Select(row => row.templateMaker).ToList();

        private void ToFromComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateState();
        }

        /// <summary>Enables Convert only for two different schemes with something to convert, and says why otherwise.</summary>
        private void UpdateState()
        {
            ConverterButton.IsEnabled = false;
            if (From_ComboBox.SelectedItem is not SchemeChoice from || To_ComboBox.SelectedItem is not SchemeChoice to)
            {
                EditorBrushes.ShowStatus(StatusText, Array.Empty<string>(), null, "Choose the scheme to convert from and the scheme to convert to.");
                return;
            }

            if (!OntologySchemeConverter.CanConvert(from.Designator, to.Designator))
            {
                EditorBrushes.ShowStatus(StatusText, new[] { "Choose two different schemes: converting " + from.DisplayName + " into itself would corrupt codes that are already " + from.DisplayName + "." }, null);
                return;
            }

            if (!TryPlan(from, to, out SchemeConversionPlan? plan, out _, out string? error))
            {
                EditorBrushes.ShowStatus(StatusText, new[] { error }, null);
                return;
            }

            if (plan.IsEmpty)
            {
                EditorBrushes.ShowStatus(StatusText, Array.Empty<string>(), null, EditorMessages.NothingToConvert(plan, from.DisplayName, to.DisplayName));
                return;
            }

            ConverterButton.IsEnabled = !busy;
            EditorBrushes.ShowStatus(StatusText, Array.Empty<string>(), null,
                $"{plan.RoiCount} ROI code(s) in {plan.TemplateCount} template(s) and {plan.LibraryEntryCount} library entr{(plan.LibraryEntryCount == 1 ? "y" : "ies")} would change.");
        }

        /// <summary>Counts what converting would change, with the code table it would use; false with a reason when the table or the library cannot be read.</summary>
        private bool TryPlan(SchemeChoice from, SchemeChoice to, [NotNullWhen(true)] out SchemeConversionPlan? plan, [NotNullWhen(true)] out IReadOnlyDictionary<string, string>? map, [NotNullWhen(false)] out string? error)
        {
            plan = null;
            map = null;
            if (!TryLoadTable(out FMAID_SNOMED_OntologyClass? loaded, out error))
            {
                return false;
            }

            List<OntologyCodeClass> library;
            try
            {
                library = OntologyTools.LoadOntologiesFromFolder(onto_path);
            }
            catch (Exception ex) when (ex is TemplateLoadException || ex is IOException || ex is UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "The ontology library in {Path} could not be read for a scheme conversion.", onto_path);
                error = ex.Message + " Nothing can be converted until the ontology library is repaired or removed.";
                return false;
            }

            map = OntologySchemeConverter.MapFrom(loaded, from.Designator);
            plan = OntologySchemeConverter.Plan(Templates, library, from.Designator, to.Designator, map);
            return true;
        }

        private bool TryLoadTable([NotNullWhen(true)] out FMAID_SNOMED_OntologyClass? loaded, [NotNullWhen(false)] out string? error)
        {
            try
            {
                table ??= new FMAID_SNOMED_OntologyClass();
                loaded = table;
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is IndexOutOfRangeException || ex is ArgumentOutOfRangeException)
            {
                logger.LogError(ex, "The FMA to SNOMED CT table {Path} could not be read.", FMAID_SNOMED_OntologyClass.DefaultPath);
                loaded = null;
                error = "The FMA to SNOMED CT table " + FMAID_SNOMED_OntologyClass.DefaultPath + " could not be read: " + ex.Message;
                return false;
            }
        }

        private async void ConverterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (From_ComboBox.SelectedItem is not SchemeChoice from || To_ComboBox.SelectedItem is not SchemeChoice to
                    || !OntologySchemeConverter.CanConvert(from.Designator, to.Designator))
                {
                    UpdateState();
                    return;
                }

                if (!TryPlan(from, to, out SchemeConversionPlan? plan, out IReadOnlyDictionary<string, string>? map, out string? error))
                {
                    MessageBox.Show(this, error, WindowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (plan.IsEmpty)
                {
                    MessageBox.Show(this, EditorMessages.NothingToConvert(plan, from.DisplayName, to.DisplayName), WindowTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (MessageBox.Show(this, EditorMessages.ConfirmSchemeConversion(plan, from.DisplayName, to.DisplayName), WindowTitle,
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                {
                    return;
                }

                List<TemplateMaker> templates = Templates;
                string fromScheme = from.Designator;
                string toScheme = to.Designator;
                busy = true;
                RootPanel.IsEnabled = false;
                EditorBrushes.ShowStatus(StatusText, Array.Empty<string>(), null, "Converting…");
                logger.LogInformation("Converting ontology codes from {From} to {To}: {Rois} ROI(s) in {Templates} template(s), {Entries} library entries.",
                    fromScheme, toScheme, plan.RoiCount, plan.TemplateCount, plan.LibraryEntryCount);
                SchemeConversionResult result;
                try
                {
                    result = await Task.Run(() => OntologySchemeConverter.Apply(templates, onto_path, fromScheme, toScheme, map, logger));
                }
                finally
                {
                    // Whatever happened, the templates in memory may differ from the files now.
                    Converted = true;
                }

                string report = EditorMessages.DescribeSchemeConversion(result, from.DisplayName, to.DisplayName);
                MessageBox.Show(this, report, WindowTitle, MessageBoxButton.OK, result.Failures.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex) when (ex is TemplateLoadException || ex is IOException || ex is UnauthorizedAccessException)
            {
                // The library is read and saved before any template changes (see OntologySchemeConverter.Apply).
                logger.LogError(ex, "The ontology scheme conversion stopped.");
                MessageBox.Show(this, ex.Message + Environment.NewLine + Environment.NewLine + "The conversion stopped before any template was changed.",
                    WindowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                // UI boundary.
                logger.LogError(ex, "The ontology scheme conversion failed.");
                MessageBox.Show(this, "The conversion failed: " + ex.Message + Environment.NewLine + Environment.NewLine + "The details are in the log.",
                    WindowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                busy = false;
                RootPanel.IsEnabled = true;
                UpdateState();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            // Closing while templates are being saved would hide the report.
            e.Cancel = busy;
        }
    }
}
