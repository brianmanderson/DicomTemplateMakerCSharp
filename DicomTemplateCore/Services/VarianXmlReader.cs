using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ROIOntologyClass;


namespace DicomTemplateMakerGUI.Services
{
    public class VarianXmlReader
    {
        private XNamespace ab = "http://www.w3.org/2001/XMLSchema-instance";
        public XDocument doc;
        public XElement root;
        public XElement? base_struct;
        public XElement? preview;
        string xml_path;
        public VarianXmlReader(string doc_path)
        {
            xml_path = doc_path;
            doc = XDocument.Load(xml_path);
            // XDocument.Load throws XmlException for a document without a root element, so Root is set.
            root = doc.Root!;
            preview = root.Element("Preview");
        }
        /// <summary>The child element <paramref name="name"/>; throws when it is missing.</summary>
        private static XElement RequiredElement(XElement parent, string name)
        {
            return parent.Element(name) ?? throw new XmlException($"<{parent.Name}> has no <{name}> element.");
        }
        /// <summary>The value of attribute <paramref name="name"/>; throws when it is missing.</summary>
        private static string RequiredAttribute(XElement element, string name)
        {
            return element.Attribute(name)?.Value ?? throw new XmlException($"<{element.Name}> has no {name} attribute.");
        }
        public void AddToTemplateMaker(TemplateMaker maker, XElement s)
        {
            try
            {
                string roi_id = RequiredAttribute(s, "ID");
                string roi_name = RequiredAttribute(s, "Name");
                if (roi_id == "")
                {
                    return;
                }
                if (roi_name == "")
                {
                    roi_name = roi_id;
                }
                XElement Identification = RequiredElement(s, "Identification");
                string volume_type = RequiredElement(Identification, "VolumeType").Value;//.ToUpper();
                XElement? StructureCode = Identification.Element("StructureCode");
                string code = "";
                string code_scheme = "";
                string code_scheme_version = "";
                if (StructureCode != null)
                {
                    code = RequiredAttribute(StructureCode, "Code");
                    code_scheme = RequiredAttribute(StructureCode, "CodeScheme");
                    code_scheme_version = RequiredAttribute(StructureCode, "CodeSchemeVersion");
                }

                string type_index = RequiredElement(s, "TypeIndex").Value;
                string color_and_style = RequiredElement(s, "ColorAndStyle").Value;
                List<string> color = new List<string>();
                string contourstyle = "";
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
                    if (splitup.Count == 3)
                    {
                        contourstyle = splitup[0];
                    }
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
                string line_style = RequiredElement(s, "DVHLineStyle").Value;
                string write_line_style = "solid";
                switch (line_style)
                {
                    case "0":
                        write_line_style = "solid";
                        break;
                    case "1":
                        write_line_style = "-------";
                        break;
                    case "2":
                        write_line_style = "*******";
                        break;
                    case "3":
                        write_line_style = "-*-*-*-";
                        break;
                    case "4":
                        write_line_style = "-**-**-";
                        break;
                }
                string line_color = RequiredElement(s, "DVHLineColor").Value;
                string line_width = RequiredElement(s, "DVHLineWidth").Value;
                string out_color = $"{color[0]}\\{color[1]}\\{color[2]}";
                OntologyCodeClass ontology = new OntologyCodeClass(name: roi_name, code_value: code, scheme_designated: code_scheme);
                ROIClass roi = new ROIClass(color: out_color, name: roi_id, roi_interpreted_type: volume_type,
                    identification_code_class: ontology, type_index: type_index, contour_style: contourstyle, dvhLineStyle: write_line_style, dvhLineColor: line_color, dvhLineWidth: line_width);
                maker.ROIs.Add(roi);
                maker.Ontologies.Add(ontology);
            }
            catch
            {
                return;
            }
        }
        public void XmlToROI(string output_path)
        {
            //string description = preview.Elements("ID");
            if (preview == null)
            {
                throw new XmlException($"{xml_path} has no <Preview> element.");
            }
            string template_name = RequiredAttribute(preview, "ID").Replace(' ', '_');
            TemplateMaker templateMaker = new TemplateMaker();
            templateMaker.define_output(Path.Combine(output_path, template_name));
            templateMaker.set_onto_path(Path.Combine(output_path, "Ontologies"));
            XElement structures = RequiredElement(root, "Structures");
            foreach (XElement s in structures.Elements())
            {
                AddToTemplateMaker(templateMaker, s);
            }
            templateMaker.make_template();
            templateMaker.write_ontologies();
        }
    }
}
