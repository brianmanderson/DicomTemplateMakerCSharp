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
        /// a message for the user and leaves the maker's ROI and ontology lists as they were.
        /// </summary>
        public static bool TryInterpret(TemplateMaker maker, string path, out string? error)
        {
            ArgumentNullException.ThrowIfNull(maker);
            error = Check(path);
            if (error != null)
            {
                return false;
            }

            List<ROIClass> rois = maker.ROIs.ToList();
            List<OntologyCodeClass> ontologies = maker.Ontologies.ToList();
            try
            {
                maker.interpret_RT(path);
                return true;
            }
            catch (Exception ex) when (ex is DicomException || ex is FormatException || ex is OverflowException || ex is ArgumentException
                || ex is InvalidOperationException || ex is IndexOutOfRangeException || ex is IOException || ex is UnauthorizedAccessException)
            {
                // Restored in place: windows bind to these list instances.
                Restore(maker.ROIs, rois);
                Restore(maker.Ontologies, ontologies);
                error = $"The RT Structure Set {path} could not be read: {ex.Message}" + Environment.NewLine + Environment.NewLine + "Nothing was added to the template.";
                return false;
            }
        }

        private static void Restore<T>(List<T> list, List<T> saved)
        {
            list.Clear();
            list.AddRange(saved);
        }
    }
}
