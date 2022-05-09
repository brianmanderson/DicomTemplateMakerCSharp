using System;
using FellowOakDicom;

namespace DicomTemplateMakerCSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            string dicom_file = @"C:\Users\b5anderson\Modular_Projects\Dicom_RT_and_Images_to_Mask\src\DicomRTTool\template_RS.dcm";
            var file = DicomFile.Open(dicom_file, FileReadOption.ReadAll);
            string modality = file.Dataset.GetString(DicomTag.Modality);
        }
    }
}
