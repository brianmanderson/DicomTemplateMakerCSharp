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
using DicomTemplateMakerGUI.DicomTemplateServices;
using DicomTemplateMakerGUI.Editors;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.Shell;
using DicomTemplateMakerGUI.StackPanelClasses;
using DicomTemplateMakerGUI.Windows;
using Microsoft.Extensions.Logging;
using ROIOntologyClass;
using TemplateSync.Sync;

namespace DicomTemplateMakerGUI
{
    /// <summary>
    /// The main window: the template list, the background RT generator and the bulk actions. Long work runs off the UI
    /// thread (see <see cref="RunBusyAsync{T}"/>); the logic and texts live in DicomTemplateMaker.Presentation.
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private static readonly TimeSpan RunnerInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan ShareProbeTimeout = TimeSpan.FromSeconds(5);

        private readonly ILogger<MainWindow> logger = AppLog.For<MainWindow>();
        private readonly UiSettingsStore settingsStore;
        private readonly RunnerService runnerService;
        private readonly RunnerStatus runnerStatus = new RunnerStatus();
        private readonly IProgress<RunReport> runnerProgress;
        private readonly ProblemTracker problemTracker = new ProblemTracker();
        private readonly List<string> startupNotes = new List<string>();
        private readonly Stack<string> busyTexts = new Stack<string>();
        private readonly Brush lightgreen = new SolidColorBrush(Color.FromRgb(144, 238, 144));
        private readonly Brush lightgray = new SolidColorBrush(Color.FromRgb(221, 221, 221));
        private string templateRoot;
        private string ontoPath;
        private TemplateSourceCatalog catalog;
        private ObservableCollection<TemplateSourceItem> airtables = new ObservableCollection<TemplateSourceItem>();
        private ObservableCollection<TemplateSourceItem> writeable_airtables = new ObservableCollection<TemplateSourceItem>();
        private List<AddTemplateRow> template_rows = new List<AddTemplateRow>();
        private List<AddTemplateRow> visible_template_rows = new List<AddTemplateRow>();
        private IReadOnlyList<TemplateProblem> currentProblems = Array.Empty<TemplateProblem>();
        private int blockingOperations;
        private int rebuildVersion;
        private bool closeAfterStop;

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

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <param name="settingsStore">The window settings (template folder, remembered folders).</param>
        /// <param name="rootResolution">The template folder chosen at startup.</param>
        /// <param name="logProblem">Why logging is off, to tell the user once; null when it works.</param>
        public MainWindow(UiSettingsStore settingsStore, TemplateRootResolution rootResolution, string? logProblem)
        {
            InitializeComponent();
            this.settingsStore = settingsStore;
            templateRoot = rootResolution.Root;
            ontoPath = Path.Combine(templateRoot, TemplateRootResolver.OntologiesFolderName);
            AddNote(rootResolution.Note);
            AddNote(settingsStore.LoadProblem);
            AddNote(logProblem);
            runnerService = new RunnerService(templateRoot, CreateRunner, AppLog.For<RunnerService>(), RunnerInterval);
            // Created on the UI thread, so every cycle's report is handled there.
            runnerProgress = new Progress<RunReport>(OnRunnerReport);
            load_airtables();
            load_writeable_airtables();
            ShowTemplateRoot();
            UpdateTemplateButtons();
            UpdateRunnerControls();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static DicomTemplateRunner CreateRunner(string root)
        {
            return new DicomTemplateRunner(root, AppPaths.TemplateRsFile, AppLog.For<DicomTemplateRunner>(), TimeProvider.System);
        }

        private void AddNote(string? note)
        {
            if (!string.IsNullOrWhiteSpace(note))
            {
                startupNotes.Add(note);
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

        [MemberNotNull(nameof(catalog))]
        public void load_airtables()
        {
            catalog = new TemplateSourceCatalog(new DpapiTokenProtector());
            try
            {
                // Old plain-text token files were kept in the working directory.
                catalog.Initialize(Environment.CurrentDirectory);
            }
            catch (Exception ex)
            {
                // Boundary: startup must never fail because of online-template settings.
                logger.LogWarning(ex, "Online templates could not be set up.");
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
                await RebuildTemplateListAsync();
                if (startupNotes.Count > 0)
                {
                    Dialogs.Info(this, "DICOM RT Template Maker", string.Join(Environment.NewLine + Environment.NewLine, startupNotes));
                }

                if (catalog.Migration.Imported.Count > 0 || catalog.Migration.Retired.Count > 0 || catalog.Migration.Problems.Count > 0 || catalog.Problems.Count > 0)
                {
                    Dialogs.Info(this, "Airtable settings", DescribeStartupNotes());
                }
            }
            catch (Exception ex)
            {
                // UI boundary.
                ReportUnexpected("Starting", ex);
            }

            try
            {
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
                logger.LogWarning(ex, "Could not read the saved online template copies.");
                Dialogs.Warning(this, "Online templates", "Could not read the saved template copies: " + ex.Message);
            }
        }

        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (closeAfterStop)
            {
                return;
            }

            if (blockingOperations > 0)
            {
                e.Cancel = true;
                Dialogs.Info(this, "Please wait", "\"" + (busyTexts.Count > 0 ? busyTexts.Peek() : "An operation") + "\" is still running. Close the program when it has finished.");
                return;
            }

            if (runnerService.State == RunnerState.Stopped)
            {
                return;
            }

            // Stop the RT generator and wait for the scan in progress before the process ends.
            e.Cancel = true;
            IsEnabled = false;
            RunnerStatusText.Text = "Stopping the RT generator…";
            try
            {
                await runnerService.StopAsync();
            }
            catch (Exception ex)
            {
                // UI boundary: closing must go on.
                logger.LogError(ex, "The RT generator did not stop cleanly.");
            }

            closeAfterStop = true;
            Close();
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

        /// <summary>Gives <paramref name="evaluator"/> the ontology library; throws TemplateLoadException when it cannot be read.</summary>
        private void LoadOntologies(TemplateMaker evaluator)
        {
            evaluator.Ontologies = OntologyTools.LoadOntologiesFromFolder(ontoPath);
            evaluator.Ontologies.Sort((p, q) => p.CodeMeaning.CompareTo(q.CodeMeaning));
        }

        // ---------------------------------------------------------------- busy state

        /// <summary>
        /// Runs <paramref name="work"/> with the actions and the template list disabled and <paramref name="text"/> in
        /// the status bar. Unless <paramref name="canAbandon"/> is true, the window cannot be closed meanwhile.
        /// Calls may nest.
        /// </summary>
        private async Task<T> RunBusyAsync<T>(string text, Func<Task<T>> work, bool canAbandon = false)
        {
            EnterBusy(text, canAbandon);
            try
            {
                return await work();
            }
            finally
            {
                LeaveBusy(canAbandon);
            }
        }

        private async Task RunBusyAsync(string text, Func<Task> work, bool canAbandon = false)
        {
            await RunBusyAsync(text, async () =>
            {
                await work();
                return true;
            }, canAbandon);
        }

        private void EnterBusy(string text, bool canAbandon)
        {
            busyTexts.Push(text);
            if (!canAbandon)
            {
                blockingOperations++;
            }

            ActionsGrid.IsEnabled = false;
            TemplateScrollViewer.IsEnabled = false;
            ChangeTemplateButton.IsEnabled = false;
            BusyText.Text = text;
            BusyProgress.Visibility = Visibility.Visible;
        }

        private void LeaveBusy(bool canAbandon)
        {
            busyTexts.Pop();
            if (!canAbandon)
            {
                blockingOperations--;
            }

            if (busyTexts.Count > 0)
            {
                BusyText.Text = busyTexts.Peek();
                return;
            }

            ActionsGrid.IsEnabled = true;
            TemplateScrollViewer.IsEnabled = true;
            ChangeTemplateButton.IsEnabled = true;
            BusyText.Text = string.Empty;
            BusyProgress.Visibility = Visibility.Collapsed;
            UpdateRunnerControls();
        }

        /// <summary>Progress messages for the status bar; create on the UI thread.</summary>
        private IProgress<string> BusyProgressReporter()
        {
            return new Progress<string>(message =>
            {
                if (busyTexts.Count > 0)
                {
                    BusyText.Text = message;
                }
            });
        }

        private void ReportUnexpected(string action, Exception ex)
        {
            logger.LogError(ex, "{Action} failed.", action);
            Dialogs.Error(this, action, action + " failed: " + ex.Message + Environment.NewLine + Environment.NewLine
                + "The details are in the log (use \"Open log folder\").");
        }

        // ---------------------------------------------------------------- template list

        private void ShowTemplateRoot()
        {
            TemplateBaseLabel.Text = templateRoot;
            TemplateBaseLabel.ToolTip = "Templates are read from and saved to " + templateRoot + Environment.NewLine
                + "The ontology library is in " + ontoPath;
        }

        /// <summary>Reads the template folder off the UI thread and rebuilds the list, keeping the selection by name.</summary>
        private async Task RebuildTemplateListAsync()
        {
            int version = ++rebuildVersion;
            string root = templateRoot;
            HashSet<string> selected = new HashSet<string>(template_rows.Where(IsSelected).Select(RowName), StringComparer.Ordinal);
            TemplateScanResult result = await RunBusyAsync(
                "Reading templates…",
                () => Task.Run(() => TemplateLibraryScanner.Scan(root, AppLog.For("DicomTemplateMakerGUI.Shell.TemplateLibraryScanner"))),
                canAbandon: true);
            if (version != rebuildVersion || root != templateRoot)
            {
                // A newer rebuild, or another template folder, took over.
                return;
            }

            var rows = new List<AddTemplateRow>();
            foreach (TemplateMaker template in result.Templates)
            {
                var row = new AddTemplateRow(template, AirTables);
                row.SelectCheckBox.IsChecked = selected.Contains(RowName(row));
                rows.Add(row);
            }

            template_rows = rows;
            UpdateText();
            UpdateTemplateButtons();
            ShowProblems(result.Problems);
        }

        private void ShowProblems(IReadOnlyList<TemplateProblem> problems)
        {
            currentProblems = problems;
            ProblemsPanel.Visibility = problems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            ProblemsText.Text = ShellMessages.ProblemsLine(problems);
            ProblemsText.ToolTip = problems.Count == 0 ? null : ShellMessages.ProblemsDetails(problems, 10);
            IReadOnlyList<TemplateProblem> fresh = problemTracker.TakeNew(problems);
            foreach (TemplateProblem problem in fresh)
            {
                logger.LogWarning("Template folder problem: {Problem}", problem.Describe());
            }

            if (fresh.Count > 0 && IsLoaded)
            {
                Dialogs.Warning(this, "Template problems", ShellMessages.NewProblems(fresh));
            }
        }

        private void ProblemsDetails_Click(object sender, RoutedEventArgs e)
        {
            Dialogs.Warning(this, "Template problems", ShellMessages.ProblemsDetails(currentProblems));
        }

        private void UpdateTemplateButtons()
        {
            bool any = template_rows.Count > 0;
            AddTemplateButton.Background = any ? lightgray : lightgreen;
            MakeRTFolderButton.IsEnabled = any;
            MakeVarianXmlFolderButton.IsEnabled = any;
            DeleteROIs_Button.IsEnabled = any;
            UpdateRunnerControls();
        }

        private void DisplayRows()
        {
            visible_template_rows = visible_template_rows.OrderBy(x => x.template_name, StringComparer.CurrentCultureIgnoreCase).ToList();
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
            bool selectedOnly = Selected_CheckBox.IsChecked == true;
            visible_template_rows = template_rows
                .Where(row => selectedOnly ? IsSelected(row) : TemplateSearch.Matches(row.templateMaker.TemplateName, SearchBox_TextBox.Text))
                .ToList();
            DisplayRows();
        }

        private static bool IsSelected(AddTemplateRow row)
        {
            return row.SelectCheckBox.IsChecked == true;
        }

        /// <summary>
        /// The row's template name. Rows are built for folders recognised as templates, which names them; if that ever
        /// does not hold, this fails here rather than acting on the wrong folder.
        /// </summary>
        private static string RowName(AddTemplateRow row)
        {
            return row.templateMaker.TemplateName ?? throw new InvalidOperationException("A template row has no template name.");
        }

        private SelectionScope<AddTemplateRow> CurrentSelection()
        {
            HashSet<AddTemplateRow> visible = new HashSet<AddTemplateRow>(visible_template_rows);
            return SelectionScope<AddTemplateRow>.Of(template_rows, IsSelected, visible.Contains);
        }

        /// <summary>
        /// The rows a bulk action applies to: the selected rows shown, or, when none is selected, all templates if the
        /// user agrees. Null when the user declines or there are no templates.
        /// </summary>
        private List<AddTemplateRow>? ChooseTargets(string title, string action, out int hiddenSelected)
        {
            SelectionScope<AddTemplateRow> scope = CurrentSelection();
            hiddenSelected = scope.HiddenSelected.Count;
            if (scope.VisibleSelected.Count > 0)
            {
                return scope.VisibleSelected.ToList();
            }

            if (template_rows.Count == 0)
            {
                Dialogs.Info(this, title, "There are no templates in " + templateRoot + ".");
                return null;
            }

            if (!Dialogs.Confirm(this, title, ShellMessages.UseAllTemplates(action, template_rows.Count, hiddenSelected)))
            {
                return null;
            }

            hiddenSelected = 0;
            return template_rows.ToList();
        }

        private void SearchTextUpdate(object sender, TextChangedEventArgs e)
        {
            UpdateText();
        }

        private void Selected_DataContextChanged(object sender, RoutedEventArgs e)
        {
            UpdateText();
        }

        private void SelectAll_Button_Click(object sender, RoutedEventArgs e)
        {
            foreach (AddTemplateRow row in visible_template_rows)
            {
                row.SelectCheckBox.IsChecked = true;
            }
        }

        private void UnselectAll_Button_Click(object sender, RoutedEventArgs e)
        {
            foreach (AddTemplateRow row in visible_template_rows)
            {
                row.SelectCheckBox.IsChecked = false;
            }
        }

        private void CheckBox_DataContextChanged(object sender, RoutedEventArgs e)
        {
            Copy_Selected_Button.IsEnabled = Copy_CheckBox.IsChecked == true;
            Deleted_Selected_Button.IsEnabled = Delete_Checkbox.IsChecked == true;
        }

        // ---------------------------------------------------------------- template folder

        private async void ChangeTemplateClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string? picked = await FileDialogs.PickFolderAsync(this, "Select the folder that holds your templates", templateRoot);
                if (picked == null)
                {
                    return;
                }

                string root = TemplateRootResolver.Normalize(picked);
                if (TemplateRootResolver.SameFolder(root, templateRoot))
                {
                    return;
                }

                if (!TemplateRootResolver.HasTemplateLayout(root)
                    && !Dialogs.Confirm(this, "Change template folder", ShellMessages.ConfirmFolderWithoutTemplates(root)))
                {
                    return;
                }

                // A running generator restarted on another folder writes RTs for that folder's templates at once: ask.
                bool wasRunning = runnerService.State == RunnerState.Running;
                bool keepRunning = false;
                if (wasRunning)
                {
                    MessageBoxResult answer = Dialogs.YesNoCancel(this, "Change template folder", ShellMessages.ConfirmChangeRootWhileRunning(root));
                    if (answer != MessageBoxResult.Yes && answer != MessageBoxResult.No)
                    {
                        return;
                    }

                    keepRunning = answer == MessageBoxResult.Yes;
                }

                await RunBusyAsync("Changing the template folder…", async () =>
                {
                    await runnerService.ChangeRootAsync(root, keepRunning);
                    if (wasRunning)
                    {
                        runnerStatus.Reset();
                        logger.LogInformation(keepRunning ? "The RT generator was restarted on {TemplateRoot}." : "The RT generator was stopped for the change to {TemplateRoot}.", root);
                    }

                    templateRoot = root;
                    ontoPath = Path.Combine(root, TemplateRootResolver.OntologiesFolderName);
                    ShowTemplateRoot();
                    logger.LogInformation("Template folder changed to {TemplateRoot}.", root);
                    if (!settingsStore.SetTemplateRoot(root))
                    {
                        Dialogs.Warning(this, "Change template folder", "The new template folder is used now, but it could not be saved for next time. The details are in the log.");
                    }

                    await RebuildTemplateListAsync();
                });
            }
            catch (Exception ex)
            {
                // UI boundary.
                ReportUnexpected("Changing the template folder", ex);
            }
        }

        private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            ShellLauncher.OpenFolder(this, AppPaths.LogsDirectory);
        }

