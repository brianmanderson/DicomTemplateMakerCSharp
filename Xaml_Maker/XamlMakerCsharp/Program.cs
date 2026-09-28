using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;

// Usage:
//   XamlMakerCsharp import <folder-of-xml-files> <template-folder>   Varian XML -> template folders
//   XamlMakerCsharp export <template-folder> <output-folder>          template folders -> Varian XML
if (args.Length != 3 || (args[0] != "import" && args[0] != "export"))
{
    Console.Error.WriteLine("Usage: XamlMakerCsharp import <xml-folder> <template-folder> | export <template-folder> <output-folder>");
    return 2;
}

string source = args[1];
string target = args[2];
if (!Directory.Exists(source))
{
    Console.Error.WriteLine($"Folder not found: {source}");
    return 2;
}

Directory.CreateDirectory(target);
int count = 0;
if (args[0] == "import")
{
    foreach (string file in Directory.GetFiles(source, "*.xml"))
    {
        new VarianXmlReader(file).XmlToROI(target);
        count++;
    }
}
else
{
    List<OntologyCodeClass> ontologies = OntologyTools.LoadOntologiesFromFolder(Path.Combine(source, "Ontologies"));
    foreach (string directory in Directory.GetDirectories(source))
    {
        if (!ROIClassTools.IsValidTemplateFolder(directory))
        {
            continue;
        }

        var writer = new VarianXmlWriter();
        writer.LoadROIsFromPath(directory, ontologies);
        writer.SaveFile(Path.Combine(target, Path.GetFileName(directory) + ".xml"));
        count++;
    }
}

Console.WriteLine($"{(args[0] == "import" ? "Imported" : "Exported")} {count} template(s).");
return 0;
