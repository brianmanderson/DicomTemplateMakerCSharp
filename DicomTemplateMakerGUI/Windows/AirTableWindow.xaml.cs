using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.StackPanelClasses;
using ROIOntologyClass;
using TemplateSync.Sync;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Browses the templates offered by the shared TG-263 snapshot and the user's Airtable tables.
    /// Opening the window uses cached data; the network is only used when the cache has expired or
    /// the user presses Refresh / Full refresh.
    /// </summary>
    public partial class AirTableWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private readonly TemplateSourceCatalog catalog;
        private readonly string folder_location;
        private readonly string onto_path;
        private List<AddAirTableRow> default_airtable_list = new List<AddAirTableRow>();
        private CancellationTokenSource loadCancellation;
        private readonly Brush lightgreen = new SolidColorBrush(Color.FromRgb(144, 238, 144));
        private readonly Brush yellow = new SolidColorBrush(Color.FromRgb(255, 255, 0));
        private readonly Brush red = new SolidColorBrush(Color.FromRgb(255, 0, 0));
        private readonly List<string> languages = new List<string> { "English/Ingles/Anglais", "Spanish/Espanol/Espagnol", "French/Frances/Francais" };

        public AirTableWindow(TemplateSourceCatalog catalog, string folder_location, string onto_path)
        {
            InitializeComponent();
            this.catalog = catalog;
            this.folder_location = folder_location;
            this.onto_path = onto_path;
            Language_ComboBox.Visibility = Visibility.Hidden;
            Laterality_CheckBox.Visibility = Visibility.Hidden;
            Template_ComboBox.DisplayMemberPath = "Name";
            Template_ComboBox.ItemsSource = catalog.Sources;
            Language_ComboBox.ItemsSource = languages;
            Language_ComboBox.SelectedIndex = 0;
            Closed += (sender, e) => loadCancellation?.Cancel();
            build_combobox();
        }

        public ObservableCollection<TemplateSourceItem> AirTables
        {
            get { return catalog.Sources; }
        }

        private void build_combobox()
        {
            Template_ComboBox.SelectedIndex = -1;
            if (catalog.Sources.Count > 0)
            {
                Template_ComboBox.SelectedIndex = 0;
            }
        }

        private async void BuildTables(LoadMode mode)
        {
            TemplateSourceItem source = Template_ComboBox.SelectedItem as TemplateSourceItem;
            if (source == null)
            {
                return;
            }

            loadCancellation?.Cancel();
            loadCancellation = new CancellationTokenSource();
            CancellationToken token = loadCancellation.Token;

            BuildButton.IsEnabled = false;
            SelectAllButton.IsEnabled = false;
            RefreshButton.IsEnabled = false;
            FullRefreshButton.IsEnabled = false;
            StackDefaultAirtablePanel.Children.Clear();
            default_airtable_list = new List<AddAirTableRow>();
            CheckBoxLabel.Visibility = Visibility.Hidden;
            IncludeLabel.Visibility = Visibility.Hidden;
            TemplateNameLabel.Visibility = Visibility.Hidden;
            Laterality_CheckBox.Visibility = Visibility.Hidden;
            Language_ComboBox.Visibility = Visibility.Hidden;
            Status_Label.Content = "Status: Loading...";
            Status_Label.Background = yellow;
            Status_Label.Visibility = Visibility.Visible;
            SourceStatus_Text.Text = source.StatusText;

            var progress = new Progress<string>(message =>
            {
                if (!token.IsCancellationRequested)
                {
                    SourceStatus_Text.Text = message;
                }
            });
            try
            {
                await source.LoadAsync(mode, progress, token);
                if (token.IsCancellationRequested || !ReferenceEquals(Template_ComboBox.SelectedItem, source))
                {
                    return;
                }

                ShowSites(source);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // UI boundary: show the reason instead of crashing or failing silently.
                Status_Label.Content = "Status: Could not load templates";
                Status_Label.Background = red;
                BuildButton.Background = red;
                SourceStatus_Text.Text = ex.Message;
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    RefreshButton.IsEnabled = true;
                    FullRefreshButton.IsEnabled = true;
                }
            }
        }

        private void ShowSites(TemplateSourceItem source)
        {
            SourceStatus_Text.Text = source.StatusText;
            List<string> sites = source.Index.SiteNames.OrderBy(o => o).ToList();
            bool anyLateral = false;
            bool anyOtherLanguage = false;
            foreach (string site in sites)
            {
                List<ROIWrapper> wrappers = source.BuildRoiWrappers(site, new List<string>());
                anyLateral |= wrappers.Any(x => x.has_lateral);
                anyOtherLanguage |= wrappers.Any(x => x.has_other_lanuages);
                AddAirTableRow atrow = new AddAirTableRow(site, source, wrappers.Count);
                AddRow(atrow);
                default_airtable_list.Add(atrow);
            }

            Laterality_CheckBox.Visibility = anyLateral ? Visibility.Visible : Visibility.Hidden;
            Language_ComboBox.Visibility = anyOtherLanguage ? Visibility.Visible : Visibility.Hidden;
            if (sites.Count > 0)
            {
                BuildButton.IsEnabled = true;
                SelectAllButton.IsEnabled = true;
                BuildButton.Background = lightgreen;
                Status_Label.Content = "Ready!";
                Status_Label.Visibility = Visibility.Hidden;
                CheckBoxLabel.Visibility = Visibility.Visible;
                IncludeLabel.Visibility = Visibility.Visible;
                TemplateNameLabel.Visibility = Visibility.Visible;
            }
            else
            {
                Status_Label.Content = source.HasData ? "No templates found!" : "Nothing downloaded yet";
                BuildButton.IsEnabled = false;
                SelectAllButton.IsEnabled = false;
                Status_Label.Background = red;
                Status_Label.Visibility = Visibility.Visible;
            }
        }

        private void AddRow(AddAirTableRow row)
        {
            Border myborder = new Border();
            myborder.Background = Brushes.Black;
            myborder.BorderThickness = new Thickness(5);
            StackDefaultAirtablePanel.Children.Add(myborder);
            StackDefaultAirtablePanel.Children.Add(row);
        }

        private void Build_button_click(object sender, RoutedEventArgs e)
        {
            var warnings = new List<string>();
            try
            {
                foreach (AddAirTableRow row in default_airtable_list)
                {
                    if (row.check_box.IsChecked != true)
                    {
                        continue;
                    }

                    TemplateMaker evaluator = new TemplateMaker();
                    evaluator.set_onto_path(Path.Combine(folder_location, "Ontologies"));
                    evaluator.define_output(Path.Combine(folder_location, row.site_name));
                    List<ROIWrapper> wrappers = row.airtable.BuildRoiWrappers(row.site_name, warnings);
                    ApplyNamingChoice(wrappers);
                    evaluator.ROIs = wrappers.Select(x => x.roi).ToList();
                    evaluator.make_template();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Building the templates failed: " + ex.Message, "Build templates", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (warnings.Count > 0)
            {
                List<string> distinct = warnings.Distinct().ToList();
                string text = string.Join(Environment.NewLine, distinct.Take(30));
                if (distinct.Count > 30)
                {
                    text += Environment.NewLine + "... and " + (distinct.Count - 30) + " more.";
                }
                MessageBox.Show(this, "The templates were built with these notes:" + Environment.NewLine + Environment.NewLine + text, "Build templates", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            Close();
        }

        /// <summary>Applies the language and laterality choices exactly as before.</summary>
        private void ApplyNamingChoice(List<ROIWrapper> wrappers)
        {
            string language = (string)Language_ComboBox.SelectedItem ?? string.Empty;
            bool languageVisible = Language_ComboBox.Visibility == Visibility.Visible;
            bool lateralityVisible = Laterality_CheckBox.Visibility == Visibility.Visible;
            bool reverse = Laterality_CheckBox.IsChecked == true;
            foreach (ROIWrapper r_wrapper in wrappers)
            {
                if (languageVisible)
                {
                    if (language.Contains("French"))
                    {
                        if (lateralityVisible) { r_wrapper.Set_French(reverse); } else { r_wrapper.Set_French(); }
                    }
                    else if (language.Contains("Spanish"))
                    {
                        if (lateralityVisible) { r_wrapper.Set_Spanish(reverse); } else { r_wrapper.Set_Spanish(); }
                    }
                    else if (language.Contains("English"))
                    {
                        if (lateralityVisible) { r_wrapper.Set_English(reverse); } else { r_wrapper.Set_English(); }
                    }
                }
                else if (lateralityVisible)
                {
                    r_wrapper.Set_English(reverse);
                }
            }
        }

        private void SearchTextUpdate(object sender, TextChangedEventArgs e)
        {
            StackDefaultAirtablePanel.Children.Clear();
            string search = SearchBox_TextBox.Text.ToLowerInvariant();
            foreach (AddAirTableRow template_row in default_airtable_list)
            {
                if (template_row.site_label.Content.ToString().ToLowerInvariant().Contains(search))
                {
                    AddRow(template_row);
                }
            }
        }

        private void Template_ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Delete_CheckBox.IsChecked = false;
            DeleteButton.IsEnabled = false;
            TemplateSourceItem source = Template_ComboBox.SelectedItem as TemplateSourceItem;
            Delete_CheckBox.IsEnabled = source != null && source.IsWritable;
            if (source != null)
            {
                BuildTables(LoadMode.IfStale);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            BuildTables(LoadMode.Refresh);
        }

        private void FullRefresh_Click(object sender, RoutedEventArgs e)
        {
            BuildTables(LoadMode.FullRefresh);
        }

        private void DeleteTemplate_Click(object sender, RoutedEventArgs e)
        {
            DeleteButton.IsEnabled = false;
            Delete_CheckBox.IsChecked = false;
            TemplateSourceItem at = Template_ComboBox.SelectedItem as TemplateSourceItem;
            if (at == null || !at.IsWritable)
            {
                return;
            }

            MessageBoxResult answer = MessageBox.Show(
                this,
                "Remove the connection to '" + at.Name + "'?" + Environment.NewLine + Environment.NewLine +
                "This deletes the saved token and the cached copy on this computer. The Airtable table itself is not changed.",
                "Remove Airtable table",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                catalog.RemoveConnection(at);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not remove the connection: " + ex.Message, "Remove Airtable table", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            build_combobox();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            DeleteButton.IsEnabled = true;
        }
        private void CheckBox_UnChecked(object sender, RoutedEventArgs e)
        {
            DeleteButton.IsEnabled = false;
        }

        private void AddAirTable_Click(object sender, RoutedEventArgs e)
        {
            AddAirTableTemplate at_window = new AddAirTableTemplate(catalog);
            at_window.Owner = this;
            at_window.ShowDialog();
            if (at_window.AddedSource != null)
            {
                Template_ComboBox.SelectedItem = at_window.AddedSource;
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (AddAirTableRow row in default_airtable_list)
            {
                row.check_box.IsChecked = true;
            }
        }
    }
}
