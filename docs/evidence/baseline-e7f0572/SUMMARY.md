# Baseline of `e7f0572`: snapshot sites, test RTs and inventories

Plan steps P1.5 and P1.6 (`docs/OVERHAUL_PLAN.md`, protocol section 1.3). These files are a
**characterisation** of what the program does today, not a statement that it is correct. Produced
2026-10-15 with `TemplateEvaluationTool` at `c96ce25` (clean tree; `gitDirty: false` in every manifest).

## What was run

```
dotnet run --project TemplateEvaluationTool -c Release -- build-sites --snapshot TemplateSnapshots/TG263.json --naming default --data-dir $OUT/data --out $OUT/sites
dotnet run --project TemplateEvaluationTool -c Release -- generate-rts --sites $OUT/sites --images DicomTemplateMakerGUI/SmallCT --out $OUT/rt
dotnet run --project TemplateEvaluationTool -c Release -- inventory --in $OUT/rt --out $OUT/inventory
```

| Input | SHA-256 |
| --- | --- |
| `TemplateSnapshots/TG263.json` (generated 2026-09-30, last changed in `b185504`), as read on Windows with CRLF (184,255 bytes; this is what `build-sites/manifest.json` records) | `059cd49e731fcc564ff36444ff7b9761fd43fd08ddae4448e7e383d51eac03c4` |
| The same file as committed, LF (177,534 bytes; a Linux checkout) | `94d0fbf4d6d7548fc5810050f04ec864c7af129766918c6511b9ffe3faa86d71` |
| `DicomTemplateMakerGUI/template_RS.dcm` (the seed every RT is cloned from) | `22402e0ee49e116e6437ffa6bf1d8b8e32400e2d0b2b0ea39cc0761e6abc6ed6` |
| `DicomTemplateMakerGUI/SmallCT` (4 slices, Explicit VR LE, no Specific Character Set) | per file in `generate-rts/manifest.json` |

**Why this is the behaviour of `e7f0572`.** Between `e7f0572` and `c96ce25` the RT generation code
(`DicomTemplateCore`, `ROIOntologyClass`, `TemplateSync`) is unchanged except for a new overload in
`TemplateEntryMapper` that reports each warning's kind with the same message. The site building moved from
`OnlineTemplatesViewModel` to `SiteBuilder`; the characterisation test
`Snapshot_sites_built_with_the_default_naming_choice_match_the_e7f0572_characterisation` hashes three sites
built through the window and was written and recorded before the move, and
`SiteBuilderTests.The_default_naming_choice_over_the_snapshot_builds_what_the_window_builds` shows the tool's
path gives the same files. The default naming choice is what the window does when nothing is changed: English,
side last, with the language and laterality choices visible because some site offers them
(`build-sites/sites.json`, `naming`).

## Layout

Each command writes its own `manifest.json` (git SHA, tool version, arguments, inputs with SHA-256, UTC time),
so the files are grouped by command:

| Folder | Files |
| --- | --- |
| `build-sites/` | `build-warnings.json` (every warning, uncapped), `sites.json` (per-site counts and the naming choice), `manifest.json` |
| `generate-rts/` | `run-report.json` (`RunReport` per site, see masking below), `manifest.json` |
| `inventory/` | one `<site>.txt` per RT (53), `manifest.json` (the RT files with their SHA-256) |

The built template folders, the RTs and the copied CT stay outside the repository.

## Masking and reproducibility

Inventory lines are `(gggg,eeee) Keyword VR VM len value`, file meta first, sequence items indented as
`(FFFE,E000) Item n`. A sequence shows its item count as VM and `-` as length (fo-dicom keeps no sequence
length after parsing). Masked values show `*` as length; every other length is the stored value length
including padding.

- Each distinct UID becomes `UID#n` in order of first appearance within the file, so equalities stay visible
  (for example `SOPInstanceUID` = `MediaStorageSOPInstanceUID` = `UID#1`, the study UID = the referenced study
  UID = `UID#2`). **Deviation:** UIDs under the DICOM root `1.2.840.10008` (SOP classes, transfer syntaxes)
  identify nothing and are kept, followed by fo-dicom's name, because C6 cannot be read otherwise.
