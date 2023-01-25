using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DicomTemplateMakerGUI.Services
{
    public class ROIClass
    {
        private string roiname;
        private OntologyCodeClass ontology_class;
        private List<byte> rgb;
        private string roi_interpreted_type;
        private byte r, g, b;
        public string color_string;
        private bool include;
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
        public List<byte> RGB
        {
            get { return rgb; }
            set
            {
                rgb = value;
                OnPropertyChanged("RGB");
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
            ROI_Interpreted_type = roi_interpreted_type;
            Ontology_Class = identification_code_class;
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
