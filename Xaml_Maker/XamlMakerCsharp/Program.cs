using DicomTemplateMakerGUI.Services;
using ROIOntologyClass;

// Usage:
//   XamlMakerCsharp import <folder-of-xml-files> <template-folder>   Varian XML -> template folders
//   XamlMakerCsharp export <template-folder> <output-folder>          template folders -> Varian XML
// Prints a report per file, with every structure that was skipped and why. Exit code: 0 when every file was
// converted (some structures may have been skipped), 1 when at least one file failed completely, 2 on usage errors.
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
int converted = 0;
int failed = 0;
int skipped = 0;
if (args[0] == "import")
{
    foreach (string file in Directory.GetFiles(source, "*.xml"))
    {
        Record(VarianXmlReader.Import(file, target));
    }
}
else
{
    List<OntologyCodeClass> ontologies;
    try
    {
        ontologies = OntologyTools.LoadOntologiesFromFolder(Path.Combine(source, "Ontologies"));
    }
    catch (TemplateLoadException ex)
    {
        Console.Error.WriteLine($"FAILED: {ex.Message}");
        return 1;
    }
    foreach (string directory in Directory.GetDirectories(source))
    {
        if (!ROIClassTools.IsValidTemplateFolder(directory))
        {
            continue;
        }

        try
        {
            var writer = new VarianXmlWriter();
            VarianXmlReport report = writer.LoadROIsFromPath(directory, ontologies);
            writer.SaveFile(Path.Combine(target, Path.GetFileName(directory) + ".xml"));
            Record(report);
        }
        catch (TemplateLoadException ex)
        {
            Console.Error.WriteLine($"{directory}: FAILED: {ex.Message}");
            failed++;
        }
    }
}

Console.WriteLine($"{(args[0] == "import" ? "Imported" : "Exported")} {converted} template(s), {failed} failed, {skipped} structure(s) skipped.");
return failed == 0 ? 0 : 1;

void Record(VarianXmlReport report)
{
    TextWriter output = report.Failed ? Console.Error : Console.Out;
    foreach (string line in report.Describe())
    {
        output.WriteLine(line);
    }
    skipped += report.SkippedStructures.Count();
    if (report.Failed)
    {
        failed++;
    }
    else
    {
        converted++;
    }
}
