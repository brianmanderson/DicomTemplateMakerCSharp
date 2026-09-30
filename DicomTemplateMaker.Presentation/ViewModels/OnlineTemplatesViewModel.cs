using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;
using TemplateSync.Infrastructure;
using TemplateSync.Storage;
using TemplateSync.Sync;

namespace DicomTemplateMakerGUI.ViewModels
{
    /// <summary>
    /// The "Online Templates" window: browses the templates offered by the shared TG-263 snapshot and the
    /// user's Airtable tables and builds the chosen ones as template folders. Opening uses cached data;
    /// the network is only used when the cache has expired or the user presses Refresh / Full refresh.
    /// </summary>
    public sealed partial class OnlineTemplatesViewModel : ObservableObject
    {
        public const string BuildTitle = "Build templates";
        public const string ReplaceTitle = "Replace existing templates";
        public const string RemoveTitle = "Remove Airtable table";

        /// <summary>At most this many names are listed in a confirmation; the rest are counted.</summary>
        internal const int MaxListedNames = 20;

        /// <summary>Starts every template name listed in the build confirmation.</summary>
        internal const string ListBullet = "  • ";

        private static readonly IReadOnlyList<string> LanguageChoices = Array.AsReadOnly(new[] { "English/Ingles/Anglais", "Spanish/Espanol/Espagnol", "French/Frances/Francais" });

        private readonly ITemplateSourceCatalog catalog;
        private readonly IDialogService dialogs;
        private readonly Func<TemplateSourceItem?> showAddTable;
        private readonly IClock clock;
        private readonly string templateFolder;
        private readonly string ontologyFolder;
        private readonly List<TemplateRowViewModel> rows = new List<TemplateRowViewModel>();
        private readonly HashSet<TemplateRowViewModel> visible = new HashSet<TemplateRowViewModel>();
        private CancellationTokenSource? operation;
        private bool isShutDown;

        /// <param name="catalog">The template sources and the connection operations.</param>
        /// <param name="dialogs">Confirmations and messages.</param>
        /// <param name="templateFolder">Where template folders are built (one folder per site).</param>
        /// <param name="ontologyFolder">The ontology library the builder updates.</param>
        /// <param name="showAddTable">Shows the "Add an Airtable table" dialog; returns the added source or null.</param>
        /// <param name="clock">Time source for the cache age; the system clock when null.</param>
        public OnlineTemplatesViewModel(ITemplateSourceCatalog catalog, IDialogService dialogs, string templateFolder, string ontologyFolder, Func<TemplateSourceItem?> showAddTable, IClock? clock = null)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            this.templateFolder = templateFolder ?? throw new ArgumentNullException(nameof(templateFolder));
            this.ontologyFolder = ontologyFolder ?? throw new ArgumentNullException(nameof(ontologyFolder));
            this.showAddTable = showAddTable ?? throw new ArgumentNullException(nameof(showAddTable));
            this.clock = clock ?? SystemClock.Instance;
            StatusText = string.Empty;
            SourceStatusText = string.Empty;
            ProgressText = string.Empty;
            ErrorText = string.Empty;
            CacheAgeText = string.Empty;
            SelectionSummary = string.Empty;
            SelectedLanguage = LanguageChoices[0];

            // Last: setting it applies the (empty) filter, which reads the fields above.
            SearchText = string.Empty;
        }

        /// <summary>Raised after a successful build; the window closes.</summary>
        public event EventHandler? CloseRequested;

        public ObservableCollection<TemplateSourceItem> Sources => catalog.Sources;

        /// <summary>Every site of the selected source, sorted by name.</summary>
        public IReadOnlyList<TemplateRowViewModel> Rows => rows;

        /// <summary>The sites that match <see cref="SearchText"/>.</summary>
        public ObservableCollection<TemplateRowViewModel> VisibleRows { get; } = new ObservableCollection<TemplateRowViewModel>();

        public IReadOnlyList<string> Languages => LanguageChoices;

        /// <summary>The load started by the latest source selection (for tests and the window's first load).</summary>
        internal Task LastLoad { get; private set; } = Task.CompletedTask;

        [ObservableProperty]
        public partial TemplateSourceItem? SelectedSource { get; set; }

