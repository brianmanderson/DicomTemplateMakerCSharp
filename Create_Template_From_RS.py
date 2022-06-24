import pydicom
import os


def main(rs_path=r'C:\Users\b5anderson\Modular_Projects\Template_Folder\Abdomen_Template.dcm'):
    ds = pydicom.read_file(rs_path)
    path = os.path.dirname(rs_path)
    folder_name = os.path.split(rs_path)[1].split('.dcm')[0]
    out_path = os.path.join(path, folder_name)
    roi_path = os.path.join(out_path, 'ROIs')
    roi_dict = {}
    if not os.path.exists(out_path):
        os.makedirs(out_path)
        if not os.path.exists(roi_path):
            os.makedirs(roi_path)
    for roi_contour in ds.ROIContourSequence:
        color = roi_contour.ROIDisplayColor
        reference_number = roi_contour.ReferencedROINumber
        roi_dict[reference_number] = {'color': color, 'Interp': "CONTROL", 'name': None}
    for roi_observation in ds.RTROIObservationsSequence:
        reference_number = roi_observation.ReferencedROINumber
        if reference_number in roi_dict.keys():
            roi_dict[reference_number]['Interp'] = roi_observation.RTROIInterpretedType
            coded = roi_observation.RTROIIdentificationCodeSequence[0]
            roi_dict[reference_number]['Codes'] = f"{coded.CodeMeaning}\{coded.CodeValue}\{coded.CodingSchemeDesignator}"
    for roi_struct in ds.StructureSetROISequence:
        reference_number = roi_struct.ROINumber
        if reference_number in roi_dict.keys():
            roi_dict[reference_number]['name'] = roi_struct.ROIName
    for key in roi_dict.keys():
        roi = roi_dict[key]
        roi_name = roi['name']
        if roi_name is not None:
            fid = open(os.path.join(roi_path, '{}.txt'.format(roi_name)), 'w+')
            fid.write('{}\{}\{}\n'.format(roi['color'][0], roi['color'][1], roi['color'][2]))
            fid.write(f"{roi['Codes']}\n")
            interp = roi['Interp']
            fid.write(interp)
            fid.close()


if __name__ == '__main__':
    main(r'O:\DICOM\BMA_Export\RT.dcm')