        // ---------------------------------------------------------------- RT generator

        private void UpdateRunnerControls()
        {
            RunnerState state = runnerService.State;
            switch (state)
            {
                case RunnerState.Running:
                    RunDICOMServerButton.Content = "Stop RT generator";
                    RunDICOMServerButton.Background = lightgreen;
                    RunDICOMServerButton.ToolTip = "The RT generator is scanning the monitored folders every " + RunnerInterval.TotalSeconds + " s. Click to stop it.";
                    break;
                case RunnerState.Paused:
                    RunDICOMServerButton.Content = "RT generator paused…";
                    RunDICOMServerButton.ClearValue(BackgroundProperty);
                    RunDICOMServerButton.ToolTip = "Paused while another operation runs; it resumes afterwards.";
                    break;
                default:
                    RunDICOMServerButton.Content = "Start RT generator (DICOM server)";
                    RunDICOMServerButton.ClearValue(BackgroundProperty);
                    RunDICOMServerButton.ToolTip = "Scans the monitored folders of every template every " + RunnerInterval.TotalSeconds
                        + " s and writes an empty RT Structure Set next to each new image series.";
                    break;
            }

            RunDICOMServerButton.IsEnabled = state == RunnerState.Running || (state == RunnerState.Stopped && template_rows.Count > 0);
            RunnerStatusText.Text = runnerStatus.Describe(state, TimeZoneInfo.Local);
            RunnerStatusText.ToolTip = runnerStatus.DescribeErrors();
        }

