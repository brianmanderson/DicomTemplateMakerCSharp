using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.IO;

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
        public VarianXmlWriter()
        {
            doc = XDocument.Load(Path.Combine(@".", "Structure Template.xml"));
            root = doc.Root;
            XElement preview = root.Element("Preview");
            string userName = Environment.UserName;
            preview.SetAttributeValue("AssignedUsers", userName);

            DateTime now = DateTime.Now;
            preview.SetAttributeValue("ApprovalHistory", $"{userName} Created [ {now.Month} {now.Day} {now.Year} {now.Hour}:{now.Minute}:{now.Second}]");
            preview.SetAttributeValue("Description", "Auto-generated xml file");
            preview.SetAttributeValue("LastModified", $"[ {now.Month} {now.Day} {now.Year} {now.Hour}:{now.Minute}:{now.Second}]");

            base_struct = root.Element("Structures");
            base_struct.RemoveAll();// Remove all previous structures here and make new ones
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
            AddROI(ROIID: code_values[0], ROIName: roi_name, volumeType: instructions[2], code: code_values[1], codeScheme: code_values[2], colorAndStyle: color);
        }
        public void AddROI(string ROIID, string ROIName, string volumeType, string code, string codeScheme, string colorAndStyle)
        {
            XElement new_structure = new XElement("Structure");
            new_structure.SetAttributeValue("ID", ROIID);
            new_structure.SetAttributeValue("Name", ROIName);

            XElement Identification = new XElement("Identification");
            Identification.Add(new XElement("VolumeID"));
            Identification.Add(new XElement("VolumeCode"));
            XElement VolumeType = new XElement("VolumeType");
            VolumeType.Value = volumeType;
            Identification.Add(VolumeType);
            Identification.Add(new XElement("VolumeCodeTable"));

            XElement StructureCode = new XElement("StructureCode");
            StructureCode.SetAttributeValue("Code", code);
            StructureCode.SetAttributeValue("CodeScheme", codeScheme);
            StructureCode.SetAttributeValue("CodeSchemeVersion", "3.2");
            Identification.Add(StructureCode);
            new_structure.Add(Identification);

            XElement TypeIndex = new XElement("TypeIndex");
            TypeIndex.Value = "2";
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
            DVHLineStyle.Value = "0";
            new_structure.Add(DVHLineStyle);

            XElement DVHLineColor = new XElement("DVHLineColor");
            DVHLineColor.Value = "-16777216";
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
        public void AddROI(string file)
        {
            string roi_name = Path.GetFileNameWithoutExtension(file);
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
            DVHLineColor.Value = "-16777216";
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
