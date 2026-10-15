# Evaluation protocol

Exact procedures for the evidence steps of `docs/OVERHAUL_PLAN.md`. Written 2026-10-15 against
`e7f0572`. Commands use the planned `TemplateEvaluationTool` (P1.1); until it exists, sections 1 and 3
describe what it must do. `$OUT` is a scratch folder outside the repository; `$EV` is
`docs/evidence/<phase>/`. Every run writes `manifest.json` (git SHA, tool version, input paths and
SHA-256, UTC time) next to its outputs.

## 1. Snapshot baseline (G1; plan P1.2-P1.8)

### 1.1 Clean state

Tool runs always pass `--data-dir $OUT/data` and never read `%LOCALAPPDATA%\DicomTemplateMaker`.
Before each manual GUI run (Windows): (1) close the app and delete `%LOCALAPPDATA%\DicomTemplateMaker`
(settings, `ui-settings.json` with `templateRoot`, connections, caches, logs; copy it first if it holds
real work); (2) unset `AIRTABLE_PAT` (it overrides every stored connection,
`AirtableConnectionStore.cs:112-122`) and never start the GUI from a shell that set it; (3) record the
app version and the snapshot SHA-256.

### 1.2 Frozen snapshot facts (P1.3, `SnapshotFactsTests.cs`)

Input `TemplateSync.Tests/TestData/TG263.2026-09-30.json` = `TemplateSnapshots/TG263.json` at `b185504`
(184,255 bytes, SHA-256 `059cd49e731fcc564ff36444ff7b9761fd43fd08ddae4448e7e383d51eac03c4`). Pin exactly:

| Fact | Value |
| --- | --- |
| Header | `schemaVersion` 1, `baseId appYl68lenTOFqTDh`, `tableId tblex7IPsmm8hvVEc`, `recordCount` 246, `generatedAtUtc 2026-09-30T21:46:50.2740828+00:00` |
| Records and sites | 246 records; 53 distinct sites; 55 records in no site; 191 in at least one; 1,178 memberships |
| Recommend-only sites | `Brachy_Breast`, `Brachy_Endobronch`, `Brachy_Ocular`, `Brachy_Prostate`, `Brachy_Skin`, `Thorax_BreastPBI`, `Thorax_BreastWBI`, `_Targets_RNI` |
| Keys | In all 246: `Colors_RGB`, `DVH_Color`, `DVH_Style`, `DVH_Type_Index`, `DVH_Width`, `Scheme`, `SchemeCode`, `Structure`, `TG_263`, `TG_263R`, `Type`; `TG_263Spanish` 173, `TG_263French` 138, `Template_Recommend` 150, `Template_Consider` 124; never: `CommonName`, `FMAID`, `Id`, `RGB`, `Record`, the six context columns |
| Scheme and Type | `SCT` in all 246; ORGAN 135, CTV 43, PTV 42, GTV 14, AVOIDANCE 3, IRRAD_VOLUME 3, MARKER 2, BRACHY_SRC_APP 2, EXTERNAL 1, `Control` 1 |
| Duplicate `Structure` | `_TS_CTVsb` twice (`recVu5JdL6BYCPCGc` Red, `recBAais5daEWac4n` Navy Blue), both in no site |
| Name defects | U+02C6 in `CTV_HighˆLN_3cm+`, `CTV_MidˆLN_3cm-`; `/` and `^` in `CTV1^GTV/Cervix/Uterus`, `CTV2^Parametria/Vagina`; space in `Hi HU`; 7 names over 16 characters; 23 records where `Structure` differs from `TG_263`; 6 site names over 16 characters with distinct 16-character prefixes; no site fails `IsUsableFolderName` |
| Codes | 7 not SCTIDs: "88" on zPTV_High, zPTV_Mid, zPTVopt, Eye_Origin_L, Eye_Origin_R; "99" on Fid, Hi HU; all others pass length, partition and Verhoeff checks; none over 16 characters; 228792002 x41, 228793007 x40, 228791009 x14 |
| Colours | 22 distinct `Colors_RGB` options, all `Name:rrr,ggg,bbb` with 0-255; exactly one per record |
| Order | `createdTime` non-decreasing; first record `recw0PPvcjLPh0yC8`, last `recy9f5hBfw1QhB44` (`Record` 1 and 325 live) |

