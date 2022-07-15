using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.DicomTemplateServices
{
    public class DicomTemplateRunner
    {
        DicomSeriesReader reader;
        string template_folder = @".";
        string roiname, color, interperter;
        Dictionary<string, List<ROIClass>> template_dictionary;
        Dictionary<string, List<string>> paths_dictionary;
        public DicomTemplateRunner()
        {
            reader = new DicomSeriesReader();
        }
        public DicomTemplateRunner(string template_folder)
        {
            this.template_folder = template_folder;
            reader = new DicomSeriesReader();
        }
        public void build_dictionary()
        {
            template_dictionary = new Dictionary<string, List<ROIClass>>();
            paths_dictionary = new Dictionary<string, List<string>>();
            string[] template_directories = Directory.GetDirectories(template_folder);
            foreach (string template_directory in template_directories)
            {
                if (File.Exists(Path.Combine(template_directory, "Paths.txt")))
                {
                    if (Directory.Exists(Path.Combine(template_directory, "ROIs")))
                    {
                        List<ROIClass> rois = new List<ROIClass>();
                        foreach (string roi_file in Directory.GetFiles(Path.Combine(template_directory, "ROIs"), "*.txt"))
                        {
                            roiname = Path.GetFileName(roi_file).Replace(".txt", "");
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
                            rois.Add(new ROIClass(color, roiname, interperter, code_class));
                        }
                        template_dictionary.Add(Path.GetFileName(template_directory), rois);
                        string[] paths = File.ReadAllLines(Path.Combine(template_directory, "Paths.txt"));
                        List<string> path_list = new List<string>();
                        foreach (string path in paths)
                        {
                            path_list.Add(path);
                        }
                        paths_dictionary.Add(Path.GetFileName(template_directory), path_list);
                    }
                }
            }
        }
        public void run_for_path(string template_name, string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }
            string[] all_directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
            foreach (string directory in all_directories)
            {
                string status_file = Path.Combine(directory, $"CreatedRT_{template_name}.txt");
                if (File.Exists(status_file))
                {
                    continue;
                }
                string[] dicom_files = Directory.GetFiles(directory, "*.dcm");
                if (dicom_files.Length > 0)
                {
                    FolderWatcher folder_watcher_class = new FolderWatcher(directory);
                    int tries = 0;
                    Thread.Sleep(3000);
                    while (folder_watcher_class.Folder_Changed)
                    {
                        folder_watcher_class.Folder_Changed = false;
                        Console.WriteLine("Waiting for files to be fully transferred...");
                        tries += 1;
                        Thread.Sleep(5000);
                        if (tries > 3)
                        {
                            return;
                        }
                    }
                    dicom_files = Directory.GetFiles(directory, "*.dcm");
                    if (dicom_files.Length == 0)
                    {
                        return;
                    }
                    reader.dicomParser.__reset__();
                    reader.parse_folder(directory);
                    foreach (string uid in reader.dicomParser.dicom_series_instance_uids)
                    {
                        string outpath = Path.Combine(directory, $"{template_name}_{uid}.dcm");
                        if (File.Exists(outpath))
                        {
                            continue;
                        }
                        reader.load_DICOM(uid);
                        reader.update_template(delete_contours: true, delete_everything: true);
                        foreach (ROIClass roi in template_dictionary[template_name])
                        {
                            reader.add_roi(roi);
                        }
                        reader.save_RT(outpath);
                    }
                    if (!File.Exists(status_file))
                    {
                        FileStream fid_status_file = File.OpenWrite(status_file);
                        fid_status_file.Close();
                    }
                }
            }
        }
        public void run_for_template_key(string template_name)
        {
            foreach (string path in paths_dictionary[template_name])
            {
                try
                {
                    run_for_path(template_name, path);
                }
                catch
                {
                    continue;
                }
            }
        }
        public void walk_down_folders()
        {
            foreach (string template_name in template_dictionary.Keys)
            {
                try
                {
                    run_for_template_key(template_name);
                }
                catch
                {
                    continue;
                }
            }

        }
        public void run()
        {
            while (true)
            {
                Thread.Sleep(3000);
                build_dictionary();
                walk_down_folders();
            }
        }
    }
}
