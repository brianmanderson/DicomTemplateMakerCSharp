import pandas as pd
import os

# Define the URL of the Google Sheets document
sheet_id = "1AC2wtYKqWXmQsb-_I1OJ6aW32--pQ4vPwFK-wX6L2II"
sheet_name = "Sheet1"
url = f"https://docs.google.com/spreadsheets/d/{sheet_id}/gviz/tq?tqx=out:csv&sheet={sheet_name}"
read_df = pd.read_csv(url)[["FMAID", "SNOMED-CT Code", "Checked?", "Description"]].dropna()
values = read_df.to_numpy()
out_dictionary = {"FMAID": [], "SNOMEDCT": [], "Description": []}
for index, single_pair in enumerate(values):
    fmaid, snomed, checked, description = single_pair
    fmaid, snomed, checked = int(fmaid), int(snomed), int(checked)
    if checked != 1:
        continue
    if fmaid == 88 or type(fmaid) == str:
        continue
    if fmaid == 7465:
        xxx = 1
    if fmaid not in out_dictionary["FMAID"]:
        if snomed not in out_dictionary["SNOMEDCT"]:
            out_dictionary["FMAID"].append(fmaid)
            out_dictionary["SNOMEDCT"].append(snomed)
            out_dictionary["Description"].append(description)
fid = open(os.path.join('.', 'DicomTemplateMakerGUI', "FMA_SNOMEDCT_Key.txt"), 'w+')
fid.write('FMA, SNOMED-CT Code\n')
for fmaid, snomed, desc in zip(out_dictionary["FMAID"], out_dictionary["SNOMEDCT"], out_dictionary["Description"]):
    fid.write(f"{fmaid},{snomed},{desc}\n")
fid.close()
df = pd.DataFrame(out_dictionary)
out_path = r'FMAID To SNOMED_Updated.xlsx'
with pd.ExcelWriter(out_path) as writer:
    df.to_excel(writer, sheet_name="Updated", index=False)

xxx = 1