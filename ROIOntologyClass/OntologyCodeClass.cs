using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ROIOntologyClass
{
    public class OntologyTools
    {
        /// <summary>
        /// Saves ontologies to JSON format. Also cleans up legacy text files if they exist.
        /// </summary>
        public static void SaveOntologiesToFolder(List<OntologyCodeClass> ontologies, string filePath)
        {
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }
            string json = JsonConvert.SerializeObject(ontologies, Formatting.Indented);
            File.WriteAllText(Path.Combine(filePath, "All_Ontologies.json"), json);

            // Clean up legacy text files after successful JSON save
            CleanupLegacyTextFiles(filePath);
        }

        /// <summary>
        /// Loads ontologies from JSON format. Falls back to legacy text files if JSON doesn't exist.
        /// If loaded from legacy format, automatically migrates to JSON.
        /// </summary>
        public static List<OntologyCodeClass> LoadOntologiesFromFolder(string filePath)
        {
            if (!Directory.Exists(filePath))
            {
                return new List<OntologyCodeClass>();
            }

            string jsonFile = Path.Combine(filePath, "All_Ontologies.json");

            // Try to load from JSON first
            if (File.Exists(jsonFile))
            {
                try
                {
                    string json = File.ReadAllText(jsonFile);
                    List<OntologyCodeClass>? ontologies = JsonConvert.DeserializeObject<List<OntologyCodeClass>>(json);
                    return ontologies ?? new List<OntologyCodeClass>();
                }
                catch
                {
                    // If JSON parsing fails, try legacy format
                }
            }

            // Fall back to loading from legacy text files
            List<OntologyCodeClass> legacyOntologies = LoadFromLegacyTextFiles(filePath);

            // If we loaded from legacy format, migrate to JSON
            if (legacyOntologies.Count > 0)
            {
                SaveOntologiesToFolder(legacyOntologies, filePath);
            }

            return legacyOntologies;
        }

        /// <summary>
        /// Loads ontologies from legacy individual text files format.
        /// </summary>
        private static List<OntologyCodeClass> LoadFromLegacyTextFiles(string filePath)
        {
            List<OntologyCodeClass> ontologies = new List<OntologyCodeClass>();

            if (!Directory.Exists(filePath))
            {
                return ontologies;
            }

            foreach (string file in Directory.GetFiles(filePath, "*.txt"))
            {
                try
                {
                    OntologyCodeClass? onto = LoadOntologyFromTextFile(file);
                    if (onto != null && !ontologies.Any(o => o.CodeValue == onto.CodeValue))
                    {
                        ontologies.Add(onto);
                    }
                }
                catch
                {
                    // Skip files that can't be parsed
                    continue;
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
            string[] instructions = File.ReadAllLines(ontologyFile);

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
        /// Removes legacy text files after migration to JSON.
        /// </summary>
        private static void CleanupLegacyTextFiles(string filePath)
        {
            if (!Directory.Exists(filePath))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(filePath, "*.txt"))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore deletion failures
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
