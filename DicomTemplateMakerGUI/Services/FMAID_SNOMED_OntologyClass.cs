using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomTemplateMakerGUI.Services
{
    public class FMAID_SNOMED_OntologyClass
    {
        public string path = @".\FMA_SNOMEDCT_Key.txt";
        public Dictionary<string, string> FMA_to_SNOMED = new Dictionary<string, string>();
        public Dictionary<string, string> SNOMED_To_FMA = new Dictionary<string, string>();
        public FMAID_SNOMED_OntologyClass() 
        {
            List<string> instructions = File.ReadAllLines(path).ToList();
            instructions.RemoveAt(0); // Take out the top line
            foreach (string instruction in instructions)
            {
                string[] codes = instruction.Split(',');
                string fmaid = codes[0];
                string snomed = codes[1];
                if (fmaid != "missing")
                {
                    if (snomed != "missing")
                    {
                        if (!FMA_to_SNOMED.ContainsKey(fmaid))
                        {
                            FMA_to_SNOMED.Add(fmaid, snomed);
                        }
                        if (!SNOMED_To_FMA.ContainsKey(snomed))
                        {
                            SNOMED_To_FMA.Add(snomed, fmaid);
                        }
                    }
                }
            }
        }

    }
}