- Non-empty DA, TM and DT values become `<DATE>`, `<TIME>`, `<DATETIME>` (this includes the code items'
  `ContextGroupVersion`, which is the constant `20161209` from `OntologyCodeClass.cs:297`).
- `<MASKED>`: Implementation Class UID, Implementation Version Name, **every** attribute of group 0010 (the
  four named ones plus Patient Birth Time, which SmallCT also has) and every PN attribute copied from the images
  (`ReferringPhysicianName`). `ROIInterpreter` is a constant of the program, not personal data, and stays visible
  because it is gap C4.
- **Deviation:** `FileMetaInformationGroupLength` shows `<DERIVED>`: it is the sum of the file meta lengths and
  so revealed the masked UID lengths (it differed between two runs before this rule).
- Empty values stay empty with length 0, so a Type 2 attribute that is present but empty remains visible.
- `run-report.json`: the output folder is `<out>`, UIDs (also inside file names) are `UID#n` numbered within
  that file, `StartedAt` and `Duration` are masked.

Two runs on the same inputs give byte-identical `build-warnings.json`, `sites.json`, `run-report.json` and
inventories although the RTs differ (new UIDs and times): `PipelineTests.Two_runs_on_the_same_inputs_give_identical_outputs_although_the_rts_differ`.
Only the manifests differ (`createdUtc`, scratch paths).

**Privacy check before committing.** The SmallCT patient name (in `^` and space form) and patient ID were read
from the CT and searched for, case-insensitively, in every file of this folder: no match. The test
`PipelineTests.No_output_carries_the_sample_ct_patient_name_or_id` repeats this on every run without writing the
values anywhere.

## Sites: built and RT ROI counts

All 53 sites were built (no site name fails `IsUsableFolderName`) and each produced one RT; no RT reported an
ROI failure or error. `build-warnings.json` is empty: no record of the snapshot triggers any of the seven warning
kinds (see "Other observations" for what this misses). The RT ROI count equals the built count for every site,
and equals the item count of the inventory's Structure Set ROI Sequence; in total 1,178 ROIs, which is exactly
the snapshot's 1,178 site memberships (608 Recommend, 570 Consider).