        /// <summary>Says in the status bar that the RT generator is paused, before an operation pauses it.</summary>
        private void ShowPausedIfRunning()
        {
            if (runnerService.State == RunnerState.Running)
            {
                RunnerStatusText.Text = runnerStatus.Describe(RunnerState.Paused, TimeZoneInfo.Local);
            }
        }

        private void OnRunnerReport(RunReport report)
        {
            runnerStatus.Record(report);
            if (runnerService.State == RunnerState.Running)
            {
                RunnerStatusText.Text = runnerStatus.Describe(RunnerState.Running, TimeZoneInfo.Local);
                RunnerStatusText.ToolTip = runnerStatus.DescribeErrors();
            }
        }

        /// <summary>Starts or stops the background RT generator.</summary>
        private async void ClickRunDicomserver(object sender, RoutedEventArgs e)
        {
            RunDICOMServerButton.IsEnabled = false;
            try
            {
                if (runnerService.State == RunnerState.Running)
                {
                    RunnerStatusText.Text = "Stopping the RT generator…";
                    await runnerService.StopAsync();
                }
                else if (!File.Exists(AppPaths.TemplateRsFile))
                {
                    Dialogs.Error(this, "Start RT generator", "The template RT file " + AppPaths.TemplateRsFile + " is missing, so no RT can be written. Reinstall the program.");
                }
                else
                {
                    runnerStatus.Reset();
                    await runnerService.StartAsync(runnerProgress);
                }
            }
            catch (Exception ex)
            {
                // UI boundary.
                ReportUnexpected("Starting or stopping the RT generator", ex);
            }
            finally
            {
                UpdateRunnerControls();
            }
        }

