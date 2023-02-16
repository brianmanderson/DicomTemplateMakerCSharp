using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.ComponentModel;

namespace XamlMakerCsharp
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
        public TemplateMaker()
        {
            ROIs = new List<ROIClass>();
            Ontologies = new List<OntologyCodeClass>();
            Paths = new List<string>();
        }
        public void set_onto_path(string onto_path)
        {
            this.onto_path = onto_path;
            if (!Directory.Exists(onto_path))
            {
                Directory.CreateDirectory(onto_path);
            }
        }
        public void write_ontologies()
        {
            foreach(OntologyCodeClass onto in Ontologies)
            {
                write_ontology(onto);
            }
        }
        public void write_ontology(OntologyCodeClass onto)
        {
            try
            {
                File.WriteAllText(Path.Combine(onto_path, $"{onto.CodeMeaning}.txt"),
                    $"{onto.CodeValue}\n{onto.Scheme}\n{onto.ContextGroupVersion}\n" +
                    $"{onto.MappingResource}\n{onto.ContextIdentifier}\n" +
                    $"{onto.MappingResourceName}\n{onto.MappingResourceUID}\n" +
                    $"{onto.ContextUID}");
            }
            catch
            {

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
                    OntologyCodeClass i = roi.Ontology_Class;
                    //write_ontology(i);
                    File.WriteAllText(Path.Combine(output, "ROIs", $"{roi.ROIName}.txt"),
                    $"{roi.R}\\{roi.G}\\{roi.B}\n" +
                    $"{i.CodeMeaning}\\{i.CodeValue}\\{i.Scheme}\\{i.ContextGroupVersion}\\" +
                    $"{i.MappingResource}\\{i.ContextIdentifier}\\{i.MappingResourceName}\\" +
                    $"{i.MappingResourceUID}\\{i.ContextUID}\n" +
                    $"{roi.ROI_Interpreted_type}\n" +
                    $"{roi.Include}");
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
    }
}
