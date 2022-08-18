using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AirtableApiClient;

namespace DicomTemplateMakerGUI.Services
{
    public class AirTableOntology
    {
        public string CommonName { get; set; }
        public List<string> Sites { get; set; }
    }
    class ReadAirTable
    {
        string APIKey = "keyfXbWgL96FyPUYH";
        string BaseKey = "appczNMj8RE4CKjtp";
        string TableKey = "tblR6fpTrCnJb4dWy";
        AirTableOntology airOnt;
        private AirtableBase airtableBase;
        public Task<List<AirtableRecord>> records_task;
        public List<OntologyCodeClass> ontologies = new List<OntologyCodeClass>();
        public Dictionary<string, List<OntologyCodeClass>> template_dictionary = new Dictionary<string, List<OntologyCodeClass>>();
        public ReadAirTable()
        {
            airtableBase = new AirtableBase(APIKey, BaseKey);
            records_task = return_recordsAsync(airtableBase);
            //
            int x = 1;
        }
        public async void read_records()
        {
            while (!records_task.IsCompleted)
            {
                Thread.Sleep(1000);
            }
            airtableBase = new AirtableBase(APIKey, BaseKey);
            List<AirtableRecord> records = records_task.Result;
            foreach (AirtableRecord r in records)
            {
                Task<AirtableRetrieveRecordResponse<AirTableOntology>> task = airtableBase.RetrieveRecord<AirTableOntology>(TableKey, r.Id);
                var response = await task;
                if (!response.Success)
                {
                    string errorMessage = null;
                    if (response.AirtableApiError is AirtableApiException)
                    {
                        errorMessage = response.AirtableApiError.ErrorMessage;
                    }
                    else
                    {
                        errorMessage = "Unknown error";
                    }
                    // Report errorMessage
                }
                else
                {
                    // Do something with your retrieved record.
                    // See how to extract fields of the retrieved record as an instance of Artist in the example section below
                    AirtableRetrieveRecordResponse<AirTableOntology> airTableOntology = task.Result;
                }
                string type = r.Fields["Type"].ToString();
                string name = r.Fields["CommonName"].ToString();
                string fmaid = r.Fields["FMAID"].ToString();
                string scheme = r.Fields["Scheme"].ToString();
                string group_version = r.Fields["ContextGroupVersion"].ToString();
                string mapping_resource = r.Fields["MappingResource"].ToString();
                string mapping_resource_name = r.Fields["MappingResourceName"].ToString();
                string context_uid = r.Fields["ContextUID"].ToString();
                string context_identifier = r.Fields["ContextIdentifier"].ToString();
                string mappingresource_uid = r.Fields["MappingResourceUID"].ToString();
                OntologyCodeClass o = new OntologyCodeClass(name: name, code_value: fmaid, scheme_designated: scheme, context_identifier: context_identifier,
                    group_version: group_version, mapping_resource: mapping_resource, mapping_resource_uid: mappingresource_uid, context_uid: context_uid, mapping_resource_name:mapping_resource_name);
                ontologies.Add(o);

                //r.Fields["Sites"];

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
