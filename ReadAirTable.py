import os
from matplotlib.colors import to_rgb
import pydicom


def add_to_base_dictionary(out_dictionary, fields):
    color_name = fields['Jeff Colors'][0].replace(' ', '')
    RGB = [int(i*255) for i in to_rgb(color_name)]
    color = f"{RGB[0]}\{RGB[1]}\{RGB[2]}"
    structure_fields = {'Name': fields['Structure'], 'Color': color,
                        'Type': fields['Type'], 'FMAID': fields['FMAID']}
    for group in fields.keys():
        if fields[group] in ['Consider', 'Recommend']:
            if group not in out_dictionary:
                out_dictionary[group] = []
            out_dictionary[group].append(structure_fields)
    return None


fid = open(os.path.join('.', 'API_Key.txt'))
API_KEY = fid.readline()
fid.close()
BASE_KEY = 'appTUL6ZaSepTawFw'
TABLE_KEY = 'tblex7IPsmm8hvVEc'
fid = open(os.path.join('.', 'Base_key.txt'))
base_id = fid.readline()
fid.close()
from pyairtable import Api, Base, Table
base = Base(API_KEY, BASE_KEY)
table = base.get_table(f"{TABLE_KEY}")
out_dictionary = {}
problem_fields = []
all_records = table.all()
for record in all_records:
    fields = record['fields']
    try:
        add_to_base_dictionary(out_dictionary, fields)
    except:
        problem_fields.append(fields)
xxx = 1