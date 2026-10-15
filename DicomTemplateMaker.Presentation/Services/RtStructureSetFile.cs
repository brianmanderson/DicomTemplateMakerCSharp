using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FellowOakDicom;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>
    /// Guards "load a template from an RT file": checks that a file is a readable RT Structure Set with ROIs before
    /// <see cref="TemplateMaker.interpret_RT"/> reads it, so a wrong or damaged file gives a readable message instead
    /// of an exception.
    /// </summary>
    public static class RtStructureSetFile
    {
        public const string NotAnRtStructureSet = "This file is not an RT Structure Set.";

        /// <summary>RT Structure Set Storage.</summary>
        public const string RtStructureSetStorageUid = "1.2.840.10008.5.1.4.1.1.481.3";

        /// <summary>Null when <paramref name="path"/> is an RT Structure Set with ROIs; otherwise a message for the user.</summary>
        public static string? Check(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            DicomDataset dataset;
            try
            {
                dataset = DicomFile.Open(path, FileReadOption.SkipLargeTags).Dataset;
            }
            catch (Exception ex) when (ex is DicomException || ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                return NotAnRtStructureSet + Environment.NewLine + Environment.NewLine + $"{path} could not be read as a DICOM file: {ex.Message}";
            }

            dataset.TryGetString(DicomTag.SOPClassUID, out string? sopClass);
            string modality = dataset.GetSingleValueOrDefault(DicomTag.Modality, string.Empty);
            if (sopClass?.Trim() != RtStructureSetStorageUid && !string.Equals(modality.Trim(), "RTSTRUCT", StringComparison.OrdinalIgnoreCase))
            {
                string kind = string.IsNullOrWhiteSpace(modality) ? "a DICOM file without a modality" : $"a {modality.Trim()} file";
                return NotAnRtStructureSet + Environment.NewLine + Environment.NewLine + $"{path} is {kind}. Choose an RT Structure Set (modality RTSTRUCT).";
            }

            if (!dataset.Contains(DicomTag.StructureSetROISequence) || !dataset.Contains(DicomTag.ROIContourSequence) || !dataset.Contains(DicomTag.RTROIObservationsSequence))
            {
                return $"The RT Structure Set {path} has no ROIs to read (its ROI sequences are missing).";
            }

            return null;
        }

        /// <summary>
        /// Adds the ROIs of the RT Structure Set <paramref name="path"/> to <paramref name="maker"/> with
        /// <see cref="TemplateMaker.interpret_RT"/>. When the file is not a readable RT Structure Set, returns false with
        /// a message for the user and leaves the maker's ROI and ontology lists as they were. ROIs that could not be read
        /// are not reported by this overload; see the overload with skippedRois.
        /// </summary>
        public static bool TryInterpret(TemplateMaker maker, string path, out string? error)
        {
            return TryInterpret(maker, path, out error, out _);
        }

        /// <summary>
        /// <see cref="TryInterpret(TemplateMaker, string, out string?)"/>, and lists in <paramref name="skippedRois"/>
        /// the ROIs that could not be read, with the reason (for example an ROI without a code). When no ROI of the file
        /// could be read, returns false with the reasons in <paramref name="error"/> and leaves the maker as it was.
        /// </summary>
        public static bool TryInterpret(TemplateMaker maker, string path, out string? error, out IReadOnlyList<string> skippedRois)
        {
            ArgumentNullException.ThrowIfNull(maker);
            skippedRois = Array.Empty<string>();
            error = Check(path);
            if (error != null)
            {
                return false;
            }

            List<ROIClass> rois = maker.ROIs.ToList();
            List<OntologyCodeClass> ontologies = maker.Ontologies.ToList();
            try
            {
                int read = maker.interpret_RT(path);
                skippedRois = maker.SkippedRois.ToList();
                if (read == 0)
                {
                    // Nothing was read, so nothing was added either; restored all the same, so the lists stay exactly as they were.
                    Restore(maker.ROIs, rois);
                    Restore(maker.Ontologies, ontologies);
                    error = $"No ROI of the RT Structure Set {path} could be read:" + Environment.NewLine + string.Join(Environment.NewLine, skippedRois)
                        + Environment.NewLine + Environment.NewLine + "Nothing was added to the template.";
                    return false;
                }
                return true;
            }
            catch (Exception ex) when (ex is DicomException || ex is FormatException || ex is OverflowException || ex is ArgumentException
                || ex is InvalidOperationException || ex is IndexOutOfRangeException || ex is IOException || ex is UnauthorizedAccessException
                || ex is NullReferenceException || ex is KeyNotFoundException || ex is InvalidCastException)
            {
                // Restored in place: windows bind to these list instances.
                Restore(maker.ROIs, rois);
                Restore(maker.Ontologies, ontologies);
                error = $"The RT Structure Set {path} could not be read: {Describe(ex)}" + Environment.NewLine + Environment.NewLine + "Nothing was added to the template.";
                return false;
            }
        }

        /// <summary>
        /// A paragraph that lists <paramref name="skippedRois"/> for a message to the user, starting with a blank line;
        /// empty when there are none.
        /// </summary>
        public static string DescribeSkipped(IReadOnlyList<string> skippedRois)
        {
            ArgumentNullException.ThrowIfNull(skippedRois);
            if (skippedRois.Count == 0)
            {
                return string.Empty;
            }
            string heading = skippedRois.Count == 1 ? "1 ROI of the file was not read:" : skippedRois.Count + " ROIs of the file were not read:";
            return Environment.NewLine + Environment.NewLine + heading + Environment.NewLine + string.Join(Environment.NewLine, skippedRois);
        }

        /// <summary>
        /// The reason for a failed read. A null reference, missing key or wrong cast means that an attribute the reader
        /// needs is missing or has an unexpected form; their own messages do not say so.
        /// </summary>
        private static string Describe(Exception ex)
        {
            if (ex is NullReferenceException || ex is KeyNotFoundException || ex is InvalidCastException)
            {
                return $"an attribute that is needed is missing or has an unexpected form ({ex.GetType().Name}: {ex.Message})";
            }
            return ex.Message;
        }

        private static void Restore<T>(List<T> list, List<T> saved)
        {
            list.Clear();
            list.AddRange(saved);
        }
    }
}
