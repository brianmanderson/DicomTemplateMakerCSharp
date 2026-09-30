using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ROIOntologyClass
{
    public class OntologyTools
    {
        /// <summary>The ontology library file inside the Ontologies folder.</summary>
        public const string LibraryFileName = "All_Ontologies.json";

        /// <summary>
        /// Replaces the ontology library in <paramref name="filePath"/> with <paramref name="ontologies"/>, in one
        /// step (see <see cref="AtomicFile"/>). This is a full save: entries missing from the list are removed from
        /// the library, which is what the ontology editor wants. Code that only adds entries (template builds) must
        /// use <see cref="MergeOntologiesIntoFolder"/>. Legacy text files are left alone: a migration that read every
        /// one of them removes them.
        /// </summary>
        public static void SaveOntologiesToFolder(List<OntologyCodeClass> ontologies, string filePath)
        {
            Directory.CreateDirectory(filePath);
            string json = JsonConvert.SerializeObject(ontologies, Formatting.Indented);
            AtomicFile.WriteAllText(Path.Combine(filePath, LibraryFileName), json);
        }

        /// <summary>
        /// Loads the ontology library; see the overload with warnings. Legacy files that could not be migrated are
        /// not reported by this overload (they are kept on disk).
        /// </summary>
        public static List<OntologyCodeClass> LoadOntologiesFromFolder(string filePath)
        {
            return LoadOntologiesFromFolder(filePath, null);
        }

        /// <summary>
        /// Loads the ontology library in <paramref name="filePath"/> (an empty list when the folder does not exist).
        /// <para>When All_Ontologies.json exists but cannot be read, does not parse or holds no list, this throws
        /// <see cref="TemplateLoadException"/> and writes nothing, so a damaged library is never replaced by a
        /// save of whatever the caller had in memory.</para>
        /// <para>Otherwise the legacy text files are migrated: the entries that parse are saved to
        /// All_Ontologies.json, and the text files are deleted only when every one of them was migrated. Each file
        /// that was not migrated (unreadable, or a code value another file already has) is added to
        /// <paramref name="warnings"/> and all text files are kept. When there are text files but none parse, this
        /// throws <see cref="TemplateLoadException"/> and writes nothing.</para>
        /// </summary>
        public static List<OntologyCodeClass> LoadOntologiesFromFolder(string filePath, ICollection<string>? warnings)
        {
            if (!Directory.Exists(filePath))
            {
                return new List<OntologyCodeClass>();
            }

            string jsonFile = Path.Combine(filePath, LibraryFileName);
            if (File.Exists(jsonFile))
            {
                return ReadLibraryFile(jsonFile);
            }

            // Null when another reader migrated the same files first; its library is complete and is read instead.
            return MigrateLegacyTextFiles(filePath, warnings) ?? ReadLibraryFile(jsonFile);
        }

        /// <summary>
        /// Adds each of <paramref name="ontologies"/> whose CodeValue and Scheme are not in the library yet; entries
        /// already in the library are kept as they are, even when the incoming copy differs (the library is what the
        /// user curates in the ontology editor). Entries without a code value are not added. The library is written
        /// only when something was added or it does not exist yet. Throws <see cref="TemplateLoadException"/>, and
        /// writes nothing, when the library exists but cannot be read.
        /// </summary>
        /// <returns>The number of entries added.</returns>
        public static int MergeOntologiesIntoFolder(IEnumerable<OntologyCodeClass?> ontologies, string filePath)
        {
            List<OntologyCodeClass> library = LoadOntologiesFromFolder(filePath);
            int added = 0;
            foreach (OntologyCodeClass? ontology in ontologies)
            {
                if (ontology == null || string.IsNullOrEmpty(ontology.CodeValue))
                {
                    continue;
                }
                if (library.Any(o => o.CodeValue == ontology.CodeValue && o.Scheme == ontology.Scheme))
                {
                    continue;
                }
                library.Add(ontology);
                added++;
            }
            if (added > 0 || !File.Exists(Path.Combine(filePath, LibraryFileName)))
            {
                library.Sort((p, q) => string.Compare(p.CodeMeaning, q.CodeMeaning));
                SaveOntologiesToFolder(library, filePath);
            }
            return added;
        }

        /// <summary>
        /// Reads and parses All_Ontologies.json without changing anything. Throws <see cref="TemplateLoadException"/>
        /// when the file cannot be read, does not parse, or does not hold a list of ontologies.
        /// </summary>
        private static List<OntologyCodeClass> ReadLibraryFile(string jsonFile)
        {
            List<OntologyCodeClass>? ontologies;
            try
            {
                ontologies = JsonConvert.DeserializeObject<List<OntologyCodeClass>>(SharedFile.ReadAllText(jsonFile));
            }
            catch (Exception ex)
            {
                throw new TemplateLoadException(jsonFile, ex.Message, ex);
            }
            if (ontologies == null)
            {
                throw new TemplateLoadException(jsonFile, "the file is empty or holds null instead of a list of ontologies.");
            }
            // Newtonsoft fills a null array element with null even though the element type is not nullable.
            if (ontologies.Any(o => o == null))
            {
                throw new TemplateLoadException(jsonFile, "the ontology list holds a null entry.");
            }
            return ontologies;
        }

        /// <summary>
        /// Migrates the legacy text files in the Ontologies folder; see
        /// <see cref="LoadOntologiesFromFolder(string, ICollection{string})"/>. Returns null when another reader created
        /// All_Ontologies.json meanwhile: the first finished migration wins and is never replaced by a later, possibly
        /// partial, one.
        /// </summary>
        private static List<OntologyCodeClass>? MigrateLegacyTextFiles(string filePath, ICollection<string>? warnings)
        {
            List<OntologyCodeClass> ontologies = new List<OntologyCodeClass>();
            List<string> migrated = new List<string>();
            List<string> problems = new List<string>();
            string jsonFile = Path.Combine(filePath, LibraryFileName);

            foreach (string file in Directory.GetFiles(filePath, "*.txt"))
            {
                OntologyCodeClass? onto;
                try
                {
                    onto = LoadOntologyFromTextFile(file);
                }
                catch (FileNotFoundException) when (File.Exists(jsonFile))
                {
                    // Deleted by a reader that finished migrating these files (it saves the library before deleting).
                    return null;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    problems.Add($"{file}: {ex.Message}");
                    continue;
                }
                if (onto == null)
                {
                    problems.Add($"{file}: expected a code value line and a coding scheme line.");
                    continue;
                }
                OntologyCodeClass? same_code = ontologies.FirstOrDefault(o => o.CodeValue == onto.CodeValue);
                if (same_code != null)
                {
                    problems.Add($"{file}: code value '{onto.CodeValue}' is already used by '{same_code.CodeMeaning}'.");
                    continue;
                }
                ontologies.Add(onto);
                migrated.Add(file);
            }

            if (File.Exists(jsonFile))
            {
                return null;
            }

            if (ontologies.Count == 0)
            {
                if (problems.Count > 0)
                {
                    throw new TemplateLoadException(filePath, $"none of its {problems.Count} legacy ontology file(s) could be read. First problem: {problems[0]}");
                }
                return ontologies;
            }

            if (!AtomicFile.TryCreateText(jsonFile, JsonConvert.SerializeObject(ontologies, Formatting.Indented)))
            {
                return null;
            }

            if (problems.Count == 0)
            {
                DeleteMigratedLegacyFiles(migrated);
            }
            else if (warnings != null)
            {
                foreach (string problem in problems)
                {
                    warnings.Add($"Legacy ontology file not migrated, so the ontology text files were kept: {problem}");
                }
            }

            return ontologies;
        }

        /// <summary>
        /// Loads a single ontology from a legacy text file.
        /// </summary>
        private static OntologyCodeClass? LoadOntologyFromTextFile(string ontologyFile)
        {
            string codeMeaning = Path.GetFileName(ontologyFile).Replace(".txt", "");
            string[] instructions = SharedFile.ReadAllLines(ontologyFile);

            if (instructions.Length < 2)
            {
                return null;
            }

            OntologyCodeClass onto = new OntologyCodeClass();
            onto.CodeMeaning = codeMeaning;
            onto.CodeValue = instructions[0];
            onto.Scheme = instructions[1];

            // Extended format with additional fields
            if (instructions.Length >= 8)
            {
                onto.ContextGroupVersion = instructions[2];
                onto.MappingResource = instructions[3];
                onto.ContextIdentifier = instructions[4];
                onto.MappingResourceName = instructions[5];
                onto.MappingResourceUID = instructions[6];
                onto.ContextUID = instructions[7];
            }

            return onto;
        }

        /// <summary>
        /// Removes the legacy text files that were migrated to JSON. Only files that were read are deleted, so a
        /// file added meanwhile is kept.
        /// </summary>
        private static void DeleteMigratedLegacyFiles(List<string> migrated)
        {
            foreach (string file in migrated)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // The library is saved as JSON, which takes precedence; a leftover text file loses nothing.
                }
            }
        }

        /// <summary>
        /// Adds an ontology to the list if it doesn't already exist (by CodeValue).
        /// </summary>
        public static bool AddOntologyIfNotExists(List<OntologyCodeClass> ontologies, OntologyCodeClass? newOntology)
        {
            if (newOntology == null || string.IsNullOrEmpty(newOntology.CodeValue))
            {
                return false;
            }

            if (!ontologies.Any(o => o.CodeValue == newOntology.CodeValue))
            {
                ontologies.Add(newOntology);
                ontologies.Sort((p, q) => p.CodeMeaning.CompareTo(q.CodeMeaning));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes an ontology from the list by CodeValue.
        /// </summary>
        public static bool RemoveOntology(List<OntologyCodeClass> ontologies, OntologyCodeClass? ontologyToRemove)
        {
            if (ontologyToRemove == null)
            {
                return false;
            }

            return ontologies.Remove(ontologyToRemove);
        }
    }

    public class OntologyCodeClass
    {
        private string? scheme_designated = "FMA";
        // Newtonsoft writes every property, so files this program saved always hold CodeMeaning; a hand-edited
        // file with "CodeMeaning": null still loads it as null, which this annotation does not model.
        private string code_meaning = "Undefined Normal Tissue";
        private string? code_value = "NormalTissue";
        private string? context_group_version = "20161209";
        private string? mapping_resource = "99VMS";
        private string? context_identifier = "VMS011";
        private string? mapping_resource_name = "Varian Medical Systems";
        private string? mapping_resource_uid = "1.2.246.352.7.1.1";
        private string? context_uid = "1.2.246.352.7.2.11";
        public string? ContextUID
        {
            get { return context_uid; }
            set
            {
                context_uid = value;
                OnPropertyChanged("ContextUID");
            }
        }
        public string? MappingResourceUID
        {
            get { return mapping_resource_uid; }
            set
            {
                mapping_resource_uid = value;
                OnPropertyChanged("MappingResourceUID");
            }
        }
        public string? MappingResourceName
        {
            get { return mapping_resource_name; }
            set
            {
                mapping_resource_name = value;
                OnPropertyChanged("MappingResourceName");
            }
        }
        public string? ContextIdentifier
        {
            get { return context_identifier; }
            set
            {
                context_identifier = value;
                OnPropertyChanged("ContextIdentifier");
            }
        }
        public string? MappingResource
        {
            get { return mapping_resource; }
            set
            {
                mapping_resource = value;
                OnPropertyChanged("MappingResource");
            }
        }
        public string? ContextGroupVersion
        {
            get { return context_group_version; }
            set
            {
                context_group_version = value;
                OnPropertyChanged("ContextGroupVersion");
            }
        }
        public string? Scheme
        {
            get { return scheme_designated; }
            set
            {
                scheme_designated = value;
                OnPropertyChanged("Scheme");
            }
        }
        public string? CodeValue
        {
            get { return code_value; }
            set
            {
                code_value = value;
                OnPropertyChanged("CodeValue");
            }
        }
        public string CodeMeaning
        {
            get { return code_meaning; }
            set
            {
                code_meaning = value;
                OnPropertyChanged("CodeMeaning");
            }
        }
        public static string RemoveIllegalCharacters(string input)
        {
            List<char> illegal_chars = new List<char>() { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
            foreach (char c in illegal_chars)
            {
                input = input.Replace(c.ToString(), "");
            }
            return input;
        }
        public OntologyCodeClass()
        {

        }
        public OntologyCodeClass(string name, string? code_value, string? scheme_designated)
        {
            CodeMeaning = RemoveIllegalCharacters(name);
            CodeValue = code_value;
            Scheme = scheme_designated;
        }
        public OntologyCodeClass(string name, string? code_value, string? scheme_designated, string? group_version, string? mapping_resource,
            string? context_identifier, string? mapping_resource_name, string? mapping_resource_uid, string? context_uid)
        {
            CodeMeaning = RemoveIllegalCharacters(name);
            if (CodeMeaning == "")
            {
                // Callers supply a usable name or a code (TemplateEntryMapper skips entries with neither);
                // with neither, this still throws NullReferenceException as it always has.
                CodeMeaning = RemoveIllegalCharacters(code_value!);
            }
            CodeValue = code_value;
            Scheme = scheme_designated;
            ContextGroupVersion = group_version;
            MappingResource = mapping_resource;
            ContextIdentifier = context_identifier;
            MappingResourceName = mapping_resource_name;
            MappingResourceUID = mapping_resource_uid;
            ContextUID = context_uid;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string info)
        {
            PropertyChangedEventHandler? handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(info));
            }
        }
    }
}
