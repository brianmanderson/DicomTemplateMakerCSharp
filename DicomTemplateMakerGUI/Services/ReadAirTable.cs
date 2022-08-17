using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AirtableApiClient;

namespace DicomTemplateMakerGUI.Services
{
    class ReadAirTable
    {
        string APIKey = "keyfXbWgL96FyPUYH";
        string BaseKey = "appTUL6ZaSepTawFw";
        string TableKey = "tblex7IPsmm8hvVEc";
        public Task<List<AirtableRecord>> records_task;
        public ReadAirTable()
        {
            AirtableBase airtableBase = new AirtableBase(APIKey, BaseKey);
            records_task = return_recordsAsync(airtableBase);
            //records_task.Wait();
            int x = 1;
        }
        public void read_records()
        {
            int x = 1;
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
                task.RunSynchronously();
                
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
