using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DicomFolderParser;
using itk.simple;
using FellowOakDicom;

namespace DicomTemplateMakerCSharp.Services
{
    class DicomSeriesReader
    {
        public DicomParser dicomParser;
        DicomFile RT_file;
        ImageSeriesReader series_reader;
        Image dicomImage;
        string loaded_series_instace_uid;
        Dictionary<DicomTag, string> dicom_tags_dict = new Dictionary<DicomTag, string>() { {DicomTag.StudyDate, "0008|0020"} ,
            { DicomTag.StudyTime, "0008|0030"} , { DicomTag.AccessionNumber, "0008|0050" }, { DicomTag.SeriesInstanceUID, "0020|000e"},
            { DicomTag.ReferringPhysicianName, "0008|0090"}, { DicomTag.StudyDescription, "0008|1030" } , {DicomTag.PatientName, "0010|0010" },
            { DicomTag.PatientID, "0010|0020" }, { DicomTag.PatientBirthDate, "0010|0030" }, { DicomTag.PatientSex, "0010|0040" } ,
            { DicomTag.StudyInstanceUID, "0020|000d" }, { DicomTag.StudyID, "0020|0010" }, { DicomTag.FrameOfReferenceUID, "0020|0052" }, { DicomTag.SOPInstanceUID, "0008|0018"} };
        List<DicomTag> change_tags = new List<DicomTag> { DicomTag.StudyDate, DicomTag.StudyTime, DicomTag.AccessionNumber, DicomTag.ReferringPhysicianName,
            DicomTag.StudyDescription, DicomTag.PatientName, DicomTag.PatientID, DicomTag.PatientBirthDate, DicomTag.PatientSex, DicomTag.StudyInstanceUID,
            DicomTag.StudyID, DicomTag.FrameOfReferenceUID };
        public DicomSeriesReader()
        {
            dicomParser = new DicomParser();
            string dicom_file = @"C:\Users\b5anderson\Modular_Projects\Dicom_RT_and_Images_to_Mask\src\DicomRTTool\template_RS.dcm";
            dicom_file = @"\\ucsdhc-varis2\radonc$\BMAnderson\BMA_Export\newtest.dcm";
            RT_file = DicomFile.Open(dicom_file, FileReadOption.ReadAll);
            series_reader = new ImageSeriesReader();
            series_reader.LoadPrivateTagsOn();
            series_reader.MetaDataDictionaryArrayUpdateOn();
            series_reader.SetOutputPixelType(PixelIDValueEnum.sitkFloat32);
            loaded_series_instace_uid = "";
        }
        public void parse_folder(string directory)
        {
            dicomParser.ParseDirectory(directory);
        }
        public void load_DICOM(string series_instance_uid)
        {
            string image_uid, value;
            VectorString dicom_filenames = dicomParser.series_instance_uids_dict[series_instance_uid];
            series_reader.SetFileNames(dicom_filenames);
            dicomImage = series_reader.Execute();
        }
        public void update_template(bool delete_contours)
        {
            foreach (DicomTag key in change_tags)
            {
                if (series_reader.HasMetaDataKey(0, dicom_tags_dict[key]))
                {
                    RT_file.Dataset.AddOrUpdate(key, series_reader.GetMetaData(0, dicom_tags_dict[key]));
                }
            }
            if (delete_contours)
            {
                /// Delete the previous ContourSequence
                DicomSequence roiContourSequence = RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.ROIContourSequence);
                foreach (DicomDataset roiContour in roiContourSequence.Items)
                {
                    roiContour.Remove(DicomTag.ContourSequence);
                }
                RT_file.Dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
                RT_file.Dataset.AddOrUpdate(DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
                // Make a new SeriesInstanceUID!!!
            }
            /// Update the SOP Instance UIDS
            DicomSequence refFrameofRefSequence = RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.ReferencedFrameOfReferenceSequence);
            foreach (DicomDataset refFrameofRef in refFrameofRefSequence.Items)
            {
                refFrameofRef.AddOrUpdate(DicomTag.FrameOfReferenceUID, series_reader.GetMetaData(0, dicom_tags_dict[DicomTag.FrameOfReferenceUID]));
                DicomSequence rtRefStudySequence = refFrameofRef.GetDicomItem<DicomSequence>(DicomTag.RTReferencedStudySequence);
                foreach (DicomDataset rtRefStudy in rtRefStudySequence)
                {
                    rtRefStudy.AddOrUpdate(DicomTag.ReferencedSOPInstanceUID, series_reader.GetMetaData(0, dicom_tags_dict[DicomTag.StudyInstanceUID]));
                    DicomSequence rTReferencedSeriesSequence = rtRefStudy.GetDicomItem<DicomSequence>(DicomTag.RTReferencedSeriesSequence);
                    foreach (DicomDataset rTReferencedSeries in rTReferencedSeriesSequence)
                    {
                        rTReferencedSeries.AddOrUpdate(DicomTag.SeriesInstanceUID, series_reader.GetMetaData(0, dicom_tags_dict[DicomTag.SeriesInstanceUID]));
                        DicomSequence contourImageSequence = rTReferencedSeries.GetDicomItem<DicomSequence>(DicomTag.ContourImageSequence);
                        DicomDataset fill_segment_base = new DicomDataset(contourImageSequence.Items[0]);
                        int total = contourImageSequence.Items.Count;
                        for (int i = 0; i < total; i++)
                        {
                            contourImageSequence.Items.RemoveAt(0);
                        }
                        for (uint i = 0; i < dicomImage.GetSize()[2]; i++)
                        {
                            DicomDataset fill_segment = new DicomDataset(fill_segment_base);
                            fill_segment.AddOrUpdate(DicomTag.ReferencedSOPInstanceUID, series_reader.GetMetaData(i, dicom_tags_dict[DicomTag.SOPInstanceUID]));
                            contourImageSequence.Items.Add(fill_segment);
                        }
                    }
                }
            }
            RT_file.Save(@"\\ucsdhc-varis2\radonc$\BMAnderson\TT\test.dcm");
        }
        public void add_RTs()
        {
            Dictionary<string, string> name_color_dict = new Dictionary<string, string>() { { "Parotid_R", "Blue" } };

        }
    }
}