        [ObservableProperty]
        public partial string SearchText { get; set; }

        /// <summary>True while a source is loading or refreshing.</summary>
        [ObservableProperty]
        public partial bool IsBusy { get; private set; }

        /// <summary>Short state of the list: loading, failed, empty or cancelled. Empty when the list is ready.</summary>
        [ObservableProperty]
        public partial string StatusText { get; private set; }

        /// <summary>What the source says about its data, e.g. "246 records · updated 2026-09-27 14:03".</summary>
        [ObservableProperty]
        public partial string SourceStatusText { get; private set; }

        /// <summary>The latest progress message of a running load.</summary>
        [ObservableProperty]
        public partial string ProgressText { get; private set; }

        /// <summary>Why the last load failed; empty otherwise.</summary>
        [ObservableProperty]
        public partial string ErrorText { get; private set; }

        /// <summary>How old the shown copy is, e.g. "Last synced with Airtable 5 hours ago.".</summary>
        [ObservableProperty]
        public partial string CacheAgeText { get; private set; }

        [ObservableProperty]
        public partial int SelectedCount { get; private set; }

        /// <summary>E.g. "3 of 40 selected; 1 hidden by the search".</summary>
        [ObservableProperty]
        public partial string SelectionSummary { get; private set; }

        [ObservableProperty]
        public partial bool IsLanguageChoiceVisible { get; private set; }

        [ObservableProperty]
        public partial bool IsLateralityChoiceVisible { get; private set; }

        [ObservableProperty]
        public partial string SelectedLanguage { get; set; }

        /// <summary>Name lateral structures with the side first (L_Breast rather than Breast_L).</summary>
        [ObservableProperty]
        public partial bool LateralityFirst { get; set; }

        /// <summary>Selects the first source, which loads it (from the cache unless it has expired).</summary>
        public Task StartAsync()
        {
            if (SelectedSource == null && Sources.Count > 0)
            {
                SelectedSource = Sources[0];
            }

            return LastLoad;
        }

        /// <summary>Cancels any running load; called when the window closes.</summary>
        public void Shutdown()
        {
            isShutDown = true;
            operation?.Cancel();
        }

        /// <summary>
        /// Loads the selected source and lists its sites. Never throws: failures are shown in
        /// <see cref="StatusText"/> and <see cref="ErrorText"/>. A newer load or <see cref="CancelCommand"/>
        /// cancels this one.
        /// </summary>
        public async Task LoadAsync(LoadMode mode)
        {
            TemplateSourceItem? source = SelectedSource;
            CancellationTokenSource cts = BeginOperation();
            ClearRows();
            ErrorText = string.Empty;
            CacheAgeText = string.Empty;
            ProgressText = string.Empty;
            if (source == null)
            {
                StatusText = string.Empty;
                SourceStatusText = string.Empty;
                EndOperation(cts);
                return;
            }

            StatusText = "Loading templates…";
            SourceStatusText = source.StatusText;
            var progress = new ContextProgress(message =>
            {
                if (IsCurrent(cts) && !cts.IsCancellationRequested)
                {
                    ProgressText = message;
                }
            });
            try
            {
                await source.LoadAsync(mode, progress, cts.Token);

                // A load that finished is shown even if Cancel came too late to stop it; a superseded one is not.
                if (IsCurrent(cts) && ReferenceEquals(SelectedSource, source))
                {
                    ShowSites(source, cancelled: false);
                }
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                if (IsCurrent(cts) && ReferenceEquals(SelectedSource, source))
                {
                    ShowSites(source, cancelled: true);
                }
            }
            catch (Exception ex)
            {
                // UI boundary: show the reason instead of crashing or failing silently.
                if (IsCurrent(cts))
                {
                    StatusText = "Could not load templates.";
                    ErrorText = ex.Message;
                    SourceStatusText = source.StatusText;
                    ProgressText = string.Empty;
                }
            }
            finally
            {
                EndOperation(cts);
            }
        }

        [RelayCommand(CanExecute = nameof(CanLoad))]
        private Task RefreshAsync() => LoadAsync(LoadMode.Refresh);

        [RelayCommand(CanExecute = nameof(CanLoad))]
        private Task FullRefreshAsync() => LoadAsync(LoadMode.FullRefresh);

