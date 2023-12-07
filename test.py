import pydicom
import os
# from DicomRTTool.ReaderWriter import DicomReaderWriter
# from PlotScrollNumpyArrays.Plot_Scroll_Images import plot_scroll_Image

ds = pydicom.read_file(r'C:\Users\b5anderson\Modular_Projects\DicomTemplateMakerCSharp\DicomTemplateMakerGUI\SmallCT\vhm.1001.dcm')
path = r'K:\BMA'
for file in os.listdir(path):
    ds = pydicom.read_file(os.path.join(path, file))
    series_desc = ds.SeriesDescription
    creation_date = ds.InstanceCreationDate[2:]
    if series_desc.find("Phase-based") != -1:
        out_name = series_desc.split('] ')[1][1:-1]
        series_desc = f"{ds.Modality}{creation_date} Ph{out_name}"
    else:
        series_desc = f"{ds.Modality}{creation_date} {ds.SeriesDescription}"
    ds.SeriesDescription = series_desc
    ds.save_as(os.path.join(path, file))
