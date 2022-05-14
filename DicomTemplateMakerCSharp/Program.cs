using System;
using DicomTemplateMakerCSharp.Services;

namespace DicomTemplateMakerCSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            string temp_folder = @"\\ucsdhc-varis2\radonc$\BMAnderson\BMA_Export";
            temp_folder = @"C:\Users\markb\Modular_Projects\Example_Data\Data\Image_Data\T1\Post1\001";
            DicomSeriesReader reader = new DicomSeriesReader();
            reader.parse_folder(temp_folder);
            foreach (string uid in reader.dicomParser.dicom_series_instance_uids)
            {
                reader.load_DICOM(uid);
                reader.update_template(true);
            }
            
        }
    }
}
