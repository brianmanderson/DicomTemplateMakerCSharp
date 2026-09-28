using System;
using System.Collections.Generic;
using System.Linq;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.Editors
{
    /// <summary>The template editor's ROI list: which ROIs the search shows, and in what order.</summary>
    public static class RoiList
    {
        /// <summary>
        /// True when <paramref name="query"/> is blank or the ROI's name, ontology name or interpreted type contains it,
        /// ignoring case and surrounding spaces. A missing ontology or interpreted type simply does not match.
        /// </summary>
        public static bool Matches(ROIClass roi, string? query)
        {
            ArgumentNullException.ThrowIfNull(roi);
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            string wanted = query.Trim();
            return Contains(roi.ROIName, wanted) || Contains(roi.Ontology_Class?.CodeMeaning, wanted) || Contains(roi.ROI_Interpreted_type, wanted);
        }

        /// <summary>
        /// The ROIs the search shows: included ROIs first, then the others; within each, PTVs, CTVs and GTVs, then the
        /// rest, each group by name.
        /// </summary>
        public static IReadOnlyList<ROIClass> Arrange(IEnumerable<ROIClass> rois, string? query)
        {
            ArgumentNullException.ThrowIfNull(rois);
            return rois.Where(roi => Matches(roi, query))
                .OrderBy(roi => roi.Include ? 0 : 1)
                .ThenBy(Group)
                .ThenBy(roi => (string?)roi.ROIName ?? string.Empty, StringComparer.CurrentCulture)
                .ToList();
        }

        private static int Group(ROIClass roi)
        {
            return InterpretedTypes.Find(roi.ROI_Interpreted_type) switch
            {
                "PTV" => 0,
                "CTV" => 1,
                "GTV" => 2,
                _ => 3,
            };
        }

        // Values read from a hand-edited file can be null even where the model says otherwise.
        private static bool Contains(string? value, string wanted)
        {
            return value != null && value.Contains(wanted, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
