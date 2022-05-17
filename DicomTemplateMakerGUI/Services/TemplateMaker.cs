using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FellowOakDicom;

namespace DicomTemplateMakerGUI.Services
{
    class TemplateMaker
    {
        string output;
        public Dictionary<int, string> color_dict, interp_dict, name_dict;
        DicomFile RT_file;
        public TemplateMaker(string dicom_file, string output)
        {
            this.output = output;
            if (!Directory.Exists(Path.Combine(output, "ROIs")))
            {
                Directory.CreateDirectory(Path.Combine(output, "ROIs"));
            }
            RT_file = DicomFile.Open(dicom_file, FileReadOption.ReadAll);
        }
        public void make_template()
        {
            color_dict = new Dictionary<int, string>();
            interp_dict = new Dictionary<int, string>();
            name_dict = new Dictionary<int, string>();
            foreach (DicomDataset rt_contour in RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.ROIContourSequence))
            {
                int roi_number = rt_contour.GetSingleValue<int>(DicomTag.ReferencedROINumber);
                string color = rt_contour.GetString(DicomTag.ROIDisplayColor);
                color_dict.Add(roi_number, color);
            }
            foreach (DicomDataset rt_observation in RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.RTROIObservationsSequence))
            {
                int ref_number = rt_observation.GetSingleValue<int>(DicomTag.ReferencedROINumber);
                string interp = rt_observation.GetString(DicomTag.RTROIInterpretedType);
                interp_dict.Add(ref_number, interp);
            }
            foreach (DicomDataset rt_struct in RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.StructureSetROISequence))
            {
                int ref_number = rt_struct.GetSingleValue<int>(DicomTag.ROINumber);
                string name = rt_struct.GetString(DicomTag.ROIName);
                name_dict.Add(ref_number, name);
            }
            foreach (int key in color_dict.Keys)
            {
                if (interp_dict.ContainsKey(key))
                {
                    if (name_dict.ContainsKey(key))
                    {
                        File.WriteAllText(Path.Combine(output, "ROIs", $"{name_dict[key]}.txt"), $"{color_dict[key]}\n{interp_dict[key]}");
                    }
                }
            }
        }
    }
}
