using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using FellowOakDicom;
using DicomTemplateMakerGUI.DicomTemplateServices;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.Services
{
    public class TemplateMaker : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChangedEventHandler? handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }
        private string? template_name;
        public string? TemplateName
        {
            get { return template_name; }
            set
            {
                template_name = value;
                OnPropertyChanged("TemplateName");
            }
        }
        public string? path;
        public string? onto_path;
        public string? color, interperter;
        public bool is_template;
        public List<ROIClass> ROIs;
        public List<OntologyCodeClass> Ontologies;
        public List<string> Paths;
        public Dictionary<string, List<string>> DicomTags = new Dictionary<string, List<string>>();
        public string? output;
        public Dictionary<int, string>? color_dict, name_dict;
        public Dictionary<int, string?>? interp_dict, code_meaning_dict, code_value_dict,
            coding_scheme_designator_dict, context_group_version_dict, context_identifier_dict, context_uid_dict, mapping_resource_dict,
            mapping_resource_name_dict, mapping_resourceUID_dict;
        DicomFile? RT_file;
        /// <summary>
        /// Legacy files that categorize_folder could not migrate; they were kept on disk. One line per file.
        /// </summary>
        public List<string> LoadWarnings { get; } = new List<string>();
        /// <summary>
        /// ROIs of the RT that the last interpret_RT call could not read, with the reason. One line per ROI.
        /// </summary>
        public List<string> SkippedRois { get; } = new List<string>();
        private const string PathsFileName = "Paths.txt";
        private const string DicomTagsFileName = "DicomTags.txt";
        // The folder whose Paths.txt and DicomTags.txt Paths and DicomTags came from or were last saved to. There,
        // make_template writes them as they are, even when empty, so that the user can clear them.
        private string? settings_folder;
        // Set when categorize_folder could not read the template in load_failure_folder; make_template refuses to
        // write there.
        private TemplateLoadException? load_failure;
        private string? load_failure_folder;
        public TemplateMaker()
        {
            ROIs = new List<ROIClass>();
            Ontologies = new List<OntologyCodeClass>();
            Paths = new List<string>();
        }
        public void set_onto_path(string onto_path)
        {
            this.onto_path = onto_path;
        }
        /// <summary>
        /// Returns <paramref name="folder"/>, or throws when <paramref name="setter"/> (the method that sets it)
        /// has not been called. Passing the null folder on to the file APIs used to fail at the same point.
        /// </summary>
        private static string RequireFolder(string? folder, string setter)
        {
            return folder ?? throw new InvalidOperationException(setter + " has not been called on this TemplateMaker.");
        }
        /// <summary>
        /// Adds the ROIs of the RT Structure Set <paramref name="dicom_file"/> to ROIs (an ROI whose name is already
        /// there is kept as it is) and their codes to Ontologies. A code is shared with the Ontologies entry of the same
        /// meaning, value and scheme, never with an entry that only has the same value (see <see cref="SameCode"/>).
        /// <para>An ROI that cannot be read whole is skipped and listed in <see cref="SkippedRois"/> with the reason:
        /// no name, no display colour, no RT ROI Observations item, or no complete code (Code Value, Coding Scheme
        /// Designator and Code Meaning). The context and mapping attributes of a code are optional.</para>
        /// </summary>
        /// <returns>The number of ROIs read, including those whose name was already in ROIs.</returns>
        public int interpret_RT(string dicom_file)
        {
            color_dict = new Dictionary<int, string>();
            interp_dict = new Dictionary<int, string?>();
            name_dict = new Dictionary<int, string>();
            code_meaning_dict = new Dictionary<int, string?>();
            code_value_dict = new Dictionary<int, string?>();
            coding_scheme_designator_dict = new Dictionary<int, string?>();
            context_group_version_dict = new Dictionary<int, string?>();
            context_identifier_dict = new Dictionary<int, string?>();
            context_uid_dict = new Dictionary<int, string?>();
            mapping_resource_dict = new Dictionary<int, string?>();
            mapping_resource_name_dict = new Dictionary<int, string?>();
            mapping_resourceUID_dict = new Dictionary<int, string?>();
            SkippedRois.Clear();
            RT_file = DicomFile.Open(dicom_file, FileReadOption.ReadAll);
            foreach (DicomDataset rt_contour in Items(RT_file.Dataset, DicomTag.ROIContourSequence))
            {
                if (!rt_contour.TryGetSingleValue(DicomTag.ReferencedROINumber, out int roi_number))
                {
                    SkippedRois.Add("An ROI Contour item has no Referenced ROI Number, so its colour belongs to no ROI.");
                    continue;
                }
                string? color = Optional(rt_contour, DicomTag.ROIDisplayColor);
                if (color != null)
                {
                    color_dict.Add(roi_number, color);
                }
            }
            foreach (DicomDataset rt_observation in Items(RT_file.Dataset, DicomTag.RTROIObservationsSequence))
            {
                if (!rt_observation.TryGetSingleValue(DicomTag.ReferencedROINumber, out int ref_number))
                {
                    SkippedRois.Add("An RT ROI Observations item has no Referenced ROI Number, so its type and code belong to no ROI.");
                    continue;
                }
                interp_dict.Add(ref_number, Optional(rt_observation, DicomTag.RTROIInterpretedType));
                DicomDataset? rt_ident = Items(rt_observation, DicomTag.RTROIIdentificationCodeSequence).FirstOrDefault();
                if (rt_ident != null)
                {
                    code_meaning_dict.Add(ref_number, Optional(rt_ident, DicomTag.CodeMeaning));
                    code_value_dict.Add(ref_number, Optional(rt_ident, DicomTag.CodeValue));
                    coding_scheme_designator_dict.Add(ref_number, Optional(rt_ident, DicomTag.CodingSchemeDesignator));
                    context_group_version_dict.Add(ref_number, Optional(rt_ident, DicomTag.ContextGroupVersion));
                    context_identifier_dict.Add(ref_number, Optional(rt_ident, DicomTag.ContextIdentifier));
                    context_uid_dict.Add(ref_number, Optional(rt_ident, DicomTag.ContextUID));
                    mapping_resource_dict.Add(ref_number, Optional(rt_ident, DicomTag.MappingResource));
                    mapping_resource_name_dict.Add(ref_number, Optional(rt_ident, DicomTag.MappingResourceName));
                    mapping_resourceUID_dict.Add(ref_number, Optional(rt_ident, DicomTag.MappingResourceUID));
                }
            }
            foreach (DicomDataset rt_struct in Items(RT_file.Dataset, DicomTag.StructureSetROISequence))
            {
                if (!rt_struct.TryGetSingleValue(DicomTag.ROINumber, out int ref_number))
                {
                    SkippedRois.Add((Optional(rt_struct, DicomTag.ROIName) ?? "An ROI without a name") + ": its Structure Set ROI item has no ROI Number, so it has no colour, type or code.");
                    continue;
                }
                name_dict.Add(ref_number, Optional(rt_struct, DicomTag.ROIName) ?? string.Empty);
            }
            foreach (int key in color_dict.Keys.Concat(interp_dict.Keys).Distinct().Where(key => !name_dict.ContainsKey(key)))
            {
                SkippedRois.Add($"ROI number {key}: it has no Structure Set ROI item, so it has no name.");
            }

            int read = 0;
            foreach (KeyValuePair<int, string> named in name_dict)
            {
                int key = named.Key;
                string name = named.Value;
                string? skipped = WhyNotRead(key, name);
                if (skipped != null)
                {
                    SkippedRois.Add((string.IsNullOrWhiteSpace(name) ? "ROI number " + key : name + " (ROI number " + key + ")") + ": " + skipped);
                    continue;
                }

                string[] colors = color_dict[key].Split('\\');
                OntologyCodeClass code_class = new OntologyCodeClass(code_meaning_dict[key]!, code_value_dict[key], coding_scheme_designator_dict[key], context_group_version_dict[key], mapping_resource_dict[key],
                    context_identifier_dict[key], mapping_resource_name_dict[key], mapping_resourceUID_dict[key], context_uid_dict[key]);
                OntologyCodeClass? known = Ontologies.FirstOrDefault(o => SameCode(o, code_class));
                if (known != null)
                {
                    code_class = known;
                }
                else
                {
                    Ontologies.Add(code_class);
                    Ontologies.Sort((p, q) => p.CodeMeaning.CompareTo(q.CodeMeaning));
                }
                ROIClass new_roi = new ROIClass(byte.Parse(colors[0]), byte.Parse(colors[1]), byte.Parse(colors[2]), name, interp_dict[key], code_class);
                if (!ROIs.Any(p => p.ROIName == new_roi.ROIName))
                {
                    ROIs.Add(new_roi);
                }
                read++;
            }
            return read;
        }

        /// <summary>Why interpret_RT skips ROI number <paramref name="key"/>, or null when it can be read.</summary>
        private string? WhyNotRead(int key, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "it has no name.";
            }
            if (color_dict == null || !color_dict.ContainsKey(key))
            {
                return "it has no ROI Display Color.";
            }
            if (interp_dict == null || !interp_dict.ContainsKey(key))
            {
                return "it has no RT ROI Observations item, so it has no interpreted type or code.";
            }
            if (code_value_dict == null || code_meaning_dict == null || coding_scheme_designator_dict == null || !code_value_dict.ContainsKey(key))
            {
                return "it has no RT ROI Identification Code, and ROIs without a code are not imported.";
            }
            OntologyCodeClass parts = new OntologyCodeClass { CodeMeaning = code_meaning_dict[key] ?? string.Empty, CodeValue = code_value_dict[key], Scheme = coding_scheme_designator_dict[key] };
            string? missing = RtStructureBuilder.MissingCodeParts(parts);
            return missing == null ? null : "its RT ROI Identification Code has no " + missing + ", and ROIs without a complete code are not imported.";
        }

        /// <summary>The items of <paramref name="sequence"/>; none when the sequence is absent.</summary>
        private static IEnumerable<DicomDataset> Items(DicomDataset dataset, DicomTag sequence)
        {
            return dataset.TryGetSequence(sequence, out DicomSequence? items) && items != null ? items.Items : Enumerable.Empty<DicomDataset>();
        }

        /// <summary>The value of <paramref name="tag"/>; null when it is absent.</summary>
        private static string? Optional(DicomDataset dataset, DicomTag tag)
        {
            return dataset.TryGetString(tag, out string? value) ? value : null;
        }

        /// <summary>
        /// True when two codes have the same meaning, value and scheme. Ontologies are matched on all three, never on the
        /// value alone: the TG-263 templates give one code (228793007, planning target volume) to dozens of names, each
        /// with its own meaning, and matching on the value gave all of them the first one's meaning.
        /// </summary>
        private static bool SameCode(OntologyCodeClass a, OntologyCodeClass b)
        {
            return a.CodeMeaning == b.CodeMeaning && a.CodeValue == b.CodeValue && a.Scheme == b.Scheme;
        }

        public void clear_folder()
        {
            string output_folder = RequireFolder(output, nameof(define_output));
            if (File.Exists(Path.Combine(output_folder, "All_ROIs.json")))
            {
                File.Delete(Path.Combine(output_folder, "All_ROIs.json"));
            }
            // Also clean up legacy ROIs folder if it exists
            string roisFolder = Path.Combine(output_folder, "ROIs");
            if (Directory.Exists(roisFolder))
            {
                try
                {
                    Directory.Delete(roisFolder, true);
                }
                catch
                {
                    // Ignore deletion failures
                }
            }
        }
        public void define_output(string output)
        {
            this.output = output;
        }
        /// <summary>
        /// Replaces the ontology library with <see cref="Ontologies"/>: a full save, which drops library entries that
        /// are not in the list. Only for callers that loaded the library into Ontologies first (the ontology scheme
        /// converter); template builds go through <see cref="make_template"/>, which merges into the library.
        /// </summary>
        public void write_ontologies()
        {
            OntologyTools.SaveOntologiesToFolder(Ontologies, RequireFolder(onto_path, nameof(set_onto_path)));
        }

        /// <summary>
        /// Adds a new ontology if it doesn't already exist, and adds it to the library on disk (the rest of the
        /// library is kept, whether or not it was loaded into Ontologies).
        /// </summary>
        public bool AddOntologyIfNew(OntologyCodeClass? newOntology)
        {
            if (newOntology == null || string.IsNullOrEmpty(newOntology.CodeValue))
            {
                return false;
            }

            if (!Ontologies.Any(o => o.CodeValue == newOntology.CodeValue))
            {
                string library_folder = RequireFolder(onto_path, nameof(set_onto_path));
                Ontologies.Add(newOntology);
                Ontologies.Sort((p, q) => p.CodeMeaning.CompareTo(q.CodeMeaning));
                OntologyTools.MergeOntologiesIntoFolder(new[] { newOntology }, library_folder);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes an ontology, and the entry with the same meaning, code and scheme from the library on disk (the
        /// rest of the library is kept, whether or not it was loaded into Ontologies).
        /// </summary>
        public bool RemoveOntology(OntologyCodeClass? ontologyToRemove)
        {
            if (ontologyToRemove == null)
            {
                return false;
            }

            if (Ontologies.Remove(ontologyToRemove))
            {
                string library_folder = RequireFolder(onto_path, nameof(set_onto_path));
                List<OntologyCodeClass> library = OntologyTools.LoadOntologiesFromFolder(library_folder);
                int removed = library.RemoveAll(o => o.CodeMeaning == ontologyToRemove.CodeMeaning
                    && o.CodeValue == ontologyToRemove.CodeValue
                    && o.Scheme == ontologyToRemove.Scheme);
                if (removed > 0)
                {
                    OntologyTools.SaveOntologiesToFolder(library, library_folder);
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// True when <paramref name="folder"/> already holds a template, or template settings, that
        /// <see cref="make_template"/> would replace: All_ROIs.json, legacy ROI files, Paths.txt or DicomTags.txt.
        /// Meant for an "overwrite the existing template?" confirmation before building into a folder.
        /// </summary>
        public static bool TemplateExists(string folder)
        {
            return Directory.Exists(folder)
                && (ROIClassTools.IsValidTemplateFolder(folder)
                    || File.Exists(Path.Combine(folder, PathsFileName))
                    || File.Exists(Path.Combine(folder, DicomTagsFileName)));
        }

        /// <summary>
        /// Saves this template in the output folder: All_ROIs.json from ROIs, Paths.txt from Paths and DicomTags.txt
        /// from DicomTags, and adds the ROIs' ontologies to the library in onto_path (when set) without removing
        /// anything from it.
        /// <para>Nothing is written and <see cref="TemplateLoadException"/> is thrown when the folder's All_ROIs.json
        /// exists but cannot be read, when categorize_folder could not read this folder, or when the ontology library
        /// cannot be read. A damaged file is never replaced; the user repairs or removes it.</para>
        /// <para>Paths and DicomTags are written as they are when they were read from, or last saved to, this folder,
        /// so clearing them clears the files. Otherwise an empty Paths or DicomTags does not replace an existing
        /// Paths.txt or DicomTags.txt: the file is kept and read into Paths or DicomTags. That keeps the monitored
        /// folders and DICOM requirements of a template that is rebuilt from an online source, a Varian XML file or
        /// an RT file.</para>
        /// </summary>
        public void make_template()
        {
            string output_folder = RequireFolder(output, nameof(define_output));
            RefuseToReplaceUnreadableTemplate(output_folder);

            // Update ontologies with any new ontologies from ROIs
            foreach (ROIClass roi in ROIs)
            {
                OntologyCodeClass? code_class = roi.Ontology_Class;
                if (code_class != null && !string.IsNullOrEmpty(code_class.CodeValue))
                {
                    if (!Ontologies.Any(o => SameCode(o, code_class)))
                    {
                        Ontologies.Add(code_class);
                    }
                }
            }
            Ontologies.Sort((p, q) => p.CodeMeaning.CompareTo(q.CodeMeaning));

            // The shared library only grows here. It is read first, so an unreadable library stops the save before
            // anything else is written.
            if (!string.IsNullOrEmpty(onto_path))
            {
                OntologyTools.MergeOntologiesIntoFolder(ROIs.Select(roi => roi.Ontology_Class), onto_path);
            }

            Directory.CreateDirectory(output_folder);
            ROIClassTools.SaveROIsToFolder(ROIs, output_folder);
            SaveSettings(output_folder);
        }

        /// <summary>Throws when make_template must not write to <paramref name="output_folder"/>; see make_template.</summary>
        private void RefuseToReplaceUnreadableTemplate(string output_folder)
        {
            if (load_failure != null && load_failure_folder != null && SameFolder(load_failure_folder, output_folder))
            {
                throw NotSaved(load_failure);
            }
            string rois_file = Path.Combine(output_folder, ROIClassTools.RoisFileName);
            if (File.Exists(rois_file))
            {
                try
                {
                    ROIClassTools.ReadRoisFile(rois_file);
                }
                catch (TemplateLoadException ex)
                {
                    throw NotSaved(ex);
                }
            }
        }

        private static TemplateLoadException NotSaved(TemplateLoadException cause)
        {
            return new TemplateLoadException(cause.FilePath,
                cause.Problem + " The template was not saved, so that this file is not replaced. Repair or remove it, then save again.", cause);
        }

        /// <summary>Writes, or keeps and reads back, Paths.txt and DicomTags.txt; see make_template.</summary>
        private void SaveSettings(string output_folder)
        {
            bool from_this_folder = settings_folder != null && SameFolder(settings_folder, output_folder);

            string paths_file = Path.Combine(output_folder, PathsFileName);
            if (from_this_folder || Paths.Count > 0 || !File.Exists(paths_file))
            {
                AtomicFile.WriteAllLines(paths_file, Paths);
            }
            else
            {
                Paths.AddRange(ReadPaths(paths_file));
            }

            string tags_file = Path.Combine(output_folder, DicomTagsFileName);
            if (from_this_folder || DicomTags.Count > 0 || !File.Exists(tags_file))
            {
                AtomicFile.WriteAllLines(tags_file, DicomTags.Select(tag => string.Join("\\", new[] { tag.Key }.Concat(tag.Value))));
            }
            else
            {
                foreach (KeyValuePair<string, List<string>> tag in ReadDicomTags(tags_file))
                {
                    DicomTags.Add(tag.Key, tag.Value);
                }
            }

            settings_folder = Path.GetFullPath(output_folder);
        }

        /// <summary>The non-blank lines of a Paths.txt file.</summary>
        private static List<string> ReadPaths(string paths_file)
        {
            return SharedFile.ReadAllLines(paths_file).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        }

        /// <summary>
        /// A DicomTags.txt file: one "key\value\value..." line per tag. Values of repeated lines for one key are joined
        /// in file order, as the RT generator reads them (TemplateRequirements.Parse), so a save never drops a line.
        /// </summary>
        private static Dictionary<string, List<string>> ReadDicomTags(string tags_file)
        {
            Dictionary<string, List<string>> tags = new Dictionary<string, List<string>>();
            foreach (string line in SharedFile.ReadAllLines(tags_file))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                string[] key_values = line.Split('\\');
                string key = key_values[0];
                List<string> values = key_values.Skip(1).ToList();
                if (tags.TryGetValue(key, out List<string>? existing))
                {
                    existing.AddRange(values);
                }
                else
                {
                    tags.Add(key, values);
                }
            }
            return tags;
        }

        private static bool SameFolder(string a, string b)
        {
            StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), comparison);
        }

        public void define_path(string path)
        {
            this.path = path;
        }

        /// <summary>
        /// Reads the template in the folder set by define_path: Paths, DicomTags and, when the folder holds a
        /// template, ROIs (is_template is then true). Ontologies found in the ROIs are added to Ontologies and to
        /// the library in onto_path. Legacy files that could not be migrated are listed in
        /// <see cref="LoadWarnings"/>.
        /// <para>Throws <see cref="TemplateLoadException"/> when the template cannot be read; is_template is then
        /// false and make_template refuses to write to this folder.</para>
        /// </summary>
        public void categorize_folder()
        {
            ROIs = new List<ROIClass>();
            Paths = new List<string>();
            DicomTags = new Dictionary<string, List<string>>();
            LoadWarnings.Clear();
            load_failure = null;
            load_failure_folder = null;
            OntologyCodeClass? code_class;
            is_template = false;
            string template_folder = RequireFolder(path, nameof(define_path));

            // Load paths
            if (File.Exists(Path.Combine(template_folder, PathsFileName)))
            {
                Paths = ReadPaths(Path.Combine(template_folder, PathsFileName));
            }

            // Load DICOM tags
            if (File.Exists(Path.Combine(template_folder, DicomTagsFileName)))
            {
                DicomTags = ReadDicomTags(Path.Combine(template_folder, DicomTagsFileName));
            }
            settings_folder = Path.GetFullPath(template_folder);

            // Check if this is a valid template folder (supports both JSON and legacy formats)
            if (ROIClassTools.IsValidTemplateFolder(template_folder))
            {
                // Load ROIs (handles both JSON and legacy formats automatically)
                List<ROIClass> rois;
                try
                {
                    rois = ROIClassTools.LoadROIsFromFolder(template_folder, Ontologies, LoadWarnings);
                }
                catch (TemplateLoadException ex)
                {
                    load_failure = ex;
                    load_failure_folder = Path.GetFullPath(template_folder);
                    throw;
                }
                is_template = true;
                TemplateName = Path.GetFileName(template_folder);
                ROIs = rois;

                // Process ROIs and update ontologies
                foreach (ROIClass roi in ROIs)
                {
                    code_class = roi.Ontology_Class;
                    if (code_class == null)
                    {
                        continue;
                    }

                    // The entry with the same meaning, value and scheme; see SameCode.
                    bool contains_code_class = false;
                    foreach (OntologyCodeClass o in Ontologies)
                    {
                        if (SameCode(o, code_class))
                        {
                            contains_code_class = true;
                            roi.Ontology_Class = o;
                            break;
                        }
                    }
                    if (!contains_code_class && !string.IsNullOrEmpty(code_class.CodeValue))
                    {
                        Ontologies.Add(code_class);
                    }
                }

                // Sort, and add this template's ontologies to the library (which is written only when it grows)
                Ontologies.Sort((p, q) => p.CodeMeaning.CompareTo(q.CodeMeaning));
                if (!string.IsNullOrEmpty(onto_path))
                {
                    OntologyTools.MergeOntologiesIntoFolder(ROIs.Select(roi => roi.Ontology_Class), onto_path);
                }
            }
        }
    }
}
