import pydicom
from DicomRTTool.ReaderWriter import DicomReaderWriter
from PlotScrollNumpyArrays.Plot_Scroll_Images import plot_scroll_Image

ds = pydicom.read_file(r'\\ucsdhc-varis2\Radonc$\DICOM\BMA_Export\RT.dcm')
reader = DicomReaderWriter(Contour_Names=['Eye_R'])
reader.walk_through_folders(r'\\ucsdhc-varis2\Radonc$\DICOM\BMA_Export')
reader.get_mask()
xxx = 1