import os
from matplotlib.colors import to_rgb
import pydicom


def add_to_base_dictionary(out_dictionary, fields):
    if fields['Structure'] == 'BODY':
        xxx = 1
    try:
        color_name = fields['Jeff Colors'][0].replace(' ', '')
        RGB = [int(i * 255) for i in to_rgb(color_name)]
    except:
        RGB = [int(i * 255) for i in to_rgb('Green')]
    color = f"{RGB[0]}\{RGB[1]}\{RGB[2]}"
    structure_fields = {'Name': fields['Structure'], 'Color': color,
                        'Type': fields['Type'], 'FMAID': fields['FMAID']}
    for group in fields.keys():
        if fields[group] in ['Consider', 'Recommended']:
            if group not in out_dictionary:
                out_dictionary[group] = []
            out_dictionary[group].append(structure_fields)
    return None

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
from pyairtable import Api, Base, Table
base = Base(API_KEY, BASE_KEY)
table = base.get_table(f"{TABLE_KEY}")
all_records = table.all()
all_records_dict = {}
current_dict = {}
for record in all_records:
    current_dict[record['fields']['FMAID']] = {'Sites': record['fields']['Sites'], 'id': record['id']}
    all_records_dict[record['id']] = record['fields']

path = r'O:\DICOM\BMA_Export'
ds = pydicom.read_file(os.path.join(path, 'Prostate.dcm'))
site = 'Prostate'
temp_out_dict = {}
for roi_observation in ds.RTROIObservationsSequence:
    test_dict = {'CommonName': None, 'FMAID': None, 'Type': None, 'RGB': None, 'Scheme': None,
                 'ContextGroupVersion': None, 'MappingResource': None,
                 'ContextIdentifier': None, 'MappingResourceName': None, 'MappingResourceUID': None,
                 'ContextUID': None, 'Sites': None}
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