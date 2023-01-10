using System;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using System.Runtime;

namespace XamlMakerCsharp
{
    class Program
    {
        static void Main(string[] args)
        {
            XNamespace ab = "http://www.w3.org/2001/XMLSchema-instance";

            XDocument doc = XDocument.Load(Path.Combine(@".", "Structure Template.xml"));
            XElement root = doc.Root;
            XElement preview = root.Element("Preview");
            preview.SetAttributeValue("AssignedUsers", "b5anderson");
            preview.SetAttributeValue("ID", "Test TemplateBMA");
            DateTime now = DateTime.Now;
            preview.SetAttributeValue("ApprovalHistory", $"b5anderson Created [ {now.Month} {now.Day} {now.Year} {now.Hour}:{now.Minute}:{now.Second}]");
            preview.SetAttributeValue("Description", "Auto-generated xml file");
            preview.SetAttributeValue("LastModified", $"[ {now.Month} {now.Day} {now.Year} {now.Hour}:{now.Minute}:{now.Second}]");

            XElement base_struct = root.Element("Structures");
            base_struct.RemoveAll();
            XElement new_structure = new XElement("Structure");
            new_structure.SetAttributeValue("ID", "Test");
            new_structure.SetAttributeValue("Name", "Test");

            XElement Identification = new XElement("Identification");
            Identification.Add(new XElement("VolumeID"));
            Identification.Add(new XElement("VolumeCode"));
            XElement VolumeType = new XElement("VolumeType");
            VolumeType.Value = "PTV";
            Identification.Add(VolumeType);
            Identification.Add(new XElement("VolumeCodeTable"));

            XElement StructureCode = new XElement("StructureCode");
            StructureCode.SetAttributeValue("Code", "123");
            StructureCode.SetAttributeValue("CodeScheme", "FMA");
            StructureCode.SetAttributeValue("CodeSchemeVersion", "3.2");
            Identification.Add(StructureCode);
            new_structure.Add(Identification);

            XElement TypeIndex = new XElement("TypeIndex");
            TypeIndex.Value = "3";
            new_structure.Add(TypeIndex);

            XElement ColorAndStyle = new XElement("ColorAndStyle");
            ColorAndStyle.Value = "Blue";
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
            XmlWriter writer = XmlWriter.Create(@"K:\test.xml");
            doc.WriteTo(writer);
            writer.Close();
            int x = 1;
        }
    }
}