        /// <summary>Deletes the RTs generated by the selected templates (N1, N2): confirmed, off the UI thread, runner paused.</summary>
        private async void DeleteROIs_Button_Click(object sender, RoutedEventArgs e)
        {
            const string title = "Delete previously generated RTs";
            try
            {
                List<AddTemplateRow>? targets = ChooseTargets(title, "Delete the generated RTs of", out int hidden);
                if (targets == null)
                {
                    return;
                }

                List<MonitoredTemplate> templates = targets.Select(row => new MonitoredTemplate(RowName(row), row.templateMaker.Paths.ToList())).ToList();
                List<string> names = templates.Where(t => t.Paths.Count > 0).Select(t => t.Name).ToList();
                if (names.Count == 0)
                {
                    Dialogs.Info(this, title, "None of these templates has monitored folders, so there are no generated RTs to delete.");
                    return;
                }

                if (!Dialogs.Confirm(this, title, ShellMessages.ConfirmDeleteGenerated(templates, hidden, runnerService.State)))
                {
                    return;
                }

                logger.LogInformation("Deleting the generated RTs of {Templates}.", string.Join(", ", names));
                // A running generator would write the deleted RTs again within one scan, so it is stopped and left stopped.
                bool generatorStopped = runnerService.State != RunnerState.Stopped;
                if (generatorStopped)
                {
                    RunnerStatusText.Text = "Stopping the RT generator…";
                }

                DeleteReport report = await RunBusyAsync(
                    "Deleting generated RTs…",
                    () => runnerService.StopThenRunAsync((runner, token) => runner.DeleteGenerated(names, token)));
                logger.LogInformation("Deleted {Deleted} generated RT(s); {Failed} could not be deleted; {Errors} path error(s).", report.Deleted.Count, report.Failed.Count, report.Errors.Count);
                if (report.HasFailures)
                {
                    Dialogs.Warning(this, title, ShellMessages.DescribeDeleteGenerated(report, generatorStopped));
                }
                else
                {
                    Dialogs.Info(this, title, ShellMessages.DescribeDeleteGenerated(report, generatorStopped));
                }
            }
            catch (Exception ex)
            {
                // UI boundary.
                ReportUnexpected(title, ex);
            }
        }

