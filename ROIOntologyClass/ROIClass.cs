using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ROIOntologyClass;

namespace ROIOntologyClass
{
    public class ROIClassTools
    {
        /// <summary>The template file that holds the ROIs.</summary>
        public const string RoisFileName = "All_ROIs.json";
        private const string LegacyFolderName = "ROIs";

        /// <summary>
        /// Saves ROIs to All_ROIs.json, replacing the file in one step (see <see cref="AtomicFile"/>). A legacy ROIs
        /// folder is left alone: <see cref="LoadROIsFromFolder(string, List{OntologyCodeClass}, ICollection{string})"/>
        /// removes it after a migration that read every file, and keeps it otherwise.
        /// </summary>
        public static void SaveROIsToFolder(List<ROIClass> rois, string filePath)
        {
            Directory.CreateDirectory(filePath);
            string json = JsonConvert.SerializeObject(rois, Formatting.Indented);
            AtomicFile.WriteAllText(Path.Combine(filePath, RoisFileName), json);
        }

        /// <summary>
        /// Loads the ROIs of the template in <paramref name="filePath"/>; see the overload with warnings. Legacy files
        /// that could not be migrated are not reported by this overload (they are kept on disk).
        /// </summary>
        public static List<ROIClass> LoadROIsFromFolder(string filePath, List<OntologyCodeClass> ontologyList)
        {
            return LoadROIsFromFolder(filePath, ontologyList, null);
        }

        /// <summary>
        /// Loads the ROIs of the template in <paramref name="filePath"/>.
        /// <para>When All_ROIs.json exists it is the template: if it cannot be read, does not parse, holds no list
        /// or an ROI that cannot be rebuilt, this throws <see cref="TemplateLoadException"/> and writes nothing
        /// (the legacy ROIs folder is not consulted).</para>
        /// <para>Otherwise the legacy text files in the ROIs folder are migrated: the ROIs that parse are saved to
        /// All_ROIs.json, and the ROIs folder is deleted only when every file in it was migrated. Each file that was
        /// not migrated is added to <paramref name="warnings"/> (one line per file) and the folder is kept. When the
        /// folder has files but none of them parse, this throws <see cref="TemplateLoadException"/> and writes
        /// nothing.</para>
        /// </summary>
        public static List<ROIClass> LoadROIsFromFolder(string filePath, List<OntologyCodeClass> ontologyList, ICollection<string>? warnings)
        {
            string jsonFile = Path.Combine(filePath, RoisFileName);
            if (!File.Exists(jsonFile))
            {
                List<ROIClass>? migrated = MigrateLegacyTextFiles(filePath, warnings);
                if (migrated != null)
                {
                    return migrated;
                }

                // Another reader (the RT generator, or another copy of the program) migrated the same files first:
                // its All_ROIs.json is complete, so it is read like any other.
            }
            else if (warnings != null)
            {
                string? kept = DescribeKeptLegacyFiles(filePath);
                if (kept != null)
                {
                    warnings.Add(kept);
                }
            }

            List<ROIClass> rois = ReadRoisFile(jsonFile);
            foreach (ROIClass roi in rois)
            {
                try
                {
                    // Rebuild non-serialized values and share the ontology instances.
                    roi.RebuildFromDeserialization(ontologyList);
                }
                catch (Exception ex)
                {
                    throw new TemplateLoadException(jsonFile, $"ROI '{roi.ROIName}' is not valid: {ex.Message}", ex);
                }
            }
            return rois;
        }