### 1.3 Baseline build, RTs and inventory (P1.5, P1.6)

```
dotnet run --project TemplateEvaluationTool -- build-sites --snapshot TemplateSync.Tests/TestData/TG263.2026-09-30.json --naming default --data-dir $OUT/data --out $OUT/sites
dotnet run --project TemplateEvaluationTool -- generate-rts --sites $OUT/sites --images DicomTemplateMakerGUI/SmallCT --out $OUT/rt
dotnet run --project TemplateEvaluationTool -- inventory --in $OUT/rt --out $EV/baseline-e7f0572/inventory
```

- `build-warnings.json`, uncapped (site, record id, `Structure`, kind: `EmptyName`, `NoUsableCharacters`,
  `RedFallbackColour`, `BadDvhColour`, `IncompleteCode`, `UnreadableRecord`, `UnusableSiteName`,
  message); `run-report.json` serialises `RunReport` (`RunReport.cs:68`).
- Inventory per RT: `(gggg,eeee) Keyword VR VM len value`, file meta first, sequences indented; each
  distinct UID becomes `UID#n` in order of first use; DA/TM/DT, Implementation Class UID/Version Name
  and Patient Name, ID, Birth Date, Sex are masked; lengths stay real except for masked values.
- Cross-check one file with `dcmdump +L` and `python -I -m pydicom show`; any non-formatting difference
  is a tool defect. `SUMMARY.md` lists the tags and sites of each A.4 gap C1-C13.
- Acceptance: 53 RTs or a reason per missing site; ROI count per RT equals the built count or the
  difference is listed (measured here, enforced in P4.9).

### 1.4 Live snapshot check and nomenclature (P1.4, P1.8)

`TemplateSnapshotTool check <file> --summary <md>`; exit 0 on pass, 5 on a failed gate.

| Kind | Rule | Today on the frozen file |
| --- | --- | --- |
| Gate | `recordCount` and `records.length` at least 240 and equal | 246, pass |
| Gate | Every record has `Structure`, `SchemeCode`, `Scheme` | pass |
| Gate | Every `Colors_RGB` item is `name:r,g,b` with 0-255 | pass |
| Warning | Site names usable as folder names | 0 |
| Warning | `Structure` unique (ordinal; also report case-insensitive) | 1 (`_TS_CTVsb`) |
| Warning | `Type` in the 25 PS3.3 defined terms | 1 (`Control`) |
| Warning | `SchemeCode` valid for `Scheme` (SCT: 6-18 digits, partition, Verhoeff; FMA: integer) | 7 |

Nomenclature: fetch the AAPM worksheet (claim S14) into `$OUT/aapm/` (record SHA-256, `Last-Modified`);
compare `Structure` and `TG_263` with its primary names (exact, then ignoring case), report `FMAID`
agreement; in-repo rules: at most 16 characters, unique ignoring case, targets start with
GTV/CTV/ITV/PTV, helpers with `z` or `_`, laterality only as `_L`/`_R`. Output `nomenclature.md`.

## 2. Airtable round trip (G2; plan P1.9, P3.1-P3.10)

### 2.1 Target schema

Target (a): "Duplicate base" of `appYl68lenTOFqTDh` with records, in the evaluation workspace; record
whether automations, option colours, `Record` values and record ids carried over (claim A5).
Target (b): `POST /v0/meta/bases` with `workspaceId` (D7) and one table "Structure Templates" that
mirrors the source, so the diff compares like with like:

| Field | Type | Options declared at creation |
| --- | --- | --- |
| `Record` | autoNumber (primary) | - |
| `CommonName`, `FMAID`, `SchemeCode`, `Scheme`, `DVH_Color`, `DVH_Style`, `DVH_Width`, `DVH_Type_Index`, `DVH_ContourStyle`, `Id`, `ContextGroupVersion`, `MappingResource`, `ContextIdentifier`, `MappingResourceName`, `ContextUID` | singleLineText | - |
| `Structure`, `TG_263`, `TG_263R`, `TG_263French`, `TG_263RFrench`, `TG_263Spanish`, `TG_263RSpanish` | singleSelect | none (exercises `typecast` option creation) |
| `Type` | singleSelect | the 25 PS3.3 terms (so `typecast` creates `Control` and it is observed) |
| `Colors_RGB` | multipleSelects | the 24 source options with their source colours |
| `Template_Recommend`, `Template_Consider` | multipleSelects | the 53 site names on both |

