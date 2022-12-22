import os
import xml.etree.ElementTree as ET
import pandas as pd
from functions import structure


def make_dataframe_dict(file_path):
    new_dict = {"StrID": [], "Type": [], "TypeIdx": [],
                "Color": [], "DVHLS": [], "DVHLC": [], "DVHLW": [],
                "iCode": [], "iCodeScheme": [],
                "iCodeSchemeVersion": []}
    for file in os.listdir(file_path):
        fid = open(os.path.join(file_path, file))
        colors = fid.readline().strip('\n').split('\\')
        data = fid.readline().strip('\n').split('\\')
        roi_type = fid.readline().strip('\n')
        fid.close()
        new_dict["StrID"].append(data[0])
        new_dict["Type"].append(roi_type)
        new_dict["TypeIdx"].append(2)
        new_dict["Color"].append(f"RGB{colors[0]} {colors[1]} {colors[2]}")
        new_dict["DVHLS"].append(0)
        new_dict["DVHLC"].append(-16777216)
        new_dict["DVHLW"].append(1)
        new_dict["iCode"].append(data[1])
        new_dict["iCodeScheme"].append(data[2])
        new_dict["iCodeSchemeVersion"].append(data[8])
    return new_dict


def main():
    file_path = r'C:\Users\b5anderson\Modular_Projects\DicomTemplateMakerCSharp\DicomTemplateMakerGUI\bin\x64\Debug\AbdPelv_Liver\ROIs'
    dict_data = make_dataframe_dict(file_path)
    df = pd.DataFrame(dict_data)
    finalfile = "Test"
    df.to_excel("test.xlsx", engine="openpyxl")

    # use the XML file below as the template to create all clinical protocols:
    StructureTemplate = ET.parse('Structure Template.xml')
    tree = StructureTemplate
    root = StructureTemplate.getroot()

    # this just adds the correct extension to the file name for the clinical protocol
    filename = finalfile + '.xml'

    # Change the clinical protocol name to the final file name so it shows the correct name when it's imported into Eclipse:
    root.find('.//Preview').set('ID', finalfile)
    StructureTemplate.write(filename)

    # This will create the structures in the XML file of the clinical protocol
    idxs = 2

    for x in range(0, len(df), 1):

        data = df.loc[x, 'StrID':'iCodeSchemeVersion']  # One structure per row of the dataframe
        if data[0] == "BODY":
            continue
        if df.Type[x] != 0:

            structure(idxs, data, tree, root, filename)
            idxs = idxs + 1
            print('success!!! ' + str(x) + ':' + data[0])

        else:
            print('Skipped: ' + df.StrID[x])

            # Save the structure template XML file in a specific folder

    folderpath = 'K:\\'
    StructureTemplate.write(folderpath + filename)
    StructureTemplate.write('.\Test.xml')

    # # ___________________________ END OF THE STRUCTURE TEMPLATE CREATION IN XML FILE_______________________________________


if __name__ == '__main__':
    main()
