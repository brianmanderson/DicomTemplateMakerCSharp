using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ROIOntologyClass;


namespace DicomTemplateMakerGUI.Services
{
    class TemplateEvaluator
    {
        public string template_name;
        public string path;
        public string color, interperter;
        public bool is_template;
        public List<ROIClass> ROIs;
        public TemplateEvaluator()
        {
            ROIs = new List<ROIClass>();
        }
        public void define_path(string path)
        {
            this.path = path;
        }
        public void categorize_folder()
        {
            OntologyCodeClass code_class;
            is_template = false;
            if (Directory.Exists(Path.Combine(path, "ROIs")))
            {
                is_template = true;
                template_name = Path.GetFileName(path);
                string[] roi_files = Directory.GetFiles(Path.Combine(path, "ROIs"), "*.txt");
                foreach (string roi_file in roi_files)
                {
                    ROIs.Add(new ROIClass(roi_file));
                }
            }
        }
    }
}