        /// <summary>
        /// A warning for legacy ROI text files still in the ROIs folder of a template that has All_ROIs.json (a
        /// migration kept them because one could not be read, or they could not be deleted); null when there are none.
        /// All_ROIs.json is what is used, so an ROI that is only in those files is not part of the template.
        /// </summary>
        public static string? DescribeKeptLegacyFiles(string filePath)
        {
            string roisFolder = Path.Combine(filePath, LegacyFolderName);
            string[] files;
            try
            {
                if (!File.Exists(Path.Combine(filePath, RoisFileName)) || !Directory.Exists(roisFolder))
                {
                    return null;
                }

                files = Directory.GetFiles(roisFolder, "*.txt");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }

            if (files.Length == 0)
            {
                return null;
            }

            string names = string.Join(", ", files.Select(Path.GetFileName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).Take(5))
                + (files.Length > 5 ? $" and {files.Length - 5} more" : string.Empty);
            return $"the ROIs folder still holds {files.Length} legacy ROI file(s) ({names}) next to {RoisFileName}. Only {RoisFileName} is used, "
                + "so an ROI that is only in those files is not in this template or its RTs. Check them, add any missing ROI, then remove the ROIs folder.";
        }

        /// <summary>
        /// <see cref="LoadROIsFromFolder(string, List{OntologyCodeClass})"/> for callers that list templates: returns
        /// false with a readable <paramref name="error"/> (and an empty list) when the template cannot be read,
        /// instead of throwing.
        /// </summary>
        public static bool TryLoadROIsFromFolder(string filePath, List<OntologyCodeClass> ontologyList, out List<ROIClass> rois, [NotNullWhen(false)] out string? error)
        {
            try
            {
                rois = LoadROIsFromFolder(filePath, ontologyList);
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is TemplateLoadException || ex is IOException || ex is UnauthorizedAccessException)
            {
                rois = new List<ROIClass>();
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Reads and parses an All_ROIs.json file without changing anything. Throws <see cref="TemplateLoadException"/>
        /// when the file cannot be read, does not parse, or does not hold a list of ROIs.
        /// </summary>
        internal static List<ROIClass> ReadRoisFile(string jsonFile)
        {
            List<ROIClass>? rois;
            try
            {
                rois = JsonConvert.DeserializeObject<List<ROIClass>>(SharedFile.ReadAllText(jsonFile));
            }
            catch (Exception ex)
            {
                throw new TemplateLoadException(jsonFile, ex.Message, ex);
            }
            if (rois == null)
            {
                throw new TemplateLoadException(jsonFile, "the file is empty or holds null instead of a list of ROIs.");
            }
            // Newtonsoft fills a null array element with null even though the element type is not nullable.
            if (rois.Any(roi => roi == null))
            {
                throw new TemplateLoadException(jsonFile, "the ROI list holds a null entry.");
            }
            return rois;
        }

        /// <summary>
        /// Checks if a folder contains a valid template (either JSON or legacy format).
        /// </summary>
        public static bool IsValidTemplateFolder(string filePath)
        {
            // Check for new JSON format
            if (File.Exists(Path.Combine(filePath, RoisFileName)))
            {
                return true;
            }

            // Check for legacy ROIs folder format
            string roisFolder = Path.Combine(filePath, LegacyFolderName);
            if (Directory.Exists(roisFolder))
            {
                string[] txtFiles = Directory.GetFiles(roisFolder, "*.txt");
                return txtFiles.Length > 0;
            }

            return false;
        }

        /// <summary>
        /// Migrates the legacy text files in the ROIs subfolder to All_ROIs.json; see
        /// <see cref="LoadROIsFromFolder(string, List{OntologyCodeClass}, ICollection{string})"/>. Returns null when
        /// another reader created All_ROIs.json meanwhile: the first finished migration wins, and a later one never
        /// replaces it (it may have read only part of the files, which the winner deletes once it has saved them).
        /// </summary>
        private static List<ROIClass>? MigrateLegacyTextFiles(string filePath, ICollection<string>? warnings)
        {
            List<ROIClass> rois = new List<ROIClass>();
            string roisFolder = Path.Combine(filePath, LegacyFolderName);
            string jsonFile = Path.Combine(filePath, RoisFileName);

            if (!Directory.Exists(roisFolder))
            {
                return File.Exists(jsonFile) ? null : rois;
            }

            List<string> migrated = new List<string>();
            List<string> problems = new List<string>();
            string[] files;
            try
            {
                files = Directory.GetFiles(roisFolder, "*.txt");
            }
            catch (DirectoryNotFoundException) when (File.Exists(jsonFile))
            {
                return null;
            }

            foreach (string file in files)
            {
                ROIClass? roi;
                try
                {
                    roi = LoadROIFromTextFile(file);
                }
                catch (Exception ex) when ((ex is FileNotFoundException || ex is DirectoryNotFoundException) && File.Exists(jsonFile))
                {
                    // Deleted by a reader that finished migrating these files (it saves All_ROIs.json before deleting).
                    return null;
                }
                catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is IOException || ex is UnauthorizedAccessException)
                {
                    problems.Add($"{file}: {ex.Message}");
                    continue;
                }
                if (roi == null)
                {
                    problems.Add($"{file}: expected a colour line (R\\G\\B), an ontology line and an interpreted-type line.");
                    continue;
                }
                if (rois.Any(r => r.ROIName == roi.ROIName))
                {
                    problems.Add($"{file}: another file already defines ROI '{roi.ROIName}'.");
                    continue;
                }
                rois.Add(roi);
                migrated.Add(file);
            }

            if (File.Exists(jsonFile))
            {
                return null;
            }

            if (rois.Count == 0)
            {
                if (problems.Count > 0)
                {
                    throw new TemplateLoadException(roisFolder, $"none of its {problems.Count} legacy ROI file(s) could be read. First problem: {problems[0]}");
                }
                return rois;
            }

            Directory.CreateDirectory(filePath);
            if (!AtomicFile.TryCreateText(jsonFile, JsonConvert.SerializeObject(rois, Formatting.Indented)))
            {
                return null;
            }

            if (problems.Count == 0)
            {
                DeleteMigratedLegacyFiles(roisFolder, migrated);
            }
            else if (warnings != null)
            {
                foreach (string problem in problems)
                {
                    warnings.Add($"Legacy ROI file not migrated, so the ROIs folder was kept: {problem}");
                }
            }

            return rois;
        }

        /// <summary>
        /// Loads a single ROI from a legacy text file.
        /// </summary>
        private static ROIClass? LoadROIFromTextFile(string roiFile)
        {
            string roiname = Path.GetFileName(roiFile).Replace(".txt", "");
            string[] instructions = SharedFile.ReadAllLines(roiFile);

            if (instructions.Length < 3)
            {
                return null;
            }

            // Parse color
            string color = instructions[0];
            string[] color_values = color.Split('\\');
            if (color_values.Length < 3)
            {
                return null;
            }

            byte r = byte.Parse(color_values[0]);
            byte g = byte.Parse(color_values[1]);
            byte b = byte.Parse(color_values[2]);

            // Parse ontology code
            string[] code_values = instructions[1].Split('\\');
            OntologyCodeClass ontology;

            if (code_values.Length >= 9)
            {
                // Extended format with all ontology fields
                ontology = new OntologyCodeClass(
                    code_values[0],
                    code_values[1],
                    code_values[2],
                    code_values[3],
                    code_values[4],
                    code_values[5],
                    code_values[6],
                    code_values[7],
                    code_values[8]
                );
            }
            else if (code_values.Length >= 3)
            {
                // Basic format with just name, code, scheme
                ontology = new OntologyCodeClass(code_values[0], code_values[1], code_values[2]);
            }
            else
            {
                ontology = new OntologyCodeClass();
            }

            // Parse interpreted type
            string interpreter = instructions.Length >= 3 ? instructions[2] : "";

            // Create ROI
            ROIClass roi = new ROIClass(r, g, b, roiname, interpreter, ontology);

            // Parse include flag
            if (instructions.Length > 3)
            {
                if (bool.TryParse(instructions[3], out bool include))
                {
                    roi.Include = include;
                }
            }

            // Parse Eclipse-specific settings
            if (instructions.Length > 4)
            {
                string[] eclipse_instructions = instructions[4].Split('\\');
                if (eclipse_instructions.Length >= 5)
                {
                    roi.TypeIndex = eclipse_instructions[0];
                    roi.ContourStyle = eclipse_instructions[1];
                    roi.DVHLineStyle = eclipse_instructions[2];
                    roi.DVHLineColor = eclipse_instructions[3];
                    roi.DVHLineWidth = eclipse_instructions[4];
                    roi.build_dvh_line_color();
                }
            }

            return roi;
        }

        /// <summary>
        /// Removes the legacy files that were migrated to JSON, then the ROIs folder when nothing else is left in
        /// it. Only files that were read are deleted, so a file added meanwhile is kept.
        /// </summary>
        private static void DeleteMigratedLegacyFiles(string roisFolder, List<string> migrated)
        {
            // The ROIs are saved as JSON, which takes precedence; a leftover legacy file or folder loses nothing.
            foreach (string file in migrated)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                }
            }
            try
            {
                if (Directory.GetFiles(roisFolder).Length == 0 && Directory.GetDirectories(roisFolder).Length == 0)
                {
                    Directory.Delete(roisFolder);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }
    }

