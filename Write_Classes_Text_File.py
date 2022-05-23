import os

template_dir = r'C:\Users\markb\Modular_Projects\DicomTemplateMakerCSharp\DicomTemplateMakerGUI\bin\Debug'

for file, directories, root in os.walk(template_dir):
    break

for directory in directories:
    for ROI in os.listdir(os.path.join(template_dir, directory, "ROIs")):
        fid = open(os.path.join(template_dir, directory, "ROIs", ROI))
        colors = fid.readline()
        colors = colors.strip('\n')
        colors = colors.split('\\')
        interp = fid.readline()
        fid.close()
        print(f'new ROIClass(byte.Parse({colors[0]}), byte.Parse({colors[1]}), byte.Parse({colors[2]}), "{ROI.split(".txt")[0]}", "{interp}"),')
