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
fid = open(os.path.join('.', 'Base_key.txt'))
BASE_KEY = fid.readline()
fid.close()
TABLE_KEY = 'tblex7IPsmm8hvVEc'
#fid = open(os.path.join('.', 'Test_Table_key.txt'))
#TABLE_KEY = fid.readline()
#fid.close()
from pyairtable import Api, Base, Table
base = Base(API_KEY, BASE_KEY)
table = base.get_table(f"{TABLE_KEY}")
out_dictionary = {}
problem_fields = []
all_records = table.all()
ptv_ctv_gtv_records = []
for record in all_records:
    if record['fields']['Type'] in ['CTV', 'GTV', 'PTV']:
        ptv_ctv_gtv_records.append(record)
table.update(ptv_ctv_gtv_records[1]['id'], {'FMAID': 88}) # Can update a single value, etc.