`RGB` and `MappingResourceUID` are absent, as in the source; each costs one failed request, then is
dropped with a warning (`AirtableTableSource.cs:372-404`). If the API refuses autoNumber as primary
(claim A3), make a `Name` singleLineText primary, add `Record` second, and record the refusal body.

### 2.2 Credentials, guards and driver

- `AIRTABLE_EVAL_PAT`: `data.records:read/write`, `schema.bases:read/write`, evaluation workspace only;
  checked with `GET /v0/meta/whoami` (claim A8) and a read of `appYl68lenTOFqTDh` that must be refused;
  mapped into `AIRTABLE_PAT` only inside the driver step; the snapshot job's secret never changes.
- `EVAL_TARGET_BASES` lists the targets; the driver refuses anything else and always refuses
  `appYl68lenTOFqTDh`/`tblex7IPsmm8hvVEc`. Live runs log to `$OUT/http.jsonl` (P2.6, `Authorization`
  redacted); the log is scanned for token-shaped strings before anything is copied to `$EV`.

```
dotnet run --project TemplateEvaluationTool -- write-airtable --base <target> --table "Structure Templates" --sites $OUT/sites --cache-dir $OUT/cache --dry-run
dotnet run --project TemplateEvaluationTool -- write-airtable --base <target> --table "Structure Templates" --sites $OUT/sites --cache-dir $OUT/cache --log $OUT/http.jsonl
```

The dry run prints creates and updates per site and `WritePlan.RequestCount` (`WritePlanner.cs:35-38`);
a live run prints `WriteResult.ApiCalls` (`SyncSettings.cs:100`). Stop if the dry-run total plus 20%
exceeds the remaining monthly budget (2.5).

### 2.3 Observation run and error-shape provocation (P3.2)

Site `_Targets_RNI` (6 ROIs, the smallest) into target (b), fresh cache directory.

| Id | Provocation | How | Code that keys on it | Record |
| --- | --- | --- | --- | --- |
| E1 | Unknown field | First write to (b): `RGB` and `MappingResourceUID` absent | regex `Unknown field names?:\s*"(?<name>[^"]+)"` (`TableFetcher.cs:37`), `AirtableTableSource.cs:372-404` | status, `error.type`, `error.message` per column |
| E2 | Deleted record | Delete one record created in E1 with the connector (allowed: throwaway target), rerun the same site with the same cache | `AirtableTableSource.cs:266-272` (404, `ROW_DOES_NOT_EXIST`, `MODEL_ID_NOT_FOUND`, `NOT_FOUND`) | body; whether the cache recovers |
| E3 | Delta read | Second run with the same cache within 7 days of the last full download (`SyncSettings.cs:13`, `AirtableTableSource.cs:307-310`) | `filterByFormula` (`TableFetcher.cs:139-143`); a failure becomes a warning that blocks writes (`:106-110`, `:334-336`) | request URL (formula), status; a 422 `INVALID_FILTER_BY_FORMULA` is a defect to fix before P3.3 |
| E4 | Wrong value shape | Scratch table in (b) where `Template_Recommend` is singleLineText; write one ROI | none (unhandled today) | expected 422 `INVALID_VALUE_FOR_COLUMN`; body |
| E5 | Wrong table id | Driver pointed at a non-existent table id in (b) | today any 404 counts as a deleted record | body; must be told apart after P3.2 |

Not provoked: the rate limit and monthly cap (claims A1, A2); record their bodies if they occur. Each
body goes into `TemplateSync.Tests/Fakes/AirtableJson.cs`, replaces the fabricated `Echo` reply
(`AirtableTableSourceTests.cs:464-473`) where it stood in, and drives a fix if the shape differs.

### 2.4 Read-back and diff (P3.4)

1. Same day: raw export of the source (P1.9) and of each target with `dotnet run --project
   TemplateSnapshotTool -- --base <id> --table <tbl> --name <name> --output <file> --sort-field Record`
   (before P2.9 it needs `--exclude-fields` for mapped columns the target lacks).
2. `diff-tables --left source.json --right target.json --key Structure --columns mapped26 --report $EV/diff-<target>.md`:
   strings trimmed; multiple selects compared as sets and as sequences; duplicate keys reported, never
   merged; `FMAID`, `Id` reported separately. Then read 10 random target records with the connector.
