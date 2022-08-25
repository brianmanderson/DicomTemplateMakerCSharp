﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;

namespace DicomTemplateMakerGUI.Services
{
    public class ROIClass
    {
        private string roiname;
        private OntologyCodeClass ontology_class;
        private List<byte> rgb;
        private string roi_interpreted_type;
        private byte r, g, b;
        private Color roi_color;
        private Brush roi_brush;
        public string color_string;
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
        public ROIClass(byte r, byte g, byte b, string name, string roi_interpreted_type, OntologyCodeClass identification_code_class)
        {
            roiname = name;
            R = r;
            G = g;
            B = b;
            ROIColor = Color.FromRgb(R, G, B);
            ROI_Brush = new SolidColorBrush(ROIColor);
            RGB = new List<byte>{ R, G, B};
            ROI_Interpreted_type = roi_interpreted_type;
            Ontology_Class = identification_code_class;
        }
        public ROIClass(string color, string name, string roi_interpreted_type, OntologyCodeClass identification_code_class)
        {
            roiname = name;
            color_string = color;
            ROI_Interpreted_type = roi_interpreted_type;
            Ontology_Class = identification_code_class;
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
