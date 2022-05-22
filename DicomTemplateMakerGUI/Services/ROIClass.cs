using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomTemplateMakerGUI.Services
{
    public class ROIClass
    {
        public string name;
        public byte R, G, B;
        public string color;
        public List<byte> RGB;
        public string roi_interpreted_type;
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
    }
}
