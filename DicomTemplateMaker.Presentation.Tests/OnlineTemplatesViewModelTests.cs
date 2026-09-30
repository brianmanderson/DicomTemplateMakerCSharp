using DicomTemplateMaker.Presentation.Tests.Fakes;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.ViewModels;
using Newtonsoft.Json.Linq;
using ROIOntologyClass;
using TemplateSync.Airtable;
using TemplateSync.Storage;
using TemplateSync.Sync;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class OnlineTemplatesViewModelTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
    private readonly TestFolder folder = new();
    private readonly FakeCatalog catalog = new();
    private readonly FakeDialogService dialogs = new();
    private readonly FakeClock clock = new(Now);
    private TemplateSourceItem? addTableResult;
    private int closeRequests;

    public void Dispose() => folder.Dispose();

    private string OntologyFolder => Path.Combine(folder.Path, "Ontologies");

    private OnlineTemplatesViewModel CreateViewModel()
    {
        var vm = new OnlineTemplatesViewModel(catalog, dialogs, folder.Path, OntologyFolder, () => addTableResult, clock);
        vm.CloseRequested += (_, _) => closeRequests++;
        return vm;
    }

    /// <summary>A writable table offering the sites Breast_L, Breast_R and Lung.</summary>
    private static TableSnapshot ThreeSites(DateTimeOffset generatedAt) => Snapshots.Of(
        generatedAt,
        Snapshots.Roi("Heart", "Breast_L", "Lung"),
        Snapshots.Roi("Lung_L", "Breast_L", "Lung"),
        Snapshots.Roi("Lung_R", "Breast_R", "Lung"));

    private async Task<(OnlineTemplatesViewModel Vm, FakeTableSource Source)> StartWithThreeSitesAsync()
    {
        var source = new FakeTableSource("Clinic", writable: true, ThreeSites(Now.AddHours(-5)));
        catalog.AddSource(source);
        OnlineTemplatesViewModel vm = CreateViewModel();
        await vm.StartAsync();
        return (vm, source);
    }

    private static TemplateRowViewModel Row(OnlineTemplatesViewModel vm, string site) => vm.Rows.Single(r => r.SiteName == site);

    private static List<string> ListedNames(string message)
        => message.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.StartsWith("  • ", StringComparison.Ordinal)).Select(l => l.Substring(4)).ToList();

    private static List<string> BuiltRoiNames(string templateFolder)
        => ROIClassTools.LoadROIsFromFolder(templateFolder, new List<OntologyCodeClass>()).Select(r => r.ROIName).ToList();

    [Fact]
    public async Task Start_loads_the_first_source_from_the_cache_and_lists_its_sites_sorted_with_the_cache_age()
    {
        (OnlineTemplatesViewModel vm, FakeTableSource source) = await StartWithThreeSitesAsync();

        Assert.Equal(new[] { LoadMode.IfStale }, source.Modes);
        Assert.Equal(new[] { "Breast_L", "Breast_R", "Lung" }, vm.Rows.Select(r => r.SiteName));
        Assert.Equal(vm.Rows, vm.VisibleRows);
        Assert.Equal(new[] { "2 ROIs", "1 ROI", "3 ROIs" }, vm.Rows.Select(r => r.RoiCountText));
        Assert.False(vm.IsBusy);
        Assert.Equal(string.Empty, vm.StatusText);
        Assert.Equal(string.Empty, vm.ErrorText);
        Assert.StartsWith("3 records", vm.SourceStatusText, StringComparison.Ordinal);
        Assert.Equal("Last synced with Airtable 5 hours ago.", vm.CacheAgeText);
        Assert.Equal("0 of 3 selected", vm.SelectionSummary);
        Assert.False(vm.BuildCommand.CanExecute(null));
    }

    [Fact]
    public async Task The_shared_source_reports_when_it_was_published()
    {
        catalog.AddSource(new FakeTableSource(TemplateSourceCatalog.SharedSourceName, writable: false, ThreeSites(Now.AddDays(-3))));
        OnlineTemplatesViewModel vm = CreateViewModel();

        await vm.StartAsync();

        Assert.Equal("Published 3 days ago.", vm.CacheAgeText);
        Assert.False(vm.RemoveConnectionCommand.CanExecute(null));
    }

    [Fact]
    public async Task While_loading_the_window_is_busy_shows_progress_and_only_offers_cancel()
    {
        var release = new TaskCompletionSource<TableLoadResult>();
        var source = new FakeTableSource("Clinic", writable: true)
        {
            OnLoad = (_, progress, _) =>
            {
                progress?.Report("Downloading Clinic…");
                return release.Task;
            },
        };
        catalog.AddSource(source);
        OnlineTemplatesViewModel vm = CreateViewModel();

        Task load = vm.StartAsync();

        Assert.False(load.IsCompleted);
        Assert.True(vm.IsBusy);
        Assert.Equal("Loading templates…", vm.StatusText);
        Assert.Equal("Downloading Clinic…", vm.ProgressText);
        Assert.True(vm.CancelCommand.CanExecute(null));
        Assert.False(vm.RefreshCommand.CanExecute(null));
        Assert.False(vm.FullRefreshCommand.CanExecute(null));
        Assert.False(vm.AddTableCommand.CanExecute(null));
        Assert.False(vm.RemoveConnectionCommand.CanExecute(null));

        source.Current = ThreeSites(Now);
        release.SetResult(new TableLoadResult(source.Current, LoadOrigin.Network));
        await load;

        Assert.False(vm.IsBusy);
        Assert.Equal(string.Empty, vm.ProgressText);
        Assert.Equal(3, vm.Rows.Count);
        Assert.False(vm.CancelCommand.CanExecute(null));
        Assert.True(vm.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_failed_load_is_explained_in_the_window_without_a_dialog_or_an_exception()
    {
        var source = new FakeTableSource("Clinic", writable: true)
        {
            OnLoad = (_, _, _) => throw new AirtableException("Airtable rejected the access token for base appX (Invalid token)."),
        };
        catalog.AddSource(source);
        OnlineTemplatesViewModel vm = CreateViewModel();

        await vm.StartAsync();

        Assert.Equal("Could not load templates.", vm.StatusText);
        Assert.Equal("Airtable rejected the access token for base appX (Invalid token).", vm.ErrorText);
        Assert.False(vm.IsBusy);
        Assert.Empty(vm.Rows);
        Assert.Empty(dialogs.Shown);
        Assert.True(vm.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_source_with_nothing_downloaded_says_so()
    {
        catalog.AddSource(new FakeTableSource("Clinic", writable: true, current: null));
        OnlineTemplatesViewModel vm = CreateViewModel();

        await vm.StartAsync();

        Assert.Equal("Nothing downloaded yet. Press Refresh.", vm.StatusText);
        Assert.Equal(string.Empty, vm.CacheAgeText);
    }

    [Fact]
    public async Task Refresh_and_full_refresh_load_the_selected_source_with_their_modes()
    {
        (OnlineTemplatesViewModel vm, FakeTableSource source) = await StartWithThreeSitesAsync();

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.FullRefreshCommand.ExecuteAsync(null);

        Assert.Equal(new[] { LoadMode.IfStale, LoadMode.Refresh, LoadMode.FullRefresh }, source.Modes);
        Assert.Equal(3, vm.Rows.Count);
    }

    [Fact]
    public async Task Cancel_stops_a_refresh_and_keeps_showing_the_copy_loaded_earlier()
    {
        (OnlineTemplatesViewModel vm, FakeTableSource source) = await StartWithThreeSitesAsync();
        source.OnLoad = async (_, _, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("unreachable");
        };

        Task refresh = vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);
        Assert.Empty(vm.Rows);
        vm.CancelCommand.Execute(null);
        await refresh;

        Assert.True(source.Tokens[1].IsCancellationRequested);
        Assert.False(vm.IsBusy);
        Assert.Equal("Refresh cancelled; showing the copy loaded earlier.", vm.StatusText);
        Assert.Equal(string.Empty, vm.ErrorText);
        Assert.Equal(new[] { "Breast_L", "Breast_R", "Lung" }, vm.Rows.Select(r => r.SiteName));
        Assert.True(vm.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public async Task Choosing_another_source_cancels_the_running_load_and_ignores_its_late_result()
    {
        var slowResult = new TaskCompletionSource<TableLoadResult>();
        var slow = new FakeTableSource("Slow", writable: true) { OnLoad = (_, _, _) => slowResult.Task };
        var fast = new FakeTableSource("Fast", writable: true, Snapshots.Of(Now, Snapshots.Roi("Heart", "FastSite")));
        TemplateSourceItem slowItem = catalog.AddSource(slow);
        TemplateSourceItem fastItem = catalog.AddSource(fast);
        OnlineTemplatesViewModel vm = CreateViewModel();

        vm.SelectedSource = slowItem;
        Task slowLoad = vm.LastLoad;
        vm.SelectedSource = fastItem;
        await vm.LastLoad;

        Assert.True(slow.Tokens[0].IsCancellationRequested);
        Assert.Equal(new[] { "FastSite" }, vm.Rows.Select(r => r.SiteName));

        slow.Current = Snapshots.Of(Now, Snapshots.Roi("Heart", "SlowSite"));
        slowResult.SetResult(new TableLoadResult(slow.Current, LoadOrigin.Network));
        await slowLoad;

        Assert.Equal(new[] { "FastSite" }, vm.Rows.Select(r => r.SiteName));
        Assert.False(vm.IsBusy);
        Assert.Equal(string.Empty, vm.StatusText);
    }

    [Fact]
    public async Task Search_is_case_insensitive_and_hides_rows_without_dropping_them()
    {
        (OnlineTemplatesViewModel vm, _) = await StartWithThreeSitesAsync();

        vm.SearchText = "BREAST";

        Assert.Equal(new[] { "Breast_L", "Breast_R" }, vm.VisibleRows.Select(r => r.SiteName));
        Assert.Equal(3, vm.Rows.Count);
    }

    // N19: "Select all?" used to tick rows hidden by the search as well.
    [Fact]
    public async Task Select_all_ticks_only_the_rows_the_search_shows()
    {
        (OnlineTemplatesViewModel vm, _) = await StartWithThreeSitesAsync();
        vm.SearchText = "breast";

        vm.SelectAllVisibleCommand.Execute(null);
        vm.SearchText = string.Empty;

        Assert.True(Row(vm, "Breast_L").IsSelected);
        Assert.True(Row(vm, "Breast_R").IsSelected);
        Assert.False(Row(vm, "Lung").IsSelected);
        Assert.Equal(2, vm.SelectedCount);
        Assert.Equal("2 of 3 selected", vm.SelectionSummary);
    }

    [Fact]
    public async Task Clear_all_unticks_hidden_rows_too()
    {
        (OnlineTemplatesViewModel vm, _) = await StartWithThreeSitesAsync();
        Row(vm, "Lung").IsSelected = true;
        Row(vm, "Breast_L").IsSelected = true;
        vm.SearchText = "breast";

        vm.ClearSelectionCommand.Execute(null);

        Assert.All(vm.Rows, r => Assert.False(r.IsSelected));
        Assert.False(vm.BuildCommand.CanExecute(null));
    }

    // N19: the build confirmation states how many templates will be built, including ticked rows the search hides.
    [Fact]
    public async Task Build_confirmation_states_how_many_templates_will_be_built_and_how_many_are_hidden()
    {
        (OnlineTemplatesViewModel vm, _) = await StartWithThreeSitesAsync();
        Row(vm, "Breast_L").IsSelected = true;
        Row(vm, "Lung").IsSelected = true;
        vm.SearchText = "breast";
        Assert.Equal("2 of 3 selected; 1 hidden by the search", vm.SelectionSummary);
        dialogs.Answers.Enqueue(false);

        await vm.BuildCommand.ExecuteAsync(null);

        ShownDialog confirmation = Assert.Single(dialogs.Shown);
        Assert.Equal("confirm", confirmation.Kind);
        Assert.Equal(OnlineTemplatesViewModel.BuildTitle, confirmation.Title);
        Assert.StartsWith("Build 2 templates into " + Path.GetFullPath(folder.Path) + "?", confirmation.Message, StringComparison.Ordinal);
        Assert.Contains("1 of them is hidden by the current search.", confirmation.Message, StringComparison.Ordinal);
        Assert.Empty(ListedNames(confirmation.Message));
        Assert.Empty(Directory.GetFileSystemEntries(folder.Path));
        Assert.Equal(0, closeRequests);
    }

    // N3: building used to overwrite an existing template folder without a word.
    [Fact]
    public async Task Rows_mark_the_templates_that_already_exist_in_the_template_folder()
    {
        folder.Sub("Breast_R");

        (OnlineTemplatesViewModel vm, _) = await StartWithThreeSitesAsync();

        Assert.False(Row(vm, "Breast_L").AlreadyExists);
        Assert.True(Row(vm, "Breast_R").AlreadyExists);
        Assert.False(Row(vm, "Lung").AlreadyExists);
    }

    // N3: the confirmation names exactly the templates that will be replaced; declining builds nothing.
    [Fact]
    public async Task Build_confirmation_lists_exactly_the_existing_templates_and_cancel_builds_nothing()
    {
        (OnlineTemplatesViewModel vm, _) = await StartWithThreeSitesAsync();
        string existing = folder.Sub("Breast_L");
        File.WriteAllText(Path.Combine(existing, "All_ROIs.json"), "sentinel");
        folder.Sub("Unrelated");
        Row(vm, "Breast_L").IsSelected = true;
        Row(vm, "Lung").IsSelected = true;
        dialogs.Answers.Enqueue(false);

        await vm.BuildCommand.ExecuteAsync(null);

        ShownDialog confirmation = Assert.Single(dialogs.Shown);
        Assert.Equal(OnlineTemplatesViewModel.ReplaceTitle, confirmation.Title);
        Assert.StartsWith("Build 2 templates into ", confirmation.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { "Breast_L" }, ListedNames(confirmation.Message));
        Assert.True(Row(vm, "Breast_L").AlreadyExists);
        Assert.Equal("sentinel", File.ReadAllText(Path.Combine(existing, "All_ROIs.json")));
        Assert.False(Directory.Exists(Path.Combine(folder.Path, "Lung")));
        Assert.False(Directory.Exists(OntologyFolder));
        Assert.Equal(0, closeRequests);
    }

    [Fact]
    public async Task A_confirmed_build_writes_every_ticked_template_and_closes_the_window()
    {
        (OnlineTemplatesViewModel vm, _) = await StartWithThreeSitesAsync();
        string existing = folder.Sub("Breast_L");
        File.WriteAllText(Path.Combine(existing, "All_ROIs.json"), "[]");
        Row(vm, "Breast_L").IsSelected = true;
        Row(vm, "Lung").IsSelected = true;
        dialogs.Answers.Enqueue(true);

        await vm.BuildCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Shown);
        Assert.Equal(new[] { "Heart", "Lung_L" }, BuiltRoiNames(existing));
        Assert.Equal(new[] { "Heart", "Lung_L", "Lung_R" }, BuiltRoiNames(Path.Combine(folder.Path, "Lung")));
        Assert.False(Directory.Exists(Path.Combine(folder.Path, "Breast_R")));
        Assert.Equal(1, closeRequests);
    }

    [Fact]
    public async Task A_build_over_an_unreadable_template_is_reported_and_keeps_the_file()
    {
        (OnlineTemplatesViewModel vm, _) = await StartWithThreeSitesAsync();
        string existing = folder.Sub("Breast_L");
        File.WriteAllText(Path.Combine(existing, "All_ROIs.json"), "sentinel");
        Row(vm, "Breast_L").IsSelected = true;
        dialogs.Answers.Enqueue(true);

        await vm.BuildCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "confirm", "error" }, dialogs.Shown.Select(d => d.Kind));
        Assert.Contains("All_ROIs.json", dialogs.Shown[1].Message, StringComparison.Ordinal);
        Assert.Equal("sentinel", File.ReadAllText(Path.Combine(existing, "All_ROIs.json")));
        Assert.Equal(0, closeRequests);
    }

    [Fact]
    public async Task A_failed_build_is_reported_and_the_window_stays_open()
    {
        (OnlineTemplatesViewModel vm, _) = await StartWithThreeSitesAsync();
        // A file where the template folder should be makes the builder fail.
        File.WriteAllText(Path.Combine(folder.Path, "Lung"), "not a folder");
        Row(vm, "Lung").IsSelected = true;
        dialogs.Answers.Enqueue(true);

        await vm.BuildCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "confirm", "error" }, dialogs.Shown.Select(d => d.Kind));
        Assert.StartsWith("Building the templates failed: ", dialogs.Shown[1].Message, StringComparison.Ordinal);
        Assert.Equal(0, closeRequests);
    }

    [Fact]
    public async Task Language_and_laterality_choices_rename_the_built_rois_as_before()
    {
        var fields = new JObject
        {
            ["Structure"] = "Breast_L",
            ["RGB"] = "0,255,0",
            ["Template_Recommend"] = new JArray("Breast"),
            ["TG_263R"] = "L_Breast",
            ["TG_263Spanish"] = "Mama_I",
            ["TG_263SpanishR"] = "I_Mama",
        };
        catalog.AddSource(new FakeTableSource("Clinic", writable: true, Snapshots.Of(Now, Snapshots.Record(fields))));
        OnlineTemplatesViewModel vm = CreateViewModel();
        await vm.StartAsync();
        Assert.True(vm.IsLanguageChoiceVisible);
        Assert.True(vm.IsLateralityChoiceVisible);

        vm.SelectedLanguage = vm.Languages[1];
        vm.LateralityFirst = true;
        Row(vm, "Breast").IsSelected = true;
        dialogs.Answers.Enqueue(true);
        await vm.BuildCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "I_Mama" }, BuiltRoiNames(Path.Combine(folder.Path, "Breast")));
    }

    [Fact]
    public async Task Naming_choices_are_hidden_when_the_templates_offer_none()
    {
        (OnlineTemplatesViewModel vm, _) = await StartWithThreeSitesAsync();

        Assert.False(vm.IsLanguageChoiceVisible);
        Assert.False(vm.IsLateralityChoiceVisible);
    }

    [Fact]
    public async Task A_site_name_that_is_not_a_single_folder_name_cannot_be_ticked_or_built()
    {
        catalog.AddSource(new FakeTableSource("Clinic", writable: true, Snapshots.Of(Now, Snapshots.Roi("Heart", "../Outside", "Lung"))));
        OnlineTemplatesViewModel vm = CreateViewModel();
        await vm.StartAsync();
        TemplateRowViewModel outside = Row(vm, "../Outside");

        vm.SelectAllVisibleCommand.Execute(null);
        outside.IsSelected = true;
        dialogs.Answers.Enqueue(true);
        await vm.BuildCommand.ExecuteAsync(null);

        Assert.False(outside.CanBuild);
        Assert.False(outside.IsSelected);
        Assert.NotEqual(string.Empty, outside.Problem);
        Assert.StartsWith("Build 1 template into ", dialogs.Shown[0].Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.GetFullPath(Path.Combine(folder.Path, "..", "Outside"))));
        Assert.True(Directory.Exists(Path.Combine(folder.Path, "Lung")));
    }

    [Fact]
    public async Task Removing_a_connection_asks_first_and_declining_keeps_it()
    {
        (OnlineTemplatesViewModel vm, _) = await StartWithThreeSitesAsync();
        dialogs.Answers.Enqueue(false);

        vm.RemoveConnectionCommand.Execute(null);

        ShownDialog question = Assert.Single(dialogs.Shown);
        Assert.Equal(OnlineTemplatesViewModel.RemoveTitle, question.Title);
        Assert.StartsWith("Remove the connection to 'Clinic'?", question.Message, StringComparison.Ordinal);
        Assert.Contains("The Airtable table itself is not changed.", question.Message, StringComparison.Ordinal);
        Assert.Empty(catalog.Removed);
        Assert.Equal("Clinic", vm.SelectedSource?.Name);
    }

    [Fact]
    public async Task A_confirmed_removal_forgets_the_table_and_shows_the_first_source()
    {
        TemplateSourceItem shared = catalog.AddSource(new FakeTableSource(TemplateSourceCatalog.SharedSourceName, writable: false, ThreeSites(Now)));
        var clinic = new FakeTableSource("Clinic", writable: true, ThreeSites(Now));
        TemplateSourceItem clinicItem = catalog.AddSource(clinic);
        OnlineTemplatesViewModel vm = CreateViewModel();
        vm.SelectedSource = clinicItem;
        await vm.LastLoad;
        Assert.True(vm.RemoveConnectionCommand.CanExecute(null));
        dialogs.Answers.Enqueue(true);

        vm.RemoveConnectionCommand.Execute(null);
        await vm.LastLoad;

        Assert.Equal(new[] { clinicItem }, catalog.Removed);
        Assert.Same(shared, vm.SelectedSource);
        Assert.Equal(3, vm.Rows.Count);
        Assert.False(vm.RemoveConnectionCommand.CanExecute(null));
    }

    [Fact]
    public async Task Adding_a_table_selects_and_loads_it()
    {
        (OnlineTemplatesViewModel vm, _) = await StartWithThreeSitesAsync();
        var added = new FakeTableSource("New table", writable: true, Snapshots.Of(Now, Snapshots.Roi("Heart", "NewSite")));
        addTableResult = catalog.AddSource(added);

        vm.AddTableCommand.Execute(null);
        await vm.LastLoad;

        Assert.Same(addTableResult, vm.SelectedSource);
        Assert.Equal(new[] { LoadMode.IfStale }, added.Modes);
        Assert.Equal(new[] { "NewSite" }, vm.Rows.Select(r => r.SiteName));
    }

    [Fact]
    public async Task Closing_the_window_cancels_the_running_load()
    {
        var source = new FakeTableSource("Clinic", writable: true)
        {
            OnLoad = async (_, _, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                throw new InvalidOperationException("unreachable");
            },
        };
        catalog.AddSource(source);
        OnlineTemplatesViewModel vm = CreateViewModel();
        Task load = vm.StartAsync();

        vm.Shutdown();
        await load;

        Assert.True(source.Tokens[0].IsCancellationRequested);
        Assert.False(vm.IsBusy);
        Assert.Empty(dialogs.Shown);
    }

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(59, "just now")]
    [InlineData(60, "1 minute ago")]
    [InlineData(59 * 60, "59 minutes ago")]
    [InlineData(3600, "1 hour ago")]
    [InlineData(47 * 3600, "47 hours ago")]
    [InlineData(48 * 3600, "2 days ago")]
    [InlineData(-30, "just now")]
    public void Cache_age_is_described_in_whole_units(int seconds, string expected)
    {
        Assert.Equal(expected, OnlineTemplatesViewModel.FormatAge(TimeSpan.FromSeconds(seconds)));
    }
}
