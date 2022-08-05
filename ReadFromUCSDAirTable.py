import os
from matplotlib.colors import to_rgb
from pyairtable import Api, Base, Table


def add_to_base_dictionary(out_dictionary, fields):
    test_dict = {'CommonName': None, 'FMAID': None, 'Type': None, 'RGB': None, 'Scheme': None,
                 'ContextGroupVersion': None, 'MappingResource': None,
                 'ContextIdentifier': None, 'MappingResourceName': None, 'MappingResourceUID': None,
                 'ContextUID': None, 'Sites': None, 'Names': None}
    for key in test_dict.keys():
        if key in fields:
            test_dict[key] = fields[key]
    i = test_dict['RGB'].split(',')
    test_dict['RGB'] = f"{i[0]}\{i[1]}\{i[2]}"
    out_dictionary[fields['FMAID']] = test_dict
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
templates = {}
for FMAID in out_dictionary.keys():
    o = out_dictionary[FMAID]
    for site in o['Sites']:
        if site not in templates:
            templates[site] = []
        templates[site].append(o)
    try:
        fid = open(os.path.join(ontology_path, f"{o['CommonName']}.txt"), 'w+')
        fid.write(f"{o['FMAID']}\n{o['Scheme']}\n{o['ContextGroupVersion']}\n{o['MappingResource']}"
                  f"\n{o['ContextIdentifier']}\n{o['MappingResourceName']}\n{o['MappingResourceUID']}"
                  f"\n{o['ContextUID']}")
        fid.close()
    except:
        continue

for template_key in templates.keys():
    template_path = os.path.join('.', 'AirTableRecords', template_key.replace('/', '.'))
    roi_path = os.path.join(template_path, 'ROIs')
    if not os.path.exists(template_path):
        os.makedirs(template_path)
        os.makedirs(roi_path)
    fid = open(os.path.join(template_path, "Paths.txt"), 'w+')
    fid.close()
    # fid.write("O:\DICOM\BMA_Export\Single_Image\n")
    # fid.close()
    for o in templates[template_key]:
        roi_name = o['Names'][0]
        roi_name = roi_name.replace('/', '.')
        fid = open(os.path.join(roi_path, f"{roi_name}.txt"), 'w+')
        fid.write(f"{o['RGB']}\n{o['CommonName']}\\{o['FMAID']}\\{o['Scheme']}\\{o['ContextGroupVersion']}"
                  f"\\{o['MappingResource']}\\{o['ContextIdentifier']}\\{o['MappingResourceName']}"
                  f"\\{o['MappingResourceUID']}\\{o['ContextUID']}\n"
                  f"{o['Type']}")
        fid.close()