        /// <summary>Writes test RTs for the selected templates on the sample CT (N9): never changes Paths.txt, never starts the generator.</summary>
        private async void CreateFolderRT_Click(object sender, RoutedEventArgs e)
        {
            const string title = "Create folder with loadable RTs";
            try
            {
                List<AddTemplateRow>? targets = ChooseTargets(title, "Generate test RTs for", out int hidden);
                if (targets == null)
                {
                    return;
                }

                string? picked = await FileDialogs.PickFolderAsync(this, "Select where to create the folder with loadable RTs", FolderPurpose.Output);
                if (picked == null)
                {
                    return;
                }

                string outputFolder = Path.Combine(picked, "Template_Output");
                List<string> names = targets.Select(RowName).ToList();
                if (!Dialogs.Confirm(this, title, ShellMessages.ConfirmTestRts(names, outputFolder, hidden)))
                {
                    return;
                }

                IProgress<string> progress = BusyProgressReporter();
                DicomTemplateRunner runner = runnerService.CreateRunner();
                TestRtResult result = await RunBusyAsync(
                    "Writing test RTs…",
                    () => Task.Run(() => TestRtGenerator.Run(runner, names, AppPaths.SmallCtFolder, outputFolder, progress)));
                logger.LogInformation("Wrote {Count} test RT(s) into {Folder} for {Templates} template(s).", result.WrittenCount, outputFolder, names.Count);
                if (result.HasFailures)
                {
                    Dialogs.Warning(this, title, ShellMessages.DescribeTestRts(result));
                }
                else
                {
                    Dialogs.Info(this, title, ShellMessages.DescribeTestRts(result));
                }
            }
            catch (Exception ex)
            {
                // UI boundary.
                ReportUnexpected(title, ex);
            }
        }

        // ---------------------------------------------------------------- template actions

