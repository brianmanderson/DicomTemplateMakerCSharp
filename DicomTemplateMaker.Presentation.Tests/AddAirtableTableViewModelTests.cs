using DicomTemplateMaker.Presentation.Tests.Fakes;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.ViewModels;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

public sealed class AddAirtableTableViewModelTests
{
    // Built at run time so no string in the source matches the real token format (keeps push protection quiet).
    private static readonly string Token = "pat" + new string('A', 14) + "." + string.Concat(Enumerable.Repeat("0f", 32));
    private const string BaseId = "appAAAAAAAAAAAAAA";
    private const string TableId = "tblBBBBBBBBBBBBBB";
    private const string OtherTableId = "tblCCCCCCCCCCCCCC";

    private readonly FakeCatalog catalog = new();
    private readonly FakeDialogService dialogs = new();
    private int closeRequests;

    private AddAirtableTableViewModel Create(Func<string, bool>? isLeaked = null)
    {
        var vm = new AddAirtableTableViewModel(catalog, dialogs, isLeaked ?? (_ => false));
        vm.CloseRequested += (_, _) => closeRequests++;
        return vm;
    }

    private AddAirtableTableViewModel CreateFilled(string name = "Clinic")
    {
        AddAirtableTableViewModel vm = Create();
        vm.Name = name;
        vm.Token = Token;
        vm.BaseId = BaseId;
        vm.Table = TableId;
        return vm;
    }

