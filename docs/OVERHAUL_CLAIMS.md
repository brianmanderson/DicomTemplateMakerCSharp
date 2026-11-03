# Overhaul claims register

Every vendor or standards claim that `OVERHAUL_PLAN.md`, `EVALUATION_PROTOCOL.md` and
`OVERHAUL_DECISIONS.md` rely on (part 1), and the re-check status of every fact taken from the
appendices of `docs/OVERHAUL_PLANNING_PROMPT.md` (part 2). Checked 2026-10-15.

Confidence: **High** = read in the primary source (standard, vendor document, package page, or the
file itself) during this session or labelled High in the prompt and not contradicted; **Medium** =
secondary or community source, or a vendor document not re-read; **unknown** = no source found.
"Confirm by" names the plan step (document, export or import test) that must settle the claim before
code depends on it; "-" means the source was read and no further check is needed.
`D` abbreviates `https://dicom.nema.org/medical/dicom/current/output/chtml/` (pages rendered as
edition 2026d).

## Part 1. Vendor and standards claims

### Standards

| Id | Claim | Source | Conf. | Used by | Confirm by |
| --- | --- | --- | --- | --- | --- |
| S1 | ROIs without contours are permitted: Structure Set ROI Sequence and RT ROI Observations Sequence are Type 3 from 2025c, ROI Contour Sequence from 2025d (all Type 1 through 2025b); a structure set may be used without structures (A.19.1); change made by CP-2445 | `D/part03/sect_C.8.8.5.html`, `sect_C.8.8.6.html`, `sect_C.8.8.8.html`, `sect_A.19.html`; https://dicom.nema.org/dicom/news/June2025/docs/cpack131/cp2445.pdf | High | D5, P2.3 (2025b pin), RR1 | - |
| S2 | Frame of Reference module is U in the RT Structure Set IOD (Table A.19.3-1, note 2: added 2014b) | `D/part03/sect_A.19.3.html` | High | P4.6, RR10 | - |
| S3 | RT ROI Observations (Table C.8-44): Segmented Property Category (0062,0003) Type 3, one item, CID 9580; RT ROI Identification Code Sequence (3006,0086) Type 3, **one item only**; CID 9580 maps (130041, DCM, "RT Target") to CID 9534 and includes CID 7150; Table C.8.8.8.3.1-1 has rows only for EXTERNAL (130047, CID 9507), MARKER (130666, CID 7112), BRACHY_SRC_APP (130045, 130078); Table C.8.8.8.3.1-2 is "Therapeutic Role Code Mapping" into (3010,0064)/(3010,0065), both Type 3. Correction: the prompt read Table -2 as a category/identification mapping | `D/part03/sect_C.8.8.8.html`, `D/part03/sect_C.8.8.8.3.html`, `D/part16/sect_CID_9580.html`, `D/part16/sect_CID_9534.html` | High | D1, P4.3 | CID 7150 read for the anatomy category (P4.3) |
| S4 | 25 RT ROI Interpreted Type defined terms (EXTERNAL, PTV, CTV, GTV, TREATED_VOLUME, IRRAD_VOLUME, OAR, BOLUS, AVOIDANCE, ORGAN, MARKER, REGISTRATION, ISOCENTER, CONTRAST_AGENT, CAVITY, BRACHY_CHANNEL, BRACHY_ACCESSORY, BRACHY_SRC_APP, BRACHY_CHNL_SHLD, SUPPORT, FIXATION, DOSE_REGION, CONTROL, DOSE_MEASUREMENT, DEVICE); the attribute is Type 2 | `D/part03/sect_C.8.8.8.html` | High | D4, P4.4, RR5 | - |
| S5 | Code Sequence Macro: Code Value 1C (16 characters or fewer, else Long Code Value or URN Code Value); Coding Scheme Version 1C (only when the designator is ambiguous); Context Identifier 3; Mapping Resource and Context Group Version 1C on Context Identifier | `D/part03/sect_8.8.html` (Tables 8.8-1, 8.8-1a, 8.8-1b) | High | D2, P4.1, RR8, RR9 | - |
| S6 | Code Meaning shall never be used as a key (PS3.3 section 8.3) | `D/part03/sect_8.3.html` | High | D1 | - |
| S7 | PS3.16: `SCT` and `SRT` share 2.16.840.1.113883.6.96; `SRT` holds SNOMED-RT-style values and is deprecated (not retired); `FMA` is 2.16.840.1.113883.6.119; local designators start with "99"; Annex O maps codes used in DICOM. CP-2533 ("Coding Schemes have wrong UIDs") and CP-2607 (SNOMED meanings) are in CPack 138 | `D/part16/chapter_8.html#table_8-1`, `D/part16/chapter_O.html` | High | D2, D17, P4.1 | Read CP-2533 before P4.1 writes scheme UIDs |
| S8 | CP-2608 adds Tracking ID (0062,0020) and Tracking UID (0062,0021) per Structure Set ROI item; Letter Ballot, CPack 138, target 2026e | https://www.dicomstandard.org/news-dir/current/docs/cpack138/cp2608.pdf | High (status) | P7.1 (stable ROI id) | Status check when P7.1 starts |
| S9 | SH 16 characters, LO 64, PN 64 per group; Implicit VR LE = 1.2.840.10008.1.2; DS has a 16-bit length in Explicit VR, so multi-valued DS over 65,534 bytes cannot be encoded there | `D/part05/sect_6.2.html`, `D/part05/chapter_7.html#sect_7.1.2` | High | D14, RR6, RR7 | - |
| S10 | IHE-RO has no structure-template or naming profile; BRTO-II is Final Text in TF Rev 4.0 (2026-05-13); Vol 3 7.3.4.1.1 "RT Structure Set for Basic Interoperability" and 7.4.8; requires at least one observation with interpreted type per ROI; HDSS is a public-comment draft | https://profiles.ihe.net/RO/index.html; https://www.ihe.net/uploadedFiles/Documents/Radiation_Oncology/IHE_RO_TF_Vol3.pdf | High | RR1, RR12 | Transcribe the content module in P2.4 |
| S11 | The TG-263 report says a vendor-neutral template format is in development as DICOM Supplement 196; Sup 196 is "Segmentation Creation Template" (ROI Template Storage), WG-07 draft Rev 06 of 2016-11-04, absent from the final supplements list. Was "unknown" in the prompt | https://www.aapm.org/pubs/reports/RPT_263.pdf; https://www.dicomstandard.org/News-dir/ftsup/docs/sups/sup196.pdf | High | D18, P7.7 | Ask WG-07 about status (P7.7) |
| S12 | HL7 mCODE `TG263CM` ConceptMap: Draft, STU4 v4.0.0 (2025-02-16), some concepts unmapped | https://hl7.org/fhir/us/mcode/ConceptMap-TG263CM.html | High | P1.8 (optional cross-check) | - |
| S13 | Supplement 147 (RT Segment Annotation) is final (2018d) and in 2026d; no toolkit or TPS implementation found; SEG absent from the RayStation and ProKnow statements | https://www.dicomstandard.org/News-dir/ftsup/docs/sups/sup147.pdf; `D/part03/sect_A.86.html` | High (status), Medium (no implementation) | P7.7 | - |
| S14 | AAPM TG-263 supplemental page links `TG263_Nomenclature_Worksheet_20170815.xls` and `EclipseStructureTemplates.zip` (7,851 bytes, Last-Modified 2018-06-04); the page states no licence and no date | https://www.aapm.org/pubs/reports/RPT_263_Supplemental/default.asp | High | D21, P1.8, P6.1 | - |
| S15 | AAPM Report 263 (January 2018, DOI 10.37206/171; executive summary IJROBP 2018;100(4):1057-66, doi:10.1016/j.ijrobp.2017.12.013) defines 717 structures and the naming rules (16 characters or fewer, case-insensitive uniqueness, prefixes); its "label from the Structure Dictionary" passage refers to ARIA 13.x. Correction: not a Med Phys article | https://www.aapm.org/pubs/reports/RPT_263.pdf | High (source), Medium (rule details not re-read) | P1.8, RR6, D17 | Re-read the naming-rule section when writing P1.8 |
| S16 | Reuse of SNOMED CT concept meanings outside PS3.16 is licence-bound | none found | unknown | D1, P4.3 | Read PS3.16's SNOMED notice and SNOMED International's terms before curating meanings |
| S17 | Equivalent Code Sequence (0008,0121), Type 3, one or more items, "codes considered equivalent by the creating system", is part of the Code Sequence Macro (Table 8.8-1) | `D/part03/sect_8.8.html` | High | D1, P4.3 | - |
| S18 | Structure Set module (Table C.8-41): ROI Description (3006,0028) Type 3 and ROI Generation Algorithm (3006,0036) Type 2 per ROI; Structure Set Label Type 1; Structure Set Name and Description Type 3 | `D/part03/sect_C.8.8.5.html` | High | D5, D12, P4.5, P4.8 | - |
| S19 | A PS3.17 worked example codes a target as Therapeutic Role with the anatomy as identification code | not located this session | unknown | D1 (alternative) | Locate in PS3.17 before choosing that pattern (P7.1) |

