import pandas as pd
import os
import numpy as np

file_path = r'C:\Users\markb\Downloads\FMAID To SNOMED.xlsx'
out_path = r'C:\Users\markb\Downloads\FMAID To SNOMED_Updated.xlsx'
df = pd.read_excel(file_path, sheet_name='Base')
airtable = pd.read_excel(file_path, sheet_name='AirTable')
airtable_array = airtable[['FMAID', 'SNOMED']].dropna().to_numpy()
fmaid_values = airtable_array[:, 0]
wanted_fmaid_array = df['FMAID'].values
for index, fmaid in enumerate(fmaid_values):
    if fmaid == 88 or type(fmaid) == str:
        continue
    if fmaid == 7465:
        xxx = 1
    spot_within_df = df[df['FMAID'] == fmaid]
    if spot_within_df.shape[0] > 0:
        for row_index in spot_within_df.index.values:
            df.at[row_index, 'SNOMED-CT Code'] = airtable_array[index, 1]
            df.at[row_index, 'Checked?'] = 1
with pd.ExcelWriter(out_path) as writer:
    df.to_excel(writer, sheet_name="Updated", index=False)
wanted_columns = df[['FMAID', 'SNOMED-CT Code']].dropna()  # Get rid of rows where we don't have both values
fid = open(os.path.join('.', "FMA_SNOMEDCT_Key.txt"), 'w+')
fid.write('FMA, SNOMED-CT Code\n')
for line in wanted_columns.values:
    fid.write(f"{line[0]},{line[1]}\n")
fid.close()
xxx = 1