# DICOM validation report

Files: 53 RT Structure Set(s). Tools: dicom3tools 20260927; dicom-validator 0.9.0.
Gate: `dciodvfy` errors and `validate_iods --edition 2025b` errors must be allowlisted. Result: **PASS**.

- dciodvfy: 0 error(s), 4818 warning(s)
- validate_iods 2025b: 53 error(s), exit code 53
- validate_iods current (report only): 53 error(s), exit code 53

## Errors not in the allowlist

none

## Allowlisted errors

- `validate_iods` C9 x53: Module "RT ROI Observations": / (3006,0080) (RT ROI Observations Sequence): / Tag (3006,0085) (ROI Observation Label) is unexpected

## Warnings (not gated)

- `dciodvfy` x53: </PatientName(0010,0010)[n]> - Value dubious for this VR [PN] = <MASKED> - Retired Person Name form
- `dciodvfy` x1178: </RTROIObservationsSequence(3006,0080)[n]/ROIInterpreter(3006,00a6)[n]> - Value dubious for this VR [PN] = <Brian_Mark_Anderson> - Retired Person Name form
- `dciodvfy` x1178: </RTROIObservationsSequence(3006,0080)[n]/RTROIIdentificationCodeSequence(3006,0086)[n]/MappingResource(0008,0105)[n]> - Unrecognized defined term = <99VMS>
- `dciodvfy` x1178: </RTROIObservationsSequence(3006,0080)[n]/RTROIIdentificationCodeSequence(3006,0086)[n]/MappingResourceName(0008,0122)[n]> - Unrecognized defined term = <Varian Medical Systems>
- `dciodvfy` x1178: </RTROIObservationsSequence(3006,0080)[n]/RTROIIdentificationCodeSequence(3006,0086)[n]/MappingResourceUID(0008,0118)[n]> - Unrecognized defined term = <1.2.246.352.7.1.1>
- `dciodvfy` x53: </ReferringPhysicianName(0008,0090)[n]> - Value dubious for this VR [PN] = <Unknown> - Retired Person Name form

## Current-edition errors (report only)

- x53: Module "RT ROI Observations": / (3006,0080) (RT ROI Observations Sequence): / Tag (3006,0085) (ROI Observation Label) is unexpected
