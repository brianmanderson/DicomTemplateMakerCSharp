using System;
using System.IO;
using System.Collections.Generic;
using DicomTemplateMakerCSharp.Services;

namespace DicomTemplateMakerCSharp
{
    class Program
    {
        string temp_folder;
        static void Main(string[] args)
        {
            string roiname, color, interperter;
            Console.WriteLine("Hello World!");
            string[] templates = Directory.GetFiles(@".", "*Template*.txt", SearchOption.AllDirectories);
            DicomSeriesReader reader = new DicomSeriesReader();
            foreach (string template_file in templates)
            {
                List<ROIClass> rois = new List<ROIClass>();
                List<string> instructions = new List<string>();
                string[] temp = File.ReadAllLines(template_file);
                foreach (string t in temp)
                {
                    instructions.Add(t);
                }
                string temp_folder = instructions[0];
                instructions.RemoveAt(0);
                while (instructions.Count > 0)
                {
                    roiname = instructions[0];
                    color = instructions[1];
                    interperter = instructions[2];
                }
                reader.parse_folder(temp_folder);
                foreach (string uid in reader.dicomParser.dicom_series_instance_uids)
                {
                    reader.load_DICOM(uid);
                    reader.update_template(true);
                }
            }

            
        }
    }
}
