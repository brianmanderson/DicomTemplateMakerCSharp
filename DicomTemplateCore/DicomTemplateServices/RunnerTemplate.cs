using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.DicomTemplateServices
{
    /// <summary>One template as the runner uses it: ROIs, paths to scan and matching requirements.</summary>
    internal sealed class RunnerTemplate
    {
        public const string PathsFileName = "Paths.txt";
        public const string DicomTagsFileName = "DicomTags.txt";

        private RunnerTemplate(string name, string folder, IReadOnlyList<ROIClass> rois, IReadOnlyList<string> paths, TemplateRequirements requirements)
        {
            Name = name;
            Folder = folder;
            Rois = rois;
            Paths = paths;
            Requirements = requirements;
            Signature = ComputeSignature(requirements, rois);
        }

        public string Name { get; }

        public string Folder { get; }

        public IReadOnlyList<ROIClass> Rois { get; }

        /// <summary>The non-blank lines of Paths.txt (empty when the template was loaded without them).</summary>
        public IReadOnlyList<string> Paths { get; }

        public TemplateRequirements Requirements { get; }

        /// <summary>A hash of the requirements and ROIs: when it changes, folders are decided again.</summary>
        public string Signature { get; }

        /// <summary>True for a folder the runner treats as a template: it has Paths.txt and ROIs (JSON or legacy).</summary>
        public static bool IsRunnable(string folder)
        {
            return File.Exists(System.IO.Path.Combine(folder, PathsFileName)) && ROIClassTools.IsValidTemplateFolder(folder);
        }

        /// <summary>Loads a template folder. Any exception means the template cannot be used.</summary>
        /// <param name="folder">The template folder.</param>
        /// <param name="readPaths">Whether to read Paths.txt.</param>
        /// <param name="warnings">
        /// Receives legacy ROI files that were not migrated, or that are still next to All_ROIs.json and so are not
        /// used (one line each); null to ignore them.
        /// </param>
        public static RunnerTemplate Load(string folder, bool readPaths, ICollection<string>? warnings = null)
        {
            string name = System.IO.Path.GetFileName(folder);
            List<ROIClass> rois = ROIClassTools.LoadROIsFromFolder(folder, new List<OntologyCodeClass>(), warnings);
            IReadOnlyList<string> paths = readPaths ? ReadPaths(folder) : Array.Empty<string>();
            string tagsFile = System.IO.Path.Combine(folder, DicomTagsFileName);
            TemplateRequirements requirements = File.Exists(tagsFile)
                ? TemplateRequirements.Parse(SharedFile.ReadAllLines(tagsFile))
                : TemplateRequirements.None;
            return new RunnerTemplate(name, folder, rois, paths, requirements);
        }

        public static IReadOnlyList<string> ReadPaths(string folder)
        {
            return SharedFile.ReadAllLines(System.IO.Path.Combine(folder, PathsFileName))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
        }

        private static string ComputeSignature(TemplateRequirements requirements, IReadOnlyList<ROIClass> rois)
        {
            var text = new StringBuilder();
            foreach (string value in requirements.SeriesDescriptions)
            {
                text.Append("series\u001f").Append(value).Append('\u001e');
            }

            foreach (string value in requirements.StudyDescriptions)
            {
                text.Append("study\u001f").Append(value).Append('\u001e');
            }

            foreach (ROIClass roi in rois)
            {
                OntologyCodeClass? code = roi.Ontology_Class;
                text.Append("roi\u001f").Append(roi.ROIName)
                    .Append('\u001f').Append(roi.ROI_Interpreted_type)
                    .Append('\u001f').Append(roi.color_string)
                    .Append('\u001f').Append(code == null ? "\u0000" : string.Join("\u001d", code.CodeMeaning, code.CodeValue, code.Scheme, code.ContextGroupVersion,
                        code.ContextIdentifier, code.ContextUID, code.MappingResource, code.MappingResourceName, code.MappingResourceUID))
                    .Append('\u001e');
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        }
    }

    /// <summary>
    /// Names of generated RT files: <c>{template}_UID{SeriesInstanceUID}.dcm</c> in the series' directory
    /// (users depend on this naming).
    /// </summary>
    internal static class GeneratedRtNames
    {
        private const string Marker = "_UID";

        /// <summary>File names compare case-insensitively where the file system usually does (Windows, macOS).</summary>
        public static StringComparison FileNameComparison { get; } =
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        public static StringComparer FileNameComparer { get; } =
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        public static string FileName(string templateName, string seriesInstanceUid) => $"{templateName}{Marker}{seriesInstanceUid}.dcm";

        /// <summary>True for a DICOM UID: digits separated by dots, at most 64 characters.</summary>
        public static bool IsValidUid(string uid)
        {
            if (uid.Length == 0 || uid.Length > 64 || uid[0] == '.' || uid[^1] == '.' || uid.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }

            return uid.All(c => c == '.' || (c >= '0' && c <= '9'));
        }

        /// <summary>True when <paramref name="fileName"/> is exactly the name of an RT generated by <paramref name="templateName"/>.</summary>
        public static bool IsGeneratedRt(string fileName, string templateName)
        {
            string prefix = templateName + Marker;
            const string extension = ".dcm";
            if (fileName.Length <= prefix.Length + extension.Length
                || !fileName.StartsWith(prefix, FileNameComparison)
                || !fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return IsValidUid(fileName.Substring(prefix.Length, fileName.Length - prefix.Length - extension.Length));
        }

        /// <summary>
        /// A test for files written by any of <paramref name="templateNames"/>: names starting with
        /// <c>{template}_UID</c>, which includes the temporary files RTs are written through.
        /// </summary>
        public static Func<string, bool> ProducedByAny(IEnumerable<string> templateNames)
        {
            var names = new HashSet<string>(templateNames, FileNameComparer);
            return fileName =>
            {
                int index = fileName.IndexOf(Marker, FileNameComparison);
                while (index > 0)
                {
                    if (names.Contains(fileName.Substring(0, index)))
                    {
                        return true;
                    }

                    index = fileName.IndexOf(Marker, index + 1, FileNameComparison);
                }

                return false;
            };
        }
    }
}