    [Fact]
    public void Empty_fields_show_hints_rather_than_errors_and_nothing_can_be_tested_or_saved()
    {
        AddAirtableTableViewModel vm = Create();

        Assert.All(new[] { vm.NameStatus, vm.TokenStatus, vm.BaseIdStatus, vm.TableStatus }, s => Assert.Equal(FieldSeverity.Info, s.Severity));
        Assert.StartsWith("Required:", vm.BaseIdStatus.Text, StringComparison.Ordinal);
        Assert.False(vm.TestConnectionCommand.CanExecute(null));
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Valid_values_have_no_messages_and_can_be_tested_and_saved()
    {
        AddAirtableTableViewModel vm = CreateFilled();

        Assert.All(new[] { vm.NameStatus, vm.TokenStatus, vm.BaseIdStatus, vm.TableStatus }, s => Assert.Equal(FieldStatus.Empty, s));
        Assert.True(vm.TestConnectionCommand.CanExecute(null));
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    // N20: each field is validated inline with a visible reason.
    [Theory]
    [InlineData("app123")]
    [InlineData("appAAAAAAAAAAAAA")]
    [InlineData("appAAAAAAAAAAAAAAA")]
    [InlineData("APPAAAAAAAAAAAAAA")]
    [InlineData("appAAAAAAAAAAAA-A")]
    [InlineData("tblBBBBBBBBBBBBBB")]
    public void A_malformed_base_id_is_an_error_with_the_expected_format(string baseId)
    {
        AddAirtableTableViewModel vm = CreateFilled();

        vm.BaseId = baseId;

        Assert.Equal(FieldSeverity.Error, vm.BaseIdStatus.Severity);
        Assert.Contains("app followed by 14 letters or digits", vm.BaseIdStatus.Text, StringComparison.Ordinal);
        Assert.False(vm.TestConnectionCommand.CanExecute(null));
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void A_base_id_with_surrounding_spaces_is_accepted()
    {
        AddAirtableTableViewModel vm = CreateFilled();

        vm.BaseId = "  " + BaseId + " ";

        Assert.Equal(FieldStatus.Empty, vm.BaseIdStatus);
    }

    [Theory]
    [InlineData("tbl123")]
    [InlineData("tblBBBBBBBBBBBBB")]
    [InlineData("tblBBBBBBBBBBBBBB_")]
    public void A_value_starting_with_tbl_must_be_a_table_id(string table)
    {
        AddAirtableTableViewModel vm = CreateFilled();

        vm.Table = table;

        Assert.Equal(FieldSeverity.Error, vm.TableStatus.Severity);
        Assert.Equal("A table id is tbl followed by 14 letters or digits.", vm.TableStatus.Text);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("Templates")]
    [InlineData("TG263 structures")]
    [InlineData("Tbl templates")]
    public void Any_other_table_value_is_accepted_as_a_table_name_and_the_window_says_so(string table)
    {
        AddAirtableTableViewModel vm = CreateFilled();

        vm.Table = table;

        Assert.Equal(FieldSeverity.Info, vm.TableStatus.Severity);
        Assert.StartsWith("'" + table + "' will be used as the table name", vm.TableStatus.Text, StringComparison.Ordinal);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    // N20: Airtable documents tokens as opaque, so an unusual format only warns.
    [Theory]
    [InlineData("not-a-personal-access-token")]
    [InlineData("pat-short")]
    public void An_unusual_token_format_is_only_a_warning(string token)
    {
        AddAirtableTableViewModel vm = CreateFilled();

        vm.Token = token;

        Assert.Equal(FieldSeverity.Warning, vm.TokenStatus.Severity);
        Assert.Contains("does not look like a personal access token", vm.TokenStatus.Text, StringComparison.Ordinal);
        Assert.True(vm.TestConnectionCommand.CanExecute(null));
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void A_legacy_api_key_is_a_warning_that_names_the_problem()
    {
        AddAirtableTableViewModel vm = CreateFilled();

        vm.Token = "key" + new string('A', 14);

        Assert.Equal(FieldSeverity.Warning, vm.TokenStatus.Severity);
        Assert.Contains("legacy API key", vm.TokenStatus.Text, StringComparison.Ordinal);
        Assert.True(vm.TestConnectionCommand.CanExecute(null));
    }

    // N20: a token published with an old release is still refused.
    [Fact]
    public void A_known_leaked_token_is_rejected_and_blocks_testing_and_saving()
    {
        var checkedTokens = new List<string>();
        AddAirtableTableViewModel vm = Create(token =>
        {
            checkedTokens.Add(token);
            return token == Token;
        });
        vm.Name = "Clinic";
        vm.BaseId = BaseId;
        vm.Table = TableId;

        vm.Token = "  " + Token + "  ";

        Assert.Equal(FieldSeverity.Error, vm.TokenStatus.Severity);
        Assert.Contains("published in an old release", vm.TokenStatus.Text, StringComparison.Ordinal);
        Assert.Contains(Token, checkedTokens);
        Assert.False(vm.TestConnectionCommand.CanExecute(null));
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    // N20: a pasted table address fills in both ids, whichever field it is pasted into.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_pasted_table_address_fills_in_both_ids(bool intoBaseField)
    {
        AddAirtableTableViewModel vm = Create();
        string address = "https://airtable.com/" + BaseId + "/" + TableId + "/viwDDDDDDDDDDDDDD?blocks=hide";

        if (intoBaseField)
        {
            vm.BaseId = address;
        }
        else
        {
            vm.Table = address;
        }

        Assert.Equal(BaseId, vm.BaseId);
        Assert.Equal(TableId, vm.Table);
        Assert.Equal(FieldStatus.Empty, vm.BaseIdStatus);
        Assert.Equal(FieldStatus.Empty, vm.TableStatus);
    }

    [Fact]
    public void An_address_without_a_scheme_is_understood_too()
    {
        AddAirtableTableViewModel vm = Create();

        vm.BaseId = "airtable.com/" + BaseId + "/" + TableId;

        Assert.Equal(BaseId, vm.BaseId);
        Assert.Equal(TableId, vm.Table);
    }

    [Fact]
    public void An_address_without_a_table_id_fills_the_base_id_and_explains_what_is_missing()
    {
        AddAirtableTableViewModel vm = Create();
        string address = "https://airtable.com/" + BaseId;

        vm.Table = address;

        Assert.Equal(BaseId, vm.BaseId);
        Assert.Equal(address, vm.Table);
        Assert.Equal(FieldSeverity.Error, vm.TableStatus.Severity);
        Assert.Contains("no table id", vm.TableStatus.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_name_with_characters_not_allowed_in_file_names_is_an_error()
    {
        AddAirtableTableViewModel vm = CreateFilled("Clinic/Main");

        Assert.Equal(FieldSeverity.Error, vm.NameStatus.Severity);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.True(vm.TestConnectionCommand.CanExecute(null));
    }

    [Fact]
    public void The_name_of_the_shared_templates_cannot_be_used()
    {
        catalog.AddSource(new FakeTableSource(TemplateSourceCatalog.SharedSourceName, writable: false));

        AddAirtableTableViewModel vm = CreateFilled("tg-263 (SHARED)");

        Assert.Equal(FieldSeverity.Error, vm.NameStatus.Severity);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    // N20: a same-name connection used to be replaced silently.
    [Fact]
    public async Task Saving_a_name_that_is_already_connected_asks_first_and_declining_changes_nothing()
    {
        catalog.AddSource(new FakeTableSource("Clinic", writable: true));
        AddAirtableTableViewModel vm = CreateFilled("clinic ");
        Assert.Equal(FieldSeverity.Warning, vm.NameStatus.Severity);
        dialogs.Answers.Enqueue(false);

        await vm.SaveCommand.ExecuteAsync(null);

        ShownDialog question = Assert.Single(dialogs.Shown);
        Assert.Equal(AddAirtableTableViewModel.ReplaceTitle, question.Title);
        Assert.StartsWith("A table named 'Clinic' is already connected.", question.Message, StringComparison.Ordinal);
        Assert.Empty(catalog.Tests);
        Assert.Empty(catalog.Added);
        Assert.Null(vm.AddedSource);
        Assert.Equal(0, closeRequests);
    }

    [Fact]
    public async Task Saving_a_name_that_is_already_connected_replaces_it_when_confirmed()
    {
        catalog.AddSource(new FakeTableSource("Clinic", writable: true));
        AddAirtableTableViewModel vm = CreateFilled();
        dialogs.Answers.Enqueue(true);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Single(catalog.Tests);
        Assert.Equal(new[] { new AddedConnection("Clinic", BaseId, TableId, Token) }, catalog.Added);
        Assert.NotNull(vm.AddedSource);
        Assert.Equal(1, closeRequests);
    }

    [Fact]
    public async Task Save_tests_the_connection_first_and_a_failed_test_keeps_the_dialog_open()
    {
        catalog.OnTest = (_, _, _, _) => Task.FromResult<string?>("Airtable could not find the base, table or record (NOT_FOUND). Check the base and table ids.");
        AddAirtableTableViewModel vm = CreateFilled();

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new[] { (BaseId, TableId, Token) }, catalog.Tests);
        Assert.Equal(FieldSeverity.Error, vm.ConnectionStatus.Severity);
        Assert.StartsWith("Airtable could not find the base", vm.ConnectionStatus.Text, StringComparison.Ordinal);
        Assert.Empty(catalog.Added);
        Assert.Equal(0, closeRequests);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Save_trims_the_values_it_stores()
    {
        AddAirtableTableViewModel vm = Create();
        vm.Name = " Clinic ";
        vm.Token = " " + Token + " ";
        vm.BaseId = " " + BaseId;
        vm.Table = TableId + " ";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new[] { new AddedConnection("Clinic", BaseId, TableId, Token) }, catalog.Added);
    }

    [Fact]
    public async Task A_successful_test_is_not_repeated_on_save_while_the_values_are_unchanged()
    {
        AddAirtableTableViewModel vm = CreateFilled();

        await vm.TestConnectionCommand.ExecuteAsync(null);
        Assert.Equal(FieldSeverity.Success, vm.ConnectionStatus.Severity);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Single(catalog.Tests);
        Assert.Single(catalog.Added);
        Assert.Equal(1, closeRequests);
    }

    [Fact]
    public async Task Changing_a_value_after_a_test_clears_the_result_and_save_tests_again()
    {
        AddAirtableTableViewModel vm = CreateFilled();
        await vm.TestConnectionCommand.ExecuteAsync(null);

        vm.Table = OtherTableId;
        Assert.Equal(FieldStatus.Empty, vm.ConnectionStatus);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new[] { (BaseId, TableId, Token), (BaseId, OtherTableId, Token) }, catalog.Tests);
        Assert.Equal(OtherTableId, Assert.Single(catalog.Added).Table);
    }

    [Fact]
    public async Task Cancel_during_save_stops_the_test_saves_nothing_and_closes()
    {
        var testStarted = new TaskCompletionSource();
        catalog.OnTest = async (_, _, _, token) =>
        {
            testStarted.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return null;
        };
        AddAirtableTableViewModel vm = CreateFilled();

        Task save = vm.SaveCommand.ExecuteAsync(null);
        await testStarted.Task;
        Assert.True(vm.IsBusy);
        Assert.False(vm.CanEdit);
        vm.CancelCommand.Execute(null);
        await save;

        Assert.Equal(1, closeRequests);
        Assert.Empty(catalog.Added);
        Assert.Null(vm.AddedSource);
        Assert.False(vm.IsBusy);
        Assert.Equal(FieldSeverity.Info, vm.ConnectionStatus.Severity);
    }

    [Fact]
    public async Task A_storage_error_is_shown_and_the_dialog_stays_open()
    {
        catalog.AddFailure = new ArgumentException("The token could not be stored securely.");
        AddAirtableTableViewModel vm = CreateFilled();

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(FieldSeverity.Error, vm.ConnectionStatus.Severity);
        Assert.Equal("Could not add the table: The token could not be stored securely.", vm.ConnectionStatus.Text);
        Assert.Equal(0, closeRequests);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Status_text_carries_a_symbol_so_it_does_not_rely_on_colour()
    {
        Assert.Equal("✖ bad", FieldStatus.Error("bad").DisplayText);
        Assert.Equal("⚠ odd", FieldStatus.Warning("odd").DisplayText);
        Assert.Equal("✔ fine", FieldStatus.Success("fine").DisplayText);
        Assert.Equal("hint", FieldStatus.Info("hint").DisplayText);
    }
}
