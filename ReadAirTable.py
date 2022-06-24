import os
import pydicom
fid = open(os.path.join('.', 'API_Key.txt'))
API_KEY = fid.readline()
fid.close()
TABLE_KEY = 'tblex7IPsmm8hvVEc'
BASE_KEY = 'appTUL6ZaSepTawFw'
fid = open(os.path.join('.', 'Base_key.txt'))
base_id = fid.readline()
fid.close()
from pyairtable import Api, Base, Table
base = Base(API_KEY, BASE_KEY)
table = base.get_table(f"{TABLE_KEY}")
all_records = table.all()
for record in all_records:
    fields = record['fields']
    name = fields['Structure']
    fmaid = fields['FMAID']
    
xxx = 1