### Varian

| Id | Claim | Source | Conf. | Used by | Confirm by |
| --- | --- | --- | --- | --- | --- |
| V1 | Current StructureTemplate exports are Version 1.2 with `Preview Version="1.2"` | https://github.com/Arbetsgrup-inom-Programmering-i-Eclipse/StrukturMall (`Strukturmallar/Lunga.xml`); https://github.com/csarux/rtpros (`protocolos/contorneo/OARs Torax SBRT Task G.xml`); `Xaml_Maker/Abdomen.xml` | High | D16, P6.4 | Release that introduced 1.2 and 18.x output: fresh export (P6.1) |
| V2 | Each scheme carries its own `CodeSchemeVersion`: FMA 3.2 and 99VMS_STRUCTCODE 1.0 (rtpros template: 17 FMA at 3.2, 3 99VMS at 1.0; our exports); RADLEX 3.8 only in our 2022 file; no SRT code in 51 GitHub-indexed 1.2 templates | rtpros template above; GitHub code search | High (FMA, 99VMS), Medium (RADLEX, "no SRT") | P6.4 | Fresh export (P6.1), dictionary versions (P6.2) |
| V3 | `ColorAndStyle` values in the wild: names, `RGB255192203`, `RGB  0255191`, `RGB 0 0 255`, style-prefixed names truncated near 16 characters | prompt B.1 (High); not re-read | High (taken on trust) | P6.4 | Corpus characterisation (P6.1) |
| V4 | Code-less structures keep `<Identification>` with empty `VolumeID`, `VolumeCode`, `VolumeCodeTable` and a `VolumeType`; StrukturMall templates have empty codes and are applied through ESAPI | StrukturMall repository | High (shape), Medium (XML import of such files) | D17, P6.4 | One-structure import (P6.3) |
| V5 | ESAPI 16.0-17.0: `Application.StructureCodes` returns dictionaries Fma, RadLex, Srt ("SNOMED RT structure scheme"), VmsStructCode; no SCT; `AddStructure(StructureCodeInfo)`; `AddStructure(dicomType, id)` accepts 14 types without OAR. Correction: the 15.6 documentation has only `AddStructure(dicomType, id)` | https://docs.developer.varian.com/api/17.0/VMS.TPS.Common.Model.API.ActiveStructureCodeDictionaries.html, `.../StructureSet.html`, `.../Application.html` | High | D4, D17, P6.2, P6.6 | 18.x behaviour (SCT?): P6.2, P6.3 |
| V6 | No ESAPI API creates or imports structure templates | prompt B.1 (High); not re-read | High (taken on trust) | D18, P6.6 | - |
| V7 | Codes chosen in the Eclipse UI or ESAPI must exist in the RT Administration Structure Dictionary; whether XML import validates codes is unknown | AAPM Report 263 (ARIA 13.x passage, S15); prompt B.1 | Medium | D17, P6.2 | One-structure SCT and SRT imports (P6.3) |
| V8 | Licences: StrukturMall MIT (created 2025-06-10); rtpros Apache-2.0 (Protocol 1.12 from CSV); Squint has no licence file (all rights reserved; "Paste XML as Classes" model for Eclipse V15); WUSTL ClinicalTemplateReader non-commercial research licence that bans redistribution | repository licence files: StrukturMall `LICENSE.txt`; rtpros `LICENSE`; https://github.com/NickChng/Squint; https://github.com/WUSTL-ClinicalDev/ClinicalTemplateReader | High | D21, P6.1 | - |
| V9 | `EclipseStructureTemplates.zip` holds five Version 1.1 templates (March 2018, 30-51 structures each) | one research pass (prompt) | unknown | P6.1 | Inventory the zip at test time (P6.1) |
| V10 | Varian publishes no TG-263 structure templates; the public XML downloads found are Ethos planning-directive templates | https://cancermedaffairs.siemens-healthineers.com/ethos-clinical-dir | Medium | P6.1 | - |
| V11 | Varian System Server 18.0 (April 2023) and 18.1 (October 2024) DICOM conformance statements are public. Contradicts the prompt (MyVarian-gated) | https://cancercare.siemens-healthineers.com/products/radiosurgery/interoperability/dicom-statements; https://varian.widen.net/s/n9pvdzglsx/varian_system_server_v18.1_dicomconformancestatement | High | D16, P5.3, P6.1 | Read for RTSTRUCT import rules (P6.1) |
| V12 | Third-party report (tomo_export PR #13, GPL-3.0, merged 2026-10-07, Eclipse 18.1): an RTSTRUCT referencing a CT UID not present is rejected; an empty `StructureSetLabel` is rejected; a 22-character label was truncated, not rejected; the fix writes `ROIGenerationAlgorithm MANUAL` | https://github.com/mwgeurts/tomo_export/pull/13 | Medium | D14, RR4, RR7 | Fixtures F3, F4 in P5.3 |
| V13 | What Eclipse writes in an RTSTRUCT: Implicit VR LE, `ISO_IR 192`, `ROIGenerationAlgorithm MANUAL` for contour-less ROIs, FMA 3.2 with `VMS011`, top-level Coding Scheme Identification Sequence | `DicomTemplateMakerGUI/Test.dcm` (re-read with pydicom; ARIA version taken on trust) | High (one file) | D2, D12, D14 | Fresh export (P6.1) |
| V14 | Template XML lives on the image server under `va_data$\programdata\vision\templates\...` | prompt B.1 | Medium (taken on trust, not relied on) | - | - |

### RayStation and other vendors

| Id | Claim | Source | Conf. | Used by | Confirm by |
| --- | --- | --- | --- | --- | --- |
| R1 | RayStation v2026 DCS (RSL-D-RS-v2026-DCS-EN-1.0, 2026-05-28): RTSTRUCT import needs one referenced image series, imported before or with it; Structure Set ROI and ROI Contour counts must match; at least one ROI; ROIs may be renamed on collision; (3006,0086) absent from the tables | https://prod-new.raysearchlabs.com/49f5d6/contentassets/254bfaf16de54a99a2ece381d4022aae/raystation-v2026-dicom-conformance-statement.pdf | High | RR1, RR2, P5.4 | Contour-less acceptance: F8 in P5.4 |
| R2 | Field Safety Notice #167168 (28 Nov 2025; 10B to v2025): dose invalidation can fail for contour-less ROIs with a material override or of type Bolus, Fixation or Support | https://laegemiddelstyrelsen.dk/da/udstyr/sikkerhedsmeddelelser/2025/12/raysearch-laboratories-ab-instruerer-i-sikker-og-korrekt-brug-af-raystation-/~/media/8954389F990646E2925EE8E7D11739A5.ashx | High | D5, F9 | - |
| R3 | Scripting: `case.PatientModel.CreateRoi(Name, Color, Type, ...)`, `OrganData.OrganType`, `LoadTemplatePatientModel`, `CreateStructuresFromTemplate`; no official public RaySearch scripting repository. `ImportDicomDataFromPath` and `ScriptableDicomExport` not verified | https://github.com/dicom/raystation-scripts/blob/master/functions/patient_model_functions.py (GPL-3.0); https://github.com/ArthurRochette/Raystation-stubs (MIT) | Medium (community) | P5.4, P7.3 | Script recorder and test run in P5.4 |
| R4 | Structure templates are database objects with no public file import or export | prompt B.2 (High) | High (taken on trust) | D18, P5.4 | Template-save experiment (P5.4) |
| R5 | RayStation exports always write `ROIGenerationAlgorithm SEMIAUTOMATIC` | R1 statement | High | D12 | - |
| O1 | Brainlab RT Elements 4.7 DCS Rev 10 (2026-06-10) 8.1.2.3.7: imports Segmented Property Category and RT ROI Identification Code Sequence, schemes SCT, SRT, FMA; exports SRT | https://assetmanagement.brainlab.com/Documents/00_2026_DICOM-ConformanceStatement-RTElements-Rev10.pdf | High | D1, P5.5 | Contour-less and ORGAN handling: F1, F8 in P5.5 |
| O2 | ProKnow DS: structure-set template CSV (`Name,Type,R,G,B`) and renaming rules exist, documentation gated in-app; Python SDK v0.23.0 has no structure-template module; the DS 2.0 DCS (E066741/04, 2024-08-19) says ProKnow uses no coded terminology | https://github.com/proknow/proknow-python (MIT); Elekta "DICOM Conformance Statement for ProKnow" PDF | unknown (CSV format), High (no codes) | D18, P7.2 | Sandbox download (P7.2) |
| O3 | Monaco: no structure-template format; 2017 statement lists EXTERNAL, ORGAN, BOLUS on import, SUPPORT on export; reads no codes; a 14-character name limit is reported | https://www.elekta.com/products/oncology-informatics/dicom-conformance-statements/ (statement not readable) | Medium | D4, P5.5 | Current statement via vendor contact (P7.7) |
| O4 | MIM accepts RT objects over the network only as Implicit VR LE | none found | unknown | D14 | MIM association test (P5.5) |
| O5 | Pinnacle: `.Script` adds ROIs with named colours; import renames invalid or duplicate names and strips a documented character list | Pinnacle R16.2 DCS (documents.philips.com, 2022-02-23: logs and warns only); https://github.com/sparkpoints/PinnacleTPSScripts (GPL-3.0) | Medium (named colours), unknown (renaming list) | P7.4 | Site with Pinnacle (P7.4) |
| O6 | Accuray Precision/iDMS: RTSTRUCT import only, no public DCS | prompt B.2 | Medium (taken on trust) | P5.5 | Vendor contact (P7.7) |
| O7 | Limbus DCS ENG-011 Rev 4 (May 2024, v1.8): placeholder ROIs without contours get `MANUAL`; one identification code item with a user-defined scheme, switchable | https://limbus.ai/DICOMConformance | High | D12 | - |
| O8 | Siemens AI-Rad Companion Organs RT VA60A DCS (June 2024): one code per configured organ from FMA, RadLex or 99VMS_STRUCTCODE; writes Mapping Resource 99VMS, version 20161209, context VMS011 | https://marketing.webassets.siemens-healthineers.com/c4977deaab3ebd4e/39f2ce454665/AI-Rad_Companion_Organs_RT_VA60A_DICOM_Conformance_Statement.pdf | High | D2 | - |
| O9 | Radformation `.structures`, MVision ConfigUI, Manteia, Mirada, Therapanacea, Carina, Varian Ethos templates have no public specification | prompt B.2 | Medium (taken on trust) | P7.7 | Vendor contact (P7.7) |
| O10 | wrssc `TG-263.xml`: GPLv3 (UW 2020, v1.0), 982 entries with name, FMAID (608 filled), interpreted type, colour | https://github.com/wrssc/ray_scripts/blob/master/protocols/TG-263.xml | High | D21, P7.6 | - |
| O11 | Gooding MJ, Koops A, Malone C, et al. "The impact of DICOM import/export on radiotherapy structures in commercial systems", Phys Imaging Radiat Oncol 2026;39:100981 (CC BY 4.0): two structure sets round-tripped through 12 systems | https://doi.org/10.1016/j.phro.2026.100981 | High | P5.3 method | - |

### Tooling and platforms

| Id | Claim | Source | Conf. | Used by | Confirm by |
| --- | --- | --- | --- | --- | --- |
| T1 | `dicom3tools` 20260927 on PyPI (wheels for Windows, macOS, Linux); tools report on stderr; dciodvfy returns 1 on error but output should be read | https://pypi.org/project/dicom3tools/ | High | P2.3 | Exit code checked empirically (protocol 3.2) |
| T2 | `dicom-validator` 0.9.0 (2026-09-09, MIT): `validate_iods --edition`; return value = number of errors | https://pypi.org/project/dicom-validator/; https://github.com/pydicom/dicom-validator | High | P2.3 | - |
| T3 | fo-dicom `DicomDataset.Validate()` checks items and throws `DicomValidationException`; not IOD validation | https://fo-dicom.github.io/stable/v5/api/FellowOakDicom.DicomDataset.html | High | P2.3 | - |
| T4 | DCMTK `dcmdump`/`drtdump` dump only; dcm4che `dcmvalidate` needs an IOD XML; DVTk LGPL with paid definition files | https://github.com/DCMTK/dcmtk; https://github.com/dcm4che/dcm4che/blob/master/dcm4che-tool/dcm4che-tool-dcmvalidate/README.md; https://www.dvtk.org/ | High | P1.6 | - |
| T5 | Orthanc Docker image `jodogne/orthanc` (maintained), REST `/instances/{id}/simplified-tags`; `python -m pynetdicom storescu` | https://orthanc.uclouvain.be/book/users/docker.html; https://orthanc.uclouvain.be/book/users/rest.html; https://pydicom.github.io/pynetdicom/stable/apps/storescu.html | High | P2.5 | - |
| T6 | dcmqi schemas (segment-context, anatomic-context) and validator; TotalSegmentator SNOMED mapping CSV; PyMedPhys has a TG-263 module but no `pymedphys-tg263` package (correction); UAB-RO TG263-Toolkit has no licence file, so it cannot be reused; `tg263` 0.2.0 (MIT, 2019); no 2023 NRG spreadsheet found | https://github.com/QIICR/dcmqi/tree/master/doc/schemas; https://github.com/wasserth/TotalSegmentator/blob/master/totalsegmentator/resources/totalsegmentator_snomed_mapping.csv; https://github.com/pymedphys/pymedphys; https://github.com/UAB-RO/TG263-Toolkit; https://pypi.org/project/tg263/ | High | P1.8, P4.13, P7.5 | - |
| T7 | GitHub Actions service containers need Linux runners | https://docs.github.com/en/actions/use-cases-and-examples/using-containerized-services/about-service-containers | Medium (not re-read) | P2.5 | The job definition itself (P2.5) |
| T8 | GitHub disables scheduled workflows in public repositories after 60 days without activity | https://docs.github.com/en/actions/managing-workflow-runs-and-deployments/managing-workflow-runs/disabling-and-enabling-a-workflow | Medium (not re-read) | P1.4 | - |

### Airtable

| Id | Claim | Source | Conf. | Used by | Confirm by |
| --- | --- | --- | --- | --- | --- |
| A1 | 5 requests/s per base; 50/s per user or service account across all its tokens (correction: not per token); wait 30 s after a 429; list pages of at most 100; 10 records per create or update request not found in the current pages | https://airtable.com/developers/web/api/rate-limits; https://airtable.com/developers/web/api/list-records | High (rates), Medium (10 per request) | P2.7, protocol 2.5 | Re-read create/update pages before P3.2 |
| A2 | Monthly caps per workspace: Free 1,000 (one-time 30-day grace, then blocked until the 1st), Team 100,000 (then 2 requests/s), Business and Enterprise Scale none; metadata calls count | https://support.airtable.com/docs/managing-api-call-limits-in-airtable | High | D7, protocol 2.5 | - |
| A3 | `typecast` creates select choices (Airtable-assigned colours); `update-field` documents name, description and formula options only, so it cannot add choices; `create-base` needs `workspaceId` and `schema.bases:write`; `create-field` accepts autoNumber and select choices with colours; the first field must be a valid primary type | https://airtable.com/developers/web/api/model/field-type; `.../update-field`; `.../create-base`; `.../create-field` | High | P3.1, protocol 2.1 | autoNumber as primary at P3.1 |
| A4 | `performUpsert` takes 1-3 `fieldsToMergeOn`; multiple matches fail; may be throttled differently | https://airtable.com/developers/web/api/update-multiple-records | High | D8 (not used) | - |
| A5 | Collaborators can duplicate a base (records and comments optional); synced tables keep a snapshot without sync; share-link "Copy base" needs the owner's toggle; workspace restrictions can block duplication; whether record ids change is not documented | https://support.airtable.com/articles/7403739720-creating-and-managing-airtable-bases; https://support.airtable.com/articles/7934312431-creating-airtable-base-share-links | High (record ids unknown) | D9, P1.9, P3.1 | Record ids compared at P3.1 |
| A6 | The error reference documents `NOT_FOUND` (404) and the rate-limit 429; `UNKNOWN_FIELD_NAME`, `INVALID_VALUE_FOR_COLUMN`, `INVALID_FILTER_BY_FORMULA`, `ROW_DOES_NOT_EXIST`, `MODEL_ID_NOT_FOUND` are not documented; the monthly-cap 429 body is unspecified | https://airtable.com/developers/web/api/errors | High (absence), unknown (shapes) | P3.2 | Observation run E1-E5 (P3.2) |
| A7 | Every API read needs a token or OAuth; no anonymous read | https://airtable.com/developers/web/api/authentication | High | D7 | - |
| A8 | `GET /v0/meta/whoami` returns the user id and the token's scopes | https://airtable.com/developers/web/api/get-user-id-scopes | Medium (not re-read) | D10, P0.5 | The call in P0.5 |
| A9 | A personal access token can create bases only in workspaces it has been granted, and a workspace grant covers every base in that workspace | https://airtable.com/developers/web/guides/personal-access-tokens; claim A3 (`create-base` needs `workspaceId`) | Medium (not re-read) | D7, P0.5 | Token-creation screen in P0.5; the refused read of `appYl68lenTOFqTDh` |

Prompt Appendix B items not relied on (taken on trust, unused): 3D Slicer, Weasis, OHIF and the
archived dicompyler (B.4); Orthanc Stone rejecting mismatched ROI sequence counts (B.4); TG-263U1
chartered through 2027 to publish French and Spanish names (B.3).

## Part 2. Status of the prompt's appendix facts

"Re-checked" = verified on 2026-10-15 at `e7f0572` (code read by three read-only agents and the
snapshot measured by script) or with the Airtable connector's read tools; "corrected" = re-checked
and found different; "trust" = taken on trust.

### A.1 Published snapshot

| Fact | Status |
| --- | --- |
| 246 records, 53 sites, 55 in neither | Re-checked (also 191 in some site, 1,178 memberships) |
| `Scheme = SCT` everywhere, no context values, Varian defaults back-filled (`AirTableEntry.cs:17-26, 62-68, 91-128`) | Re-checked; defaults also include Scheme "FMA" (`:19`) |
| Workflow excludes the six context columns (`refresh-template-snapshot.yml:50-66`) | Corrected lines: list at `:51`, passed at `:65`; also excludes `RGB`, `TG_263SpanishR`, `TG_263FrenchR` |
| Source has populated `FMAID` and an `Id` column outside the 26 mapped | Corrected: `FMAID` filled in 246 records but 82 "88", 10 "99", 20 labels; `Id` empty in all 246 |
| `CommonName` exported but empty | Re-checked (absent from every snapshot record; empty live) |
| Duplicate `_TS_CTVsb` (Red vs Navy Blue, no site); `TemplateIndex.cs:75` first-wins | Re-checked; also two select options of that name in the live `Structure` field |
| Two U+02C6 names; `/`, `^`, space names; 7 names and 6 sites over 16 characters; one `Control`; 7 non-SCTID codes; 40/41/14 shared target codes | Re-checked |
| Snapshot sorted by `Record`; live reads unsorted (`AirtableTableSource.cs:329`) | Re-checked (first and last records are `Record` 1 and 325) |
| No test loads `TG263.json`; shrink guard `TemplateSnapshotTool/Program.cs:52-56`; commits to `main`; step exits 0 without the secret | Re-checked; the tool itself exits 2 without a token |
| `main` unprotected; cron disabled after 60 idle days | Trust (T8) |

### A.2 Data model

| Item | Status |
| --- | --- |
| 1 Code-meaning drift (`TemplateMaker.cs:486-497, 303-319`) | Re-checked; `interpret_RT` re-points by value too (`:145-152`) |
| 2 Varian context written (`RtStructureBuilder.cs:424-433`) | Corrected: only three attributes are gated on Context Identifier; Context UID, Mapping Resource Name and UID are written whenever set (`:431-433`) |
| 3 Interpreted type free text; `InterpretedTypes.cs` 22 terms, editors only | Re-checked |
| 4 Inconsistent identity rules | Re-checked; `ROIClass.cs:597-626` is the same three-field match as `:613-615`; value-only matches also at `OntologyCodeClass.cs:266` and in legacy migration |
| 5 `color_string` persisted derivation | Re-checked |
| 6 `Include` false for JSON-loaded ROIs | Corrected: false only when the key is missing; serialised otherwise; generator ignores it |
| 7 Naming choice renames in place | Re-checked (`ROIClass.cs:768-824`, `OnlineTemplatesViewModel.cs:626-654`) |
| 8 `RemoveIllegalCharacters` on every meaning | Corrected: only in the 3- and 9-argument constructors; not in the setter, JSON load or legacy migration |
| 9 No JSON Schema, version, metadata or stable id | Trust |

### A.3 Airtable write path

| Fact | Status |
| --- | --- |
| Never run live; fakes with `Echo` (`AirtableTableSourceTests.cs:464-473`) | Re-checked (live history: trust) |
| Required columns (`AirtableTableSource.cs:252-264`); arrays (`WritePlanner.cs:126, 133, 141, 177, 192`); `typecast` always | Re-checked; presence only, no type check |
| Missing columns cost one request each (`:372-404`); GUI cap 30 (`MainWindow.xaml.cs:1241`) | Re-checked; `MakeTemplateWindow.xaml.cs:626-629` is uncapped |
| Additive-only | Corrected: removes the current site from the opposite list (`WritePlanner.cs:170-173`); no deletes anywhere |
| Pre-write refresh, delta warning, `filterByFormula` never sent live | Re-checked; delta needs a full download in the last 7 days (`SyncSettings.cs:13`, `AirtableTableSource.cs:307-310`) |
| Error-shape assumptions; no body logging | Re-checked; any 404 counts as a deleted record; error messages reach the GUI log via exceptions |
| `AIRTABLE_PAT` overrides every connection; connection test reads one record | Re-checked; the test uses the entered token |
| GUI callers; `TemplateSourceItem.WriteTemplatesAsync` at `:85-111` | Corrected lines: `WriteTemplateAsync :85-89`, `WriteTemplatesAsync :92-118` |
| Column spelling (`AirTableEntry.cs:41`) | Re-checked; 26 names at `:34-43` |
| R1-R10 | Re-checked; R2: on read the name is `CommonName ?? Structure` |

### A.4 RT generation

| Fact | Status |
| --- | --- |
| Clone of `template_RS.dcm` (RayStation, Implicit VR LE) and inherited values | Re-checked with pydicom; also `ISO_IR 100`, `StructureSetLabel "RS: Unapproved"`, one ROI |
| Identity copied at `:26-30`; de-identification attributes `:33-36` | Re-checked; the list also copies study date/time, accession, referring physician, sex, study id |
| FoR UID at `:166`; Position Reference Indicator inherited | Re-checked (empty in the seed) |
| Code item at `:420-433` | Re-checked |
| Validation present/absent | Re-checked (list in the plan's P2.4 basis) |
| `interpret_RT` nine `GetString` (`TemplateMaker.cs:107-123`) | Re-checked at `:112-120`; a missing sequence raises an exception `TryInterpret` does not catch |
| Split series, one RT per directory | Re-checked (runner path, not `ParseDirectory`) |
| `CleaningRTStructureCsharp` | Re-checked |
| Test RT generation only on 4-slice `SmallCT` (Explicit VR LE, real name) | Re-checked |
| `Test.dcm` content | Re-checked (Implicit VR LE, `ISO_IR 192`, MANUAL, CSIS, FMA 3.2/`VMS011`, address, station, operator present); "RTM 16.1" and "no ROI Observation Label": trust |
| C1-C13 | Re-checked through the items above; C7 collision confirmed in `AirTableRecords/` |

### A.5 Varian XML

| Fact | Status |
| --- | --- |
| Hard-coded root values and dates (`VarianXmlWriter.cs:28-39`), `Name`/`ID`/Preview ID | Re-checked; folder name supplied at `:68` |
| `<Identification>` omitted when no code; reader skips | Re-checked |
| VolumeType verbatim | Re-checked |
| Colour handling | Re-checked (fixed-width RGB throws; system colours; truncation at `:145-148`) |
| Dropped fields on import | Re-checked (`CodeSchemeVersion` required then discarded) |
| BOM inferred; no dispose; no root check | Re-checked (BOM still inferred, not executed) |
| Goldens characterisation | Re-checked; normalised by `XDocument.ToString()` |
| `XamlMakerCsharp` bypasses `VarianXmlBatch` | Re-checked |
| Dead Python/notebook pipeline | Re-checked (files present); history and authorship: trust |

### A.6 CI, release, hygiene

| Fact | Status |
| --- | --- |
| Release job never run; tags v1.0.0-v1.0.4 | Re-checked (tags); release assets embedding tokens: trust |
| Leaked tokens in history; push protection off; `main` unprotected | Trust |
| No LICENSE, SECURITY.md, dependabot; workflows pick the SDK differently | Re-checked |
| 415 facts + 50 theories = 630 cases; data located via the `.sln` | Re-checked; 630 passed in this session |
| Dead and legacy files | Re-checked; `Test_*_key.txt` hold a base id and a table id, not tokens |
| Deferred items | Re-checked against `docs/MODERNIZATION_PLAN.md:116-124, 194-205` |

### Appendix C. Airtable bases

| Fact | Status |
| --- | --- |
| `appYl68lenTOFqTDh` schema and two extra tables | Re-checked (field ids recorded in the connector output; `Collaborators` field in "Copies of deleted") |
| `appzWlVKRp9TrrTUJ` schema | Re-checked |
| `appTUL6ZaSepTawFw`, `appAZeazznL7m4fpq` descriptions | Trust (names re-checked in the base list) |
| Other bases: ownership unknown, not throwaway candidates | Re-checked list; the account also sees "Structure Naming v2", three dated "Structure Naming" copies, "Technical Integration Catalog", two "BaseTemplate", "VideoExplainTemplate"; permission is "create" on every base; workspaces: owner of "UCSD" and "UCSD_Templates", create on "Structure Naming by Disease Site" |
| Leaked `patQ` token pointed at `appzWlVKRp9TrrTUJ` | Trust |
