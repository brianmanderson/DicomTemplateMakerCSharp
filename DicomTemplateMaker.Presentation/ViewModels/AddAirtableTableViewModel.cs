using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DicomTemplateMakerGUI.Services;
using TemplateSync.Credentials;

namespace DicomTemplateMakerGUI.ViewModels
{
    /// <summary>
    /// "Add an Airtable table": connects a user's own table. Every field is checked as it is typed, the
    /// connection is proven with a single one-record request before saving, and the token is stored
    /// encrypted by the catalog (never as a plain-text file).
    /// </summary>
    public sealed partial class AddAirtableTableViewModel : ObservableObject
    {
        public const string ReplaceTitle = "Replace Airtable table";

        private readonly ITemplateSourceCatalog catalog;
        private readonly IDialogService dialogs;
        private readonly Func<string, bool> isLeakedToken;
        private CancellationTokenSource? operation;
        private ConnectionValues? verified;
        private bool expandingLink;

        /// <summary>False while the constructor assigns the fields, so validation never sees half-initialised values.</summary>
        private bool constructed;

        /// <param name="catalog">Tests and stores the connection.</param>
        /// <param name="dialogs">Asks before an existing table with the same name is replaced.</param>
        /// <param name="isLeakedToken">Recognises tokens published with old releases; <see cref="LeakedTokens.IsKnownLeaked"/> when null.</param>
        public AddAirtableTableViewModel(ITemplateSourceCatalog catalog, IDialogService dialogs, Func<string, bool>? isLeakedToken = null)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            this.isLeakedToken = isLeakedToken ?? (token => LeakedTokens.IsKnownLeaked(token));
            Name = string.Empty;
            Token = string.Empty;
            BaseId = string.Empty;
            Table = string.Empty;
            NameStatus = FieldStatus.Empty;
            TokenStatus = FieldStatus.Empty;
            BaseIdStatus = FieldStatus.Empty;
            TableStatus = FieldStatus.Empty;
            ConnectionStatus = FieldStatus.Empty;
            constructed = true;
            Validate();
        }

        /// <summary>Raised when the dialog should close: after saving, or on Cancel.</summary>
        public event EventHandler? CloseRequested;

        /// <summary>The source that was added, or null when nothing was saved.</summary>
        public TemplateSourceItem? AddedSource { get; private set; }

        /// <summary>Shown in the list of template sources.</summary>
        [ObservableProperty]
        public partial string Name { get; set; }

        /// <summary>The personal access token (masked in the view).</summary>
        [ObservableProperty]
        public partial string Token { get; set; }

        /// <summary>app + 14 letters or digits, or a pasted table address (both ids are then filled in).</summary>
        [ObservableProperty]
        public partial string BaseId { get; set; }

        /// <summary>tbl + 14 letters or digits, the table's exact name, or a pasted table address.</summary>
        [ObservableProperty]
        public partial string Table { get; set; }

        [ObservableProperty]
        public partial FieldStatus NameStatus { get; private set; }

        [ObservableProperty]
        public partial FieldStatus TokenStatus { get; private set; }

        [ObservableProperty]
        public partial FieldStatus BaseIdStatus { get; private set; }

        [ObservableProperty]
        public partial FieldStatus TableStatus { get; private set; }

        /// <summary>The result of the last connection test (or of saving).</summary>
        [ObservableProperty]
        public partial FieldStatus ConnectionStatus { get; private set; }

        /// <summary>True while the connection is being tested or saved; the fields are read-only meanwhile.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEdit))]
        public partial bool IsBusy { get; private set; }

        public bool CanEdit => !IsBusy;

        /// <summary>Cancels a running test; called when the window closes.</summary>
        public void Shutdown()
        {
            operation?.Cancel();
        }

