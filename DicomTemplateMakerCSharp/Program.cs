using System;
using DicomTemplateMakerCSharp.Services;

namespace DicomTemplateMakerCSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            string temp_folder = @"C:\Users\b5anderson\Modular_Projects\Temp_Patient";
            DicomSeriesReader reader = new DicomSeriesReader();
            reader.parse_folder(temp_folder);
            foreach (string uid in reader.dicomParser.dicom_series_instance_uids)
            {
                reader.load_DICOM(uid);
            }
            
        }
    }
}
