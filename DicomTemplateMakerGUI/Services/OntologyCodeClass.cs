using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Media;

namespace DicomTemplateMakerGUI.Services
{
    public class OntologyCodeClass
    {
        private string code_meaning, code_value, scheme_designated;
        private string context_group_version = "20161209";
        private string mapping_resource = "99VMS";
        private string context_identifier = "VMS011";
        private string mapping_resource_name = "Varian Medical Systems";
        private string mapping_resource_uid = "1.2.246.352.7.1.1";
        private string context_uid = "1.2.246.352.7.2.11";
        public string ContextUID
        {
            get { return context_uid; }
            set
            {
                context_uid = value;
                OnPropertyChanged("ContextUID");
            }
        }
        public string MappingResourceUID
        {
            get { return mapping_resource_uid; }
            set
            {
                mapping_resource_name = value;
                OnPropertyChanged("MappingResourceUID");
            }
        }
        public string MappingResourceName
        {
            get { return mapping_resource_name; }
            set
            {
                mapping_resource_name = value;
                OnPropertyChanged("MappingResourceName");
            }
        }
        public string ContextIdentifier
        {
            get { return context_identifier; }
            set
            {
                context_identifier = value;
                OnPropertyChanged("ContextIdentifier");
            }
        }
        public string MappingResource
        {
            get { return mapping_resource; }
            set
            {
                mapping_resource = value;
                OnPropertyChanged("MappingResource");
            }
        }
        public string ContextGroupVersion
        {
            get { return context_group_version; }
            set
            {
                context_group_version = value;
                OnPropertyChanged("ContextGroupVersion");
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
        }
        public OntologyCodeClass(string name, string code_value, string scheme_designated)
        {
            CodeMeaning = name;
            CodeValue = code_value;
            Scheme = scheme_designated;
        }
        public OntologyCodeClass(string name, string code_value, string scheme_designated, string group_version, string mapping_resource,
            string context_identifier, string mapping_resource_name, string mapping_resource_uid, string context_uid)
        {
            CodeMeaning = name;
            CodeValue = code_value;
            Scheme = scheme_designated;
            ContextGroupVersion = group_version;
            MappingResource = mapping_resource;
            ContextIdentifier = context_identifier;
            MappingResourceName = mapping_resource_name;
            MappingResourceUID = mapping_resource_uid;
            ContextUID = context_uid;
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
