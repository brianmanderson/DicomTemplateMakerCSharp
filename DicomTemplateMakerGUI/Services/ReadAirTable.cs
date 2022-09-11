using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AirtableApiClient;
using Newtonsoft.Json;

namespace DicomTemplateMakerGUI.Services
{
    public class AirTableEntry
    {
        public string Structure { get; set; }
        public string Type { get; set; }
        public string FMAID { get; set; }
        public string RGB { get; set; }
        public List<string> Colors_RGB { get; set; }

        public string Color_RGB;
        public string Scheme { get; set; }
        public string ContextGroupVersion { get; set; }
        public string MappingResource { get; set; }
        public string ContextIdentifier { get; set; }
        public string MappingResourceName { get; set; }
        public string MappingResourceUID { get; set; }
        public string ContextUID { get; set; }
        public List<string> Template_Recommend { get; set; }
        public List<string> Template_Consider { get; set; }
        public AirTableEntry()
        {
            OntologyCodeClass o = new OntologyCodeClass();
            Scheme = o.Scheme;
            ContextGroupVersion = o.ContextGroupVersion;
            MappingResource = o.MappingResource;
            ContextIdentifier = o.ContextIdentifier;
            MappingResourceName = o.MappingResourceName;
            MappingResourceUID = o.MappingResourceUID;
            ContextUID = o.ContextUID;
        }
    }
    public class ReadAirTable
    {
        string AirTableName = "TG263_AirTable";
        bool writeable = false;
        string APIKey = "keyfXbWgL96FyPUYH";
        string BaseKey = "appczNMj8RE4CKjtp";
        //string BaseKey = "appTUL6ZaSepTawFw";
        string TableKey = "tblR6fpTrCnJb4dWy";
        //string TableKey = "tblex7IPsmm8hvVEc";
        private AirtableBase airtableBase;
        public Task<List<AirtableRecord>> records_task;
        public Task<bool> finished_task;
        public Dictionary<string, List<AirTableEntry>> template_dictionary = new Dictionary<string, List<AirTableEntry>>();
        public Dictionary<string, List<ROIClass>> roi_dictionary = new Dictionary<string, List<ROIClass>>();
        public ReadAirTable()
        {
        }
        public ReadAirTable(string airtablename, string apikey, string basekey, string tablekey)
        {
            AirTableName = airtablename;
            APIKey = apikey;
            BaseKey = basekey;
            TableKey = tablekey;
        }
        public void read_records()
        {
            airtableBase = new AirtableBase(APIKey, BaseKey);
            records_task = return_recordsAsync(airtableBase);
            finished_task = read_recordsAsync();
        }
        public void await_finish()
        {
            while (!finished_task.IsCompleted)
            {
                Thread.Sleep(1000);
            }
            finished_task.Wait();
            int x = 5;
        }
        public void add_roi(string site, AirTableEntry r, bool include)
        {
            if (!template_dictionary.ContainsKey(site))
            {
                template_dictionary.Add(site, new List<AirTableEntry>());
            }
            if (!template_dictionary[site].Where(p => p.Structure == r.Structure).Any())
            {
                template_dictionary[site].Add(r);
            }
            if (!roi_dictionary.ContainsKey(site))
            {
                roi_dictionary.Add(site, new List<ROIClass>());
            }
            if (!roi_dictionary[site].Where(p => p.ROIName == r.Structure).Any())
            {
                OntologyCodeClass o = new OntologyCodeClass(name: r.Structure, code_value: r.FMAID, scheme_designated: r.Scheme, context_identifier: r.ContextIdentifier,
                    group_version: r.ContextGroupVersion, mapping_resource: r.MappingResource, mapping_resource_uid: r.MappingResourceUID, context_uid: r.ContextUID,
                    mapping_resource_name: r.MappingResourceName);
                string[] colors;
                if (r.RGB != null)
                {
                    colors = r.RGB.Split(',');
                    var Color_color = Color.FromArgb(int.Parse(colors[0]), int.Parse(colors[1]), int.Parse(colors[2]));
                    var color_lookup = Enum.GetValues(typeof(KnownColor)).Cast<KnownColor>().Select(Color.FromKnownColor).ToLookup(c => c.ToArgb());
                    var named_color = color_lookup[Color_color.ToArgb()];
                }
                else
                {
                    string[] c = r.Colors_RGB[0].Split(':')[1].Split(',');
                    //var color = System.Drawing.Color.FromName(r.Colors[0]);
                    colors = new string[] { $"{c[0]}", $"{c[1]}", $"{c[2]}" };
                }
                ROIClass roi = new ROIClass(r: byte.Parse(colors[0]), g: byte.Parse(colors[1]), b: byte.Parse(colors[2]), name: r.Structure, roi_interpreted_type: r.Type, identification_code_class: o);
                roi.Include = include;
                roi_dictionary[site].Add(roi);
            }
        }
        public async Task<bool> read_recordsAsync()
        {
            airtableBase = new AirtableBase(APIKey, BaseKey);
            bool output = true;
            await records_task;
            List<AirtableRecord> records = records_task.Result;
            foreach (AirtableRecord rr in records)
            {
                Task<AirtableRetrieveRecordResponse<AirTableEntry>> task = airtableBase.RetrieveRecord<AirTableEntry>(TableKey, rr.Id);
                while (!task.IsCompleted)
                {
                    await task;
                }
                var response = await task;
                if (response.Success)
                {
                    // Do something with your retrieved record.
                    // See how to extract fields of the retrieved record as an instance of Artist in the example section below
                    AirtableRetrieveRecordResponse<AirTableEntry> airTableOntology = task.Result;
                    AirTableEntry r = airTableOntology.Record.Fields;
                    if (r.Template_Recommend != null)
                    {
                        foreach (string site in r.Template_Recommend)
                        {
                            bool include = true;
                            try
                            {
                                add_roi(site, r, include);
                            }
                            catch
                            {
                                continue;
                            }
                        }
                    }
                    if (r.Template_Consider != null)
                    {
                        foreach (string site in r.Template_Consider)
                        {
                            bool include = false;
                            try
                            {
                                add_roi(site, r, include);
                            }
                            catch
                            {
                                continue;
                            }
                        }
                    }
                }
            }
            return output;
        }
        public async Task<List<AirtableRecord>> return_recordsAsync(AirtableBase airtableBase)
        {
            List<AirtableRecord> records = new List<AirtableRecord>();
            string offset = null;
            string errorMessage = null;
            int record_index = 0;
            using (airtableBase)
            {
                //
                // Use 'offset' and 'pageSize' to specify the records that you want
                // to retrieve.
                // Only use a 'do while' loop if you want to get multiple pages
                // of records.
                //
                while (offset != null | record_index == 0)
                {
                    record_index += 1;
                    Task<AirtableListRecordsResponse> task = airtableBase.ListRecords(TableKey, offset: offset);

                    AirtableListRecordsResponse response = await task;

                    if (response.Success)
                    {
                        records.AddRange(response.Records.ToList());
                        offset = response.Offset;
                    }
                    else if (response.AirtableApiError is AirtableApiException)
                    {
                        errorMessage = response.AirtableApiError.ErrorMessage;
                        if (response.AirtableApiError is AirtableInvalidRequestException)
                        {
                            errorMessage += "\nDetailed error message: ";
                            errorMessage += response.AirtableApiError.DetailedErrorMessage;
                        }
                    }
                    else
                    {
                        errorMessage = "Unknown error";
                    }
                }
                return records;
            }
        }
    }
}
