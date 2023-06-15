using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using FellowOakDicom;
using System.ComponentModel;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.Services
{
    public class TemplateMaker : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }
        private string template_name;
        public string TemplateName
        {
            get { return template_name; }
            set
            {
                template_name = value;
                OnPropertyChanged("TemplateName");
            }
        }
        public string path;
        private string onto_path;
        public string color, interperter;
        public bool is_template;
        public List<ROIClass> ROIs;
        public List<OntologyCodeClass> Ontologies;
        public List<string> Paths;
        public Dictionary<string, List<string>> DicomTags = new Dictionary<string, List<string>>();
        public string output;
        public Dictionary<int, string> color_dict, interp_dict, name_dict, code_meaning_dict, code_value_dict,
            coding_scheme_designator_dict, context_group_version_dict, context_identifier_dict, context_uid_dict, mapping_resource_dict,
            mapping_resource_name_dict, mapping_resourceUID_dict;
        DicomFile RT_file;
        public TemplateMaker()
        {
            ROIs = new List<ROIClass>();
            Ontologies = new List<OntologyCodeClass>();
            Paths = new List<string>();
        }
        public void set_onto_path(string onto_path)
        {
            this.onto_path = onto_path;
        }
        public void write_ontology(OntologyCodeClass onto)
        {
            try
            {
                onto.write_ontology(onto_path);
            }
            catch
            {

            }

        }
        public void interpret_RT(string dicom_file)
        {
            color_dict = new Dictionary<int, string>();
            interp_dict = new Dictionary<int, string>();
            name_dict = new Dictionary<int, string>();
            code_meaning_dict = new Dictionary<int, string>();
            code_value_dict = new Dictionary<int, string>();
            coding_scheme_designator_dict = new Dictionary<int, string>();
            context_group_version_dict = new Dictionary<int, string>();
            context_identifier_dict = new Dictionary<int, string>();
            context_uid_dict = new Dictionary<int, string>();
            mapping_resource_dict = new Dictionary<int, string>();
            mapping_resource_name_dict = new Dictionary<int, string>();
            mapping_resourceUID_dict = new Dictionary<int, string>();
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
                if (rt_roi_identification_sequence != null)
                {
                    foreach (DicomDataset rt_ident in rt_roi_identification_sequence)
                    {
                        code_meaning_dict.Add(ref_number, rt_ident.GetString(DicomTag.CodeMeaning));
                        code_value_dict.Add(ref_number, rt_ident.GetString(DicomTag.CodeValue));
                        coding_scheme_designator_dict.Add(ref_number, rt_ident.GetString(DicomTag.CodingSchemeDesignator));
                        context_group_version_dict.Add(ref_number, rt_ident.GetString(DicomTag.ContextGroupVersion));
                        context_identifier_dict.Add(ref_number, rt_ident.GetString(DicomTag.ContextIdentifier));
                        context_uid_dict.Add(ref_number, rt_ident.GetString(DicomTag.ContextUID));
                        mapping_resource_dict.Add(ref_number, rt_ident.GetString(DicomTag.MappingResource));
                        mapping_resource_name_dict.Add(ref_number, rt_ident.GetString(DicomTag.MappingResourceName));
                        mapping_resourceUID_dict.Add(ref_number, rt_ident.GetString(DicomTag.MappingResourceUID));
                        break;
                    }
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
                        OntologyCodeClass code_class = new OntologyCodeClass();
                        if (code_meaning_dict.ContainsKey(key))
                        {
                            code_class = new OntologyCodeClass(code_meaning_dict[key], code_value_dict[key], coding_scheme_designator_dict[key], context_group_version_dict[key], mapping_resource_dict[key],
                                context_identifier_dict[key], mapping_resource_name_dict[key], mapping_resourceUID_dict[key], context_uid_dict[key]);
                        }
                        ROIClass new_roi;
                        foreach (OntologyCodeClass o in Ontologies)
                        {
                            if (o.CodeValue == code_class.CodeValue)
                            {
                                code_class = o;
                                break;
                            }
                        }
                        new_roi = new ROIClass(byte.Parse(colors[0]), byte.Parse(colors[1]), byte.Parse(colors[2]), name_dict[key], interp_dict[key], code_class);
                        foreach (ROIClass r in ROIs)
                        {
                            if (r.ROIName == name_dict[key])
                            {
                                new_roi = r;
                                break;
                            }
                        }
                        if (!ROIs.Any(p => p.ROIName == new_roi.ROIName))
                        {
                            ROIs.Add(new_roi);
                        }
                        if (!Ontologies.Any(p => p.CodeMeaning == code_class.CodeMeaning) & !Ontologies.Any(p => p.CodeValue == code_class.CodeValue))
                        {
                            Ontologies.Add(code_class);
                            Ontologies.Sort((p, q) => p.CodeMeaning.CompareTo(q.CodeMeaning));
                            write_ontology(code_class);
                            new_roi = new ROIClass(byte.Parse(colors[0]), byte.Parse(colors[1]), byte.Parse(colors[2]), name_dict[key], interp_dict[key], code_class);
                            if (!ROIs.Any(p => p.ROIName == new_roi.ROIName))
                            {
                                ROIs.Add(new_roi);
                            }
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
        public void write_ontologies()
        {
            foreach (OntologyCodeClass onto in Ontologies)
            {
                write_ontology(onto);
            }
        }
        public void make_template()
        {
            if (!Directory.Exists(output))
            {
                Directory.CreateDirectory(output);
            }
            if (!Directory.Exists(Path.Combine(output, "ROIs")))
            {
                Directory.CreateDirectory(Path.Combine(output, "ROIs"));
            }
            foreach (ROIClass roi in ROIs)
            {
                try
                {
                    roi.write_roi(Path.Combine(output, "ROIs"));
                }
                catch
                {

                }
            }
            File.WriteAllLines(Path.Combine(output, "Paths.txt"), Paths.ToArray());
            using (StreamWriter file = new StreamWriter(Path.Combine(output, "DicomTags.txt")))
            {
                foreach (string key in DicomTags.Keys)
                {
                    string start = "";
                    start += $"{key}";
                    foreach (string line in DicomTags[key])
                    {
                        start += $"\\{line}";
                    }
                    file.WriteLine(start);
                }
            }
        }
        public void define_path(string path)
        {
            this.path = path;
        }
        public void categorize_folder()
        {
            ROIs = new List<ROIClass>();
            Paths = new List<string>();
            OntologyCodeClass code_class;
            is_template = false;
            if (File.Exists(Path.Combine(path, "Paths.txt")))
            {
                string[] file_paths = File.ReadAllLines(Path.Combine(path, "Paths.txt"));
                foreach (string file_path in file_paths)
                {
                    Paths.Add(file_path);
                }
            }
            if (File.Exists(Path.Combine(path, "DicomTags.txt")))
            {
                string[] file_paths = File.ReadAllLines(Path.Combine(path, "DicomTags.txt"));
                foreach (string file_path in file_paths)
                {

                    string[] key_values = file_path.Split('\\');
                    string key = key_values[0];
                    List<string> values = key_values.Skip(1).ToList();
                    DicomTags.Add(key, values);
                }
            }
            if (Directory.Exists(Path.Combine(path, "ROIs")))
            {
                is_template = true;
                TemplateName = Path.GetFileName(path);
                string[] roi_files = Directory.GetFiles(Path.Combine(path, "ROIs"), "*.txt");
                foreach (string roi_file in roi_files)
                {
                    ROIClass roi = new ROIClass(roi_file);
                    ROIs.Add(roi);
                    code_class = roi.Ontology_Class;
                    if (!File.Exists(Path.Combine(onto_path, $"{code_class.CodeMeaning}.txt")))
                    {
                        write_ontology(code_class);
                    }
                    bool contains_code_class = false;
                    foreach (OntologyCodeClass o in Ontologies)
                    {
                        if (o.CodeMeaning == code_class.CodeMeaning)
                        {
                            if (o.CodeValue == code_class.CodeValue)
                            {
                                contains_code_class = true;
                                roi.Ontology_Class = o;
                                break;
                            }
                        }
                    }
                    if (!contains_code_class)
                    {
                        Ontologies.Add(code_class);
                        //write_ontology(code_class);
                    }
                }
            }
        }
    }
}
