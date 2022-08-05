import pydicom
import os
import numpy as np
import copy

file_path = r'M:\KuznetsovaS\DICOM Example\REG_test4'

ds = pydicom.read_file(os.path.join(file_path, 'test.dcm'))
new_sequence = copy.deepcopy(ds.DeformableRegistrationSequence[1].PreDeformationMatrixRegistrationSequence)
post = pydicom.Dataset("PostDeformationMatrixRegistrationSequence")
new_values = new_sequence[0].FrameOfReferenceTransformationMatrix[:]
new_values[3] = new_values[1]
new_values[7] = new_values[1]
new_values[11] = new_values[1]
new_sequence[0].FrameOfReferenceTransformationMatrix = new_values
ds.DeformableRegistrationSequence[1].add_new([0x0064, 0x0010], "Test", [new_sequence])
pydicom.write_file(os.path.join(file_path, 'output.dcm'), ds)
xxx = 1