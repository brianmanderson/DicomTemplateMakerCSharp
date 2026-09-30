using System.Collections.Generic;
using System.IO;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.Editors
{
    /// <summary>Read-only facts about a template folder, for confirmations.</summary>
    public static class ExistingTemplate
    {
        /// <summary>
        /// The number of ROIs in the folder's All_ROIs.json, or null when there is none or it cannot be read. Nothing is
        /// written: legacy ROI files are not migrated here.
        /// </summary>
        public static int? CountRois(string folder)
        {
            if (!File.Exists(Path.Combine(folder, ROIClassTools.RoisFileName)))
            {
                return null;
            }

            // A throwaway library: reading the file must not change the caller's lists.
            return ROIClassTools.TryLoadROIsFromFolder(folder, new List<OntologyCodeClass>(), out List<ROIClass> rois, out _) ? rois.Count : null;
        }
    }
}
