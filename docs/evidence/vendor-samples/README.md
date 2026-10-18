# Vendor RT Structure Set samples

Element inventories of RT Structure Sets written by planning systems, kept as evidence of what those systems
write. The DICOM files themselves are not kept in the repository.

## `aria-rtm-16.1-rtstruct.txt`

An RT Structure Set exported from ARIA RTM 16.1 (Varian Medical Systems, `SoftwareVersions 16.1.137.0`), with two
ROIs (`Eye_R`, `Eye_L`) and no contours. It was previously committed as `DicomTemplateMakerGUI/Test.dcm`, which no
code or project referenced, and was removed because it carried institution and operator details. Captured on
2026-10-18 with

```
dotnet run --project TemplateEvaluationTool -c Release -- inventory --in <folder holding only Test.dcm> --out <out>
```

(input SHA-256 `4dc1097c222d2e9b11802a08b586a582b465a3df3354b5108ae31e86306246dd`). The line format and the
tool's masking are described in [`../baseline-e7f0572/SUMMARY.md`](../baseline-e7f0572/SUMMARY.md#masking-and-reproducibility).

**Masked.** The tool masks patient (group 0010) values, UIDs, dates and times, `OperatorsName`, the implementation
class UID and the implementation version name. After capture, the values of `InstitutionName`,
`InstitutionAddress`, `StationName` and `DeviceSerialNumber` were also replaced with `<MASKED>` (length `*`).
Person attributes that were empty in the export (`ReferringPhysicianName`, `ROIInterpreter`) are left empty, as
written; `PerformingPhysicianName` and `PhysiciansOfRecord` are absent.

**Conventions it shows.**

- Implicit VR Little Endian transfer syntax (`1.2.840.10008.1.2`) and `SpecificCharacterSet` `ISO_IR 192`.
- `ROIGenerationAlgorithm` `MANUAL` for both ROIs, although neither has contours (the ROI Contour Sequence items
  hold only `ROIDisplayColor` and `ReferencedROINumber`, no Contour Sequence).
- ROI identification codes from FMA (`CodingSchemeDesignator FMA`, `CodingSchemeVersion 3.2`, for example
  `12514` "Right eyeball"), each with `ContextIdentifier VMS011`, `MappingResource 99VMS` and
  `MappingResourceName` "Varian Medical Systems".
- A top-level Coding Scheme Identification Sequence (one item: `FMA` with a coding scheme UID), next to top-level
  Context Group Identification and Mapping Resource Identification Sequences for `VMS011` / `99VMS`.
- No `ROIObservationLabel` (3006,0085); both observations have `ObservationNumber` `0`, `RTROIInterpretedType`
  `ORGAN` and an empty `ROIInterpreter`.
- `ApprovalStatus` `UNAPPROVED`; the Contour Image Sequence lists 136 CT images.
