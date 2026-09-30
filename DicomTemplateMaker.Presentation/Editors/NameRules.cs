using System;
using System.Collections.Generic;
using System.Linq;
using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.Editors
{
    /// <summary>
    /// Rules for template and ROI names, shared by the template editor and the rename window. A problem blocks the
    /// action and says why; a warning is shown but does not block.
    /// </summary>
    public static class NameRules
    {
        /// <summary>
        /// DICOM keeps this many characters of the Structure Set Label (the template name) and of the ROI Observation
        /// Label (the ROI name) in generated RTs.
        /// </summary>
        public const int DicomLabelLength = 16;

        /// <summary>The most characters DICOM allows in an ROI Name (VR LO).</summary>
        public const int MaxRoiNameLength = 64;

        // Windows rules, applied on every OS so the checks behave as on the PCs that run the program.
        private static readonly char[] InvalidFolderNameChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };

        private static readonly HashSet<string> ReservedDeviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³",
        };

        /// <summary>
        /// Why <paramref name="name"/> cannot be a template name (the template is one folder in the template folder),
        /// or null when it can.
        /// </summary>
        public static string? TemplateNameProblem(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Enter a template name.";
            }

            if (name.Trim().Length != name.Length)
            {
                return "The name starts or ends with a space. Remove it.";
            }

            if (name == "." || name == "..")
            {
                return "\"" + name + "\" cannot be used as a template name.";
            }

            List<string> invalid = InvalidCharacters(name);
            if (invalid.Count > 0)
            {
                return "The name contains characters that a folder name cannot contain: " + string.Join(" ", invalid) + ".";
            }

            if (name.EndsWith('.'))
            {
                return "The name ends with a dot, which Windows removes from folder names. Remove it.";
            }

            string stem = name.Split('.')[0].TrimEnd();
            if (ReservedDeviceNames.Contains(stem))
            {
                return "\"" + stem + "\" is reserved by Windows for a device and cannot be used as a folder name.";
            }

            if (string.Equals(name, TemplateRootResolver.OntologiesFolderName, StringComparison.OrdinalIgnoreCase))
            {
                return "\"" + TemplateRootResolver.OntologiesFolderName + "\" is the folder that holds the ontology library. Choose another name.";
            }

            return null;
        }

        /// <summary>The non-blocking warning for a template name longer than <see cref="DicomLabelLength"/>, or null.</summary>
        public static string? TemplateNameWarning(string? name)
        {
            if (name == null || name.Length <= DicomLabelLength)
            {
                return null;
            }

            return $"The name has {name.Length} characters. Generated RTs keep only the first {DicomLabelLength} as the structure set label (\"{name.Substring(0, DicomLabelLength)}\").";
        }

        /// <summary>
        /// Why <paramref name="name"/> cannot be the name of <paramref name="self"/> (null for a new ROI) among
        /// <paramref name="rois"/>, or null when it can. Names must differ ignoring case and surrounding spaces, since
        /// planning systems may treat "Brain" and "brain" as the same structure.
        /// </summary>
        public static string? RoiNameProblem(string? name, IEnumerable<ROIClass> rois, ROIClass? self)
        {
            ArgumentNullException.ThrowIfNull(rois);
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Enter an ROI name.";
            }

            if (name.Trim().Length != name.Length)
            {
                return "The name starts or ends with a space. Remove it.";
            }

            if (name.Contains('\\'))
            {
                return "The name contains a backslash (\\), which DICOM uses to separate values.";
            }

            if (name.Any(char.IsControl))
            {
                return "The name contains a control character such as a line break.";
            }

            if (name.Length > MaxRoiNameLength)
            {
                return $"The name has {name.Length} characters; DICOM allows at most {MaxRoiNameLength} in an ROI name.";
            }

            ROIClass? other = FindDuplicate(name, rois, self);
            if (other != null)
            {
                return "Another ROI is already named \"" + other.ROIName + "\" (names must differ, ignoring case).";
            }

            return null;
        }

        /// <summary>The non-blocking warning for an ROI name longer than <see cref="DicomLabelLength"/>, or null.</summary>
        public static string? RoiNameWarning(string? name)
        {
            if (name == null || name.Length <= DicomLabelLength)
            {
                return null;
            }

            return $"The name has {name.Length} characters. Generated RTs keep only the first {DicomLabelLength} as the ROI observation label "
                + $"(\"{name.Substring(0, DicomLabelLength)}\"), and some planning systems allow at most {DicomLabelLength} characters in a structure name.";
        }

        /// <summary>An ROI other than <paramref name="self"/> whose name equals <paramref name="name"/> ignoring case and surrounding spaces.</summary>
        public static ROIClass? FindDuplicate(string name, IEnumerable<ROIClass> rois, ROIClass? self)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(rois);
            string wanted = name.Trim();
            // ROIName is null for a hand-edited file that says "ROIName": null.
            return rois.FirstOrDefault(roi => !ReferenceEquals(roi, self) && string.Equals(((string?)roi.ROIName)?.Trim(), wanted, StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> InvalidCharacters(string name)
        {
            var found = new List<string>();
            foreach (char c in name)
            {
                string shown = char.IsControl(c) ? "(a control character)" : c.ToString();
                if ((char.IsControl(c) || Array.IndexOf(InvalidFolderNameChars, c) >= 0) && !found.Contains(shown))
                {
                    found.Add(shown);
                }
            }

            return found;
        }
    }
}