| Site | Recommend | Consider | Built ROIs (harness) | RT ROIs (run report) | StructureSetLabel |
| --- | ---: | ---: | ---: | ---: | --- |
| `AbdPelv_Anal` | 11 | 11 | 22 | 22 | same |
| `AbdPelv_Bladder` | 9 | 16 | 25 | 25 | same |
| `AbdPelv_Gyn` | 7 | 12 | 19 | 19 | same |
| `AbdPelv_Liver` | 13 | 20 | 33 | 33 | same |
| `AbdPelv_Pancreas` | 13 | 14 | 27 | 27 | same |
| `AbdPelv_Paraaortic` | 9 | 14 | 23 | 23 | `AbdPelv_Paraaort` (truncated) |
| `AbdPelv_ProstBed` | 10 | 13 | 23 | 23 | same |
| `AbdPelv_Prostate` | 11 | 14 | 25 | 25 | same |
| `AbdPelv_Rectum` | 8 | 13 | 21 | 21 | same |
| `AbdPelv_RenalAdrenal` | 11 | 14 | 25 | 25 | `AbdPelv_RenalAdr` (truncated) |
| `AbdPelv_RetroperitonealSpace` | 11 | 11 | 22 | 22 | `AbdPelv_Retroper` (truncated) |
| `AbdPelv_Spleen` | 9 | 15 | 24 | 24 | same |
| `AbdPelv_Stomach` | 15 | 13 | 28 | 28 | same |
| `Brachy_Breast` | 22 | 0 | 22 | 22 | same |
| `Brachy_Endobronch` | 9 | 0 | 9 | 9 | `Brachy_Endobronc` (truncated) |
| `Brachy_Gyn` | 22 | 2 | 24 | 24 | same |
| `Brachy_Ocular` | 22 | 0 | 22 | 22 | same |
| `Brachy_Prostate` | 29 | 0 | 29 | 29 | same |
| `Brachy_Skin` | 10 | 0 | 10 | 10 | same |
| `CNS_Brain` | 14 | 20 | 34 | 34 | same |
| `CNS_Cervical` | 4 | 14 | 18 | 18 | same |
| `CNS_Craniospinal` | 22 | 14 | 36 | 36 | same |
| `CNS_Lumbar` | 6 | 5 | 11 | 11 | same |
| `CNS_Sacral` | 2 | 10 | 12 | 12 | same |
| `CNS_Thoracic` | 7 | 7 | 14 | 14 | same |
| `Extremities` | 6 | 13 | 19 | 19 | same |
| `H&N_Face_Parotid` | 14 | 21 | 35 | 35 | same |
| `H&N_LX_HPX_Thyroid` | 16 | 19 | 35 | 35 | `H&N_LX_HPX_Thyro` (truncated) |
| `H&N_Nasopharynx` | 29 | 13 | 42 | 42 | same |
| `H&N_Neck` | 12 | 20 | 32 | 32 | same |
| `H&N_Orbit` | 15 | 16 | 31 | 31 | same |
| `H&N_Oropharynx` | 16 | 24 | 40 | 40 | same |
| `H&N_Sinonasal` | 18 | 15 | 33 | 33 | same |
| `Thorax_AxillaSCN` | 7 | 11 | 18 | 18 | same |
| `Thorax_BreastPBI` | 12 | 0 | 12 | 12 | same |
| `Thorax_BreastRNI` | 22 | 6 | 28 | 28 | same |
| `Thorax_BreastWBI` | 15 | 0 | 15 | 15 | same |
| `Thorax_CWRNI` | 17 | 9 | 26 | 26 | same |
| `Thorax_Esoph` | 11 | 14 | 25 | 25 | same |
| `Thorax_Esoph_GEJ` | 16 | 9 | 25 | 25 | same |
| `Thorax_Esoph_S` | 5 | 24 | 29 | 29 | same |
| `Thorax_Lung` | 12 | 16 | 28 | 28 | same |
| `Thorax_Mediastinum_Thymus` | 11 | 12 | 23 | 23 | `Thorax_Mediastin` (truncated) |
| `_Miscellaneous` | 1 | 7 | 8 | 8 | same |
| `_Targets_1Level` | 3 | 5 | 8 | 8 | same |
| `_Targets_2Levels` | 5 | 5 | 10 | 10 | same |
| `_Targets_3Levels` | 7 | 5 | 12 | 12 | same |
| `_Targets_4Levels` | 9 | 5 | 14 | 14 | same |
| `_Targets_Basic` | 3 | 14 | 17 | 17 | same |
| `_Targets_Breast` | 7 | 6 | 13 | 13 | same |
| `_Targets_CW` | 4 | 9 | 13 | 13 | same |
| `_Targets_Details` | 3 | 20 | 23 | 23 | same |
| `_Targets_RNI` | 6 | 0 | 6 | 6 | same |

The eight sites with no Consider ROIs are exactly the protocol's Recommend-only sites (`Brachy_Breast`,
`Brachy_Endobronch`, `Brachy_Ocular`, `Brachy_Prostate`, `Brachy_Skin`, `Thorax_BreastPBI`, `Thorax_BreastWBI`,
`_Targets_RNI`).

## A.4 gaps C1-C13 in the baseline

"All 53" means the tag and value appear in every inventory. Tags are as printed in the inventories.