3. Every difference gets an R-id; an unclassified difference fails the step.

| R | Expected difference on leg (a) at `e7f0572` | Under the recommended decisions after P3.5 |
| --- | --- | --- |
| R1 | `TG_263`, `TG_263R`, Spanish, French and reversed forms empty on new records | Fixed (D3): written from stored variants |
| R2 | `CommonName` = `RemoveIllegalCharacters(Structure)` while the source is empty | Fixed (D1): concept meaning, curated once |
| R3 | `Colors_RGB` gains `Auto:r,g,b` options | Fixed: the matching named option is written |
| R4 | `RGB` written unpadded; absent column costs one failed request | Fixed: padded; column still absent in (a) and (b), warning expected |
| R5 | Six context columns written with Varian constants on SCT codes | Fixed (D2): blank for SCT |
| R6 | `DVH_Type_Index`, `DVH_ContourStyle` written but not read | Fixed (D6): carried through |
| R7 | Renamed ROIs (language/laterality) create new records | Fixed (D3); evaluation uses the default naming |
| R8 | Removed ROIs and sites persist; the opposite list loses the site (`WritePlanner.cs:170-173`) | Tolerated (D8) |
| R9 | `FMAID`, `Id` empty on created records | Tolerated (D9); `Id` is empty in the source anyway |
| R10 | `Colors_RGB` order flips with alternating writers; `Brain`/`brain` two records | Fixed: set comparison; case-only duplicate warning |

Leg (b) (P3.7) is diffed against leg (a) with the same tool; expected: identical apart from what an RT
cannot carry (DVH fields, name variants) until P7.1.

### 2.5 Quota budget and cleanup

Model from the frozen snapshot with `WritePlanner` semantics (one refresh per session; per site, creates
and updates in batches of 10); recompute with `--dry-run` before every live run.

| Operation | Requests |
| --- | --- |
| Refresh of the 246-record table (pages of 100) | 3 |
| All 53 sites into an empty base | 157-162 writes (35-39 create batches + 122-123 update batches, by site order) + 3 refresh + 2 failed probes = about 165 |
| All 53 sites into the duplicate (a) | about 40 (colour and context differences only) |
| Record-level copy of 246 records (D9 alternative) | 25 creates + 3 reads = about 28 |
| Read-back export / idempotency rerun (P3.6) | 3 / 3 (reads only) |
| Observation run (2.3) / create target (b) (metadata calls count) | 15-25 / 1-2 |
| One cycle: observation + (b) + (a) + two read-backs + idempotency + leg (b) | about 400 |

Free plan (1,000 per workspace per month, one 30-day grace period, then blocked, claim A2): at most two
cycles a month; schedule one. Team (100,000, then 2 requests/s, which the client sees as slow requests,
not as the billing 429 it detects at `AirtableHttpClient.cs:317-322`): no practical limit. Log
`ApiCalls` per run in `$EV/quota.md`. Cleanup: after sign-off delete scratch tables with the connector
and the bases in the Airtable UI; revoke `AIRTABLE_EVAL_PAT` when the evaluation ends.

### 2.6 Manual WPF pass (P3.9; Windows; clean state 1.1)

Against target (a), with a template that is not snapshot-built (or after P2.1): (1) Add Airtable table
with the target's URL, Test connection, record the exact text, Save; (2) tick `Airtable write?` and
Write to Airtable for one site, recording confirmation and result texts, warnings (30-line cap,
`MainWindow.xaml.cs:1241`) and whether the tick resets; (3) MakeTemplateWindow write (saves the template
first; warnings uncapped, `MakeTemplateWindow.xaml.cs:626-629`); (4) break the network mid-write and
record whether a rerun resumes. Copy the session's logs to `$EV/p3/wpf/` after scanning for tokens.

## 3. DICOM validation stage (G3; plan P1.7, P2.3-P2.5)

### 3.1 Tools and pins