        private async void Click_Build(object sender, RoutedEventArgs e)
        {
            try
            {
                TemplateMaker template_maker = new TemplateMaker();
                template_maker.set_onto_path(ontoPath);
                try
                {
                    LoadOntologies(template_maker);
                }
                catch (TemplateLoadException ex)
                {
                    Dialogs.Error(this, "Add a new template", ex.Message + Environment.NewLine + Environment.NewLine + "No template can be made until the ontology library is repaired or removed.");
                    return;
                }

                MakeTemplateWindow template_window = new MakeTemplateWindow(templateRoot, template_maker, AirTables);
                template_window.Owner = this;
                template_window.ShowDialog();
                await RebuildTemplateListAsync();
            }
            catch (Exception ex)
            {
                // UI boundary.
                ReportUnexpected("Adding a template", ex);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow about_window = new AboutWindow();
            about_window.Owner = this;
            about_window.Show();
        }

        private async void Read_Airtable(object sender, RoutedEventArgs e)
        {
            try
            {
                AirTableWindow airtable_window = new AirTableWindow(catalog, templateRoot, ontoPath);
                airtable_window.Owner = this;
                airtable_window.ShowDialog();
                load_writeable_airtables();
                check_airtables(AirTableComboBox.SelectedItem as TemplateSourceItem);
                await RebuildTemplateListAsync();
            }
            catch (Exception ex)
            {
                // UI boundary.
                ReportUnexpected("Loading online templates", ex);
            }
        }

        private async void Add_Ontology_Button(object sender, RoutedEventArgs e)
        {
            try
            {
                // Checked before the window is built (a window whose constructor throws stays open, hidden), in the
                // folder the window reads.
                EditOntologyWindow ontology_window;
                try
                {
                    OntologyTools.LoadOntologiesFromFolder(ontoPath);
                    ontology_window = new EditOntologyWindow(templateRoot, template_rows);
                }
                catch (TemplateLoadException ex)
                {
                    Dialogs.Error(this, "Edit ontologies", ex.Message + Environment.NewLine + Environment.NewLine + "The ontology library was left unchanged. Repair or remove the file, then try again.");
                    return;
                }

                ontology_window.Owner = this;
                ontology_window.ShowDialog();
                await RebuildTemplateListAsync();
            }
            catch (Exception ex)
            {
                // UI boundary.
                ReportUnexpected("Editing ontologies", ex);
            }
        }

        private async void FMA_SNOMED_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ChangeOntologyWindow onto_window = new ChangeOntologyWindow(template_rows, ontoPath);
                onto_window.Owner = this;
                onto_window.ShowDialog();
                if (onto_window.Converted)
                {
                    // A template that could not be saved was converted in memory only; read them all again.
                    await RebuildTemplateListAsync();
                }
            }
            catch (Exception ex)
            {
                // UI boundary.
                ReportUnexpected("Changing the ontology scheme", ex);
            }
        }

        /// <summary>Moves the selected (shown) template folders to the Recycle Bin (N11).</summary>
        private async void Deleted_Selected_Button_Click(object sender, RoutedEventArgs e)
        {
            const string title = "Delete templates";
            try
            {
                SelectionScope<AddTemplateRow> scope = CurrentSelection();
                if (scope.VisibleSelected.Count == 0)
                {
                    Dialogs.Info(this, title, scope.HiddenSelected.Count > 0
                        ? "The selected templates are all hidden by the current search, so nothing is deleted. Clear the search to see them."
                        : "Select the templates to delete first.");
                    return;
                }

                // A folder without a Recycle Bin (network share, mapped or removable drive) would be deleted permanently.
                (List<(string Name, string Folder)> targets, List<NotRecyclable> refused) = RecycleBinRule.Split(
                    scope.VisibleSelected.Select(row => (RowName(row), row.TemplatePath)), RecycleBin.DriveTypeOf);
                if (targets.Count == 0)
                {
                    Dialogs.Warning(this, title, ShellMessages.DescribeNotRecyclable(refused));
                    return;
                }

                if (!Dialogs.Confirm(this, title, ShellMessages.ConfirmDeleteTemplates(targets, scope.HiddenSelected.Count, refused)))
                {
                    return;
                }

                List<string> folders = targets.Select(t => t.Folder).ToList();
                logger.LogInformation("Moving {Count} template folder(s) to the Recycle Bin: {Folders}.", folders.Count, string.Join(", ", folders));
                ShowPausedIfRunning();
                BulkFolderResult result = await RunBusyAsync(
                    "Moving templates to the Recycle Bin…",
                    () => runnerService.RunPausedAsync((_, token) => RecycleBin.SendFolders(folders, logger, token)));
                Delete_Checkbox.IsChecked = false;
                await RebuildTemplateListAsync();
                if (result.Failed.Count > 0)
                {
                    Dialogs.Warning(this, title, ShellMessages.DescribeDeleteTemplates(result));
                }
                else
                {
                    Dialogs.Info(this, title, ShellMessages.DescribeDeleteTemplates(result));
                }
            }
            catch (Exception ex)
            {
                // UI boundary.
                ReportUnexpected(title, ex);
            }
        }

        private async void Copy_Selected_Button_Click(object sender, RoutedEventArgs e)
        {
            const string title = "Copy templates";
            try
            {
                SelectionScope<AddTemplateRow> scope = CurrentSelection();
                if (scope.VisibleSelected.Count == 0)
                {
                    Dialogs.Info(this, title, "Select the templates to copy first.");
                    return;
                }

                List<(string Name, string Folder)> sources = scope.VisibleSelected.Select(row => (RowName(row), row.TemplatePath)).ToList();
                string root = templateRoot;
                (List<(string Source, string Copy)> Copies, List<string> Failures) result = await RunBusyAsync("Copying templates…", () => Task.Run(() =>
                {
                    var copies = new List<(string Source, string Copy)>();
                    var failures = new List<string>();
                    foreach ((string name, string folder) in sources)
                    {
                        try
                        {
                            // The copy gets no monitored folders, so a running generator writes nothing for it yet.
                            string copy = TemplateCopier.Copy(folder, root, name);
                            copies.Add((name, copy));
                            logger.LogInformation("Copied template {Template} to {Copy} (without monitored folders).", name, copy);
                        }
                        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                        {
                            failures.Add(name + ": " + ex.Message);
                            logger.LogError(ex, "Could not copy template {Template}.", name);
                        }
                    }

                    return (copies, failures);
                }));
                foreach (AddTemplateRow row in template_rows)
                {
                    row.SelectCheckBox.IsChecked = false;
                }

                await RebuildTemplateListAsync();
                HashSet<string> copyNames = new HashSet<string>(result.Copies.Select(c => c.Copy), StringComparer.Ordinal);
                foreach (AddTemplateRow row in template_rows.Where(r => copyNames.Contains(RowName(r))))
                {
                    row.SelectCheckBox.IsChecked = true;
                }

                if (result.Failures.Count > 0)
                {
                    Dialogs.Warning(this, title, ShellMessages.DescribeCopies(result.Copies, result.Failures));
                }
                else
                {
                    Dialogs.Info(this, title, ShellMessages.DescribeCopies(result.Copies, result.Failures));
                }
            }
            catch (Exception ex)
            {
                // UI boundary.
                ReportUnexpected(title, ex);
            }
            finally
            {
                Copy_CheckBox.IsChecked = false;
            }
        }

        // ---------------------------------------------------------------- Varian XML

        /// <summary>
        /// Picks a Varian XML folder, starting in the site share when one is set and reachable (probed off the UI thread),
        /// else in the last Varian XML folder unless that lies in the share that did not answer.
        /// </summary>
        private async Task<string?> PickVarianFolderAsync(string title)
        {
            string? share = settingsStore.Settings.VarianXmlShare;
            if (share == null)
            {
                return await FileDialogs.PickFolderAsync(this, title, FolderPurpose.VarianXml);
            }

            FolderState state = await RunBusyAsync("Checking " + share + "…", () => FolderProbe.CheckAsync(share, ShareProbeTimeout), canAbandon: true);
            if (state == FolderState.Exists)
            {
                return await FileDialogs.PickFolderAsync(this, title, FolderPurpose.VarianXml, share);
            }

            logger.LogInformation("The Varian XML share {Share} is not reachable ({State}); using the last folder instead.", share, state);
            return await FileDialogs.PickFolderAsync(this, title, FolderPurpose.VarianXml, unreachableShare: share);
        }

        private async void CreateVarianXml_Click(object sender, RoutedEventArgs e)
        {
            const string title = "Export Varian XML";
            try
            {
                List<AddTemplateRow>? targets = ChooseTargets(title, "Export", out int hidden);
                if (targets == null)
                {
                    return;
                }

                string? outputFolder = await PickVarianFolderAsync("Select where to write the Varian XML templates");
                if (outputFolder == null)
                {
                    return;
                }

                List<VarianExportItem> items = targets.Select(row => new VarianExportItem(RowName(row), row.TemplatePath, row.templateMaker.Ontologies.ToList())).ToList();
                IReadOnlyList<string> existing = VarianXmlBatch.ExistingExportFiles(items.Select(i => i.TemplateName), outputFolder);
                bool replace = true;
                if (existing.Count > 0)
                {
                    MessageBoxResult answer = Dialogs.YesNoCancel(this, title, ShellMessages.ConfirmExportOverwrite(outputFolder, existing, items.Count)
                        + (hidden > 0 ? Environment.NewLine + Environment.NewLine + ShellMessages.HiddenNote(hidden) : string.Empty));
                    if (answer == MessageBoxResult.Cancel || answer == MessageBoxResult.None)
                    {
                        return;
                    }

                    replace = answer == MessageBoxResult.Yes;
                }

                IProgress<string> progress = BusyProgressReporter();
                VarianExportResult result = await RunBusyAsync(
                    "Writing Varian XML files…",
                    () => Task.Run(() => VarianXmlBatch.Export(items, outputFolder, replace, progress)));
                logger.LogInformation("Exported {Written} Varian XML file(s) to {Folder}; {Skipped} kept, {Failed} failed.", result.Written.Count, outputFolder, result.SkippedExisting.Count, result.Failed.Count);
                foreach (string line in result.Written.SelectMany(r => r.Describe()))
                {
                    logger.LogInformation("{Line}", line);
                }

                string summary = ShellMessages.DescribeExport(result) + (hidden > 0 && existing.Count == 0 ? Environment.NewLine + Environment.NewLine + ShellMessages.HiddenNote(hidden) : string.Empty);
                if (result.Failed.Count > 0)
                {
                    Dialogs.Warning(this, title, summary);
                }
                else
                {
                    Dialogs.Info(this, title, summary);
                }
            }
            catch (Exception ex)
            {
                // UI boundary.
                ReportUnexpected(title, ex);
            }
        }

        private async void Load_XMLs_Click(object sender, RoutedEventArgs e)
        {
            const string title = "Import Varian XML";
            try
            {
                string? xmlFolder = await PickVarianFolderAsync("Select the folder of Varian XML templates to import");
                if (xmlFolder == null)
                {
                    return;
                }

                string root = templateRoot;
                IReadOnlyList<VarianImportCandidate> candidates = await RunBusyAsync(
                    "Reading Varian XML files…",
                    () => Task.Run(() => VarianXmlBatch.PlanImport(xmlFolder, root)));
                if (candidates.Count == 0)
                {
                    Dialogs.Info(this, title, "There are no .xml files in " + xmlFolder + ".");
                    return;
                }

                List<string> existing = candidates.Where(c => c.Problem == null && c.Exists && c.TemplateName != null).Select(c => c.TemplateName!).ToList();
                bool replace = false;
                if (existing.Count > 0)
                {
                    int importable = candidates.Count(c => c.Problem == null);
                    MessageBoxResult answer = Dialogs.YesNoCancel(this, title, ShellMessages.ConfirmImportReplace(root, existing, importable));
                    if (answer == MessageBoxResult.Cancel || answer == MessageBoxResult.None)
                    {
                        return;
                    }

                    replace = answer == MessageBoxResult.Yes;
                }

                IProgress<string> progress = BusyProgressReporter();
                VarianImportResult result = await RunBusyAsync(
                    "Importing Varian XML files…",
                    () => Task.Run(() => VarianXmlBatch.Import(candidates, root, replace, progress)));
                foreach (string line in result.Reports.SelectMany(r => r.Describe()))
                {
                    logger.LogInformation("Varian XML import: {Line}", line);
                }

                foreach ((string file, string reason) in result.NotImported)
                {
                    logger.LogWarning("Varian XML import: {File} not imported: {Reason}", file, reason);
                }

                await RebuildTemplateListAsync();
                if (result.FailedReports.Any() || result.NotImported.Count > 0)
                {
                    Dialogs.Warning(this, title, ShellMessages.DescribeImport(result));
                }
                else
                {
                    Dialogs.Info(this, title, ShellMessages.DescribeImport(result));
                }
            }
            catch (Exception ex)
            {
                // UI boundary.
                ReportUnexpected(title, ex);
            }
        }

        // ---------------------------------------------------------------- Airtable

        private void AirTableCheckBox_DataContextChanged(object sender, RoutedEventArgs e)
        {
            check_airtables(AirTableComboBox.SelectedItem as TemplateSourceItem);
        }

        /// <summary>
        /// Writes the selected templates shown to the chosen Airtable table after a confirmation naming the table and the
        /// templates. Runs as a busy operation: the other actions are disabled and the window cannot close meanwhile.
        /// </summary>
        private async void WriteToAirTable_Click(object sender, RoutedEventArgs e)
        {
            const string title = "Write to Airtable";
            TemplateSourceItem? table = AirTableComboBox.SelectedItem as TemplateSourceItem;
            bool started = false;
            try
            {
                SelectionScope<AddTemplateRow> scope = CurrentSelection();
                if (table == null || scope.VisibleSelected.Count == 0)
                {
                    Dialogs.Info(this, title, scope.HiddenSelected.Count > 0 && table != null
                        ? "The selected templates are all hidden by the current search, so nothing is written. Clear the search to see them."
                        : "Select at least one template and an Airtable table to write to.");
                    return;
                }

                List<AddTemplateRow> selected = scope.VisibleSelected.ToList();
                List<(string Name, int RoiCount)> summary = selected.Select(row => (RowName(row), row.templateMaker.ROIs.Count)).ToList();
                if (!Dialogs.Confirm(this, title, ShellMessages.ConfirmAirtableWrite(table.Name, summary, scope.HiddenSelected.Count)))
                {
                    return;
                }

                started = true;
                logger.LogInformation("Writing {Count} template(s) to Airtable table {Table}: {Templates}.", selected.Count, table.Name, string.Join(", ", summary.Select(t => t.Name)));
                var templates = selected.Select(row => new KeyValuePair<string, IEnumerable<ROIClass>>(RowName(row), row.templateMaker.ROIs.ToList())).ToList();
                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.Value = 0;
                IProgress<string> progress = BusyProgressReporter();
                IReadOnlyList<WriteResult> results = await RunBusyAsync(
                    "Writing " + selected.Count + " template(s) to " + table.Name + "…",
                    () => table.WriteTemplatesAsync(templates, progress, CancellationToken.None));
                ProgressBar.Value = 100;
                WriteToAirTable_Button.Content = "Wrote to Airtable!";
                Dialogs.Info(this, title, DescribeWrite(table, results));
            }
            catch (AirtableWriteException ex)
            {
                logger.LogError(ex, "Writing to Airtable table {Table} failed.", table?.Name);
                WriteToAirTable_Button.Content = "Failed writing to Airtable";
                string finished = ex.Completed.Count > 0 && table != null ? Environment.NewLine + Environment.NewLine + DescribeWrite(table, ex.Completed) : string.Empty;
                Dialogs.Error(this, "Write to Airtable failed", ex.Message + finished);
            }
            catch (Exception ex)
            {
                // UI boundary: report every failure instead of letting an async void handler crash the app.
                logger.LogError(ex, "Writing to Airtable table {Table} failed.", table?.Name);
                WriteToAirTable_Button.Content = "Failed writing to Airtable";
                Dialogs.Error(this, "Write to Airtable failed", ex.Message);
            }
            finally
            {
                if (started)
                {
                    // One write per tick of "Airtable write?".
                    ProgressBar.Visibility = Visibility.Hidden;
                    AirTableCheckbox.IsChecked = false;
                    check_airtables(table);
                }
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

            WriteToAirTable_Button.Content = "Write to Airtable";
            WriteToAirTable_Button.ToolTip = canWrite
                ? "Checks " + airtable.Name + " for recent changes, then sends only the ROIs that differ."
                : "Tick 'Airtable write?' to enable writing.";
            LoadAirTables_Button.IsEnabled = true;
            LoadAirTables_Button.ToolTip = airtable.StatusText;
        }

        private void AirTableSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            check_airtables(AirTableComboBox.SelectedItem as TemplateSourceItem);
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
                    Dialogs.Warning(this, "Refresh " + table.Name, result.Warning);
                }
            }
            catch (Exception ex)
            {
                // UI boundary.
                logger.LogWarning(ex, "Could not refresh {Table}.", table.Name);
                Dialogs.Error(this, "Refresh", "Could not refresh " + table.Name + ": " + ex.Message);
            }
            finally
            {
                LoadAirTables_Button.Content = "Refresh";
                ReadAirTableButton.IsEnabled = true;
                check_airtables(table);
            }
        }
    }
}