    public class ROIClass
    {
        // Newtonsoft writes every property, so files this program saved always hold ROIName. A hand-edited file
        // with "ROIName": null still loads it as null, which this annotation does not model (one without the
        // key gets "").
        private string roiname = "";
        // Null for an ROI read from a template file whose Ontology_Class is null or missing; the other
        // constructors always set it.
        private OntologyCodeClass? ontology_class;
        // Never read by this program; null for an ROI read from a file whose RGB is null or missing.
        private List<byte>? rgb;
        private List<byte>? rgb_dvh;
        private string? roi_interpreted_type;
        private byte r, g, b;
        private byte r_dvh, g_dvh, b_dvh;
        public string color_string = "";
        public string? dvh_color_string;
        private bool include;
        private string contourstyle = "contour"; // segment, transluce, contour
        private string dvhlinestyle = "solid"; // 0 is solid, 1 is dashed -------, 2 is small dashed *******, 3 is dash dot -*-*-*-, 4 dash dot dot -**-**-
        private string dvhlinecolor = "-16777216"; // default value means follow what is going on in the color
        private string dvhlinewidth = "1";
        private string typeindex = "2";
        public string TypeIndex
        {
            get { return typeindex; }
            set
            {
                typeindex = value;
                OnPropertyChanged("TypeIndex");
            }
        }
        public string ContourStyle
        {
            get { return contourstyle; }
            set
            {
                contourstyle = value;
                OnPropertyChanged("ContourStyle");
            }
        }
        public string DVHLineStyle
        {
            get { return dvhlinestyle; }
            set
            {
                dvhlinestyle = value;
                OnPropertyChanged("DVHLineStyle");
            }
        }
        public string DVHLineColor
        {
            get { return dvhlinecolor; }
            set
            {
                dvhlinecolor = value;
                OnPropertyChanged("DVHLineColor");
            }
        }
        public string DVHLineWidth
        {
            get { return dvhlinewidth; }
            set
            {
                dvhlinewidth = value;
                OnPropertyChanged("DVHLineWidth");
            }
        }
        public bool Include
        {
            get { return include; }
            set
            {
                include = value;
                OnPropertyChanged("Include");
            }
        }
        public OntologyCodeClass? Ontology_Class
        {
            get { return ontology_class; }
            set
            {
                ontology_class = value;
                OnPropertyChanged("Ontology_Class");
            }
        }
        public string ROIName
        {
            get { return roiname; }
            set
            {
                roiname = value;
                OnPropertyChanged("ROIName");
            }
        }

