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
        public void categorize_folder(List<OntologyCodeClass> ontologies)
        {
            is_template = false;

            // Check if this is a valid template folder (supports both JSON and legacy formats)
            if (ROIClassTools.IsValidTemplateFolder(path))
            {
                is_template = true;
                template_name = Path.GetFileName(path);
                // Load ROIs using the new method that handles both formats
                ROIs = ROIClassTools.LoadROIsFromFolder(path, ontologies);
            }
        }
    }
}