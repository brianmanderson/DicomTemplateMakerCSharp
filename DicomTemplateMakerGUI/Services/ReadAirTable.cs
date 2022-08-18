using System;
using System.Collections.Generic;
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
        public string CommonName { get; set; }
        public List<string> Names { get; set; }
        public string Type { get; set; }
        public string FMAID { get; set; }
        public string RGB { get; set; }
        public string Scheme { get; set; }
        public string ContextGroupVersion { get; set; }
        public string MappingResource { get; set; }
        public string ContextIdentifier { get; set; }
        public string MappingResourceName { get; set; }
        public string MappingResourceUID { get; set; }
        public string ContextUID { get; set; }
        public List<string> Sites { get; set; }
    }
    public class ReadAirTable
    {
        string APIKey = "keyfXbWgL96FyPUYH";
        string BaseKey = "appczNMj8RE4CKjtp";
        string TableKey = "tblR6fpTrCnJb4dWy";
        private AirtableBase airtableBase;
        public Task<List<AirtableRecord>> records_task;
        public Dictionary<string, List<AirTableEntry>> template_dictionary = new Dictionary<string, List<AirTableEntry>>();
        public ReadAirTable()
        {
            airtableBase = new AirtableBase(APIKey, BaseKey);
            records_task = return_recordsAsync(airtableBase);
            //
            int x = 1;
        }
        public async void read_records()
        {
            airtableBase = new AirtableBase(APIKey, BaseKey);
            List<AirtableRecord> records = records_task.Result;
            foreach (AirtableRecord rr in records)
            {
                Task<AirtableRetrieveRecordResponse<AirTableEntry>> task = airtableBase.RetrieveRecord<AirTableEntry>(TableKey, rr.Id);
                while (!task.IsCompleted)
                {
                    Thread.Sleep(10);
                }
                var response = await task;

                if (response.Success)
                {
                    // Do something with your retrieved record.
                    // See how to extract fields of the retrieved record as an instance of Artist in the example section below
                    AirtableRetrieveRecordResponse<AirTableEntry> airTableOntology = task.Result;
                    AirTableEntry r = airTableOntology.Record.Fields;
                    foreach (string site in r.Sites)
                    {
                        if (!template_dictionary.ContainsKey(site))
                        {
                            template_dictionary.Add(site, new List<AirTableEntry>());
                        }
                        template_dictionary[site].Add(r);
                    }
                    OntologyCodeClass o = new OntologyCodeClass(name: r.CommonName, code_value: r.FMAID, scheme_designated: r.Scheme, context_identifier: r.ContextIdentifier, group_version: r.ContextGroupVersion,
                        mapping_resource: r.MappingResource, mapping_resource_uid: r.MappingResourceUID, context_uid: r.ContextUID, mapping_resource_name: r.MappingResourceName);
                }
            }
        }
        public async Task<AirtableListRecordsResponse> get_record_response(AirtableBase airtableBase, string TableKey)
        {
            return await airtableBase.ListRecords(TableKey);
        }
        public async Task<List<AirtableRecord>> return_recordsAsync(AirtableBase airtableBase)
        {
            List<AirtableRecord> records = new List<AirtableRecord>();
            string offset = null;
            string errorMessage = null;
            using (airtableBase)
            {
                //
                // Use 'offset' and 'pageSize' to specify the records that you want
                // to retrieve.
                // Only use a 'do while' loop if you want to get multiple pages
                // of records.
                //
                Task<AirtableListRecordsResponse> task = airtableBase.ListRecords(TableKey);
                
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
                return records;
            }
        }
    }
}
