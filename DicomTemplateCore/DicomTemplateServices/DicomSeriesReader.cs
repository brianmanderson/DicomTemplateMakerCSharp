using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FellowOakDicom;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.DicomTemplateServices
{
    /// <summary>
    /// Fills a template RT Structure Set for one image series. Slice metadata comes from header-only
    /// fo-dicom reads of the series files (formerly a full SimpleITK volume load, used only for tags).
    /// </summary>
    class DicomSeriesReader
    {
        public DicomParser dicomParser;
        DicomFile RT_file;
        List<DicomDataset> slices = new List<DicomDataset>();
        private DicomDataset? rt_structure_set, roi_observation_set, roi_contour_set;
        // Built by build_reference_numbers (called from update_template); null until then.
        List<int>? referenced_roi_number_list, observation_number_list;
        List<DicomTag> change_tags = new List<DicomTag> { DicomTag.StudyDate, DicomTag.StudyTime, DicomTag.AccessionNumber, DicomTag.ReferringPhysicianName,
            DicomTag.StudyDescription, DicomTag.PatientName, DicomTag.PatientID, DicomTag.PatientBirthDate, DicomTag.PatientSex, DicomTag.StudyInstanceUID,
            DicomTag.StudyID, DicomTag.FrameOfReferenceUID };
        public DicomSeriesReader()
            : this(Path.Combine(".", "template_RS.dcm"))
        {
        }
        public DicomSeriesReader(string template_rs_path)
        {
            dicomParser = new DicomParser();
            RT_file = DicomFile.Open(template_rs_path, FileReadOption.ReadAll);
        }
        public void parse_folder(string directory)
        {
            dicomParser.ParseDirectory(directory);
        }
        public void load_DICOM(string series_instance_uid)
        {
            List<string> dicom_filenames = dicomParser.series_instance_uids_dict[series_instance_uid];
            slices = dicom_filenames.Select(file => DicomFile.Open(file, FileReadOption.SkipLargeTags).Dataset).ToList();
        }
        /// <summary>
        /// Value of a tag in slice <paramref name="slice"/> without DICOM padding (trailing space or NUL), as
        /// SimpleITK/GDCM returned it; throws if the tag is absent, as SimpleITK did.
        /// </summary>
        private string GetMetaData(int slice, DicomTag tag)
        {
            DicomDataset dataset = slices[slice];
            if (!dataset.Contains(tag))
            {
                throw new KeyNotFoundException($"Tag {tag} is missing from slice {slice} of the series.");
            }
            return (dataset.GetString(tag) ?? string.Empty).TrimEnd(' ', '\0');
        }
        private bool HasMetaDataKey(int slice, DicomTag tag)
        {
            return slices.Count > slice && slices[slice].Contains(tag);
        }
        public void delete_all_structures()
        {
            DicomSequence rt_structure_set_sequence = RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.StructureSetROISequence);
            DicomSequence roi_contour_sequence = RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.ROIContourSequence);
            foreach (DicomDataset rs_object in roi_contour_sequence.Items)
            {
                roi_contour_set = new DicomDataset(rs_object);
                break;
            }
            DicomSequence roi_observation_sequence = RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.RTROIObservationsSequence);
            foreach (DicomDataset rs_object in roi_observation_sequence.Items)
            {
                roi_observation_set = new DicomDataset(rs_object);
                break;
            }
            rt_structure_set_sequence.Items.Clear();
            roi_contour_sequence.Items.Clear();
            roi_observation_sequence.Items.Clear();
        }
        public void delete_all_contours()
        {
            /// Delete the previous ContourSequence
            DicomSequence roiContourSequence = RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.ROIContourSequence);
            roiContourSequence.Items.Clear();
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
            if (referenced_roi_number_list == null || observation_number_list == null)
            {
                throw new InvalidOperationException("update_template must be called before add_roi.");
            }
            DicomSequence rt_structure_set_sequence = RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.StructureSetROISequence);
            DicomSequence roi_contour_sequence = RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.ROIContourSequence);
            DicomSequence roi_observation_sequence = RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.RTROIObservationsSequence);

            rt_structure_set = new DicomDataset();
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
            rt_structure_set.AddOrUpdate(DicomTag.ROIName, roi_class.ROIName);
            rt_structure_set.AddOrUpdate(DicomTag.ROIGenerationAlgorithm, "SEMIAUTOMATIC");
            rt_structure_set.AddOrUpdate(DicomTag.ReferencedFrameOfReferenceUID, GetMetaData(0, DicomTag.FrameOfReferenceUID));
            rt_structure_set_sequence.Items.Add(rt_structure_set);

            //roi_contour_set = new DicomDataset(roi_contour_set);
            roi_contour_set = new DicomDataset();
            roi_contour_set.AddOrUpdate(DicomTag.ReferencedROINumber, roi_number);
            roi_contour_set.AddOrUpdate(DicomTag.ROIDisplayColor, roi_class.color_string);
            roi_contour_sequence.Items.Add(roi_contour_set);

            roi_observation_set = new DicomDataset();
            roi_observation_set.AddOrUpdate(DicomTag.ObservationNumber, roi_observation_number);
            roi_observation_set.AddOrUpdate(DicomTag.ReferencedROINumber, roi_number);
            // Still fails at this point for an ROI without an interpreted type, after the structure set
            // and contour items above were added, as it always has.
            string interpreted_type = roi_class.ROI_Interpreted_type
                ?? throw new ArgumentException($"ROI '{roi_class.ROIName}' has no interpreted type.", nameof(roi_class));
            roi_observation_set.AddOrUpdate(DicomTag.RTROIInterpretedType, interpreted_type.ToUpper());
            roi_observation_set.AddOrUpdate(DicomTag.ROIInterpreter, "Brian_Mark_Anderson");
            // (3006,0085) ROI Observation Label is retired in the current DICOM standard (hence fo-dicom's
            // name) but is still written, as before, for systems that read it.
            if (roi_class.ROIName.Length > 16)
            {
                roi_observation_set.AddOrUpdate(DicomTag.ROIObservationLabelRETIRED, roi_class.ROIName.Substring(0, 16));
            }
            else
            {
                roi_observation_set.AddOrUpdate(DicomTag.ROIObservationLabelRETIRED, roi_class.ROIName);
            }
            roi_observation_sequence.Items.Add(roi_observation_set);

            DicomDataset code_set;
            code_set = new DicomDataset();
            // Still fails at this point for an ROI without an ontology class (a template file whose Ontology_Class
            // is null or missing), after the items above were added, as it always has.
            if (roi_class.Ontology_Class == null)
            {
                throw new ArgumentException($"ROI '{roi_class.ROIName}' has no ontology class.", nameof(roi_class));
            }
            code_set.AddOrUpdate(DicomTag.CodeMeaning, roi_class.Ontology_Class.CodeMeaning);
            code_set.AddOrUpdate(DicomTag.CodeValue, roi_class.Ontology_Class.CodeValue);
            code_set.AddOrUpdate(DicomTag.ContextGroupVersion, roi_class.Ontology_Class.ContextGroupVersion);
            code_set.AddOrUpdate(DicomTag.ContextIdentifier, roi_class.Ontology_Class.ContextIdentifier);
            code_set.AddOrUpdate(DicomTag.ContextUID, roi_class.Ontology_Class.ContextUID);
            code_set.AddOrUpdate(DicomTag.MappingResource, roi_class.Ontology_Class.MappingResource);
            code_set.AddOrUpdate(DicomTag.MappingResource, roi_class.Ontology_Class.MappingResource);
            code_set.AddOrUpdate(DicomTag.MappingResourceName, roi_class.Ontology_Class.MappingResourceName);
            code_set.AddOrUpdate(DicomTag.MappingResourceUID, roi_class.Ontology_Class.MappingResourceUID);
            code_set.AddOrUpdate(DicomTag.CodingSchemeDesignator, roi_class.Ontology_Class.Scheme);
            roi_observation_set.AddOrUpdate(DicomTag.RTROIIdentificationCodeSequence, code_set);
        }
        public void update_image_sequence()
        {
            DicomSequence refFrameofRefSequence = RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.ReferencedFrameOfReferenceSequence);
            foreach (DicomDataset refFrameofRef in refFrameofRefSequence.Items)
            {
                refFrameofRef.AddOrUpdate(DicomTag.FrameOfReferenceUID, GetMetaData(0, DicomTag.FrameOfReferenceUID));
                DicomSequence rtRefStudySequence = refFrameofRef.GetDicomItem<DicomSequence>(DicomTag.RTReferencedStudySequence);
                foreach (DicomDataset rtRefStudy in rtRefStudySequence)
                {
                    rtRefStudy.AddOrUpdate(DicomTag.ReferencedSOPInstanceUID, GetMetaData(0, DicomTag.SOPInstanceUID));
                    rtRefStudy.AddOrUpdate(DicomTag.ReferencedSOPClassUID, GetMetaData(0, DicomTag.SOPClassUID));
                    DicomSequence rTReferencedSeriesSequence = rtRefStudy.GetDicomItem<DicomSequence>(DicomTag.RTReferencedSeriesSequence);
                    foreach (DicomDataset rTReferencedSeries in rTReferencedSeriesSequence)
                    {
                        rTReferencedSeries.AddOrUpdate(DicomTag.SeriesInstanceUID, GetMetaData(0, DicomTag.SeriesInstanceUID));
                        DicomSequence contourImageSequence = rTReferencedSeries.GetDicomItem<DicomSequence>(DicomTag.ContourImageSequence);
                        DicomDataset fill_segment_base = new DicomDataset();
                        fill_segment_base.AddOrUpdate(DicomTag.ReferencedSOPClassUID, GetMetaData(0, DicomTag.SOPClassUID));
                        contourImageSequence.Items.Clear();
                        for (int i = 0; i < slices.Count; i++)
                        {
                            DicomDataset fill_segment = new DicomDataset(fill_segment_base);
                            fill_segment.AddOrUpdate(DicomTag.ReferencedSOPInstanceUID, GetMetaData(i, DicomTag.SOPInstanceUID));
                            contourImageSequence.Items.Add(fill_segment);
                        }
                    }
                }
            }
        }
        public string return_dicom_tag(DicomTag dicom_tag)
        {
            if (HasMetaDataKey(0, dicom_tag))
            {
                return GetMetaData(0, dicom_tag);
            }
            else
            {
                return "NA";
            }
        }
        public void update_dicom_tag(DicomTag dicom_tag, string new_value)
        {
            RT_file.Dataset.AddOrUpdate(dicom_tag, new_value);
        }
        public void update_template(bool delete_contours, bool delete_everything)
        {
            foreach (DicomTag key in change_tags)
            {
                if (HasMetaDataKey(0, key))
                {
                    RT_file.Dataset.AddOrUpdate(key, GetMetaData(0, key));
                }
            }
            foreach (DicomDataset rs_object in RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.StructureSetROISequence).Items)
            {
                rt_structure_set = new DicomDataset(rs_object);
                break;
            }
            foreach (DicomDataset rs_object in RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.ROIContourSequence).Items)
            {
                roi_contour_set = new DicomDataset(rs_object);
                break;
            }
            foreach (DicomDataset rs_object in RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.RTROIObservationsSequence).Items)
            {
                roi_observation_set = new DicomDataset(rs_object);
                break;
            }
            if (delete_contours)
            {
                delete_all_contours();
            }
            if (delete_everything)
            {
                delete_all_structures();
            }
            build_reference_numbers();
            /// Update the SOP Instance UIDS
            update_image_sequence();
        }
        public void save_RT(string file_name)
        {
            RT_file.Save(file_name);
        }
    }
}
