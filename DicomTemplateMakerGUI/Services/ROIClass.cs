using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;

namespace DicomTemplateMakerGUI.Services
{
    public class ROIClass
    {
        private string roiname;
        private string code_meaning, code_value, scheme_designated;
        private List<byte> rgb;
        private string roi_interpreted_type;
        private byte r, g, b;
        private Color roi_color;
        private Brush roi_brush;
        public string Scheme
        {
            get { return scheme_designated; }
            set
            {
                scheme_designated = value;
                OnPropertyChanged("Scheme");
            }
        }
        public string CodeValue
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

        public Color ROIColor
        {
            get { return roi_color; }
            set
            {
                roi_color = value;
                OnPropertyChanged("ROIColor");
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
        public ROIClass(byte r, byte g, byte b, string Name, string Roi_interp, string code_meaning, string code_value, string scheme_designated)
        {
            roiname = Name;
            R = r;
            G = g;
            B = b;
            ROIColor = Color.FromRgb(R, G, B);
            ROI_Brush = new SolidColorBrush(ROIColor);
            RGB = new List<byte>{ R, G, B};
            ROI_Interpreted_type = Roi_interp;
            CodeMeaning = code_meaning;
            CodeValue = code_value;
            Scheme = scheme_designated;
        }
        public void update_color(byte R, byte G, byte B)
        {
            this.R = R;
            this.G = G;
            this.B = B;
            this.ROIColor = Color.FromRgb(R, G, B);
            this.ROI_Brush = new SolidColorBrush(ROIColor);
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
}