        public List<byte>? RGB
        {
            get { return rgb; }
            set
            {
                rgb = value;
                OnPropertyChanged("RGB");
            }
        }
        public List<byte>? RGB_DVH
        {
            get { return rgb_dvh; }
            set
            {
                rgb_dvh = value;
                OnPropertyChanged("RGB_DVH");
            }
        }
        public string? ROI_Interpreted_type
        {
            get { return roi_interpreted_type; }
            set
            {
                roi_interpreted_type = value;
                OnPropertyChanged("ROI_Interpreted_type");
            }
        }
        public byte R_DVH
        {
            get { return r_dvh; }
            set
            {
                r_dvh = value;
                OnPropertyChanged("R_DVH");
            }
        }
        public byte G_DVH
        {
            get { return g_dvh; }
            set
            {
                g_dvh = value;
                OnPropertyChanged("G_DVH");
            }
        }
        public byte B_DVH
        {
            get { return b_dvh; }
            set
            {
                b_dvh = value;
                OnPropertyChanged("B_DVH");
            }
        }
        public byte R
        {
            get { return r; }
            set
            {
                r = value;
                OnPropertyChanged("R");
            }
        }
        public byte G
        {
            get { return g; }
            set
            {
                g = value;
                OnPropertyChanged("G");
            }
        }
        public byte B
        {
            get { return b; }
            set
            {
                b = value;
                OnPropertyChanged("B");
            }
        }

