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
            XElement base_struct = root.Element("Structures");
            XElement Structure = new XElement(base_struct.Element("Structure"));
            Structure.SetAttributeValue("ID", "Test");
            Structure.SetAttributeValue("Name", "Test");

            XElement Identification = Structure.Element("Identification");
            Identification.SetAttributeValue("VolumeType", "Test");
            base_struct.Add(Structure);
            int x = 1;
        }
    }
}
