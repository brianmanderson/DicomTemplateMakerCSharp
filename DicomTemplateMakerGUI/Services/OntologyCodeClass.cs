using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;

namespace DicomTemplateMakerGUI.Services
{
    public class OntologyCodeClass
    {
        private string code_meaning, code_value, scheme_designated;
        public string mapping_resource = "99VMS";
        public string context_identifier = "VMS011";
        public string mapping_resource_name = "Varian Medical Systems";
        public string mapping_resource_uid = "1.2.246.352.7.1.1";
        public string context_uid = "1.2.246.352.7.2.11";
        private string common_name = "Test";
        public string CommonName
        {
            get { return common_name; }
            set
            {
                common_name = value;
                OnPropertyChanged("CommonName");
            }
        }
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
        public OntologyCodeClass()
        {
            CommonName = "NormalTissue";
            CodeValue = "123";
            Scheme = "FMA";
        }
        public OntologyCodeClass(string name, string code_value, string scheme_designated)
        {
            CommonName = name;
            CodeValue = code_value;
            Scheme = scheme_designated;
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
