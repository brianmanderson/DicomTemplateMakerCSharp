using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;

namespace DicomTemplateMakerGUI.Services
{
    public class ROIClass
    {
        public string name;
        private List<byte> rgb;
        private string roi_interpreted_type;
        private byte r, g, b;
        private Color roi_color;
        private Brush roi_brush;

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
            get { return r; }
            set
            {
                r = value;
                OnPropertyChanged("G");
            }
        }
        public byte B
        {
            get { return r; }
            set
            {
                r = value;
                OnPropertyChanged("B");
            }
        }
        // reference identifies the structure set ROI sequence
        // observation_number unique within observation sequence
        public ROIClass(byte R, byte G, byte B, string name, string roi_interpreted_type)
        {
            this.name = name;
            this.R = R;
            this.G = G;
            this.B = B;
            this.ROIColor = Color.FromRgb(R, G, B);
            this.ROI_Brush = new SolidColorBrush(ROIColor);
            this.RGB = new List<byte>{ R, G, B};
            this.ROI_Interpreted_type = roi_interpreted_type;
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
