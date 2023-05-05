import pandas as pd
import os

file_path = r'C:\Users\b5anderson\Downloads\TG263_Nomenclature_to_SNOMEDCT_Codes_and_Qualifiers.xlsx'
df = pd.read_excel(file_path, sheet_name='Mapped TG263 Terms')
wanted_columns = df[['FMAID', 'SNOMED-CT Code']].dropna()  # Get rid of rows where we don't have both values
fid = open(os.path.join('.', "FMA_SNOMEDCT_Key.txt"), 'w+')
fid.write('FMA, SNOMED-CT Code\n')
for line in wanted_columns.values:
    fid.write(f"{line[0]},{line[1]}\n")
fid.close()
xxx = 1