        /// <summary>
        /// Parameterless constructor required for JSON deserialization.
        /// After deserializing, call RebuildFromDeserialization() to restore
        /// derived values (colour string, DVH colour, shared ontology instance).
        /// </summary>
        public ROIClass()
        {
            // Default values are set in field initializers
        }

        /// <summary>
        /// Rebuilds derived values (colour string, DVH colour, shared ontology instance) after JSON deserialization.
        /// Call this method after deserializing an ROIClass object.
        /// </summary>
        public void RebuildFromDeserialization(List<OntologyCodeClass> ontologyList)
        {
            // Rebuild color_string if not already set
            if (string.IsNullOrEmpty(color_string))
            {
                color_string = $"{R}\\{G}\\{B}";
            }
            bool foundOntology = false;
            foreach (OntologyCodeClass ontology in ontologyList)
            {
                if (Ontology_Class == null)
                {
                    // Still fails here when there are ontologies to match against, as it always has;
                    // LoadROIsFromFolder reports it as a TemplateLoadException.
                    throw new InvalidOperationException($"ROI '{ROIName}' has no ontology class.");
                }
                if (ontology.CodeMeaning == Ontology_Class.CodeMeaning &&
                    ontology.CodeValue == Ontology_Class.CodeValue &&
                    ontology.Scheme == Ontology_Class.Scheme)
                {
                    Ontology_Class = ontology;
                    foundOntology = true;
                    break;
                }
            }
            if (!foundOntology && Ontology_Class != null)
            {
                ontologyList.Add(Ontology_Class);
            }
            // Rebuild DVH color and brush
            build_dvh_line_color();
        }

        // reference identifies the structure set ROI sequence
        // observation_number unique within observation sequence
        public ROIClass(string color, string name, string? roi_interpreted_type, OntologyCodeClass identification_code_class, string type_index, string contour_style,
            string dvhLineStyle, string dvhLineColor, string dvhLineWidth)
        {
            ROIName = name;
            Include = true;
            color_string = color;
            string[] colors = color.Split('\\');
            R = Byte.Parse(colors[0]);
            G = Byte.Parse(colors[1]);
            B = Byte.Parse(colors[2]);
            RGB = new List<byte> { R, G, B };
            ROI_Interpreted_type = roi_interpreted_type;
            Ontology_Class = identification_code_class;
            TypeIndex = type_index;
            ContourStyle = contour_style;
            DVHLineStyle = dvhLineStyle;
            DVHLineColor = dvhLineColor;
            build_dvh_line_color();
            DVHLineWidth = dvhLineWidth;
        }
        public void build_dvh_line_color()
        {
            if (DVHLineColor == "-16777216")
            {
                R_DVH = R;
                G_DVH = G;
                B_DVH = B;
            }
            else
            {
                double color_int = (Int32.Parse(DVHLineColor));
                double blue = (double)Math.Floor(color_int / (256 * 256));
                double green = (double)Math.Floor((color_int - (blue * 256 * 256)) / 256);
                double red = color_int - (green * 256 + blue * 256 * 256);
                R_DVH = byte.Parse(red.ToString());
                G_DVH = byte.Parse(green.ToString());
                B_DVH = byte.Parse(blue.ToString());
            }
        }
        public ROIClass(byte r, byte g, byte b, string name, string? roi_interpreted_type, OntologyCodeClass identification_code_class)
        {
            roiname = name;
            R = r;
            G = g;
            B = b;
            Include = true;
            color_string = $"{R.ToString()}\\{G.ToString()}\\{B.ToString()}";
            RGB = new List<byte> { R, G, B };
            ROI_Interpreted_type = roi_interpreted_type;
            Ontology_Class = identification_code_class;
            build_dvh_line_color();
        }
        public ROIClass(string color, string name, string? roi_interpreted_type, OntologyCodeClass identification_code_class)
        {
            roiname = name;
            Include = true;
            color_string = color;
            string[] colors = color.Split('\\');
            R = Byte.Parse(colors[0]);
            G = Byte.Parse(colors[1]);
            B = Byte.Parse(colors[2]);
            RGB = new List<byte> { R, G, B };
            ROI_Interpreted_type = roi_interpreted_type;
            Ontology_Class = identification_code_class;
            build_dvh_line_color();
        }

