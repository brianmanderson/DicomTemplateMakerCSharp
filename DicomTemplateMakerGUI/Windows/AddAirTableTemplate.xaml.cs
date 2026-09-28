using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using DicomTemplateMakerGUI.Services;
using TemplateSync.Credentials;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Connects a user's own Airtable table. Values are validated, the connection is proven with a
    /// single one-record request, and the token is stored encrypted (never as a plain-text file).
    /// </summary>
    public partial class AddAirTableTemplate : Window
    {
        private readonly TemplateSourceCatalog catalog;

        public AddAirTableTemplate(TemplateSourceCatalog catalog)
        {
            InitializeComponent();
            this.catalog = catalog;
        }

        /// <summary>The source that was added, or null if the dialog was cancelled.</summary>
        public TemplateSourceItem? AddedSource { get; private set; }

        private void AddAirTableTextUpdate(object sender, TextChangedEventArgs e)
        {
            Revalidate();
        }

        private void AddAirTablePasswordUpdate(object sender, RoutedEventArgs e)
        {
            Revalidate();
        }

        private void Revalidate()
        {
            bool allFilled = TableName_TextBox.Text.Trim().Length > 0
                && API_PasswordBox.Password.Trim().Length > 0
                && Base_TextBox.Text.Trim().Length > 0
                && Table_TextBox.Text.Trim().Length > 0;
            string? problem = allFilled
                ? AirtableIds.Validate(TableName_TextBox.Text, Base_TextBox.Text, Table_TextBox.Text, API_PasswordBox.Password)
                : null;
            Validation_Text.Text = problem ?? string.Empty;
            AddAirTableButton.IsEnabled = allFilled && problem == null;
        }

        private async void AddAirTableButton_Click(object sender, RoutedEventArgs e)
        {
            string name = TableName_TextBox.Text.Trim();
            string baseId = Base_TextBox.Text.Trim();
            string table = Table_TextBox.Text.Trim();
            string token = API_PasswordBox.Password.Trim();
            AddAirTableButton.IsEnabled = false;
            AddAirTableButton.Content = "Testing connection...";
            Validation_Text.Text = string.Empty;
            try
            {
                string? problem = await catalog.TestConnectionAsync(baseId, table, token, CancellationToken.None);
                if (problem != null)
                {
                    Validation_Text.Text = problem;
                    return;
                }

                AddedSource = catalog.AddConnection(name, baseId, table, token);
                Close();
            }
            catch (Exception ex)
            {
                // UI boundary: keep the dialog open and explain what went wrong.
                Validation_Text.Text = "Could not add the table: " + ex.Message;
            }
            finally
            {
                AddAirTableButton.Content = "Test connection and add";
                if (IsLoaded)
                {
                    Revalidate();
                }
            }
        }
    }
}