        [RelayCommand(CanExecute = nameof(CanTestConnection))]
        private async Task TestConnectionAsync()
        {
            ConnectionValues values = CurrentValues();
            CancellationTokenSource cts = BeginOperation();
            try
            {
                await VerifyAsync(values, cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                ConnectionStatus = FieldStatus.Info("Connection test cancelled.");
            }
            catch (Exception ex)
            {
                // UI boundary: explain instead of crashing.
                ConnectionStatus = FieldStatus.Error("The connection test failed: " + ex.Message);
            }
            finally
            {
                EndOperation(cts);
            }
        }

        private bool CanTestConnection()
        {
            return !IsBusy
                && Token.Trim().Length > 0 && BaseId.Trim().Length > 0 && Table.Trim().Length > 0
                && !TokenStatus.IsError && !BaseIdStatus.IsError && !TableStatus.IsError;
        }

        /// <summary>
        /// Asks before replacing a table with the same name, tests the connection unless these exact values
        /// were just tested successfully, then stores it and closes.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            string name = Name.Trim();
            ConnectionValues values = CurrentValues();
            TemplateSourceItem? existing = catalog.Sources.FirstOrDefault(s => s.IsWritable && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                string question = "A table named '" + existing.Name + "' is already connected." + Environment.NewLine + Environment.NewLine +
                    "Replace it with this base id, table and token? Only the settings saved on this computer change; no Airtable table is changed.";
                if (!dialogs.Confirm(ReplaceTitle, question))
                {
                    return;
                }
            }

            CancellationTokenSource cts = BeginOperation();
            bool saved = false;
            try
            {
                if (values.Equals(verified) || await VerifyAsync(values, cts.Token))
                {
                    AddedSource = catalog.AddConnection(name, values.BaseId, values.Table, values.Token);
                    ConnectionStatus = FieldStatus.Success("Saved.");
                    saved = true;
                }
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                ConnectionStatus = FieldStatus.Info("Cancelled; nothing was saved.");
            }
            catch (Exception ex)
            {
                // UI boundary: keep the dialog open and explain what went wrong.
                ConnectionStatus = FieldStatus.Error("Could not add the table: " + ex.Message);
            }
            finally
            {
                EndOperation(cts);
            }

            if (saved)
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool CanSave() => CanTestConnection() && Name.Trim().Length > 0 && !NameStatus.IsError;

        [RelayCommand]
        private void Cancel()
        {
            operation?.Cancel();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        partial void OnNameChanged(string value)
        {
            if (constructed)
            {
                Validate();
            }
        }

        partial void OnTokenChanged(string value)
        {
            if (constructed)
            {
                ConnectionFieldChanged();
            }
        }

        partial void OnBaseIdChanged(string value)
        {
            if (constructed && !ExpandLink(value))
            {
                ConnectionFieldChanged();
            }
        }

        partial void OnTableChanged(string value)
        {
            if (constructed && !ExpandLink(value))
            {
                ConnectionFieldChanged();
            }
        }

        partial void OnIsBusyChanged(bool value)
        {
            TestConnectionCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
        }

        /// <summary>Tests the connection; true when it works. The outcome is shown in <see cref="ConnectionStatus"/>.</summary>
        private async Task<bool> VerifyAsync(ConnectionValues values, CancellationToken cancellationToken)
        {
            ConnectionStatus = FieldStatus.Info("Testing the connection…");
            string? problem = await catalog.TestConnectionAsync(values.BaseId, values.Table, values.Token, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (problem != null)
            {
                ConnectionStatus = FieldStatus.Error(problem);
                return false;
            }

            verified = values;
            ConnectionStatus = FieldStatus.Success("The connection works: the table was found and the token can read it.");
            return true;
        }

        /// <summary>
        /// A pasted table address (https://airtable.com/app…/tbl…) in either id field fills in both ids.
        /// Returns true when the fields were rewritten (and validated).
        /// </summary>
        private bool ExpandLink(string value)
        {
            if (expandingLink || !AirtableLink.TryParse(value, out string? baseId, out string? tableId) || (baseId == null && tableId == null))
            {
                return false;
            }

            expandingLink = true;
            try
            {
                if (baseId != null)
                {
                    BaseId = baseId;
                }

                if (tableId != null)
                {
                    Table = tableId;
                }
            }
            finally
            {
                expandingLink = false;
            }

            ConnectionFieldChanged();
            return true;
        }

        private void ConnectionFieldChanged()
        {
            // A test result only speaks for the values that were tested.
            if (!IsBusy)
            {
                ConnectionStatus = CurrentValues().Equals(verified)
                    ? FieldStatus.Success("The connection works: the table was found and the token can read it.")
                    : FieldStatus.Empty;
            }

            Validate();
        }

        private void Validate()
        {
            NameStatus = ValidateName(Name);
            TokenStatus = ValidateToken(Token);
            BaseIdStatus = ValidateBaseId(BaseId);
            TableStatus = ValidateTable(Table);
            TestConnectionCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
        }

        private FieldStatus ValidateName(string value)
        {
            string name = value.Trim();
            if (name.Length == 0)
            {
                return FieldStatus.Info("Required: the name shown in the list of template sources.");
            }

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return FieldStatus.Error("The name contains characters that are not allowed in file names.");
            }

            TemplateSourceItem? same = catalog.Sources.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            if (same != null && !same.IsWritable)
            {
                return FieldStatus.Error("'" + same.Name + "' is the name of the shared templates; choose another name.");
            }

            if (same != null)
            {
                return FieldStatus.Warning("A table named '" + same.Name + "' is already connected. Saving replaces it (you will be asked first).");
            }

            return FieldStatus.Empty;
        }

        private FieldStatus ValidateToken(string value)
        {
            string token = value.Trim();
            if (token.Length == 0)
            {
                return FieldStatus.Info("Required: create a personal access token at airtable.com/create/tokens with the data.records:read and data.records:write scopes for this base.");
            }

            if (isLeakedToken(token))
            {
                return FieldStatus.Error("This token was published in an old release of this program and must not be used. Create your own token at airtable.com/create/tokens.");
            }

            if (token.StartsWith("key", StringComparison.Ordinal))
            {
                return FieldStatus.Warning("This looks like a legacy API key; Airtable no longer accepts those. Use a personal access token (it starts with \"pat\").");
            }

            if (!AirtableIds.LooksLikePersonalAccessToken(token))
            {
                return FieldStatus.Warning("This does not look like a personal access token (usually \"pat\", 14 characters, a dot, then 64 more). You can still test it.");
            }

            return FieldStatus.Empty;
        }

        private static FieldStatus ValidateBaseId(string value)
        {
            string id = value.Trim();
            if (id.Length == 0)
            {
                return FieldStatus.Info("Required: app followed by 14 letters or digits. You can also paste the table's address (https://airtable.com/app…/tbl…).");
            }

            if (AirtableLink.LooksLikeLink(id))
            {
                return FieldStatus.Error("That address has no base id (app…). Open the table in Airtable and copy the address from the browser.");
            }

            if (!AirtableIds.IsBaseId(id))
            {
                return FieldStatus.Error("A base id is app followed by 14 letters or digits (copy it from the base's address).");
            }

            return FieldStatus.Empty;
        }

        private static FieldStatus ValidateTable(string value)
        {
            string table = value.Trim();
            if (table.Length == 0)
            {
                return FieldStatus.Info("Required: the table id (tbl followed by 14 letters or digits) or the table's exact name.");
            }

            if (AirtableLink.LooksLikeLink(table))
            {
                return FieldStatus.Error("That address has no table id (tbl…). Open the table itself in Airtable and copy the address again.");
            }

            if (table.StartsWith("tbl", StringComparison.Ordinal))
            {
                return AirtableIds.IsTableId(table)
                    ? FieldStatus.Empty
                    : FieldStatus.Error("A table id is tbl followed by 14 letters or digits.");
            }

            return FieldStatus.Info("'" + table + "' will be used as the table name, which must match Airtable exactly. A table id (tbl…) keeps working if the table is renamed.");
        }

        private ConnectionValues CurrentValues() => new ConnectionValues(BaseId.Trim(), Table.Trim(), Token.Trim());

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
            if (ReferenceEquals(operation, cts))
            {
                operation = null;
                IsBusy = false;
            }

            cts.Dispose();
        }

        /// <summary>The values a connection test was run with.</summary>
        private sealed record ConnectionValues(string BaseId, string Table, string Token)
        {
            /// <summary>Never includes the token, so it cannot end up in a log or a debugger display.</summary>
            public override string ToString() => BaseId + "/" + Table;
        }
    }
}
