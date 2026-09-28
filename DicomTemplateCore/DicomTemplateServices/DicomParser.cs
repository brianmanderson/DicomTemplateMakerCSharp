using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FellowOakDicom;

namespace DicomTemplateMakerGUI.DicomTemplateServices
{
    /// <summary>
    /// Finds the image series in one folder (not recursive) and orders each series' files along the
    /// slice direction. This replaces SimpleITK's GetGDCMSeriesIDs / GetGDCMSeriesFileNames with
    /// header-only fo-dicom reads: files are grouped by SeriesInstanceUID, non-image objects such as
    /// RT Structure Sets are ignored, and series are listed in UID order.
    /// </summary>
    public class DicomParser
    {
        public Dictionary<string, List<string>> series_instance_uids_dict = new Dictionary<string, List<string>>();
        public List<string> dicom_series_instance_uids = new List<string>();

        public void __reset__()
        {
            series_instance_uids_dict = new Dictionary<string, List<string>>();
            dicom_series_instance_uids = new List<string>();
        }

        public void ParseDirectory(string directory)
        {
            var bySeries = new Dictionary<string, List<SliceHeader>>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(directory))
            {
                SliceHeader? header = SliceHeader.TryRead(file);
                if (header == null)
                {
                    continue;
                }

                if (!bySeries.TryGetValue(header.SeriesInstanceUid, out List<SliceHeader>? slices))
                {
                    slices = new List<SliceHeader>();
                    bySeries.Add(header.SeriesInstanceUid, slices);
                }

                slices.Add(header);
            }

            foreach (string uid in bySeries.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                dicom_series_instance_uids.Add(uid);
                series_instance_uids_dict[uid] = SliceHeader.Order(bySeries[uid]).Select(s => s.FilePath).ToList();
            }
        }
    }

    /// <summary>The header of one image file, read without pixel data.</summary>
    internal sealed class SliceHeader
    {
        private SliceHeader(string filePath, string seriesInstanceUid, DicomDataset dataset)
        {
            FilePath = filePath;
            SeriesInstanceUid = seriesInstanceUid;
            Dataset = dataset;
        }

        public string FilePath { get; }

        public string SeriesInstanceUid { get; }

        public DicomDataset Dataset { get; }

        /// <summary>Reads the header of an image file. Returns null for non-DICOM files and non-image objects.</summary>
        public static SliceHeader? TryRead(string filePath)
        {
            try
            {
                DicomFile file = DicomFile.Open(filePath, FileReadOption.SkipLargeTags);
                DicomDataset dataset = file.Dataset;
                if (!IsImage(dataset))
                {
                    return null;
                }

                string? uid = dataset.GetSingleValueOrDefault<string?>(DicomTag.SeriesInstanceUID, null);
                return string.IsNullOrWhiteSpace(uid) ? null : new SliceHeader(filePath, uid.Trim(), dataset);
            }
            catch (Exception ex) when (ex is DicomFileException || ex is DicomDataException || ex is IOException || ex is UnauthorizedAccessException || ex is FormatException || ex is InvalidOperationException)
            {
                return null;
            }
        }

        private static bool IsImage(DicomDataset dataset)
        {
            DicomUID? sopClass = dataset.GetSingleValueOrDefault<DicomUID?>(DicomTag.SOPClassUID, null);
            return sopClass != null ? sopClass.IsImageStorage : dataset.Contains(DicomTag.Rows);
        }

        /// <summary>
        /// Orders slices the way GDCM's series helper does: by position along the slice normal when every
        /// file has ImagePositionPatient and ImageOrientationPatient, otherwise by InstanceNumber, and
        /// finally by file name.
        /// </summary>
        public static IEnumerable<SliceHeader> Order(IReadOnlyCollection<SliceHeader> slices)
        {
            double[]? normal = SliceNormal(slices.First().Dataset);
            if (normal != null && slices.All(s => Position(s.Dataset) != null))
            {
                return slices
                    .OrderBy(s => Dot(Position(s.Dataset)!, normal))
                    .ThenBy(s => InstanceNumber(s.Dataset) ?? int.MaxValue)
                    .ThenBy(s => s.FilePath, StringComparer.Ordinal);
            }

            return slices
                .OrderBy(s => InstanceNumber(s.Dataset) ?? int.MaxValue)
                .ThenBy(s => s.FilePath, StringComparer.Ordinal);
        }

        private static double[]? SliceNormal(DicomDataset dataset)
        {
            if (!dataset.TryGetValues(DicomTag.ImageOrientationPatient, out double[]? iop) || iop == null || iop.Length < 6)
            {
                return null;
            }

            return new[]
            {
                (iop[1] * iop[5]) - (iop[2] * iop[4]),
                (iop[2] * iop[3]) - (iop[0] * iop[5]),
                (iop[0] * iop[4]) - (iop[1] * iop[3]),
            };
        }

        private static double[]? Position(DicomDataset dataset)
        {
            return dataset.TryGetValues(DicomTag.ImagePositionPatient, out double[]? ipp) && ipp != null && ipp.Length >= 3 ? ipp : null;
        }

        private static int? InstanceNumber(DicomDataset dataset)
        {
            return dataset.TryGetSingleValue(DicomTag.InstanceNumber, out int number) ? number : (int?)null;
        }

        private static double Dot(double[] a, double[] b) => (a[0] * b[0]) + (a[1] * b[1]) + (a[2] * b[2]);
    }
}
