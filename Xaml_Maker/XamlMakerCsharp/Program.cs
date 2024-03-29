﻿using System;
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
            if (true)
            {
                string xml_path = @"\\ro-ariaimg-v\va_data$\ProgramData\Vision\Templates\structure\Template_Output_VarianXml";
                string out_path = @"C:\Users\b5anderson\Modular_Projects\DicomTemplateMakerCSharp\DicomTemplateMakerGUI\bin\x64\Debug";
                foreach (string file in Directory.GetFiles(xml_path, "*.xml"))
                {
                    //string new_file = @"K:\Template_Output_VarianXml\AbdPelv_Anal.xml";
                    VarianXmlReader reader = new VarianXmlReader(file);
                    reader.XmlToROI(out_path);
                }
            }
            if (false)
            {
                string rois_path = @"C:\Users\b5anderson\Modular_Projects\DicomTemplateMakerCSharp\DicomTemplateMakerGUI\bin\x64\Debug";
                foreach (string directory in Directory.GetDirectories(rois_path))
                {
                    VarianXmlWriter xmlwriter = new VarianXmlWriter();

                    xmlwriter.LoadROIsFromPath(directory);
                    xmlwriter.SaveFile($@"K:\{Path.GetFileName(directory)}.xml");
                }
            }


            XElement TCPGamma = Structure.Element("TCPGamma");
            TCPGamma.FirstAttribute.Value = "true";
            base_struct.Add(Structure);
            XmlWriter writer = XmlWriter.Create("test.xml");
            doc.WriteTo(writer);
            writer.Close();
            int x = 1;
        }
    }
}
