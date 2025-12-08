using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace ROIOntologyClass
{
    public class ROIClassTools
    {
        /// <summary>
        /// Saves ROIs to JSON format. Also cleans up legacy text files if they exist.
        /// </summary>
        public static void SaveROIsToFolder(List<ROIClass> rois, string filePath)
        {
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }
            string json = JsonConvert.SerializeObject(rois, Formatting.Indented);
            File.WriteAllText(Path.Combine(filePath, "All_ROIs.json"), json);

            // Clean up legacy ROIs folder after successful JSON save
            CleanupLegacyROIsFolder(filePath);
        }

        /// <summary>
        /// Loads ROIs from JSON format. Falls back to legacy text files if JSON doesn't exist.
        /// If loaded from legacy format, automatically migrates to JSON.
        /// </summary>
        public static List<ROIClass> LoadROIsFromFolder(string filePath)
        {
            string jsonFile = Path.Combine(filePath, "All_ROIs.json");

            // Try to load from JSON first
            if (File.Exists(jsonFile))
            {
                try
                {
                    string json = File.ReadAllText(jsonFile);
                    List<ROIClass> rois = JsonConvert.DeserializeObject<List<ROIClass>>(json);
                    if (rois != null)
                    {
                        // Rebuild non-serializable properties after deserialization
                        foreach (var roi in rois)
                        {
                            roi.RebuildFromDeserialization();
                        }
                        return rois;
                    }
                }
                catch
                {
                    // If JSON parsing fails, try legacy format
                }
            }

            // Fall back to loading from legacy text files in ROIs folder
            List<ROIClass> legacyROIs = LoadFromLegacyTextFiles(filePath);

            // If we loaded from legacy format, migrate to JSON
            if (legacyROIs.Count > 0)
            {
                SaveROIsToFolder(legacyROIs, filePath);
            }

            return legacyROIs;
        }

        /// <summary>
        /// Checks if a folder contains a valid template (either JSON or legacy format).
        /// </summary>
        public static bool IsValidTemplateFolder(string filePath)
        {
            // Check for new JSON format
            if (File.Exists(Path.Combine(filePath, "All_ROIs.json")))
            {
                return true;
            }

            // Check for legacy ROIs folder format
            string roisFolder = Path.Combine(filePath, "ROIs");
            if (Directory.Exists(roisFolder))
            {
                string[] txtFiles = Directory.GetFiles(roisFolder, "*.txt");
                return txtFiles.Length > 0;
            }

            return false;
        }

        /// <summary>
        /// Loads ROIs from legacy individual text files in the ROIs subfolder.
        /// </summary>
        private static List<ROIClass> LoadFromLegacyTextFiles(string filePath)
        {
            List<ROIClass> rois = new List<ROIClass>();
            string roisFolder = Path.Combine(filePath, "ROIs");

            if (!Directory.Exists(roisFolder))
            {
                return rois;
            }

            foreach (string file in Directory.GetFiles(roisFolder, "*.txt"))
            {
                try
                {
                    ROIClass roi = LoadROIFromTextFile(file);
                    if (roi != null && !rois.Any(r => r.ROIName == roi.ROIName))
                    {
                        rois.Add(roi);
                    }
                }
                catch
                {
                    // Skip files that can't be parsed
                    continue;
                }
            }

            return rois;
        }

        /// <summary>
        /// Loads a single ROI from a legacy text file.
        /// </summary>
        private static ROIClass LoadROIFromTextFile(string roiFile)
        {
            string roiname = Path.GetFileName(roiFile).Replace(".txt", "");
            string[] instructions = File.ReadAllLines(roiFile);

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
        /// Removes legacy ROIs folder after migration to JSON.
        /// </summary>
        private static void CleanupLegacyROIsFolder(string filePath)
        {
            string roisFolder = Path.Combine(filePath, "ROIs");

            if (!Directory.Exists(roisFolder))
            {
                return;
            }

            try
            {
                // Delete all .txt files in ROIs folder
                foreach (string file in Directory.GetFiles(roisFolder, "*.txt"))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Ignore individual file deletion failures
                    }
                }

                // Try to delete the folder if empty
                if (Directory.GetFiles(roisFolder).Length == 0 && Directory.GetDirectories(roisFolder).Length == 0)
                {
                    Directory.Delete(roisFolder);
                }
            }
            catch
            {
                // Ignore deletion failures
            }
        }
    }

    public class ROIClass
    {
        private string roiname;
        private OntologyCodeClass ontology_class;
        private List<byte> rgb, rgb_dvh;
        private string roi_interpreted_type;
        private byte r, g, b;
        private byte r_dvh, g_dvh, b_dvh;
        private Color roi_color, dvh_color;
        private Brush roi_brush, dvh_brush;
        public string color_string, dvh_color_string;
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
        public OntologyCodeClass Ontology_Class
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

        [JsonIgnore]
        public Brush ROI_Brush
        {
            get { return roi_brush; }
            set
            {
                roi_brush = value;
                OnPropertyChanged("ROI_Brush");
            }
        }

        [JsonIgnore]
        public Brush DVH_Brush
        {
            get { return dvh_brush; }
            set
            {
                dvh_brush = value;
                OnPropertyChanged("DVH_Brush");
            }
        }

        [JsonIgnore]
        public Color ROIColor
        {
            get { return roi_color; }
            set
            {
                roi_color = value;
                OnPropertyChanged("ROIColor");
            }
        }

        [JsonIgnore]
        public Color DVH_Color
        {
            get { return dvh_color; }
            set
            {
                dvh_color = value;
                OnPropertyChanged("DVH_Color");
            }
        }
        public List<byte> RGB
        {
            get { return rgb; }
            set
            {
                rgb = value;
                OnPropertyChanged("RGB");
            }
        }
        public List<byte> RGB_DVH
        {
            get { return rgb_dvh; }
            set
            {
                rgb_dvh = value;
                OnPropertyChanged("RGB_DVH");
            }
        }
        public string ROI_Interpreted_type
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
        /// non-serializable properties (Color, Brush).
        /// </summary>
        public ROIClass()
        {
            // Default values are set in field initializers
        }

        /// <summary>
        /// Rebuilds non-serializable properties (Color, Brush) after JSON deserialization.
        /// Call this method after deserializing an ROIClass object.
        /// </summary>
        public void RebuildFromDeserialization()
        {
            // Rebuild ROI color and brush from R, G, B values
            ROIColor = Color.FromRgb(R, G, B);
            ROI_Brush = new SolidColorBrush(ROIColor);

            // Rebuild color_string if not already set
            if (string.IsNullOrEmpty(color_string))
            {
                color_string = $"{R}\\{G}\\{B}";
            }

            // Rebuild DVH color and brush
            build_dvh_line_color();
        }

        // reference identifies the structure set ROI sequence
        // observation_number unique within observation sequence
        public ROIClass(string color, string name, string roi_interpreted_type, OntologyCodeClass identification_code_class, string type_index, string contour_style,
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
            ROIColor = Color.FromRgb(R, G, B);
            ROI_Brush = new SolidColorBrush(ROIColor);
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
                DVH_Brush = ROI_Brush;
                DVH_Color = ROIColor;
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
                DVH_Color = Color.FromRgb(R_DVH, G_DVH, B_DVH);
                DVH_Brush = new SolidColorBrush(DVH_Color);
            }
        }
        public ROIClass(byte r, byte g, byte b, string name, string roi_interpreted_type, OntologyCodeClass identification_code_class)
        {
            roiname = name;
            R = r;
            G = g;
            B = b;
            Include = true;
            ROIColor = Color.FromRgb(R, G, B);
            color_string = $"{R.ToString()}\\{G.ToString()}\\{B.ToString()}";
            ROI_Brush = new SolidColorBrush(ROIColor);
            RGB = new List<byte> { R, G, B };
            ROI_Interpreted_type = roi_interpreted_type;
            Ontology_Class = identification_code_class;
            build_dvh_line_color();
        }
        public ROIClass(string color, string name, string roi_interpreted_type, OntologyCodeClass identification_code_class)
        {
            roiname = name;
            Include = true;
            color_string = color;
            string[] colors = color.Split('\\');
            R = Byte.Parse(colors[0]);
            G = Byte.Parse(colors[1]);
            B = Byte.Parse(colors[2]);
            RGB = new List<byte> { R, G, B };
            ROIColor = Color.FromRgb(R, G, B);
            ROI_Brush = new SolidColorBrush(ROIColor);
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
            ROIColor = Color.FromRgb(R, G, B);
            ROI_Brush = new SolidColorBrush(ROIColor);
            build_dvh_line_color();
        }
        public void update_dvh_color(byte R, byte G, byte B)
        {
            DVHLineColor = (Int32.Parse(R.ToString()) + Int32.Parse(G.ToString()) * 256 + Int32.Parse(B.ToString()) * 256 * 256).ToString();
            build_dvh_line_color();
        }
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string info)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(info));
            }
        }
    }

    public class ROIWrapper
    {
        private string english_name;
        private string english_name_reverse;
        private string spanish_name;
        private string spanish_name_reverse;
        private string french_name;
        private string french_name_reverse;
        public bool has_other_lanuages = false;
        public bool has_lateral = false;
        public ROIClass roi;
        public ROIWrapper(ROIClass base_ROI, string name, string name_r, string spanish, string spanish_r, string french, string french_r)
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