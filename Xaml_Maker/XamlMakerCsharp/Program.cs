using System;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.IO;

namespace XamlMakerCsharp
{
    class Program
    {
        static void Main(string[] args)
        {
            XDocument doc = XDocument.Load(Path.Combine(@".", "Structure Template.xml"));
            XElement root = doc.Root;
            root.SetAttributeValue("ID", "Test");
            XElement base_struct = root.Element("Structures");
            XElement Structure = new XElement(base_struct.Element("Structure"));
            base_struct.AddAfterSelf(Structure);
            Structure.SetAttributeValue("ID", "Test");
            Structure.SetAttributeValue("Name", "Test");

            XElement Identification = Structure.Element("Identification");
            XElement VolumeType = Identification.Element("VolumeType");
            VolumeType.Value = "PTV";

            XElement StructureCode = Identification.Element("StructureCode");
            StructureCode.SetAttributeValue("Code", "123");
            StructureCode.SetAttributeValue("CodeScheme", "FMA");
            StructureCode.SetAttributeValue("CodeSchemeVersion", "3.2");

            Structure.SetElementValue("TypeIndex", "3");

            Structure.Element("ColorAndStyle").Value = "Red";

            XElement SearchCTLow = Structure.Element("SearchCTLow");
            SearchCTLow.FirstAttribute.Value = "true";

            XElement SearchCTHigh = Structure.Element("SearchCTHigh");
            SearchCTHigh.FirstAttribute.Value = "true";

            Structure.SetElementValue("DVHLineStyle", "0");
            Structure.SetElementValue("DVHLineColor", "-16777216");
            Structure.SetElementValue("DVHLineWidth", "1");

            XElement EUDAlpha = Structure.Element("EUDAlpha");
            EUDAlpha.FirstAttribute.Value = "true";

            XElement TCPAlpha = Structure.Element("TCPAlpha");
            TCPAlpha.FirstAttribute.Value = "true";

            XElement TCPBeta = Structure.Element("TCPBeta");
            TCPBeta.FirstAttribute.Value = "true";

            XElement TCPGamma = Structure.Element("TCPGamma");
            TCPGamma.FirstAttribute.Value = "true";

            //base_struct.Element("Structure").AddAfterSelf(Structure);
            XmlWriterSettings settings = new XmlWriterSettings();
            XmlWriter writer = XmlWriter.Create("test.xml");
            base_struct.WriteTo(writer);
            writer.Close();
            int x = 1;
        }
    }
}