        public void update_color(byte R, byte G, byte B)
        {
            this.R = R;
            this.G = G;
            this.B = B;
            RGB = new List<byte> { R, G, B };
            color_string = $"{R.ToString()}\\{G.ToString()}\\{B.ToString()}";
            build_dvh_line_color();
        }
        public void update_dvh_color(byte R, byte G, byte B)
        {
            DVHLineColor = (Int32.Parse(R.ToString()) + Int32.Parse(G.ToString()) * 256 + Int32.Parse(B.ToString()) * 256 * 256).ToString();
            build_dvh_line_color();
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

    public class ROIWrapper
    {
        private string? english_name;
        private string? english_name_reverse;
        private string? spanish_name;
        private string? spanish_name_reverse;
        private string? french_name;
        private string? french_name_reverse;
        public bool has_other_lanuages = false;
        public bool has_lateral = false;
        public ROIClass roi;
        public ROIWrapper(ROIClass base_ROI, string? name, string? name_r, string? spanish, string? spanish_r, string? french, string? french_r)
        {
            roi = base_ROI;
            english_name = name;
            spanish_name = spanish;
            french_name = french;
            english_name_reverse = name_r;
            spanish_name_reverse = spanish_r;
            french_name_reverse = french_r;
            if (english_name_reverse != null)
            {
                has_lateral = true;
            }
            if (spanish_name_reverse != null)
            {
                has_lateral = true;
                has_other_lanuages = true;
            }
            if (french_name_reverse != null)
            {
                has_lateral = true;
                has_other_lanuages = true;
            }
            if (french_name != null)
            {
                has_other_lanuages = true;
            }
            if (spanish_name != null)
            {
                has_other_lanuages = true;
            }
        }
        public void Set_Spanish()
        {
            if (spanish_name != null)
            {
                roi.ROIName = spanish_name;
            }
        }
        public void Set_Spanish(bool reverse)
        {
            Set_Spanish();
            if (reverse)
            {
                if (spanish_name_reverse != null)
                {
                    roi.ROIName = spanish_name_reverse;

                }
            }
        }
        public void Set_French()
        {
            if (french_name != null)
            {
                roi.ROIName = french_name;
            }
        }
        public void Set_French(bool reverse)
        {
            Set_French();
            if (reverse)
            {
                if (french_name_reverse != null)
                {
                    roi.ROIName = french_name_reverse;

                }
            }
        }
        public void Set_English()
        {
            if (english_name != null)
            {
                roi.ROIName = english_name;
            }
        }
        public void Set_English(bool reverse)
        {
            Set_English();
            if (reverse)
            {
                if (english_name_reverse != null)
                {
                    roi.ROIName = english_name_reverse;

                }
            }
        }
    }
}
