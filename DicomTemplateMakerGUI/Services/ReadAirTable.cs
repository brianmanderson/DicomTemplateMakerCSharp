using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AirtableApiClient;
using System.ComponentModel;
using ROIOntologyClass;


namespace DicomTemplateMakerGUI.Services
{
    class Variance
    {
        public string Prop { get; set; }
        public object valA { get; set; }
        public object valB { get; set; }
    }
    static class extentions
    {
        public static List<string> DetailedCompare<T>(this T val1, T val2)
        {
            List<string> variances = new List<string>();
            foreach (System.Reflection.PropertyInfo propertyInfo in val1.GetType().GetProperties())
            {
                var value1 = propertyInfo.GetValue(val1);
                var value2 = propertyInfo.GetValue(val2);
                if (value1 is IList<string>)
                {
                    if (((List<string>)value1).All(((List<string>)value2).Contains) && ((List<string>)value1).Count == ((List<string>)value2).Count)
                    {
                        continue;
                    }
                }
                else if ((string)value1 == (string)value2)
                {
                    continue;
                }
                variances.Add(propertyInfo.Name);
            }
            return variances;
        }


    }
    public class AirTableEntry
    {
        public string Structure { get; set; }
        public string CommonName { get; set; }
        public string Type { get; set; }
        public List<string> Colors_RGB { get; set; }
        public List<string> Template_Recommend { get; set; } = new List<string>();
        public List<string> Template_Consider { get; set; } = new List<string>();
        public string SchemeCode { get; set; }
        public string Scheme { get; set; }
        public string ContextGroupVersion { get; set; }
        public string MappingResource { get; set; }
        public string ContextIdentifier { get; set; }
        public string MappingResourceName { get; set; }
        public string MappingResourceUID { get; set; }
        public string ContextUID { get; set; }
        public string Id { get; set; }
        public string TG_263 { get; set; }
        public string TG_263R { get; set; }
        public string TG_263Spanish { get; set; }
        public string TG_263SpanishR { get; set; }
        public string TG_263French { get; set; }
        public string TG_263FrenchR { get; set; }
        public string RGB { get; set; }
        public string DVH_Color { get; set; }
        public string DVH_Style { get; set; }
        public string DVH_Width { get; set; }
        public string DVH_Type_Index { get; set; }
        public string DVH_ContourStyle { get; set; }
        public AirTableEntry()
        {
            OntologyCodeClass o = new OntologyCodeClass();
            Scheme = o.Scheme;
            CommonName = null;
            ContextGroupVersion = o.ContextGroupVersion;
            MappingResource = o.MappingResource;
            ContextIdentifier = o.ContextIdentifier;
            MappingResourceName = o.MappingResourceName;
            MappingResourceUID = o.MappingResourceUID;
            ContextUID = o.ContextUID;
        }
        public AirTableEntry(ROIClass roi)
        {
            OntologyCodeClass o = roi.Ontology_Class;
            Scheme = o.Scheme;
            SchemeCode = o.CodeValue;
            CommonName = o.CodeMeaning;
            ContextGroupVersion = o.ContextGroupVersion;
            MappingResource = o.MappingResource;
            ContextIdentifier = o.ContextIdentifier;
            MappingResourceName = o.MappingResourceName;
            MappingResourceUID = o.MappingResourceUID;
            ContextUID = o.ContextUID;
            Structure = roi.ROIName;
            Type = roi.ROI_Interpreted_type;
            Colors_RGB = new List<string>() { $"Auto:{roi.R},{roi.G},{roi.B}" };
            DVH_Color = roi.DVHLineColor;
            DVH_Style = roi.DVHLineStyle;
            DVH_Width = roi.DVHLineWidth;
            DVH_Type_Index = roi.TypeIndex;
            DVH_ContourStyle = roi.ContourStyle;
        }
    }
    public class ReadAirTable
    {
        private string airtableName = "TG263_AirTable";
        public string AirTableName
        {
            get { return airtableName; }
            set
            {
                airtableName = value;
                OnPropertyChanged("AirTableName");
            }
        }
        string APIKey = "keyNr4aIdTYupQOJG";
        string BaseKey = "appTUL6ZaSepTawFw";
        string TableKey = "tblex7IPsmm8hvVEc";
        private AirtableBase airtableBase;
        public string file_path;
        public Task<List<AirtableRecord>> records_task;
        public Task<bool> finished_task;
        public Task<bool> finished_write;
        public List<AirTableEntry> AirTableEntry_List = new List<AirTableEntry>();
        public Dictionary<string, List<AirTableEntry>> template_dictionary = new Dictionary<string, List<AirTableEntry>>();
        public Dictionary<string, List<ROIWrapper>> roi_dictionary = new Dictionary<string, List<ROIWrapper>>();
        public ReadAirTable()
        {
        }
        public ReadAirTable(string file)
        {
            file_path = file;
            string[] lines = File.ReadAllLines(file_path);
            AirTableName = Path.GetFileNameWithoutExtension(file_path);
            APIKey = lines[0];
            BaseKey = lines[1];
            TableKey = lines[2];

        }
        public ReadAirTable(string airtablename, string apikey, string basekey, string tablekey)
        {
            AirTableName = airtablename;
            APIKey = apikey;
            BaseKey = basekey;
            TableKey = tablekey;
        }
        public void Delete()
        {
            if (File.Exists(file_path))
            {
                File.Delete(file_path);
            }
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
                roi_dictionary.Add(site, new List<ROIWrapper>());
            }
            if (!roi_dictionary[site].Where(p => p.roi.ROIName == r.Structure).Any())
            {
                string code_meaning = r.Structure;
                if (r.CommonName != null)
                {
                    code_meaning = r.CommonName;
                }
                OntologyCodeClass o = new OntologyCodeClass(name: code_meaning, code_value: r.SchemeCode, scheme_designated: r.Scheme, context_identifier: r.ContextIdentifier,
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
                if (r.DVH_Color != null)
                {
                    roi.DVHLineColor = r.DVH_Color;
                    roi.build_dvh_line_color();
                }
                if (r.DVH_Style != null)
                {
                    roi.DVHLineStyle = r.DVH_Style;
                }
                if (r.DVH_Width != null)
                {
                    roi.DVHLineWidth = r.DVH_Width;
                }

                roi.Include = include;
                ROIWrapper wrapper_roi = new ROIWrapper(roi, r.Structure, r.TG_263R, r.TG_263Spanish, r.TG_263SpanishR, r.TG_263French, r.TG_263FrenchR);
                roi_dictionary[site].Add(wrapper_roi);
            }
        }
        public async void UpdateRecord(string TableKey, Fields new_field, string id, bool typecast)
        {
            await airtableBase.UpdateRecord(TableKey, new_field, id, typecast);
        }
        public void WriteToAirTable(string site, List<ROIClass> rois)
        {
            finished_write = WriteToAirTableTask(site, rois);
        }
        public async Task<bool> WriteToAirTableTask(string site, List<ROIClass> rois)
        {
            bool output = true;
            foreach (ROIClass roi in rois)
            {
                AirTableEntry new_entry = new AirTableEntry(roi);
                new_entry.Id = "0";
                if (roi.Include)
                {
                    new_entry.Template_Recommend.Add(site);
                }
                else
                {
                    new_entry.Template_Consider.Add(site);
                }
                var records = AirTableEntry_List.Where(x => x.Structure == roi.ROIName);
                if (records.Any())
                {
                    AirTableEntry entry = records.First();
                    new_entry.Id = entry.Id;
                    var t = extentions.DetailedCompare(entry, new_entry);
                    if (t.Count > 0)
                    {
                        Fields new_field = new Fields();
                        Console.WriteLine(roi.ROIName);
                        foreach (System.Reflection.PropertyInfo propertyInfo in new_entry.GetType().GetProperties())
                        {
                            var value = propertyInfo.GetValue(new_entry);
                            var previous_value = propertyInfo.GetValue(entry);
                            if (value != null)
                            {
                                if (value is IList<string>)
                                {
                                    if (previous_value != null)
                                    {
                                        new_field.AddField(propertyInfo.Name, ((List<string>)value).Union((List<string>)previous_value).ToList());
                                    }
                                    else
                                    {
                                        new_field.AddField(propertyInfo.Name, (List<string>)value);
                                    }
                                }
                                else
                                {
                                    new_field.AddField(propertyInfo.Name, value);
                                }
                            }
                        }
                        if (new_field.FieldsCollection.ContainsKey("Template_Recommend"))
                        {
                            if (!roi.Include)
                            {
                                if (((List<string>)new_field.FieldsCollection["Template_Recommend"]).Contains(site))
                                {
                                    ((List<string>)new_field.FieldsCollection["Template_Recommend"]).Remove(site);
                                }
                            }
                            else
                            {
                                if (((List<string>)new_field.FieldsCollection["Template_Consider"]).Contains(site))
                                {
                                    ((List<string>)new_field.FieldsCollection["Template_Consider"]).Remove(site);
                                }
                            }
                        }
                        await airtableBase.UpdateRecord(TableKey, new_field, entry.Id, true);
                        //UpdateRecord(TableKey, new_field, entry.Id, true);
                        new_entry.Template_Consider = new_entry.Template_Consider.Union(entry.Template_Consider.ToList()).ToList();
                        new_entry.Template_Recommend = new_entry.Template_Recommend.Union(entry.Template_Recommend.ToList()).ToList();
                        AirTableEntry_List.Remove(entry);
                        AirTableEntry_List.Add(new_entry);
                    }

                }
                else
                {
                    Fields new_field = new Fields();
                    foreach (System.Reflection.PropertyInfo propertyInfo in new_entry.GetType().GetProperties())
                    {
                        var value = propertyInfo.GetValue(new_entry);
                        if (value != null)
                        {
                            if (value is IList<string>)
                            {
                                new_field.AddField(propertyInfo.Name, (List<string>)value);
                                //new_entry[propertyInfo.Name] = (List<string>)value;
                            }
                            else
                            {
                                new_field.AddField(propertyInfo.Name, value);
                                //new_entry[propertyInfo.Name] = value;
                            }
                        }
                    }
                    // A bit round about... but we have to create the record to get an Id for the record...
                    var k = await airtableBase.CreateRecord(TableKey, new_field, true);
                    new_entry.Id = k.Record.Id;
                    new_field = new Fields();
                    new_field.AddField("Id", k.Record.Id);
                    await airtableBase.UpdateRecord(TableKey, new_field, new_entry.Id, true);
                    AirTableEntry_List.Add(new_entry);
                }
            }
            return output;
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
                    try
                    {
                        await task;
                    }
                    catch
                    {
                        continue;
                    }
                }
                var response = await task;
                if (response.Success)
                {
                    // Do something with your retrieved record.
                    // See how to extract fields of the retrieved record as an instance of Artist in the example section below
                    AirtableRetrieveRecordResponse<AirTableEntry> airTableOntology = task.Result;
                    AirTableEntry r = airTableOntology.Record.Fields;
                    //Fields new_field = new Fields();
                    //new_val.Add("Hi");
                    //new_field.AddField("Testing", "Test");
                    //await airtableBase.UpdateRecord(TableKey, new_field, airTableOntology.Record.Id, false);
                    r.Id = airTableOntology.Record.Id;
                    if (!AirTableEntry_List.Contains(r))
                    {
                        AirTableEntry_List.Add(r);
                    }
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
