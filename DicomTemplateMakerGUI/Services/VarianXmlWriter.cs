using System;
using System.Linq;
using System.Drawing;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.Services
{
    public class VarianXmlWriter
    {
        static string ReturnUppercaseWord(string word)
        {
            return $"{word.FirstOrDefault().ToString().ToUpper()}{word.Substring(1).ToLower()}";
        }
        private XNamespace ab = "http://www.w3.org/2001/XMLSchema-instance";
        public XDocument doc;
        public XElement root;
        public XElement base_struct;
        private XDocument create_new_document()
        {
            DateTime now = DateTime.Now;
            string userName = Environment.UserName;
            XDocument doc =
                new XDocument(
                    new XElement("StructureTemplate",
                        new XAttribute(XNamespace.Xmlns + "xsi", ab),
                        new XAttribute("Version", "1.1"),
                    new XElement("Preview",
                        new XAttribute("ApprovalHistory", $"{userName} Created [ {now.Month} {now.Day} {now.Year} {now.Hour}:{now.Minute}:{now.Second}]"),
                        new XAttribute("ApprovalStatus", "Unapproved"),
                        new XAttribute("AssignedUsers", userName),
                        new XAttribute("Description", "Auto-generated xml file"),
                        new XAttribute("Diagnosis", ""),
                        new XAttribute("Type", "Structure"),
                        new XAttribute("ID", "Template"),
                        new XAttribute("LastModified", $"[ {now.Month} {now.Day} {now.Year} {now.Hour}:{now.Minute}:{now.Second}]")),
                    new XElement("Structures")));
            return doc;
        }
        public VarianXmlWriter()
        {
            doc = create_new_document();
            root = doc.Root;
            base_struct = root.Element("Structures");
        }
        public void SaveFile(string out_path)
        {
            XmlWriter writer = XmlWriter.Create(out_path);
            doc.WriteTo(writer);
            writer.Close();
        }
        public void LoadROIsFromPath(string template_folder)
        {
            if (Directory.Exists(Path.Combine(template_folder, "ROIs")))
            {
                SetID(Path.GetFileName(template_folder));
                foreach (string file in Directory.GetFiles(Path.Combine(template_folder, "ROIs")))
                {
                    InterpretProgramTextFile(file);
                }
            }
        }
        public void SetID(string template_id)
        {
            XElement preview = root.Element("Preview");
            preview.SetAttributeValue("ID", template_id);
        }
        public void InterpretProgramTextFile(string file)
        {
            string roi_name = Path.GetFileNameWithoutExtension(file);
            ROIClass roi = new ROIClass(file);
            string[] instructions = File.ReadAllLines(file);
            string color = instructions[0];
            string[] color_values = color.Split('\\');

            color_values[0] = roi.R.ToString().PadLeft(3);
            color_values[1] = roi.G.ToString().PadLeft(3);
            color_values[2] = roi.B.ToString().PadLeft(3);

            int num_zeros = 0;
            foreach (string c_val in color_values)
            {
                if (c_val == "  0")
                {
                    num_zeros++;
                }
            }
            color = $"RGB{color_values[0]}{color_values[1]}{color_values[2]}";
            if (roi.ContourStyle != "")
            {
                var Color_color = Color.FromArgb(int.Parse(color_values[0]), int.Parse(color_values[1]), int.Parse(color_values[2]));
                var color_lookup = Enum.GetValues(typeof(KnownColor)).Cast<KnownColor>().Select(Color.FromKnownColor).ToLookup(c => c.ToArgb());
                var named_color = color_lookup[Color_color.ToArgb()].ToList();
                if (named_color.Count > 0)
                {
                    color = $"{roi.ContourStyle} - {named_color[0].Name}";
                    if (color.Length > 16)
                    {
                        color = color.Remove(16);
                    }
                }
            }
            else if (num_zeros == 2)
            {
                if (color_values[0] == "255")
                {
                    color = "Red";
                }
                if (color_values[1] == "255")
                {
                    color = "Green";
                }
                if (color_values[2] == "255")
                {
                    color = "Blue";
                }
            }


            string[] code_values = instructions[1].Split('\\');
            AddROI(roi, colorAndStyle: color);
        }
        public void AddROI(ROIClass roi, string colorAndStyle)
        {
            XElement new_structure = new XElement("Structure");
            new_structure.SetAttributeValue("ID", roi.ROIName);
            new_structure.SetAttributeValue("Name", roi.Ontology_Class.CodeMeaning);

            XElement Identification = new XElement("Identification");
            Identification.Add(new XElement("VolumeID"));
            Identification.Add(new XElement("VolumeCode"));
            XElement VolumeType = new XElement("VolumeType");
            VolumeType.Value = roi.ROI_Interpreted_type;
            Identification.Add(VolumeType);
            Identification.Add(new XElement("VolumeCodeTable"));

            XElement StructureCode = new XElement("StructureCode");
            StructureCode.SetAttributeValue("Code", roi.Ontology_Class.CodeValue);
            StructureCode.SetAttributeValue("CodeScheme", roi.Ontology_Class.Scheme);
            StructureCode.SetAttributeValue("CodeSchemeVersion", "3.2");
            Identification.Add(StructureCode);
            new_structure.Add(Identification);

            XElement TypeIndex = new XElement("TypeIndex");
            TypeIndex.Value = roi.TypeIndex;
            new_structure.Add(TypeIndex);

            XElement ColorAndStyle = new XElement("ColorAndStyle");
            ColorAndStyle.Value = colorAndStyle;
            new_structure.Add(ColorAndStyle);

            XElement SearchCTLow = new XElement("SearchCTLow");
            SearchCTLow.SetAttributeValue(ab + "nil", "true");
            new_structure.Add(SearchCTLow);

            XElement SearchCTHigh = new XElement("SearchCTHigh");
            SearchCTHigh.SetAttributeValue(ab + "nil", "true");
            new_structure.Add(SearchCTHigh);

            XElement DVHLineStyle = new XElement("DVHLineStyle");
            switch(roi.DVHLineStyle)
            {
                case "solid":
                    DVHLineStyle.Value = "0";
                    break;
                case "-------":
                    DVHLineStyle.Value = "1";
                    break;
                case "*******":
                    DVHLineStyle.Value = "2";
                    break;
                case "-*-*-*-":
                    DVHLineStyle.Value = "3";
                    break;
                case "-**-**-":
                    DVHLineStyle.Value = "4";
                    break;
            }
            new_structure.Add(DVHLineStyle);

            XElement DVHLineColor = new XElement("DVHLineColor");
            DVHLineColor.Value = roi.DVHLineColor;
            new_structure.Add(DVHLineColor);

            XElement DVHLineWidth = new XElement("DVHLineWidth");
            DVHLineWidth.Value = roi.DVHLineWidth;
            new_structure.Add(DVHLineWidth);

            XElement EUDAlpha = new XElement("EUDAlpha");
            EUDAlpha.SetAttributeValue(ab + "nil", "true");
            new_structure.Add(EUDAlpha);

            XElement TCPAlpha = new XElement("TCPAlpha");
            TCPAlpha.SetAttributeValue(ab + "nil", "true");
            new_structure.Add(TCPAlpha);

            XElement TCPBeta = new XElement("TCPBeta");
            TCPBeta.SetAttributeValue(ab + "nil", "true");
            new_structure.Add(TCPBeta);

            XElement TCPGamma = new XElement("TCPGamma");
            TCPGamma.SetAttributeValue(ab + "nil", "true");
            new_structure.Add(TCPGamma);

            base_struct.Add(new_structure);
        }
        public void AddROI(string file)
        {
            string roi_name = Path.GetFileNameWithoutExtension(file);
            /// Ought to remove this, or make an ROIClass out of the file. Not used for now
            string[] instructions = File.ReadAllLines(file);
            string color = instructions[0];
            string[] color_values = color.Split('\\');
            color_values[0] = color_values[0].PadLeft(3);
            color_values[1] = color_values[1].PadLeft(3);
            color_values[2] = color_values[2].PadLeft(3);
            int num_zeros = 0;
            foreach (string c_val in color_values)
            {
                if (c_val == "  0")
                {
                    num_zeros++;
                }
            }
            color = $"RGB{color_values[0]}{color_values[1]}{color_values[2]}";
            if (num_zeros == 2)
            {
                if (color_values[0] == "255")
                {
                    color = "Segment - Red";
                }
                if (color_values[1] == "255")
                {
                    color = "Segment - Green";
                }
                if (color_values[2] == "255")
                {
                    color = "Segment - Blue";
                }
            }


            string[] code_values = instructions[1].Split('\\');

            XElement new_structure = new XElement("Structure");
            new_structure.SetAttributeValue("ID", code_values[0]);
            new_structure.SetAttributeValue("Name", roi_name);

            XElement Identification = new XElement("Identification");
            Identification.Add(new XElement("VolumeID"));
            Identification.Add(new XElement("VolumeCode"));
            XElement VolumeType = new XElement("VolumeType");
            VolumeType.Value = instructions[2];
            Identification.Add(VolumeType);
            Identification.Add(new XElement("VolumeCodeTable"));

            XElement StructureCode = new XElement("StructureCode");
            StructureCode.SetAttributeValue("Code", code_values[1]);
            StructureCode.SetAttributeValue("CodeScheme", code_values[2]);
            StructureCode.SetAttributeValue("CodeSchemeVersion", "3.2");
            Identification.Add(StructureCode);
            new_structure.Add(Identification);

            XElement TypeIndex = new XElement("TypeIndex");
            TypeIndex.Value = "2";
            new_structure.Add(TypeIndex);

            XElement ColorAndStyle = new XElement("ColorAndStyle");
            ColorAndStyle.Value = color;
            new_structure.Add(ColorAndStyle);

            XElement SearchCTLow = new XElement("SearchCTLow");
            SearchCTLow.SetAttributeValue(ab + "nil", "true");
            new_structure.Add(SearchCTLow);

            XElement SearchCTHigh = new XElement("SearchCTHigh");
            SearchCTHigh.SetAttributeValue(ab + "nil", "true");
            new_structure.Add(SearchCTHigh);

            XElement DVHLineStyle = new XElement("DVHLineStyle");
            DVHLineStyle.Value = "0";
            new_structure.Add(DVHLineStyle);

            XElement DVHLineColor = new XElement("DVHLineColor");
            string DVH_Color = (Int32.Parse(color_values[0]) + Int32.Parse(color_values[1]) * 256 + Int32.Parse(color_values[2]) * 256 * 256).ToString();
            DVHLineColor.Value = DVH_Color;
            new_structure.Add(DVHLineColor);

            XElement DVHLineWidth = new XElement("DVHLineWidth");
            DVHLineWidth.Value = "1";
            new_structure.Add(DVHLineWidth);

            XElement EUDAlpha = new XElement("EUDAlpha");
            EUDAlpha.SetAttributeValue(ab + "nil", "true");
            new_structure.Add(EUDAlpha);

            XElement TCPAlpha = new XElement("TCPAlpha");
            TCPAlpha.SetAttributeValue(ab + "nil", "true");
            new_structure.Add(TCPAlpha);

            XElement TCPBeta = new XElement("TCPBeta");
            TCPBeta.SetAttributeValue(ab + "nil", "true");
            new_structure.Add(TCPBeta);

            XElement TCPGamma = new XElement("TCPGamma");
            TCPGamma.SetAttributeValue(ab + "nil", "true");
            new_structure.Add(TCPGamma);

            base_struct.Add(new_structure);
        }
    }
}