        private bool CanLoad() => SelectedSource != null && !IsBusy;

        [RelayCommand(CanExecute = nameof(IsBusy))]
        private void Cancel()
        {
            operation?.Cancel();
        }

        /// <summary>Ticks every row the search shows. Rows hidden by the search are left as they are.</summary>
        [RelayCommand(CanExecute = nameof(CanSelectVisible))]
        private void SelectAllVisible()
        {
            foreach (TemplateRowViewModel row in VisibleRows)
            {
                row.IsSelected = true;
            }
        }

        private bool CanSelectVisible() => !IsBusy && VisibleRows.Any(r => r.CanBuild);

        /// <summary>Unticks every row, including rows hidden by the search.</summary>
        [RelayCommand(CanExecute = nameof(CanClearSelection))]
        private void ClearSelection()
        {
            foreach (TemplateRowViewModel row in rows)
            {
                row.IsSelected = false;
            }
        }

        private bool CanClearSelection() => SelectedCount > 0;

        /// <summary>
        /// Builds every ticked site as a template folder, after a confirmation that states how many will be
        /// built, how many of them the search hides, and which existing templates will be replaced. The templates are
        /// written on the thread pool, one after another, with progress in <see cref="ProgressText"/>; Cancel stops
        /// between two templates (the ones already built stay). The ontology library is read before anything is written
        /// (a damaged library stops the build) and updated once at the end.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanBuild))]
        private async Task BuildAsync()
        {
            List<TemplateRowViewModel> selected = rows.Where(r => r.IsSelected && r.CanBuild).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            foreach (TemplateRowViewModel row in selected)
            {
                row.AlreadyExists = Directory.Exists(TemplatePath(row.SiteName));
            }

            List<string> existing = selected.Where(r => r.AlreadyExists).Select(r => r.SiteName).ToList();
            int hidden = selected.Count(r => !visible.Contains(r));
            string title = existing.Count > 0 ? ReplaceTitle : BuildTitle;
            if (!dialogs.Confirm(title, DescribeBuild(selected.Count, hidden, existing)))
            {
                return;
            }

            // Everything the build needs from the rows and choices is taken here, on the UI thread.
            var jobs = new List<(string Site, List<ROIClass> Rois)>();
            var warnings = new List<string>();
            foreach (TemplateRowViewModel row in selected)
            {
                List<ROIWrapper> wrappers = row.Source.BuildRoiWrappers(row.SiteName, warnings);
                ApplyNamingChoice(wrappers);
                jobs.Add((row.SiteName, wrappers.Select(x => x.roi).ToList()));
            }

            CancellationTokenSource cts = BeginOperation();
            IsBuilding = true;
            StatusText = "Building templates…";
            var progress = new ContextProgress(message =>
            {
                if (IsCurrent(cts))
                {
                    ProgressText = message;
                }
            });
            int built = 0;
            Exception? failure = null;
            try
            {
                built = await Task.Run(() => BuildTemplates(jobs, progress, cts.Token), CancellationToken.None);
            }
            catch (Exception ex)
            {
                // UI boundary: report and keep the window open; templates finished before the error stay built.
                failure = ex;
            }
            finally
            {
                IsBuilding = false;
                StatusText = string.Empty;
                ProgressText = string.Empty;
                EndOperation(cts);
            }

            if (failure != null)
            {
                foreach (TemplateRowViewModel row in selected)
                {
                    row.AlreadyExists = Directory.Exists(TemplatePath(row.SiteName));
                }

                BuildFailure buildFailure = failure as BuildFailure ?? new BuildFailure(0, failure);
                Exception cause = buildFailure.InnerException ?? buildFailure;
                if (cause is OperationCanceledException)
                {
                    dialogs.ShowWarning(BuildTitle, buildFailure.Built == 0
                        ? "The build was cancelled before any template was written."
                        : "The build was cancelled after " + buildFailure.Built + " of " + jobs.Count + " templates were built; those stay built.");
                    return;
                }

                string done = buildFailure.Built == 0 ? string.Empty : " after " + buildFailure.Built + " of " + jobs.Count + " templates were built";
                dialogs.ShowError(BuildTitle, "Building the templates failed" + done + ": " + cause.Message);
                return;
            }

            if (warnings.Count > 0)
            {
                dialogs.ShowWarning(BuildTitle, "The " + (built == 1 ? "template was" : built + " templates were") + " built with these notes:" + Environment.NewLine + Environment.NewLine
                    + ListLimited(warnings.Distinct().ToList(), 30, string.Empty));
            }

            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>The build loop, off the UI thread. Throws <see cref="BuildFailure"/> with the count built so far.</summary>
        private int BuildTemplates(IReadOnlyList<(string Site, List<ROIClass> Rois)> jobs, IProgress<string> progress, CancellationToken cancellationToken)
        {
            int built = 0;
            try
            {
                // Read first: a library that cannot be read stops the build before any template is written.
                OntologyTools.LoadOntologiesFromFolder(ontologyFolder);
                foreach ((string site, List<ROIClass> rois) in jobs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress.Report("Building " + (built + 1) + " of " + jobs.Count + ": " + site);
                    TemplateMaker evaluator = new TemplateMaker();
                    evaluator.define_output(TemplatePath(site));
                    evaluator.ROIs = rois;
                    evaluator.make_template();
                    built++;
                }
            }
            catch (Exception ex)
            {
                // The templates written so far still bring their codes into the library.
                if (built > 0)
                {
                    TryMergeCodes(jobs.Take(built));
                }

                throw new BuildFailure(built, ex);
            }

            try
            {
                progress.Report("Updating the ontology library…");
                OntologyTools.MergeOntologiesIntoFolder(jobs.SelectMany(j => j.Rois.Select(roi => roi.Ontology_Class)), ontologyFolder);
                return built;
            }
            catch (Exception ex)
            {
                throw new BuildFailure(built, ex);
            }
        }

        private void TryMergeCodes(IEnumerable<(string Site, List<ROIClass> Rois)> jobs)
        {
            try
            {
                OntologyTools.MergeOntologiesIntoFolder(jobs.SelectMany(j => j.Rois.Select(roi => roi.Ontology_Class)), ontologyFolder);
            }
            catch (Exception ex) when (ex is TemplateLoadException || ex is IOException || ex is UnauthorizedAccessException)
            {
                // The build failure is what gets reported; the codes are added the next time the templates are read.
            }
        }

        /// <summary>True while templates are being written; the window must not close meanwhile (Cancel stops the build).</summary>
        [ObservableProperty]
        public partial bool IsBuilding { get; private set; }

        /// <summary>Shown when the window is closed while templates are being built.</summary>
        public const string WaitForBuildMessage = "Templates are being built. Press Cancel to stop after the current template, or wait until the build has finished; then close the window.";

        private bool CanBuild() => !IsBusy && SelectedCount > 0;

        /// <summary>Forgets the selected Airtable table on this computer after the user confirms.</summary>
        [RelayCommand(CanExecute = nameof(CanRemoveConnection))]
        private void RemoveConnection()
        {
            TemplateSourceItem? item = SelectedSource;
            if (item == null || !item.IsWritable)
            {
                return;
            }

            string question = "Remove the connection to '" + item.Name + "'?" + Environment.NewLine + Environment.NewLine +
                "This deletes the saved token and the cached copy on this computer. The Airtable table itself is not changed.";
            if (!dialogs.Confirm(RemoveTitle, question))
            {
                return;
            }

            try
            {
                catalog.RemoveConnection(item);
            }
            catch (Exception ex)
            {
                // UI boundary: the connection stays listed; say why.
                dialogs.ShowError(RemoveTitle, "Could not remove the connection: " + ex.Message);
            }

            if (!Sources.Contains(item))
            {
                SelectedSource = Sources.FirstOrDefault();
            }
        }

        private bool CanRemoveConnection() => !IsBusy && SelectedSource != null && SelectedSource.IsWritable;

        /// <summary>Opens "Add an Airtable table" and selects the table that was added.</summary>
        [RelayCommand(CanExecute = nameof(CanAddTable))]
        private void AddTable()
        {
            TemplateSourceItem? added = showAddTable();
            if (added != null)
            {
                SelectedSource = added;
            }
        }

        private bool CanAddTable() => !IsBusy;

        partial void OnSelectedSourceChanged(TemplateSourceItem? value)
        {
            UpdateCommands();
            if (!isShutDown)
            {
                LastLoad = LoadAsync(LoadMode.IfStale);
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        partial void OnIsBusyChanged(bool value)
        {
            UpdateCommands();
        }

        private CancellationTokenSource BeginOperation()
        {
            operation?.Cancel();
            var cts = new CancellationTokenSource();
            operation = cts;
            IsBusy = true;
            return cts;
        }

        private void EndOperation(CancellationTokenSource cts)
        {
            if (IsCurrent(cts))
            {
                operation = null;
                IsBusy = false;
            }

            cts.Dispose();
        }

        private bool IsCurrent(CancellationTokenSource cts) => ReferenceEquals(operation, cts);

        private void ShowSites(TemplateSourceItem source, bool cancelled)
        {
            if (isShutDown)
            {
                return;
            }

            ClearRows();
            var ignored = new List<string>();
            bool anyLateral = false;
            bool anyOtherLanguage = false;
            foreach (string site in source.Index.SiteNames.OrderBy(s => s, StringComparer.CurrentCulture))
            {
                List<ROIWrapper> wrappers = source.BuildRoiWrappers(site, ignored);
                anyLateral |= wrappers.Any(x => x.has_lateral);
                anyOtherLanguage |= wrappers.Any(x => x.has_other_lanuages);
                bool canBuild = IsUsableFolderName(site);
                var row = new TemplateRowViewModel(site, source, wrappers.Count, canBuild, canBuild && Directory.Exists(TemplatePath(site)));
                row.PropertyChanged += OnRowPropertyChanged;
                rows.Add(row);
            }

            IsLateralityChoiceVisible = anyLateral;
            IsLanguageChoiceVisible = anyOtherLanguage;
            SourceStatusText = source.StatusText;
            CacheAgeText = DescribeAge(source);
            ProgressText = string.Empty;
            if (cancelled)
            {
                StatusText = rows.Count > 0 ? "Refresh cancelled; showing the copy loaded earlier." : "Cancelled before any templates were loaded.";
            }
            else if (rows.Count > 0)
            {
                StatusText = string.Empty;
            }
            else
            {
                StatusText = source.HasData ? "No templates found." : "Nothing downloaded yet. Press Refresh.";
            }

            ApplyFilter();
        }

        private void ClearRows()
        {
            foreach (TemplateRowViewModel row in rows)
            {
                row.PropertyChanged -= OnRowPropertyChanged;
            }

            rows.Clear();
            visible.Clear();
            VisibleRows.Clear();
            IsLateralityChoiceVisible = false;
            IsLanguageChoiceVisible = false;
            UpdateSelection();
        }

        private void ApplyFilter()
        {
            string search = SearchText.Trim();
            VisibleRows.Clear();
            visible.Clear();
            foreach (TemplateRowViewModel row in rows)
            {
                if (search.Length == 0 || row.SiteName.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    VisibleRows.Add(row);
                    visible.Add(row);
                }
            }

            UpdateSelection();
        }

        private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TemplateRowViewModel.IsSelected))
            {
                UpdateSelection();
            }
        }

