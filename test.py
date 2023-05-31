import pydicom
import os
# from DicomRTTool.ReaderWriter import DicomReaderWriter
# from PlotScrollNumpyArrays.Plot_Scroll_Images import plot_scroll_Image

path = r'C:\Users\b5anderson\Downloads\SmallCTBreast'
for file in os.listdir(path):
    ds = pydicom.read_file(os.path.join(path, file))
    ds.SeriesDescription = "Breast"
    ds.save_as(os.path.join(path, file))
