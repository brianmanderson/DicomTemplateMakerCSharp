using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomTemplateMakerCSharp.Services
{
    class ROIClass
    {
        string name;
        string color;
        int reference_roi_number, observation_number;
        string roi_interpreted_type;
        // reference identifies the structure set ROI sequence
        // observation_number unique within observation sequence
        public ROIClass(string color, string name, string roi_interpreted_type)
        {
            this.name = name;
            this.color = color;
            this.roi_interpreted_type = roi_interpreted_type;
        }
    }
}
