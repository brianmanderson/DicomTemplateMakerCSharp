using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using FellowOakDicom;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;

namespace DicomTemplateMakerGUI.Services
{
    public class TemplateMaker
    {
        public string template_name;
        string rois_present;
        public string path;
        public string color, interperter;
        public bool is_template;
        public List<ROIClass> ROIs;
        public List<OntologyCodeClass> Ontologies;
        public List<string> Paths;
        string output;
        public Dictionary<int, string> color_dict, interp_dict, name_dict, code_meaning_dict, code_value_dict,
            coding_scheme_designator_dict;
        DicomFile RT_file;
        public TemplateMaker()
        {
            ROIs = new List<ROIClass>();
            Ontologies = new List<OntologyCodeClass>();
            Paths = new List<string>();
        }
        public void interpret_RT(string dicom_file)
        {
            color_dict = new Dictionary<int, string>();
            interp_dict = new Dictionary<int, string>();
            name_dict = new Dictionary<int, string>();
            code_meaning_dict = new Dictionary<int, string>();
            code_value_dict = new Dictionary<int, string>();
            coding_scheme_designator_dict = new Dictionary<int, string>();
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
                DicomSequence rt_roi_identification_sequence = rt_observation.GetDicomItem<DicomSequence>(DicomTag.RTROIIdentificationCodeSequence);
                foreach (DicomDataset rt_ident in rt_roi_identification_sequence)
                {
                    code_meaning_dict.Add(ref_number, rt_ident.GetString(DicomTag.CodeMeaning));
                    code_value_dict.Add(ref_number, rt_ident.GetString(DicomTag.CodeValue));
                    coding_scheme_designator_dict.Add(ref_number, rt_ident.GetString(DicomTag.CodingSchemeDesignator));
                    break;
                }
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
                        string[] colors = color_dict[key].Split('\\');
                        OntologyCodeClass code_class = new OntologyCodeClass(code_meaning_dict[key], code_value_dict[key], coding_scheme_designator_dict[key]);
                        if (!Ontologies.Contains(code_class))
                        {
                            Ontologies.Add(code_class);
                        }
                        ROIClass new_roi = new ROIClass(byte.Parse(colors[0]), byte.Parse(colors[1]), byte.Parse(colors[2]), name_dict[key], interp_dict[key], code_class);
                        if (!ROIs.Contains(new_roi))
                        {
                            ROIs.Add(new_roi);
                        }
                        
                    }
                }
            }
        }
        public void clear_folder()
        {
            foreach (string file in Directory.GetFiles(Path.Combine(output, "ROIs")))
            {
                File.Delete(file);
            }
        }
        public void define_output(string output)
        {
            this.output = output;
        }
        public void make_template()
        {
            if (!Directory.Exists(Path.Combine(output, "ROIs")))
            {
                Directory.CreateDirectory(Path.Combine(output, "ROIs"));
            }
            foreach (ROIClass roi in ROIs)
            {
                OntologyCodeClass i = roi.Ontology_Class;
                File.WriteAllText(Path.Combine(output, "ROIs", $"{roi.ROIName}.txt"),
                    $"{roi.R}\\{roi.G}\\{roi.B}\n" +
                    $"{i.CodeMeaning}\\{i.CodeValue}\\{i.Scheme}\n" +
                    $"{roi.ROI_Interpreted_type}");
            }
            File.WriteAllLines(Path.Combine(output, "Paths.txt"), Paths.ToArray());
        }
        public void categorize_folder(string path)
        {
            ROIs = new List<ROIClass>();
            Paths = new List<string>();
            this.path = path;
            is_template = false;
            if (File.Exists(Path.Combine(path, "Paths.txt")))
            {
                string[] file_paths = File.ReadAllLines(Path.Combine(path, "Paths.txt"));
                foreach (string file_path in file_paths)
                {
                    Paths.Add(file_path);
                }
            }
            if (Directory.Exists(Path.Combine(path, "ROIs")))
            {
                is_template = true;
                template_name = Path.GetFileName(path);
                string[] roi_files = Directory.GetFiles(Path.Combine(path, "ROIs"), "*.txt");
                foreach (string roi_file in roi_files)
                {
                    string roiname = Path.GetFileName(roi_file).Replace(".txt", "");
                    string[] instructions = File.ReadAllLines(roi_file);
                    color = instructions[0];
                    string[] color_values = color.Split('\\');
                    string[] code_values = instructions[1].Split('\\');
                    OntologyCodeClass code_class = new OntologyCodeClass(code_values[0], code_values[1], code_values[2]);
                    interperter = "";
                    if (instructions.Length == 3)
                    {
                        interperter = instructions[2];
                    }
                    ROIs.Add(new ROIClass(byte.Parse(color_values[0]), byte.Parse(color_values[1]), byte.Parse(color_values[2]), roiname, interperter, code_class));
                }
            }
        }
    }
}
