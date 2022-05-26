using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DicomTemplateMakerGUI.Services
{
    public class ROIClass
    {
        public string name;
        public List<byte> RGB;
        private string roi_interpreted_type;
        private byte r, g, b;
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
            RGB = new List<byte>{ R, G, B};
            this.roi_interpreted_type = roi_interpreted_type;
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
