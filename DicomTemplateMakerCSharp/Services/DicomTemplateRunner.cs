using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomTemplateMakerCSharp.Services
{
    class DicomTemplateRunner
    {
        DicomSeriesReader reader;
        string template_folder = @".";
        string roiname, color, interperter;
        Dictionary<string, List<ROIClass>> template_dictionary;
        public DicomTemplateRunner()
        {
            reader = new DicomSeriesReader();
        }
        public void build_dictionary()
        {
            template_dictionary = new Dictionary<string, List<ROIClass>>();
            string[] template_directories = Directory.GetDirectories(template_folder, "*", SearchOption.AllDirectories);
            foreach (string template_directory in template_directories)
            {
                if (File.Exists(Path.Join(template_directory, "Paths.txt")))
                {
                    if (Directory.Exists(Path.Join(template_directory, "ROIs")))
                    {
                        List<ROIClass> rois = new List<ROIClass>();
                        foreach (string roi_file in Directory.GetFiles(Path.Join(template_directory, "ROIs"), "*.txt"))
                        {
                            roiname = Path.GetFileName(roi_file).Split(".txt")[0];
                            string[] instructions = File.ReadAllLines(roi_file);
                            color = instructions[0];
                            interperter = "";
                            if (instructions.Length == 2)
                            {
                                interperter = instructions[1];
                            }
                            rois.Add(new ROIClass(color, roiname, interperter));
                        }
                        string[] paths = File.ReadAllLines(Path.Join(template_directory, "Paths.txt"));
                        foreach (string path in paths)
                        {
                            template_dictionary.Add(path, rois);
                        }
                    }
                }
            }
        }
        public void walk_down_folders()
        {
            foreach (string path in template_dictionary.Keys)
            {
                if (!Directory.Exists(path))
                {
                    continue;
                }
                string[] all_directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
                foreach (string directory in all_directories)
                {
                    string[] dicom_files = Directory.GetFiles(directory, "*.dcm");
                    if (dicom_files.Length > 0)
                    {
                        reader.dicomParser.__reset__();
                        reader.parse_folder(directory);
                        foreach (string uid in reader.dicomParser.dicom_series_instance_uids)
                        {
                            reader.load_DICOM(uid);
                            reader.update_template(delete_contours: true);
                            foreach (ROIClass roi in template_dictionary[path])
                            {
                                reader.add_roi(roi);
                            }
                            reader.save_RT(Path.Join(directory, "test.dcm"));
                        }
                    }
                }
            }
        }
        public void run()
        {
            build_dictionary();
            walk_down_folders();
        }
    }
}
