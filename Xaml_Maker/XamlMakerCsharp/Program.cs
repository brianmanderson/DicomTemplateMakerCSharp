using System;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using System.Runtime;
using XamlMakerCsharp;

namespace XamlMakerCsharp
{
    class Program
    {
        static void Main(string[] args)
        {
            string rois_path = @"C:\Users\b5anderson\Modular_Projects\DicomTemplateMakerCSharp\DicomTemplateMakerGUI\bin\x64\Debug";
            foreach (string directory in Directory.GetDirectories(rois_path))
            {
                VarianXmlWriter xmlwriter = new VarianXmlWriter();

                xmlwriter.LoadROIsFromPath(directory);
                xmlwriter.SaveFile($@"K:\{Path.GetFileName(directory)}.xml");
            }

            int x = 1;
        }
    }
}
