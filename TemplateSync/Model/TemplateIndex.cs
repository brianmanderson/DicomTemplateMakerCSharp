using System;
using System.Collections.Generic;
using System.Linq;

namespace TemplateSync.Model
{
    /// <summary>An ROI as it appears in one site's template.</summary>
    public sealed class SiteRoi
    {
        public SiteRoi(AirTableEntry entry, bool include)
        {
            Entry = entry;
            Include = include;
        }

        public AirTableEntry Entry { get; }

        /// <summary>True when the site lists the ROI under Template_Recommend, false for Template_Consider.</summary>
        public bool Include { get; }
    }

    /// <summary>
    /// Groups table entries into per-site templates. Reproduces the previous ReadAirTable.add_roi
    /// rules: records are visited in table order, Template_Recommend sites before Template_Consider
    /// sites, and within a site the first entry with a given Structure wins.
    /// </summary>
    public sealed class TemplateIndex
    {
        private readonly Dictionary<string, List<SiteRoi>> _sites;

        private TemplateIndex(Dictionary<string, List<SiteRoi>> sites)
        {
            _sites = sites;
        }

        public IReadOnlyCollection<string> SiteNames => _sites.Keys;

        public IReadOnlyList<SiteRoi> GetSite(string site)
        {
            return _sites.TryGetValue(site, out List<SiteRoi>? rois) ? rois : (IReadOnlyList<SiteRoi>)Array.Empty<SiteRoi>();
        }

        public static TemplateIndex Build(IEnumerable<AirTableEntry> entries)
        {
            var sites = new Dictionary<string, List<SiteRoi>>(StringComparer.Ordinal);
            foreach (AirTableEntry entry in entries)
            {
                foreach (string site in entry.Template_Recommend ?? Enumerable.Empty<string>())
                {
                    Add(sites, site, entry, include: true);
                }

                foreach (string site in entry.Template_Consider ?? Enumerable.Empty<string>())
                {
                    Add(sites, site, entry, include: false);
                }
            }

            return new TemplateIndex(sites);
        }

        private static void Add(Dictionary<string, List<SiteRoi>> sites, string site, AirTableEntry entry, bool include)
        {
            if (site == null)
            {
                return;
            }

            if (!sites.TryGetValue(site, out List<SiteRoi>? rois))
            {
                rois = new List<SiteRoi>();
                sites.Add(site, rois);
            }

            if (!rois.Any(existing => string.Equals(existing.Entry.Structure, entry.Structure, StringComparison.Ordinal)))
            {
                rois.Add(new SiteRoi(entry, include));
            }
        }
    }
}