| Tool | Pin | Use |
| --- | --- | --- |
| fo-dicom `DicomDataset.Validate()` | 5.2.6 (`Directory.Packages.props:5`) | Element-level checks inside tests; not IOD validation (claim T3) |
| `dciodvfy` from `pip install dicom3tools==20260927` | version line recorded in each report | IOD and module rules; all output on stderr (claim T1) |
| `validate_iods` from `pip install dicom-validator==0.9.0` | `--edition 2025b` (last edition in which the three ROI sequences are Type 1, so a zero-ROI RT fails); current edition in report mode | IOD rules; exit code = error count (claim T2) |
| DCMTK `dcmdump` | package version recorded | Inventory cross-check only |
| Orthanc `jodogne/orthanc` (tag pinned) + `pynetdicom` 3.0.x `storescu` | recorded | Association and independent parse (claim T5) |

### 3.2 Procedure (CI job `dicom-validation`, ubuntu-latest)

```
EVAL_RT_OUT=$OUT/rt dotnet test --solution DicomTemplateMaker.sln   # evaluation tests write their RTs when EVAL_RT_OUT is set
for f in $OUT/rt/*.dcm; do dciodvfy -new "$f" 2> "$OUT/dciodvfy/$(basename "$f").txt"; echo "$f $?" >> "$OUT/dciodvfy/exit.txt"; done
validate_iods --edition 2025b $OUT/rt > $OUT/validate_iods-2025b.txt; echo $? >> $OUT/validate_iods-exit.txt
validate_iods --edition current $OUT/rt > $OUT/validate_iods-current.txt || true
```

Gate: every `Error -` line from `dciodvfy` and every error from `validate_iods --edition 2025b` must
match an entry of `docs/evidence/validator-allowlist.json` (`tool`, `pattern`, `C-id`, `reason`,
`added`). Exit codes are kept beside the parsed counts; the first run decides whether `dciodvfy`'s exit
code can be trusted (claim T1). Orthanc smoke (P2.5) in the same job: service container
`jodogne/orthanc` (4242/8042); `python -m pynetdicom storescu localhost 4242 <file>` for each RT and its
CT; ROI names, `ROIDisplayColor`, types and codes from `/instances/{id}/simplified-tags` must equal the
inventory. Orthanc proves association and an independent parse, not conformance.

### 3.3 TPS-realism rules (P2.4, `RtRealismChecker`)

| Rule | Check |
| --- | --- |
| RR1 | At least one ROI (RayStation requires one, claim R1; IHE-RO BRTO-II requires an observation with interpreted type per ROI, claim S10) |
| RR2 | Identical ROI-number sets in Structure Set ROI, ROI Contour and RT ROI Observations |
| RR3 | Contour Sequence absent, not empty, for template ROIs |
| RR4 | Exactly one referenced study and one series; every CT SOP instance of the series listed once |
| RR5 | Interpreted type in the 25 PS3.3 defined terms or empty (Type 2) |
| RR6 | ROI names unique ignoring case and trailing spaces; warn above 16 characters (D14) |
| RR7 | `StructureSetLabel` present, non-empty and unique across the template set (claim V12) |
| RR8 | Code syntax per scheme: SCT 6-18 digits, valid partition, Verhoeff; FMA integer; values over 16 characters in `LongCodeValue` |
| RR9 | Context attributes only as a unit and only where D2 allows; no retired `ROIObservationLabel`; `ROIGenerationAlgorithm` per D12 |
| RR10 | Frame of Reference module present with Position Reference Indicator; its UID equals Referenced Frame of Reference and every Structure Set ROI item |
| RR11 | IHE-RO "RT Structure Set for Basic Interoperability" content (TF Vol 3, 7.3.4.1.1 and 7.4.8), each requirement transcribed as a sub-rule when P2.4 starts |

## 4. TPS acceptance matrix (G3; plan P5)

### 4.1 Systems

One row per system of the prompt's B.1 and B.2; none stays blank (available / vendor contact / no access).

| System | Template path to test | Read-back | Status | Version, conformance statement rev. | Operator, environment, identity convention |
| --- | --- | --- | --- | --- | --- |
| Varian Eclipse (first) | RTSTRUCT import; StructureTemplate XML import (P6.3); ESAPI apply script (P6.6) | ESAPI script; re-export | | | |
| Varian Ethos | RTSTRUCT | re-export | | | |
| RaySearch RayStation | RTSTRUCT; script module (P7.3); "save as structure template" experiment | `ScriptableDicomExport` | | | |
| Brainlab RT Elements | RTSTRUCT | re-export | | | |
| Elekta ProKnow DS | RTSTRUCT; structure-set template CSV (P7.2) | SDK download | | | |
| Elekta Monaco | RTSTRUCT with images | re-export | | | |
| MIM | RTSTRUCT over the network (Implicit VR association test, claim O4) | re-export | | | |
| Philips Pinnacle | RTSTRUCT; `.Script` (P7.4) | re-export | | | |
| Accuray Precision / iDMS | RTSTRUCT | re-export | | | |
| Siemens AI-Rad Companion Organs RT; Limbus Contour | Organ or template configuration (vendor contact; one row each when filled) | - | | | |
| Radformation, MVision, Manteia, Mirada, Therapanacea, Carina | Vendor contact only (no public specification) | - | | | |

