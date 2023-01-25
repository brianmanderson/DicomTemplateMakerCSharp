using System;
using System.Collections.Generic;
using System.Text;
using System;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using System.Runtime;
using XamlMakerCsharp;

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
            foreach (XElement s in structures.Elements())
            {
                string roi_name = s.Attribute("Name").Value;
                string roi_id = s.Attribute("ID").Value;
                
                XElement Identification = s.Element("Identification");
                string volume_type = Identification.Element("VolumeType").Value;
                XElement StructureCode = Identification.Element("StructureCode");
                string code = StructureCode.Attribute("Code").Value;
                string code_scheme = StructureCode.Attribute("CodeScheme").Value;
                string code_scheme_version = StructureCode.Attribute("CodeSchemeVersion").Value;

                string type_index = s.Element("TypeIndex").Value;
                string color_and_style = s.Element("ColorAndStyle").Value;
                List<string> color = new List<string>();
                if (color_and_style.StartsWith("RGB"))
                {
                    color_and_style = color_and_style.Substring(3);
                    color.Add(color_and_style.Substring(0, 3).Trim());
                    color.Add(color_and_style.Substring(3, 3).Trim());
                    color.Add(color_and_style.Substring(6, 3).Trim());
                }
                else
                {
                    color.Add("255");
                    color.Add("0");
                    color.Add("0");
                }
                string out_color = $"{color[0]}\\{color[1]}\\{color[2]}";
                OntologyCodeClass ontology = new OntologyCodeClass(name: roi_id, code_value: code, scheme_designated: code_scheme_version);
                ROIClass roi = new ROIClass(color: out_color, name: roi_name, roi_interpreted_type: volume_type,
                    identification_code_class: ontology);
                string dvh_line_style = s.Element("DVHLineStyle").Value;
                int x = 1;
            }
        }
    }
}
