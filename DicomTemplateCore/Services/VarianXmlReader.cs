using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DicomTemplateMakerGUI.DicomTemplateServices;
using ROIOntologyClass;


namespace DicomTemplateMakerGUI.Services
{
    /// <summary>What happened to one structure in a Varian XML import or export.</summary>
    public sealed class VarianStructureResult
    {
        public VarianStructureResult(string structure, string? skipReason, string? note = null)
        {
            Structure = structure;
            SkipReason = skipReason;
            Note = note;
        }

        /// <summary>The structure (ROI) ID, or a description of where it is when it has none.</summary>
        public string Structure { get; }

        /// <summary>Why the structure was left out; null when it was imported or exported.</summary>
        public string? SkipReason { get; }

        /// <summary>
        /// Something to know about a structure that was imported, e.g. that it has no structure code, so generated RTs
        /// leave it out; null when there is nothing.
        /// </summary>
        public string? Note { get; }

        public bool Skipped => SkipReason != null;
    }

    /// <summary>
    /// The outcome of importing one Varian XML file into a template folder, or of exporting one template folder
    /// to Varian XML.
    /// </summary>
    public sealed class VarianXmlReport
    {
        public VarianXmlReport(string source)
        {
            Source = source;
        }

        /// <summary>The XML file (import) or template folder (export).</summary>
        public string Source { get; }

        /// <summary>The template folder that was written (import only); null when nothing was written.</summary>
        public string? Target { get; internal set; }

        /// <summary>One entry per structure, in file order.</summary>
        public List<VarianStructureResult> Structures { get; } = new List<VarianStructureResult>();

        /// <summary>Why the whole file failed; null when it did not.</summary>
        public string? Error { get; internal set; }

        /// <summary>True when the file failed as a whole (nothing, or not everything, was written).</summary>
        public bool Failed => Error != null;

        public int SucceededCount => Structures.Count(s => !s.Skipped);

        public IEnumerable<VarianStructureResult> SkippedStructures => Structures.Where(s => s.Skipped);

        /// <summary>The structures that were imported with a <see cref="VarianStructureResult.Note"/>.</summary>
        public IEnumerable<VarianStructureResult> NotedStructures => Structures.Where(s => !s.Skipped && s.Note != null);

        /// <summary>A summary line followed by one line per skipped structure and per note, for logs and messages.</summary>
        public IEnumerable<string> Describe()
        {
            int skipped = Structures.Count - SucceededCount;
            if (Failed)
            {
                yield return $"{Source}: FAILED: {Error}";
            }
            else
            {
                string target = Target != null ? $" -> {Target}" : string.Empty;
                yield return $"{Source}{target}: {SucceededCount} structure(s) done, {skipped} skipped.";
            }
            foreach (VarianStructureResult result in SkippedStructures)
            {
                yield return $"  skipped '{result.Structure}': {result.SkipReason}";
            }
            foreach (VarianStructureResult result in NotedStructures)
            {
                yield return $"  note '{result.Structure}': {result.Note}";
            }
        }
    }

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
        /// <summary>
        /// Adds structure <paramref name="s"/> to <paramref name="maker"/>, or leaves it out (a structure without an
        /// ID, or one that cannot be read) and says why. A structure without a complete structure code is added (a
        /// Varian export gives it back as it was) with a note that generated RTs leave it out.
        /// </summary>
        public VarianStructureResult AddToTemplateMaker(TemplateMaker maker, XElement s)
        {
            string label = StructureLabel(s);
            try
            {
                string roi_id = RequiredAttribute(s, "ID");
                string roi_name = RequiredAttribute(s, "Name");
                if (roi_id == "")
                {
                    return new VarianStructureResult(label, "the structure has an empty ID.");
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
                string? missing = RtStructureBuilder.MissingCodeParts(ontology);
                string? note = missing == null
                    ? null
                    : StructureCode == null
                        ? "it has no structure code, so generated RTs leave it out until a code is chosen for it."
                        : $"its structure code has no {missing}, so generated RTs leave it out until a complete code is chosen for it.";
                return new VarianStructureResult(roi_id, null, note);
            }
            catch (Exception ex)
            {
                // Any structure that cannot be read is left out, as before; now the reason is reported.
                return new VarianStructureResult(label, ex.Message);
            }
        }
        /// <summary>The structure's ID, else its Name, else its position, for the import report.</summary>
        private static string StructureLabel(XElement s)
        {
            string? id = s.Attribute("ID")?.Value;
            if (!string.IsNullOrEmpty(id))
            {
                return id;
            }
            string? name = s.Attribute("Name")?.Value;
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
            return $"structure #{s.ElementsBeforeSelf().Count() + 1} (no ID)";
        }
        /// <summary>
        /// Imports this file as the template named by its Preview ID, in a folder of <paramref name="output_path"/>,
        /// and adds its ontologies to the library in output_path/Ontologies (existing library entries are kept).
        /// <para>Structures that cannot be read are skipped and listed in the report. When no structure can be
        /// imported nothing is written and the report's Error says so. Throws XmlException when the document has no
        /// Preview ID or Structures element, and <see cref="TemplateLoadException"/> (nothing written) when the
        /// existing template of that name or the ontology library cannot be read. <see cref="Import"/> turns these
        /// into a failed report.</para>
        /// </summary>
        public VarianXmlReport XmlToROI(string output_path)
        {
            //string description = preview.Elements("ID");
            if (preview == null)
            {
                throw new XmlException($"{xml_path} has no <Preview> element.");
            }
            string preview_id = RequiredAttribute(preview, "ID");
            if (string.IsNullOrWhiteSpace(preview_id))
            {
                // Would otherwise write All_ROIs.json into output_path itself.
                throw new XmlException($"{xml_path}: the <Preview> ID is empty, so the template has no name.");
            }
            string template_name = preview_id.Replace(' ', '_');
            TemplateMaker templateMaker = new TemplateMaker();
            string template_folder = Path.Combine(output_path, template_name);
            templateMaker.define_output(template_folder);
            templateMaker.set_onto_path(Path.Combine(output_path, "Ontologies"));
            XElement structures = RequiredElement(root, "Structures");
            VarianXmlReport report = new VarianXmlReport(xml_path);
            foreach (XElement s in structures.Elements())
            {
                report.Structures.Add(AddToTemplateMaker(templateMaker, s));
            }
            if (templateMaker.ROIs.Count == 0)
            {
                report.Error = report.Structures.Count == 0
                    ? "the file has no structures; nothing was written."
                    : $"none of its {report.Structures.Count} structure(s) could be imported; nothing was written.";
                return report;
            }
            // make_template also adds the template's ontologies to the library (a merge, not a replacement).
            templateMaker.make_template();
            report.Target = template_folder;
            return report;
        }
        /// <summary>
        /// Imports <paramref name="xml_path"/> into <paramref name="output_path"/> (see <see cref="XmlToROI"/>)
        /// without throwing for a file that cannot be read or imported: the report's Error says why.
        /// </summary>
        public static VarianXmlReport Import(string xml_path, string output_path)
        {
            try
            {
                return new VarianXmlReader(xml_path).XmlToROI(output_path);
            }
            catch (Exception ex) when (ex is XmlException || ex is IOException || ex is UnauthorizedAccessException || ex is TemplateLoadException)
            {
                VarianXmlReport report = new VarianXmlReport(xml_path);
                report.Error = ex.Message;
                return report;
            }
        }
    }
}
