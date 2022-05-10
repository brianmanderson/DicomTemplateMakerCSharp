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
        public DicomSeriesReader()
        {
            dicomParser = new DicomParser();
            string dicom_file = @"C:\Users\b5anderson\Modular_Projects\Dicom_RT_and_Images_to_Mask\src\DicomRTTool\template_RS.dcm";
            RT_file = DicomFile.Open(dicom_file, FileReadOption.ReadAll);
            series_reader = new ImageSeriesReader();
            series_reader.LoadPrivateTagsOn();
            loaded_series_instace_uid = "";
        }
        public void parse_folder(string directory)
        {
            dicomParser.ParseDirectory(directory);
        }
        public void load_DICOM(string series_instance_uid)
        {
            string image_uid;
            VectorString dicom_filenames = dicomParser.series_instance_uids_dict[series_instance_uid];
            series_reader.SetFileNames(dicom_filenames);
            dicomImage = series_reader.Execute();
            image_uid = series_reader.GetMetaData(0, "0020|000e");
        }
        public void update_tags()
        {

        }
    }
}
