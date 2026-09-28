using System;
using System.Collections.Generic;
using System.Linq;

namespace DicomTemplateMakerGUI.Editors
{
    /// <summary>
    /// The RT ROI Interpreted Types offered for an ROI (DICOM defined terms), in the order the editors show them. The
    /// one list used by every editor.
    /// </summary>
    public static class InterpretedTypes
    {
        /// <summary>The type a new ROI starts with.</summary>
        public const string Default = "ORGAN";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            "ORGAN", "PTV", "CTV", "GTV", "MARKER", "AVOIDANCE", "CONTROL", "BOLUS", "EXTERNAL", "ISOCENTER", "REGISTRATION",
            "CONTRAST_AGENT", "CAVITY", "BRACHY_CHANNEL", "BRACHY_ACCESSORY", "SUPPORT", "FIXATION", "DOSE_REGION",
            "DOSE_MEASUREMENT", "BRACHY_SRC_APP", "TREATED_VOLUME", "IRRAD_VOLUME",
        };

        /// <summary>
        /// The entry of <see cref="All"/> that <paramref name="value"/> names, ignoring case and surrounding spaces (a
        /// Varian XML file may say "Organ"); null when the value is missing, blank or not a known type.
        /// </summary>
        public static string? Find(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string wanted = value.Trim();
            return All.FirstOrDefault(type => string.Equals(type, wanted, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Why an ROI with interpreted type <paramref name="value"/> needs attention, or null when the type is one of
        /// <see cref="All"/>.
        /// </summary>
        public static string? Problem(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "No interpreted type: choose one, or generated RTs leave this ROI out.";
            }

            return Find(value) == null ? "Unknown interpreted type \"" + value.Trim() + "\": choose one from the list." : null;
        }
    }
}
