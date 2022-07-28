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

ds = pydicom.read_file(r'C:\Users\b5anderson\Modular_Projects\Template_Folder\Abdomen_Template\ANON398831\RS.1.2.246.352.205.4963739911030282051.5106223655473496236.dcm')
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
ontology_dict = {}

ontology_path = os.path.join('.', 'AirTableRecords', "Ontologies")
if not os.path.exists(ontology_path):
    os.makedirs(ontology_path)
for template_key in out_dictionary.keys():
    template_path = os.path.join('.', 'AirTableRecords', template_key)
    roi_path = os.path.join(template_path, 'ROIs')
    if not os.path.exists(template_path):
        os.makedirs(template_path)
        os.makedirs(roi_path)
    for roi in out_dictionary[template_key]:
        roi_name = roi['Name']
        if roi_name not in ontology_dict:
            if roi['FMAID'] == 88:
                scheme = "99VMS_STRUCTCODE"
                if roi_name.find('CTV') != -1:
                    base = 'CTV'
                elif roi_name.find('PTV') != -1:
                    base = 'PTV'
                elif roi_name.find('GTV') != -1:
                    base = 'GTV'
                if roi_name.find('High') != -1:
                    out = f"{base}_High"
                elif roi_name.find('Mid') != -1:
                    out = f"{base}_Mid"
                elif roi_name.find('Low') != -1:
                    out = f"{base}_Low"
                else:
                    out = f"{base}"
                roi['FMAID'] = out
            else:
                scheme = "FMA"
            ontology_dict[roi['Name']] = {"Name": roi['Name'], "Code": roi['FMAID'], "Scheme": scheme}
for roi in ontology_dict.keys():
    onto = ontology_dict[roi]
    try:
        fid = open(os.path.join(ontology_path, f"{onto['Name']}.txt"), 'w+')
        fid.write(f"{onto['Code']}\n{onto['Scheme']}\n20161209\n99VMS\nVMS011\nVarian Medical Systems\n1.2.246.352.7.1.1"
                  f"\n1.2.246.352.7.2.11")
        fid.close()
    except:
        continue
for template_key in out_dictionary.keys():
    template_path = os.path.join('.', 'AirTableRecords', template_key.replace('/', '.'))
    roi_path = os.path.join(template_path, 'ROIs')
    if not os.path.exists(template_path):
        os.makedirs(template_path)
        os.makedirs(roi_path)
    fid = open(os.path.join(template_path, "Paths.txt"), 'w+')
    fid.write("O:\DICOM\BMA_Export\Single_Image\n")
    fid.close()
    for roi in out_dictionary[template_key]:
        roi_name = roi['Name']
        if roi_name not in ontology_dict:
            continue
        onto = ontology_dict[roi_name]
        roi_name = roi_name.replace('/', '.')
        fid = open(os.path.join(roi_path, f"{roi_name}.txt"), 'w+')
        fid.write(f"{roi['Color']}\n{onto['Name']}\\{onto['Code']}\\{onto['Scheme']}\\20161209\\99VMS\\VMS011\\Varian Medical Systems\\1.2.246.352.7.1.1\\1.2.246.352.7.2.11\n"
                  f"{roi['Type']}")
        fid.close()
xxx = 1