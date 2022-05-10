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
        Dictionary<string, DicomTag> dicom_tags_dict = new Dictionary<string, DicomTag>() { { "0008|0020", DicomTag.StudyDate } ,
            { "0008|0030", DicomTag.StudyTime } , { "0008|0050", DicomTag.AccessionNumber },
            { "0008|0090", DicomTag.ReferringPhysicianName}, { "0008|1030", DicomTag.StudyDescription} , { "0010|0010", DicomTag.PatientName },
            { "0010|0020", DicomTag.PatientID}, { "0010|0030", DicomTag.PatientBirthDate}, { "0010|0040", DicomTag.PatientSex} ,
            { "0020|000d", DicomTag.StudyInstanceUID}, { "0020|0010", DicomTag.StudyID}, { "0020|0052", DicomTag.FrameOfReferenceUID} };
        public DicomSeriesReader()
        {
            dicomParser = new DicomParser();
            string dicom_file = @"C:\Users\b5anderson\Modular_Projects\Dicom_RT_and_Images_to_Mask\src\DicomRTTool\template_RS.dcm";
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
            update_template();
        }
        public void update_template()
        {
            foreach (string key in dicom_tags_dict.Keys)
            {
                if (series_reader.HasMetaDataKey(0, key))
                {
                    RT_file.Dataset.AddOrUpdate(dicom_tags_dict[key], series_reader.GetMetaData(0, key));
                }
            }
        }
    }
}
