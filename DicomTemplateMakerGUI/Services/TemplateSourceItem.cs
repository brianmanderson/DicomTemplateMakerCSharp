using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ROIOntologyClass;
using TemplateSync.Model;
using TemplateSync.Storage;
using TemplateSync.Sync;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>
    /// One template table as the windows see it: the shared TG-263 snapshot or a user's Airtable
    /// table. Replaces the former ReadAirTable class; every operation is an awaitable Task.
    /// </summary>
    public sealed class TemplateSourceItem : INotifyPropertyChanged
    {
        private string statusText;
        private TemplateIndex index = TemplateIndex.Build(Enumerable.Empty<AirTableEntry>());

        public TemplateSourceItem(ITemplateTableSource source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            statusText = "Not loaded yet.";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ITemplateTableSource Source { get; }

        public string Name => Source.Name;

        public bool IsWritable => Source.IsWritable;

        public bool HasData => Source.Current != null;

        /// <summary>Human-readable freshness, e.g. "246 records \u00B7 updated 2026-09-27 14:03".</summary>
        public string StatusText
        {
            get { return statusText; }
            private set
            {
                statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public TemplateIndex Index
        {
            get { return index; }
            private set
            {
                index = value;
                OnPropertyChanged(nameof(Index));
            }
        }

        public async Task<TableLoadResult> LoadAsync(LoadMode mode, IProgress<string> progress, CancellationToken cancellationToken)
        {
            TableLoadResult result = await Source.LoadAsync(mode, progress, cancellationToken);
            Rebuild();
            StatusText = Describe(result.Warning);
            return result;
        }

        /// <summary>Fresh ROI objects for one site (safe to rename for language/laterality choices).</summary>
        public List<ROIWrapper> BuildRoiWrappers(string site, ICollection<string> warnings)
        {
            var wrappers = new List<ROIWrapper>();
            foreach (SiteRoi siteRoi in Index.GetSite(site))
            {
                ROIWrapper wrapper = TemplateEntryMapper.ToRoiWrapper(siteRoi, warnings);
                if (wrapper != null)
                {
                    wrappers.Add(wrapper);
                }
            }

            return wrappers;
        }

        public async Task<WriteResult> WriteTemplateAsync(string site, IEnumerable<ROIClass> rois, IProgress<string> progress, CancellationToken cancellationToken)
        {
            IReadOnlyList<WriteResult> results = await WriteTemplatesAsync(new[] { new KeyValuePair<string, IEnumerable<ROIClass>>(site, rois) }, progress, cancellationToken);
            return results[0];
        }

        /// <summary>Writes several templates with a single refresh of the table first.</summary>
        public async Task<IReadOnlyList<WriteResult>> WriteTemplatesAsync(IEnumerable<KeyValuePair<string, IEnumerable<ROIClass>>> templates, IProgress<string> progress, CancellationToken cancellationToken)
        {
            var unavailable = Source as UnavailableAirtableSource;
            if (unavailable != null)
            {
                throw new InvalidOperationException(unavailable.Problem);
            }

            var table = Source as AirtableTableSource;
            if (table == null)
            {
                throw new InvalidOperationException(Name + " is read-only.");
            }

            List<TemplateWrite> writes = templates
                .Select(t => new TemplateWrite(t.Key, t.Value.Select(roi => new LocalRoi(TemplateEntryMapper.ToDesiredEntry(roi), roi.Include)).ToList()))
                .ToList();
            try
            {
                return await table.WriteTemplatesAsync(writes, progress, cancellationToken);
            }
            finally
            {
                Rebuild();
                StatusText = Describe(null);
            }
        }

        private void Rebuild()
        {
            TableSnapshot snapshot = Source.Current;
            Index = snapshot == null
                ? TemplateIndex.Build(Enumerable.Empty<AirTableEntry>())
                : TemplateIndex.Build(snapshot.Records.Select(AirTableEntry.FromRecord));
        }

        private string Describe(string warning)
        {
            TableSnapshot snapshot = Source.Current;
            string text;
            if (snapshot == null)
            {
                text = IsWritable ? "Not downloaded yet. Press Refresh." : "Shared templates are not available yet.";
            }
            else
            {
                string when = snapshot.GeneratedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
                text = IsWritable
                    ? snapshot.RecordCount + " records \u00B7 updated " + when
                    : snapshot.RecordCount + " records \u00B7 published " + when;
                var shared = Source as SnapshotTableSource;
                if (shared != null && shared.CurrentOrigin == LoadOrigin.Bundled)
                {
                    text += " (copy shipped with the program)";
                }
            }

            return string.IsNullOrEmpty(warning) ? text : text + Environment.NewLine + "\u26A0 " + warning;
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