        private void UpdateSelection()
        {
            int selected = rows.Count(r => r.IsSelected);
            int hidden = rows.Count(r => r.IsSelected && !visible.Contains(r));
            SelectedCount = selected;
            if (rows.Count == 0)
            {
                SelectionSummary = string.Empty;
            }
            else
            {
                SelectionSummary = selected + " of " + rows.Count + " selected" + (hidden > 0 ? "; " + hidden + " hidden by the search" : string.Empty);
            }

            UpdateCommands();
        }

        private void UpdateCommands()
        {
            RefreshCommand.NotifyCanExecuteChanged();
            FullRefreshCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            SelectAllVisibleCommand.NotifyCanExecuteChanged();
            ClearSelectionCommand.NotifyCanExecuteChanged();
            BuildCommand.NotifyCanExecuteChanged();
            RemoveConnectionCommand.NotifyCanExecuteChanged();
            AddTableCommand.NotifyCanExecuteChanged();
        }

        /// <summary>Applies the language and laterality choices exactly as the window always has.</summary>
        private void ApplyNamingChoice(List<ROIWrapper> wrappers)
        {
            string language = SelectedLanguage ?? string.Empty;
            bool languageVisible = IsLanguageChoiceVisible;
            bool lateralityVisible = IsLateralityChoiceVisible;
            bool reverse = LateralityFirst;
            foreach (ROIWrapper r_wrapper in wrappers)
            {
                if (languageVisible)
                {
                    if (language.Contains("French", StringComparison.Ordinal))
                    {
                        if (lateralityVisible) { r_wrapper.Set_French(reverse); } else { r_wrapper.Set_French(); }
                    }
                    else if (language.Contains("Spanish", StringComparison.Ordinal))
                    {
                        if (lateralityVisible) { r_wrapper.Set_Spanish(reverse); } else { r_wrapper.Set_Spanish(); }
                    }
                    else if (language.Contains("English", StringComparison.Ordinal))
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

        private string TemplatePath(string site) => Path.Combine(templateFolder, site);

        /// <summary>
        /// A site becomes one folder directly inside the template folder, so its name must be a single valid
        /// folder name: no separators or other invalid characters, and not "." or "..".
        /// </summary>
        internal static bool IsUsableFolderName(string name)
        {
            return !string.IsNullOrWhiteSpace(name)
                && name != "."
                && name != ".."
                && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && name.IndexOf(Path.DirectorySeparatorChar) < 0
                && name.IndexOf(Path.AltDirectorySeparatorChar) < 0;
        }

        private string DescribeBuild(int count, int hidden, List<string> existing)
        {
            var text = new StringBuilder();
            text.Append("Build ").Append(count == 1 ? "1 template" : count + " templates")
                .Append(" into ").Append(Path.GetFullPath(templateFolder)).Append('?');
            if (hidden > 0)
            {
                text.AppendLine().Append(hidden).Append(" of them ").Append(hidden == 1 ? "is" : "are").Append(" hidden by the current search.");
            }

            if (existing.Count > 0)
            {
                text.AppendLine().AppendLine()
                    .AppendLine(existing.Count == 1 ? "This template already exists and will be replaced:" : "These " + existing.Count + " templates already exist and will be replaced:")
                    .AppendLine(ListLimited(existing, MaxListedNames, ListBullet))
                    .AppendLine()
                    .Append(existing.Count == 1 ? "Replace it?" : "Replace them?");
            }

            return text.ToString();
        }

        private static string ListLimited(List<string> items, int max, string prefix)
        {
            var text = new StringBuilder();
            foreach (string item in items.Take(max))
            {
                if (text.Length > 0)
                {
                    text.AppendLine();
                }

                text.Append(prefix).Append(item);
            }

            if (items.Count > max)
            {
                text.AppendLine().Append("… and ").Append(items.Count - max).Append(" more.");
            }

            return text.ToString();
        }

        private string DescribeAge(TemplateSourceItem source)
        {
            TableSnapshot? snapshot = source.Source.Current;
            if (snapshot == null)
            {
                return string.Empty;
            }

            string age = FormatAge(clock.UtcNow - snapshot.GeneratedAtUtc);
            return source.IsWritable ? "Last synced with Airtable " + age + "." : "Published " + age + ".";
        }

        internal static string FormatAge(TimeSpan age)
        {
            if (age < TimeSpan.FromMinutes(1))
            {
                return "just now";
            }

            if (age < TimeSpan.FromHours(1))
            {
                int minutes = (int)age.TotalMinutes;
                return minutes == 1 ? "1 minute ago" : minutes + " minutes ago";
            }

            if (age < TimeSpan.FromDays(2))
            {
                int hours = (int)age.TotalHours;
                return hours == 1 ? "1 hour ago" : hours + " hours ago";
            }

            return (int)age.TotalDays + " days ago";
        }

        /// <summary>A build that stopped: how many templates were written before <see cref="Exception.InnerException"/>.</summary>
        private sealed class BuildFailure : Exception
        {
            public BuildFailure(int built, Exception inner)
                : base(inner.Message, inner)
            {
                Built = built;
            }

            public int Built { get; }
        }
    }
}