### 4.2 Fixture families (P5.2; on SmallCT and on the 50+ slice CT)

| Id | Family | What it tests |
| --- | --- | --- |
| F1 | All-ORGAN template (the ORGAN ROIs of `AbdPelv_Prostate`) | Baseline acceptance |
| F2 | Targets with `^` and `/` names (`CTV1^GTV/Cervix/Uterus`, `CTV2^Parametria/Vagina`) | Character handling |
| F3 | One name over 16 characters (`_TS_PTV_Breast_Eval`) | Length limits |
| F4 | Label-prefix pair `AbdPelv_Prostate_Intact` / `_Postop` | Label collision (D14) |
| F5 | SCT-, FMA- and 99VMS_STRUCTCODE-coded ROIs, with and without context attributes | Code display (D2, D17) |
| F6 | `Control`- and `BODY`-typed ROIs | Interpreted-type mapping (D4) |
| F7 | One `Include=false` ROI | Optional ROI handling (D5) |
| F8 | One empty ROI per snapshot type: ORGAN, PTV, CTV, GTV, AVOIDANCE, IRRAD_VOLUME, EXTERNAL, MARKER, BRACHY_SRC_APP | Contour-less ROI acceptance |
| F9 | Contour-less BOLUS, FIXATION, SUPPORT (RayStation FSN #167168, claim R2) | Known vendor defect area |
| F10 | Case-only duplicate names (`Brain`, `brain`) | Rename-on-collision behaviour |
| F11 | Non-ASCII patient name (ISO_IR 192) on the test identity | Character sets |
| F12 | Implicit VR LE vs Explicit VR LE copies of F1 | Transfer syntax (D14) |
| F13 | Code-less ROIs ("88"/"99" omitted, D1b) | Missing identification code |
| F14 | Target with Segmented Property Category and Therapeutic Role (P4.3) | New attributes ignored or shown |

Every fixture carries the P0.2 test identity (or the site convention) and `SeriesDescription`
"evaluation, not for clinical use", and is imported only into the non-clinical environment of D15.

### 4.3 Procedures

Eclipse (P5.3): (1) confirm a test or research database (D15); record Eclipse/ARIA version and the
conformance statement revision (claim V11); (2) import CT and RT together through one path (DICOM
import or Varian Service Portal, never a de-identification gateway), keeping UIDs; (3) run
`tools/esapi/ListStructures.cs` (research mode): per structure `Id`, `IsEmpty`, `DicomType`, `Color`,
`StructureCode` (code, scheme, version) to CSV; (4) re-export, run `inventory`, then
`diff-rt --left fixture --right reexport` (names, colours, types, codes, ROI count), the method of
Gooding et al. 2026 (claim O11); repeat for the P6.3 XML templates through the template import UI.

RayStation (P5.4): (1) research or test database; record version and conformance statement (claim R1);
(2) import CT and RT together by script (`ImportDicomDataFromPath` is unverified, claim R3: learn the
call with the script recorder); list `case.PatientModel.RegionsOfInterest` (name, colour, type, organ
type); (3) `ScriptableDicomExport`, inventory and diff as for Eclipse; (4) save the imported structure
set as a structure template with the recorder on and record the calls (is there a template API?).

### 4.4 Result cells and pass criteria

Each cell (system x family) records **L** loaded (yes/no, error text), **N** name unchanged (or new
name), **C** colour unchanged (or new colour), **T** type mapped to what, **K** code visible (value and
scheme, or absent). A cell passes when L, N, C hold and T, K match the expectation written for that
family before the import. Template for one system:

| Family | L | N | C | T (mapped to) | K | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| F1 ... F14 | | | | | | |
