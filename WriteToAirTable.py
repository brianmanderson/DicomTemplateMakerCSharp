import os
from matplotlib.colors import to_rgb
import pydicom
from pyairtable import Api, Base, Table


def add_to_airtable_from_path(table: Table, path_to_rt, site):
    all_records = table.all()
    all_records_dict = {}
    current_dict = {}
    for record in all_records:
        id = record['id']
        fields = record['fields']
        record_dict = {'Sites': [], 'Names': [], 'id': id}
        if 'Sites' in fields:
            record_dict['Sites'] = fields['Sites']
        if 'Names' in fields:
            record_dict['Names'] = fields['Names']
        current_dict[record['fields']['FMAID']] = record_dict
        all_records_dict[record['id']] = record['fields']
    ds = pydicom.read_file(path_to_rt)
    temp_out_dict = {}
    for roi_observation in ds.RTROIObservationsSequence:
        test_dict = {'CommonName': None, 'FMAID': None, 'Type': None, 'RGB': None, 'Scheme': None,
                     'ContextGroupVersion': None, 'MappingResource': None,
                     'ContextIdentifier': None, 'MappingResourceName': None, 'MappingResourceUID': None,
                     'ContextUID': None, 'Sites': None, 'Names': None}
        test_dict['Type'] = roi_observation.RTROIInterpretedType
        if test_dict['Type'] == "":
            test_dict['Type'] = "AVOIDANCE"
        rt_code = roi_observation.RTROIIdentificationCodeSequence[0]
        test_dict['FMAID'] = rt_code.CodeValue
        test_dict['Sites'] = [site]
        if rt_code.CodeValue in current_dict:
            if site not in current_dict[rt_code.CodeValue]['Sites']:
                test_dict['Sites'] = current_dict[rt_code.CodeValue]['Sites'] + [site]
        test_dict['CommonName'] = rt_code.CodeMeaning
        test_dict['Scheme'] = rt_code.CodingSchemeDesignator
        test_dict['ContextIdentifier'] = rt_code.ContextIdentifier
        test_dict['ContextGroupVersion'] = rt_code.ContextGroupVersion
        test_dict['ContextUID'] = rt_code.ContextUID
        test_dict['MappingResource'] = rt_code.MappingResource
        test_dict['MappingResourceName'] = rt_code.MappingResourceName
        test_dict['MappingResourceUID'] = rt_code.MappingResourceUID
        temp_out_dict[roi_observation.ReferencedROINumber] = test_dict
    for roi in ds.ROIContourSequence:
        if roi.ReferencedROINumber in temp_out_dict:
            color = roi.ROIDisplayColor
            temp_out_dict[roi.ReferencedROINumber]['RGB'] = f"{color[0]},{color[1]},{color[2]}"
    for roi in ds.StructureSetROISequence:
        if roi.ROINumber in temp_out_dict:
            name = roi.ROIName
            temp_out_dict[roi.ROINumber]['Names'] = [name]
            if temp_out_dict[roi.ROINumber]['FMAID'] in current_dict:
                if name not in current_dict[temp_out_dict[roi.ROINumber]['FMAID']]['Names']:
                    temp_out_dict[roi.ROINumber]['Names'] = current_dict[temp_out_dict[roi.ROINumber]['FMAID']]['Names'] + [name]
    out_list = []
    fmaids = []
    for dictionary in temp_out_dict.values():
        if dictionary['FMAID'] not in fmaids:
            fmaids.append(dictionary['FMAID'])
            out_list.append(dictionary)
    to_write = []
    for dictionary in out_list:
        if dictionary['FMAID'] in current_dict:
            if all_records_dict[current_dict[dictionary['FMAID']]['id']] != dictionary:
                table.update(current_dict[dictionary['FMAID']]['id'], dictionary, typecast=True)
        else:
            to_write.append(dictionary)
    if to_write:
        table.batch_create(to_write, typecast=True)

fid = open(os.path.join('.', 'API_Key.txt'))
API_KEY = fid.readline()
fid.close()
fid = open(os.path.join('.', 'Test_Base_key.txt'))
BASE_KEY = fid.readline()
fid.close()
TABLE_KEY = 'tblex7IPsmm8hvVEc'
fid = open(os.path.join('.', 'Test_Table_key.txt'))
TABLE_KEY = fid.readline()
fid.close()
path = r'O:\DICOM\BMA_Export'
for site in ['Prostate', 'Breast']:
    base = Base(API_KEY, BASE_KEY)
    table = base.get_table(f"{TABLE_KEY}")
    add_to_airtable_from_path(table=table, path_to_rt=os.path.join(path, f'{site}.dcm'), site=site)