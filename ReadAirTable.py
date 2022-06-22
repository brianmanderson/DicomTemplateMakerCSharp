import os
import pydicom
fid = open(os.path.join('.', 'API_Key.txt'))
api_key = fid.readline()
fid.close()
fid = open(os.path.join('.', 'Base_key.txt'))
base_id = fid.readline()
fid.close()
from pyairtable import Api, Base, Table
base = Base(api_key, base_id)
table = base.get_table('General')
table.all()
xxx = 1