using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DicomTemplateMakerGUI.Services;


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
        public void categorize_folder(string path)
        {
            OntologyCodeClass code_class;
            this.path = path;
            is_template = false;
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
                    if (code_values.Length == 3)
                    {
                        code_class = new OntologyCodeClass(code_values[0], code_values[1], code_values[2]);
                    }
                    else
                    {
                        code_class = new OntologyCodeClass(code_values[0], code_values[1], code_values[2], code_values[3],
                            code_values[4], code_values[5], code_values[6], code_values[7], code_values[8]);
                    }
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
