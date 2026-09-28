using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>
    /// What the online-template windows need from <see cref="TemplateSourceCatalog"/>: the source list
    /// and the add, test and remove operations for the user's own Airtable tables.
    /// </summary>
    public interface ITemplateSourceCatalog
    {
        /// <summary>The shared TG-263 snapshot first, then the user's Airtable tables.</summary>
        ObservableCollection<TemplateSourceItem> Sources { get; }

        /// <summary>One cheap request that proves the ids and token work. Returns the problem, or null when it works.</summary>
        Task<string?> TestConnectionAsync(string baseId, string table, string token, CancellationToken cancellationToken);

        /// <summary>Stores the connection (token encrypted) and lists it, replacing a table with the same name.</summary>
        TemplateSourceItem AddConnection(string name, string baseId, string table, string token);

        /// <summary>Forgets a user's table on this computer (token and cached copy). The shared source cannot be removed.</summary>
        void RemoveConnection(TemplateSourceItem item);
    }
}
