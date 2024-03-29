﻿using System;
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
        private DicomDataset rt_contour, rt_observation, rt_structure_set;
        string loaded_series_instance_uid;
        List<int> referenced_roi_number_list, observation_number_list;
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
            RT_file = DicomFile.Open(dicom_file, FileReadOption.ReadAll);
            series_reader = new ImageSeriesReader();
            series_reader.LoadPrivateTagsOn();
            series_reader.MetaDataDictionaryArrayUpdateOn();
            series_reader.SetOutputPixelType(PixelIDValueEnum.sitkFloat32);
            loaded_series_instance_uid = "";
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
        public void delete_all_contours()
        {
            /// Delete the previous ContourSequence
            DicomSequence roiContourSequence = RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.ROIContourSequence);
            foreach (DicomDataset roiContour in roiContourSequence.Items)
            {
                roiContour.Remove(DicomTag.ContourSequence);
            }
            RT_file.Dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            RT_file.Dataset.AddOrUpdate(DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
        }
        public void build_reference_numbers()
        {
            referenced_roi_number_list = new List<int>();
            observation_number_list = new List<int>();
            foreach (DicomDataset rt_structure_set in RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.StructureSetROISequence))
            {
                int roi_number = rt_structure_set.GetSingleValue<int>(DicomTag.ROINumber);
                if (!referenced_roi_number_list.Contains(roi_number))
                {
                    referenced_roi_number_list.Add(roi_number);
                }
            }
            foreach (DicomDataset roi_contour_set in RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.ROIContourSequence))
            {
                int roi_number = roi_contour_set.GetSingleValue<int>(DicomTag.ReferencedROINumber);
                if (!referenced_roi_number_list.Contains(roi_number))
                {
                    referenced_roi_number_list.Add(roi_number);
                }
            }
            foreach (DicomDataset roi_observation_set in RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.RTROIObservationsSequence))
            {
                int roi_number = roi_observation_set.GetSingleValue<int>(DicomTag.ReferencedROINumber);
                if (!referenced_roi_number_list.Contains(roi_number))
                {
                    referenced_roi_number_list.Add(roi_number);
                }
                int observation_label = roi_observation_set.GetSingleValue<int>(DicomTag.ObservationNumber);
                if (!observation_number_list.Contains(observation_label))
                {
                    observation_number_list.Add(observation_label);
                }
            }
        }
        public void add_roi(ROIClass roi_class)
        {
            DicomSequence rt_structure_set_sequence = RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.StructureSetROISequence);
            DicomDataset rt_structure_set = new DicomDataset(rt_structure_set_sequence.Items[0]);
            DicomSequence roi_contour_sequence = RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.ROIContourSequence);
            DicomDataset roi_contour_set = new DicomDataset(roi_contour_sequence.Items[0]);
            DicomSequence roi_observation_sequence = RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.RTROIObservationsSequence);
            DicomDataset roi_observation_set = new DicomDataset(roi_observation_sequence.Items[0]);
            int roi_number = 1;
            while (referenced_roi_number_list.Contains(roi_number))
            {
                roi_number++;
            }
            referenced_roi_number_list.Add(roi_number);
            int roi_observation_number = 1;
            while (observation_number_list.Contains(roi_observation_number))
            {
                roi_observation_number++;
            }
            observation_number_list.Add(roi_observation_number);
            rt_structure_set.AddOrUpdate(DicomTag.ROINumber, roi_number);
            rt_structure_set.AddOrUpdate(DicomTag.ROIName, roi_class.name);
            rt_structure_set_sequence.Items.Add(rt_structure_set);

            roi_contour_set.AddOrUpdate(DicomTag.ReferencedROINumber, roi_number);
            roi_contour_set.AddOrUpdate(DicomTag.ROIDisplayColor, roi_class.color);
            roi_contour_sequence.Items.Add(roi_contour_set);

            roi_observation_set.AddOrUpdate(DicomTag.ObservationNumber, roi_observation_number);
            roi_observation_set.AddOrUpdate(DicomTag.ReferencedROINumber, roi_number);
            roi_observation_set.AddOrUpdate(DicomTag.RTROIInterpretedType, roi_class.roi_interpreted_type);
            roi_observation_sequence.Items.Add(roi_observation_set);
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
                delete_all_contours();
            }
            build_reference_numbers();
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
        }
        public void save_RT(string file_name)
        {
            RT_file.Save(file_name);
        }
        public void add_RTs()
        {
            Dictionary<string, string> name_color_dict = new Dictionary<string, string>() { { "Parotid_R", "Blue" } };

        }
    }
}
