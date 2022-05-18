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
        public string template_name;
        string rois_present;
        public string path;
        public string color, interperter;
        public bool is_template;
        public List<ROIClass> ROIs;
        string output;
        public Dictionary<int, string> color_dict, interp_dict, name_dict;
        DicomFile RT_file;
        public TemplateMaker(string output)
        {
            this.output = output;
            ROIs = new List<ROIClass>();
            color_dict = new Dictionary<int, string>();
            interp_dict = new Dictionary<int, string>();
            name_dict = new Dictionary<int, string>();
            if (!Directory.Exists(Path.Combine(output, "ROIs")))
            {
                Directory.CreateDirectory(Path.Combine(output, "ROIs"));
            }
        }
        public void interpret_RT(string dicom_file)
        {
            RT_file = DicomFile.Open(dicom_file, FileReadOption.ReadAll);
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
                        ROIs.Add(new ROIClass(color_dict[key], name_dict[key], interp_dict[key]));
                    }
                }
            }
        }
        public void make_template()
        {
            foreach (ROIClass roi in ROIs)
            {
                File.WriteAllText(Path.Combine(output, "ROIs", $"{roi.name}.txt"), $"{roi.color}\n{roi.roi_interpreted_type}");
            }
        }
    }
}
