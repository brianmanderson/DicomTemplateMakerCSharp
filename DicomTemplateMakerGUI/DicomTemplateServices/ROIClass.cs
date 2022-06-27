using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomTemplateMakerGUI.DicomTemplateServices
{
    class ROIClass
    {
        public string ROIName;
        public string color_string;
        public string ROI_Interpreted_type;
        // reference identifies the structure set ROI sequence
        // observation_number unique within observation sequence
        public ROIClass(string color, string name, string roi_interpreted_type)
        {
            this.ROIName = name;
            this.color_string = color;
            this.ROI_Interpreted_type = roi_interpreted_type;
        }
    }
}
