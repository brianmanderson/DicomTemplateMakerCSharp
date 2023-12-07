using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using System.IO;

namespace ROIOntologyClass
{
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
        private string dvhlinestyle = "0"; // 0 is solid, 1 is dashed --------, 2 is small dashed *****, 3 is dash dot -*-*-, 4 dash dot dot -**-**-
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
        public Brush ROI_Brush
        {
            get { return roi_brush; }
            set
            {
                roi_brush = value;
                OnPropertyChanged("ROI_Brush");
            }
        }
        public Brush DVH_Brush
        {
            get { return dvh_brush; }
            set
            {
                dvh_brush = value;
                OnPropertyChanged("DVH_Brush");
            }
        }

        public Color ROIColor
        {
            get { return roi_color; }
            set
            {
                roi_color = value;
                OnPropertyChanged("ROIColor");
            }
        }
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
                G_DVH= byte.Parse(green.ToString());
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
        public ROIClass(string roi_file)
        {
            read_text_file(roi_file);
        }
        private void read_text_file(string roi_file)
        {
            roiname = Path.GetFileName(roi_file).Replace(".txt", "");
            string[] instructions = File.ReadAllLines(roi_file);
            string color = instructions[0];
            string[] color_values = color.Split('\\');
            string[] code_values = instructions[1].Split('\\');
            if (code_values.Length == 3)
            {
                Ontology_Class = new OntologyCodeClass(code_values[0], code_values[1], code_values[2]);
            }
            else
            {
                Ontology_Class = new OntologyCodeClass(code_values[0], code_values[1], code_values[2], code_values[3],
                    code_values[4], code_values[5], code_values[6], code_values[7], code_values[8]);
            }
            string interperter = "";
            if (instructions.Length >= 3)
            {
                interperter = instructions[2];
            }
            Include = true;
            if (instructions.Length > 3)
            {
                Include = bool.Parse(instructions[3]);
            }
            if (instructions.Length > 4)
            {
                string[] eclipse_instructions = instructions[4].Split('\\');
                TypeIndex = eclipse_instructions[0];
                ContourStyle = eclipse_instructions[1];
                DVHLineStyle = eclipse_instructions[2];
                DVHLineColor = eclipse_instructions[3];
                DVHLineWidth = eclipse_instructions[4];
            }
            R = byte.Parse(color_values[0]);
            G = byte.Parse(color_values[1]);
            B = byte.Parse(color_values[2]);
            ROIColor = Color.FromRgb(R, G, B);
            color_string = $"{R.ToString()}\\{G.ToString()}\\{B.ToString()}";
            ROI_Brush = new SolidColorBrush(ROIColor);
            RGB = new List<byte> { R, G, B };
            ROI_Interpreted_type = interperter;
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
        public void write_roi(string output)
        {
            OntologyCodeClass i = Ontology_Class;
            File.WriteAllText(Path.Combine(output, $"{ROIName}.txt"),
                $"{R}\\{G}\\{B}\n" +
                $"{i.CodeMeaning}\\{i.CodeValue}\\{i.Scheme}\\{i.ContextGroupVersion}\\" +
                $"{i.MappingResource}\\{i.ContextIdentifier}\\{i.MappingResourceName}\\" +
                $"{i.MappingResourceUID}\\{i.ContextUID}\n" +
                $"{ROI_Interpreted_type}\n" +
                $"{Include}\n" + 
                $"{TypeIndex}\\{ContourStyle}\\{DVHLineStyle}\\{DVHLineColor}\\{DVHLineWidth}");
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
