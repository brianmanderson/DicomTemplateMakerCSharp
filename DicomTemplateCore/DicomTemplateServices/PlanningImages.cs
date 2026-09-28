using System;
using System.Collections.Generic;
using System.Linq;
using FellowOakDicom;

namespace DicomTemplateMakerGUI.DicomTemplateServices
{
    /// <summary>
    /// Which image series get an RT: volumetric planning images (CT, MR and PET), not localizers (scouts, topograms)
    /// or Secondary Capture objects such as scanner dose reports and screenshots, which often share the CT's Frame of
    /// Reference and would put a second structure set next to it at import.
    /// </summary>
    internal static class PlanningImages
    {
        /// <summary>The modalities that get an RT.</summary>
        public static readonly IReadOnlyList<string> Modalities = new[] { "CT", "MR", "PT" };

        /// <summary>Secondary Capture Image Storage; the multi-frame SC classes extend this UID.</summary>
        private const string SecondaryCaptureSopClassRoot = "1.2.840.10008.5.1.4.1.1.7";

        /// <summary>Why the series whose first image is <paramref name="first"/> gets no RT, or null when it gets one.</summary>
        public static string? WhyNotPlanningSeries(DicomDataset first)
        {
            ArgumentNullException.ThrowIfNull(first);
            string sopClass = RtStructureBuilder.GetValue(first, DicomTag.SOPClassUID)?.Trim() ?? string.Empty;
            if (sopClass == SecondaryCaptureSopClassRoot || sopClass.StartsWith(SecondaryCaptureSopClassRoot + ".", StringComparison.Ordinal))
            {
                return "The series is a Secondary Capture (for example a dose report or a screenshot), not a planning image; no RT is written for it.";
            }

            string modality = RtStructureBuilder.GetValue(first, DicomTag.Modality)?.Trim().ToUpperInvariant() ?? string.Empty;
            if (!Modalities.Contains(modality, StringComparer.Ordinal))
            {
                return modality.Length == 0
                    ? "The series has no Modality, so it is not known to be a planning image; no RT is written for it."
                    : $"The series' modality is {modality}; only {string.Join(", ", Modalities)} series get an RT.";
            }

            if (first.TryGetValues(DicomTag.ImageType, out string[]? imageType) && imageType != null
                && imageType.Any(v => string.Equals(v?.Trim(), "LOCALIZER", StringComparison.OrdinalIgnoreCase)))
            {
                return "The series is a localizer (scout or topogram), not a planning image; no RT is written for it.";
            }

            return null;
        }
    }
}
