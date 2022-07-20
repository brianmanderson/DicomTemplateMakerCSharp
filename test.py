import pydicom
import os
# from DicomRTTool.ReaderWriter import DicomReaderWriter
# from PlotScrollNumpyArrays.Plot_Scroll_Images import plot_scroll_Image

path = r'O:\DICOM\BMA_Export\Single_Image'
dataset = pydicom.Dataset()
dataset.PatientName = "Anderson^Brian"
for file in os.listdir(path):
    ds = pydicom.read_file(os.path.join(path, file))
    ds[0x0010, 0x0010] = pydicom.DataElement(0x00100010, "PN", "Brian Mark Anderson")
    ds.PatientID = "0003141592654"
    ds.SeriesDescription = "UCSD_Residency_Template_Patient"
    ds.save_as(os.path.join(path, file))
    xxx = 1
ds = pydicom.read_file(r'O:\DICOM\BMA_Export\Single_Image\Liver.dcm')
ds2 = pydicom.read_file(r'O:\DICOM\BMA_Export\Single_Image\Bladder.dcm')
reader = DicomReaderWriter(Contour_Names=['Eye_R'])
reader.walk_through_folders(r'\\ucsdhc-varis2\Radonc$\DICOM\BMA_Export')
reader.get_mask()
xxx = 1