| Gap | Where it appears | Tags and values |
| --- | --- | --- |
| C1 Varian context group on SNOMED codes | All 53; all 1,178 code items | Every RT ROI Identification Code item has `(0008,0102)` `SCT` together with `(0008,0105) MappingResource 99VMS`, `(0008,0106) ContextGroupVersion` (`20161209`, masked), `(0008,010F) ContextIdentifier VMS011`, `(0008,0117) ContextUID`, `(0008,0118) MappingResourceUID` (Varian roots `1.2.246.352.7.2.11` and `1.2.246.352.7.1.1`, masked as `UID#n`), `(0008,0122) MappingResourceName Varian Medical Systems`. The snapshot has none of these columns: the values are the defaults of `OntologyCodeClass` (`OntologyCodeClass.cs:297-302`). |
| C2 interpreted types outside the 25 terms | None | All `(3006,00A4)` values are defined terms: ORGAN 638, CTV 194, PTV 180, GTV 104, EXTERNAL 44, MARKER 8, AVOIDANCE 4, IRRAD_VOLUME 3, BRACHY_SRC_APP 2, CONTROL 1. The snapshot's one `Control` (`zPTVopt`, `Brachy_Breast`) is written `CONTROL` by `ToUpperInvariant` (`RtStructureBuilder.cs:438`). `BODY`/`NONE` come only from Varian imports, which this baseline does not exercise. |
| C3 SEMIAUTOMATIC on contour-less ROIs | All 53; all 1,178 ROIs | `(3006,0036) ROIGenerationAlgorithm SEMIAUTOMATIC` in every Structure Set ROI item; no ROI has contours. |
| C4 hard-coded identity | All 53 | `(0008,0070) Manufacturer UCSD Residency`, `(0008,1090) ManufacturerModelName Universal_RT_Creator`, `(3006,00A6) ROIInterpreter Brian_Mark_Anderson` in all 1,178 observation items. |
| C5 seed inheritance | All 53 | From the seed: `(0018,1020) SoftwareVersions Hello_from_Brian_M_Anderson`, `(0008,103E) SeriesDescription Anonymized`, `(0020,0011) SeriesNumber 1`, `(0020,0013) InstanceNumber 1`, `(0008,1070) OperatorsName` empty, `(300E,0002) ApprovalStatus UNAPPROVED`, `(0020,1040) PositionReferenceIndicator` empty (the CT has `SN`). Absent: `(3006,0004) StructureSetName`, `(0008,0080) InstitutionName`, `(0008,1010) StationName`. From the images: `(0008,1030) StudyDescription Visible Human Male`, `(0008,0050) AccessionNumber 1`, `(0020,0010) StudyID 1`, study date and time, `ReferringPhysicianName` and the patient module (masked). |
| C6 transfer syntax by accident | All 53 | `(0002,0010) TransferSyntaxUID 1.2.840.10008.1.2` (Implicit VR Little Endian, the seed's) although SmallCT is Explicit VR Little Endian. `(0008,0005) SpecificCharacterSet ISO_IR 100` because SmallCT declares none (`RtStructureBuilder.cs:129-135`). |
| C7 label = first 16 characters; no name | 6 sites truncated; name absent in all 53 | `(3006,0002) StructureSetLabel`: `AbdPelv_Paraaort`, `AbdPelv_RenalAdr`, `AbdPelv_Retroper`, `Brachy_Endobronc`, `H&N_LX_HPX_Thyro`, `Thorax_Mediastin` (all distinct; table above). `(3006,0004) StructureSetName` never written. |
| C8 no scheme version, scheme identification or long code value | All 53 (by absence) | No `(0008,0103) CodingSchemeVersion`, no top-level `(0008,0110) CodingSchemeIdentificationSequence`, no `(0008,0119) LongCodeValue`. No code value exceeds 16 characters, so the missing `LongCodeValue` path is not triggered. |
| C9 retired ROI Observation Label | All 53; truncated in 2 | `(3006,0085) ROIObservationLabel` in every observation item; `PTV_ChestWall_Eval` becomes `PTV_ChestWall_Ev` in `Thorax_CWRNI` and `_Targets_CW`. |
| C10 `interpret_RT` intolerant of minimal code items | Not observable in an inventory | Every one of the 1,178 code items carries all nine attributes `interpret_RT` reads, so this program's own snapshot RTs can seed templates; the gap concerns other vendors' RTs and bare-SCT items (P2.2). |
| C11 Consider ROIs written | 45 sites, 570 ROIs | Every Consider (`Include=false`) ROI is in the RT exactly like a Recommend ROI; nothing in the RT distinguishes them (no ROI Description). Per-site counts in the table above. |
| C12 duplicate names; names over 16 characters | No duplicates; 1 long name in 2 sites | No RT has two `(3006,0026) ROIName` values equal ignoring case and trailing spaces. `PTV_ChestWall_Eval` (18 characters) in `Thorax_CWRNI` and `_Targets_CW`. Of the snapshot's 7 names over 16 characters only this one is in a site. |
| C13 Frame of Reference inherited | All 53 | `(0020,0052) FrameOfReferenceUID` equals the one in `(3006,0010) ReferencedFrameOfReferenceSequence` and every `(3006,0024) ReferencedFrameOfReferenceUID` (`UID#4` throughout), so the equality holds today; `(0020,1040) PositionReferenceIndicator` is present and empty (the seed's), while the CT says `SN`. |

## Other observations (not C-ids)

- **Non-SNOMED codes pass silently.** Seven code items carry `SCT` with values that are not SCTIDs: `88` on
  `zPTV_High`, `zPTV_Mid` (`AbdPelv_Pancreas`, `_Targets_Details`), `zPTVopt` (`Brachy_Breast`), `Eye_Origin_L`,
  `Eye_Origin_R` (`Brachy_Ocular`); `99` on `Fid`, `Hi HU` (`_Miscellaneous`). No build warning reports them
  (`IncompleteCode` only checks that a value and a scheme exist). WS1 item 6 / P4.2.
- **U+02C6 becomes `^`.** The snapshot names `CTV_HighˆLN_3cm+` and `CTV_MidˆLN_3cm-` (`AbdPelv_Anal`, Consider)
  stay as U+02C6 in `All_ROIs.json`, but the RT has `CTV_High^LN_3cm+` and `CTV_Mid^LN_3cm-`. This is deliberate
  (`RtRoiName`, `RtStructureBuilder.cs:295-297`), and the character-set guard (`CanEncode`) never sees the
  original; a template read back from this RT will not match the snapshot name.
- `ITV` is typed `CTV` with code `1201745009` in 10 sites (snapshot data, not a program defect).
- `(0008,1150) ReferencedSOPClassUID` in the RT Referenced Study Sequence is `1.2.840.10008.3.1.2.3.1`,
  Detached Study Management, a retired SOP class (`RtStructureBuilder.cs:19, :386`); for P1.7 to judge.
- `build-warnings.json` is empty, so the warning kinds as defined today say nothing about this snapshot; the
  publication gate (P1.4) and the nomenclature report (P1.8) are where its defects (duplicate `_TS_CTVsb`, the
  non-SCTID codes, the 16-character limit) will be reported.

## Cross-check of one inventory (`_Targets_1Level`)

- **pydicom 3.0.2.** An independent re-implementation of the inventory format with pydicom (same masking
  rules, lengths from pydicom's raw elements) was diffed against the tool's output for `_Targets_1Level`,
  `AbdPelv_Anal`, `H&N_Nasopharynx` and `Thorax_BreastRNI`. Every element, VR, VM, value, item and nesting level
  agrees. The remaining differences are formatting: pydicom names two standard UIDs differently ("Implicit VR
  Little Endian" without fo-dicom's suffix; "Detached Study Management SOP Class" without "(Retired)"), and
  pydicom has no raw length for the three elements it decodes while reading (group length, transfer syntax,
  character set), whose lengths (4, 18, 10) are what the encoding implies. The first diff found one tool defect,
  now fixed in `c96ce25`: the File Meta Information Version (OB) was printed with VM 2 (fo-dicom counts bytes)
  instead of 1.
- `python -I -m pydicom show` does not run with pydicom 3.0.2 ("No module named pydicom.__main__"); its
  equivalent, the `pydicom show` command (`pydicom.cli.main`), lists 266 elements for this RT, the same 266
  lines as the inventory (plus its two separator lines). Its output contains the patient name and was not kept.
- `dcmdump +L` (protocol 1.3) was not run: DCMTK is not installed here. Left for the validator stage (P1.7,
  P2.3), which installs pinned tools in CI.
