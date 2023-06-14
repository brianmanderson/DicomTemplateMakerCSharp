using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using ROIOntologyClass;
using FellowOakDicom;

namespace DicomTemplateMakerGUI.DicomTemplateServices
{
    public class DicomTemplateRunner
    {
        DicomSeriesReader reader;
        string template_folder = @".";
        string roiname, color, interperter;
        Dictionary<string, List<ROIClass>> template_dictionary;
        public Dictionary<string, Dictionary<string, List<string>>> Template_DicomTags = new Dictionary<string, Dictionary<string, List<string>>>();
        Dictionary<string, List<string>> paths_dictionary;
        Dictionary<string, List<string>> files_and_series_descriptions_dictionary = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> files_and_study_descriptions_dictionary = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> files_and_modality_dictionary = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> directory_and_files_dictionary = new Dictionary<string, List<string>>();
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
            OntologyCodeClass code_class;
            template_dictionary = new Dictionary<string, List<ROIClass>>();
            paths_dictionary = new Dictionary<string, List<string>>();
            Template_DicomTags = new Dictionary<string, Dictionary<string, List<string>>>();
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
                            if (instructions.Length >= 3)
                            {
                                interperter = instructions[2];
                            }
                            bool include = true;
                            if (instructions.Length > 3)
                            {
                                include = bool.Parse(instructions[3]);
                            }
                            if (include)
                            {
                                rois.Add(new ROIClass(color, roiname, interperter, code_class));
                            }
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
                    string template_name = Path.GetFileName(template_directory);
                    Dictionary<string, List<string>> out_dictionary = new Dictionary<string, List<string>>
                    {
                        { "Study Description", new List<string>() },
                        { "Series Description", new List<string>() }
                    };
                    if (File.Exists(Path.Combine(template_directory, "DicomTags.txt")))
                    {
                        string[] file_paths = File.ReadAllLines(Path.Combine(template_directory, "DicomTags.txt"));
                        List<string> series_list = new List<string>();
                        List<string> studies_list = new List<string>();
                        foreach (string file_path in file_paths)
                        {
                            string[] key_values = file_path.Split('\\');
                            string key = key_values[0];
                            List<string> values = key_values.Skip(1).ToList();
                            if (key == "Series Description")
                            {
                                series_list.AddRange(values);
                            }
                            else if (key == "Study Description")
                            {
                                studies_list.AddRange(values);
                            }
                        }
                        out_dictionary["Study Description"] = studies_list;
                        out_dictionary["Series Description"] = series_list;
                    }
                    Template_DicomTags.Add(template_name, out_dictionary);
                }
            }
        }
        public void run_for_path(string template_name, string path, bool delete_RT)
        {
            if (!Directory.Exists(path))
            {
                return;
            }
            Dictionary<string, List<string>> DicomTags = Template_DicomTags[template_name];
            List<string> all_directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories).ToList();
            all_directories.Add(path);
            bool run_program;
            foreach (string directory in all_directories)
            {
                string[] temp_dcm_files = Directory.GetFiles(directory, $"{template_name}_UID*");
                if (temp_dcm_files.Any())
                {
                    if (delete_RT)
                    {
                        foreach (string tmp_file in temp_dcm_files)
                        {
                            File.Delete(tmp_file);
                        }
                    }
                    continue;
                }
                List<string> dicom_files = Directory.GetFiles(directory, "*.dcm").ToList();
                if (dicom_files.Count > 0)
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
                    dicom_files = Directory.GetFiles(directory, "*.dcm").ToList();
                    if (dicom_files.Count == 0)
                    {
                        return;
                    }
                    run_program = false;
                    string directory_key = $"{directory}_{template_name}";
                    if (!directory_and_files_dictionary.ContainsKey(directory_key))
                    {
                        run_program = true;
                    }
                    else if (directory_and_files_dictionary[directory_key].Except(dicom_files).Any())
                    {
                        run_program = true;
                    }
                    else if (dicom_files.Except(directory_and_files_dictionary[directory_key]).Any())
                    {
                        run_program = true;
                    }
                    bool has_tags = false;
                    if (DicomTags["Series Description"].Count > 0)
                    {
                        has_tags = true;
                        if (files_and_series_descriptions_dictionary.ContainsKey(directory_key))
                        {
                            foreach (string series_description in files_and_series_descriptions_dictionary[directory_key])
                            {
                                foreach (string dicom_tag in DicomTags["Series Description"])
                                {
                                    if (series_description.ToLower().Contains(dicom_tag.ToLower()))
                                    {
                                        run_program = true;
                                        break;
                                    }
                                    else if (dicom_tag.ToLower().Contains(series_description.ToLower()))
                                    {
                                        run_program = true;
                                        break;
                                    }
                                }
                                if (run_program)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    if (DicomTags["Study Description"].Count > 0)
                    {
                        has_tags = true;
                        if (files_and_study_descriptions_dictionary.ContainsKey(directory_key))
                        {
                            foreach (string study_description in files_and_study_descriptions_dictionary[directory_key])
                            {
                                foreach (string dicom_tag in DicomTags["Study Description"])
                                {
                                    if (study_description.ToLower().Contains(dicom_tag.ToLower()))
                                    {
                                        run_program = true;
                                        break;
                                    }
                                    else if (dicom_tag.ToLower().Contains(study_description.ToLower()))
                                    {
                                        run_program = true;
                                        break;
                                    }
                                }
                                if (run_program)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    if (!has_tags)
                    {
                        run_program = true;
                    }
                    if (run_program)
                    {
                        reader.dicomParser.__reset__();
                        reader.parse_folder(directory);
                        if (!directory_and_files_dictionary.ContainsKey(directory_key))
                        {
                            directory_and_files_dictionary.Add(directory_key, dicom_files);
                        }
                        else
                        {
                            directory_and_files_dictionary[directory_key] = dicom_files;
                        }
                        if (!files_and_modality_dictionary.ContainsKey(directory_key))
                        {
                            files_and_modality_dictionary.Add(directory_key, new List<string>());
                        }
                        if (!files_and_series_descriptions_dictionary.ContainsKey(directory_key))
                        {
                            files_and_series_descriptions_dictionary.Add(directory_key, new List<string>());
                        }
                        if (!files_and_study_descriptions_dictionary.ContainsKey(directory_key))
                        {
                            files_and_study_descriptions_dictionary.Add(directory_key, new List<string>());
                        }
                        foreach (string uid in reader.dicomParser.dicom_series_instance_uids)
                        {
                            string outpath = Path.Combine(directory, $"{template_name}_UID{uid}.dcm");
                            if (File.Exists(outpath))
                            {
                                continue;
                            }
                            reader.load_DICOM(uid);
                            string modality = reader.return_dicom_tag(DicomTag.Modality);
                            string series_description = reader.return_dicom_tag(DicomTag.SeriesDescription);
                            string study_description = reader.return_dicom_tag(DicomTag.StudyDescription);
                            files_and_modality_dictionary[directory_key].Add(modality);
                            files_and_series_descriptions_dictionary[directory_key].Add(series_description);
                            files_and_study_descriptions_dictionary[directory_key].Add(study_description);
                            bool checked_tags_go = false;
                            bool has_keys = false;
                            if (DicomTags["Study Description"].Count > 0)
                            {
                                has_keys = true;
                                foreach (string study_desc in DicomTags["Study Description"])
                                {
                                    if (study_desc.ToLower().Contains(study_description.ToLower()))
                                    {
                                        checked_tags_go = true;
                                        break;
                                    }
                                    else if (study_description.ToLower().Contains(study_desc.ToLower()))
                                    {
                                        checked_tags_go = true;
                                        break;
                                    }
                                }
                            }
                            if (DicomTags["Series Description"].Count > 0)
                            {
                                has_keys = true;
                                foreach (string series_desc in DicomTags["Series Description"])
                                {
                                    if (series_desc.ToLower().Contains(series_description.ToLower()))
                                    {
                                        checked_tags_go = true;
                                        break;
                                    }
                                    else if (series_description.ToLower().Contains(series_desc.ToLower()))
                                    {
                                        checked_tags_go = true;
                                        break;
                                    }
                                }
                            }
                            if (has_keys & !checked_tags_go)
                            {
                                run_program = false;
                            }
                            if (run_program)
                            {
                                reader.update_template(delete_contours: true, delete_everything: true);
                                if (template_name.Length > 16)
                                {
                                    reader.update_dicom_tag(DicomTag.StructureSetLabel, template_name.Substring(0, 16));
                                }
                                else
                                {
                                    reader.update_dicom_tag(DicomTag.StructureSetLabel, template_name);
                                }
                                reader.update_dicom_tag(DicomTag.Manufacturer, "UCSD Residency");
                                reader.update_dicom_tag(DicomTag.ManufacturerModelName, "Universal_RT_Creator");
                                foreach (ROIClass roi in template_dictionary[template_name])
                                {
                                    try
                                    {
                                        reader.add_roi(roi);
                                    }
                                    catch
                                    {
                                        continue;
                                    }
                                }
                                reader.save_RT(outpath);
                                files_and_modality_dictionary.Remove(directory_key);
                            }
                        }
                    }
                }
            }
        }
        public void run_for_template_key(string template_name, bool delete_RT)
        {
            foreach (string path in paths_dictionary[template_name])
            {
                try
                {
                    run_for_path(template_name, path, delete_RT);
                }
                catch
                {
                    continue;
                }
            }
        }
        public void walk_down_folders(bool delete_RT)
        {
            foreach (string template_name in template_dictionary.Keys)
            {
                try
                {
                    run_for_template_key(template_name, delete_RT);
                }
                catch
                {
                    continue;
                }
            }

        }
        public void delete_rts()
        {
            build_dictionary();
            walk_down_folders(true);
        }
        public void run()
        {
            while (true)
            {
                Thread.Sleep(3000);
                build_dictionary();
                walk_down_folders(false);
            }
        }
    }
}
