using System;
using System.Collections.Generic;
using System.Text;
using System;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using System.Runtime;

namespace XamlMakerCsharp
{
    public class VarianXmlReader
    {
        private XNamespace ab = "http://www.w3.org/2001/XMLSchema-instance";
        public XDocument doc;
        public XElement root;
        public XElement base_struct;
        public XElement preview;
        public VarianXmlReader(string xml_path)
        {
            doc = XDocument.Load(xml_path);
            root = doc.Root;
            preview = root.Element("Preview");
        }
        public void XmlToROI(string output_path)
        {
            //string description = preview.Elements("ID");
            string template_name = preview.Attribute("ID").Value.Replace(' ', '_');
            if (!Directory.Exists(Path.Combine(output_path, template_name)))
            {
                Directory.CreateDirectory(Path.Combine(output_path, template_name));
            }
            XElement structures = root.Element("Structures");
            foreach (XElement structure in structures.Elements())
            {
                int x = 1;
            }
        }
    }
}
