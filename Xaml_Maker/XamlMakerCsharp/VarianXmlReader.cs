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
        string xml_path;
        public VarianXmlReader(string doc_path)
        {
            xml_path = doc_path;
            doc = XDocument.Load(xml_path);
            root = doc.Root;
            preview = root.Element("Preview");
        }
        public void AddToTemplateMaker(TemplateMaker maker, XElement s)
        {
            try
            {
                string roi_id = s.Attribute("ID").Value;
                string roi_name = s.Attribute("Name").Value;
                if (roi_id == "")
                {
                    return;
                }
                XElement Identification = s.Element("Identification");
                string volume_type = Identification.Element("VolumeType").Value;//.ToUpper();
                XElement StructureCode = Identification.Element("StructureCode");
                if (StructureCode is null)
                {
                    return;
                }
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
                    List<string> splitup = color_and_style.Split(' ').ToList();
                    string color_name = splitup.Last();
                    if (color_name.ToLower().Contains("oran"))
                    {
                        color_name = "Orange";
                    }
                    else if (color_name.ToLower().Contains("magent"))
                    {
                        color_name = "Magenta";
                    }
                    else if (color_name.ToLower().Contains("yell"))
                    {
                        color_name = "Yellow";
                    }
                    else if (color_name.ToLower().Contains("brow"))
                    {
                        color_name = "Brown";
                    }
                    System.Drawing.Color k = System.Drawing.Color.FromName(color_name);
                    if (k.IsKnownColor)
                    {
                        color.Add(k.R.ToString());
                        color.Add(k.G.ToString());
                        color.Add(k.B.ToString());
                    }
                    else
                    {
                        color.Add("255");
                        color.Add("0");
                        color.Add("0");
                    }
                }
                string out_color = $"{color[0]}\\{color[1]}\\{color[2]}";
                OntologyCodeClass ontology = new OntologyCodeClass(name: roi_name, code_value: code, scheme_designated: code_scheme);
                ROIClass roi = new ROIClass(color: out_color, name: roi_id, roi_interpreted_type: volume_type,
                    identification_code_class: ontology);
                maker.ROIs.Add(roi);
                maker.Ontologies.Add(ontology);
                string dvh_line_style = s.Element("DVHLineStyle").Value;
                int x = 1;
            }
            catch
            {
                return;
            }
        }
        public void XmlToROI(string output_path)
        {
            //string description = preview.Elements("ID");
            string template_name = preview.Attribute("ID").Value.Replace(' ', '_');
            TemplateMaker templateMaker = new TemplateMaker();
            templateMaker.define_output(Path.Combine(output_path, template_name));
            templateMaker.set_onto_path(Path.Combine(output_path, "Ontologies"));
            XElement structures = root.Element("Structures");
            foreach (XElement s in structures.Elements())
            {
                AddToTemplateMaker(templateMaker, s);
            }
            templateMaker.make_template();
            templateMaker.write_ontologies();
        }
    }
}
