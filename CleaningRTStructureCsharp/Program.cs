using System;

namespace CleaningRTStructureCsharp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            RTCleaner cleaner = new RTCleaner(@"C:\Users\b5anderson\Modular_Projects\DicomTemplateMakerCSharp\DicomTemplateMakerGUI\TG263Abdomen.dcm");
            cleaner.save(@"C:\Users\b5anderson\Modular_Projects\DicomTemplateMakerCSharp\DicomTemplateMakerGUI\new.dcm");
        }
    }
}
