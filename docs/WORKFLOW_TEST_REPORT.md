# Workflow test report

Phases B to E of `docs/PROJECT_MANAGER_PROMPT.md`, run on 2026-11-03 against `08eca39` (main) on the
maintainer's Windows 10 machine. Every command, its exit code, counts, output location and verdict, then
the findings ranked by their effect on patients and data. Verdicts: PASS, FAIL, NOT RUN (why), UNVERIFIED.
`$S` is the session scratch folder outside the repository
(`%LOCALAPPDATA%\Temp\claude\...\scratchpad`); nothing under it enters the repository.

## 1. Summary, failures first

- **Release-candidate recheck, 2026-11-05 (section 6.1):** at `de002b7` (`main` with fix pull
  requests 23 to 31), everything is PASS:
  - the gate (837 tests);
  - the baseline;
  - the validators, run without `PATH` (F-3 fixed);
  - the publish rehearsal (`LICENSE` present, no `.pdb` or XML-doc file; F-4 fixed);
  - the app checks: *Open log folder* (now PASS), F-1 and F-6 (both fixed), and the generator.

  O-6's empty second window is explained as a test-harness artifact.
- **Not run on this machine:** the `dcmdump` cross-check (no DCMTK) and the Orthanc transfer (no Docker);
  Orthanc is covered by the CI artifact of the latest `main` run (section 4.6). The offline clean-VM install
  of the published build stays with the user.
- **Phase E (section 6):** the workstation was locked on 2026-11-03, so two items were verified from
  the log and settings files; a continuation session drove the other nine on 2026-11-04 and all nine
  PASS. Still not done: pressing *Open log folder* (the log folder item stays PARTIAL), the
  network-drive refusal of *Delete selected* (no network drive) and loading the test RTs into a
  planning system (D15).
- **Six findings, none affecting RT content** (section 7): F-1 template names lose an underscore in
  the main window list (confirmed 2026-11-04); F-2 the release job only warns when the bundled snapshot
  is missing; F-3 `validate_rts.py` fails when the validators are installed but not on `PATH`; F-4 the
  publish output ships `.pdb` and XML-doc files; F-5 README and modernization-plan drift; F-6 a renamed
  template keeps its old place in the main window list until the next rescan.
- **Everything else reproduced the committed baseline exactly:** gate green (825 tests), 53 sites, 53 RTs,
  1,178 ROIs, byte-identical `build-warnings.json`, `sites.json`, `run-report.json`, the 53 inventories
  and the realism report; validators PASS with the two allowlisted items; snapshot check exit 0 with
  warnings 0 / 1 / 1 / 7; nomenclature report identical apart from its timestamp; the four command-line
  tools and the `write-airtable` dry run behave as documented; the publish rehearsal produced a
  `1.1.0-rc.0` build that starts with its data files beside it.
- **Decided on 2026-11-03 and transcribed:** D1 (a), D7 (accepted, details pending), D15 (Eclipse test
  database first); P0.1 closed by maintainer decision. One tension from that decision is put to the
  maintainers in section 8, question 1, and nothing was changed.

## 2. Environment

| Item | Value |
| --- | --- |
| Commit | `08eca39` (main), working tree clean apart from the new docs |
| .NET SDK | 10.0.402 (`global.json` 10.0.100, `rollForward latestFeature`) |
| Python | 3.12.10; venv at `$S/venv` with `dicom3tools==20260927`, `dicom-validator==0.9.0`, `pydicom==3.0.2`, `pynetdicom==3.0.4`, `xlrd==2.0.1` |
| Not available | Docker, DCMTK (`dcmdump`) |
| `AIRTABLE_PAT` | Not set in the process, user or machine environment |
| `%LOCALAPPDATA%\DicomTemplateMaker` | Did not exist before Phase E (nothing to copy or delete) |
| Running app instances before the build | None |

## 3. Phase B: the gate

Run exactly as the README says, from the repository root, Debug configuration, sequentially in one
background script (`$S/gate/*.log`).

| Command | Exit | Duration | Result |
| --- | --- | --- | --- |
| `dotnet build DicomTemplateMaker.sln` | 0 | 38 s | Build succeeded, 0 warnings, 0 errors |
| `dotnet test --solution DicomTemplateMaker.sln` | 0 | 69 s | 825 passed, 0 failed, 0 skipped (TemplateSync.Tests 6.0 s, Presentation.Tests 8.4 s, TemplateEvaluationTool.Tests 23.9 s, DicomTemplateCore.Tests 55.2 s) |
| `dotnet format DicomTemplateMaker.sln --verify-no-changes` | 0 | 33 s | No output: nothing to format |

Verdict: **PASS**. (The plan's 630 cases at `e7f0572` have grown to 825 with the evaluation tool's and the
P1/P2 tests.)

## 4. Phase C: reproduce the baseline (protocol 1.3, 1.4, 3.2)

`$OUT` = `$S/eval`. Commands in one background script (`$OUT/summary.txt`, per-command logs beside it).

### 4.1 Pipeline

| Step | Command | Exit | Duration | Counts |
| --- | --- | --- | --- | --- |
| Build | `dotnet build TemplateEvaluationTool -c Release` | 0 | 12 s | |
| build-sites | `dotnet run --project TemplateEvaluationTool -c Release --no-build -- build-sites --snapshot TemplateSync.Tests/TestData/TG263.2026-09-30.json --naming default --data-dir $OUT/data --out $OUT/sites` | 0 | 5 s | 53 of 53 sites built, 0 warnings |
| generate-rts | `... generate-rts --sites $OUT/sites --images DicomTemplateMakerGUI/SmallCT --out $OUT/rt` | 0 | 4 s | 53 RTs for 53 sites in `$OUT/rt/series` (beside a copy of the 4 CT slices), failures: none |
| inventory | `... inventory --in $OUT/rt --out $OUT/inventory` | 0 | 3 s | 53 inventories |
| realism | `... realism --in $OUT/rt --out $OUT/realism --allowlist docs/evidence/validator-allowlist.json` | 0 | 2 s | PASS: 9 RR8 errors, all allowlisted (D1b); warnings RR6 x2, RR9 x106 |
| snapshot check | `dotnet run --project TemplateSnapshotTool -- check TemplateSnapshots/TG263.json --summary $OUT/check.md` | 0 | 6 s | Passed; warnings 0 (folder names), 1 (`_TS_CTVsb` duplicate), 1 (`Control`), 7 (codes 88/99) |

### 4.2 Diff against `docs/evidence/baseline-e7f0572/` (line endings ignored, manifests excluded)

| File or folder | Result |
| --- | --- |
| `build-sites/build-warnings.json` | SAME (empty) |
| `build-sites/sites.json` | SAME |
| `generate-rts/run-report.json` | SAME |
| `inventory/*.txt` (53) | SAME |
| `realism/realism.json`, `realism.md` | SAME |
| `validators/` | SAME after the rerun of 4.3 (the first run is the finding below) |

Verdict: **PASS**. The generator at `08eca39` writes what the baseline recorded; only the manifests differ
(scratch paths, creation time).

### 4.3 Validators (`tools/dicom-validation/validate_rts.py`)

First run, venv interpreter only:
`$S/venv/Scripts/python.exe -I tools/dicom-validation/validate_rts.py --rt-dir $OUT/rt --out $OUT/validators --allowlist docs/evidence/validator-allowlist.json`
exit **1** after 128 s. `summary.md`: "Result: FAIL ... Problems: dciodvfy is not installed (pip install
dicom3tools)", `dciodvfy` 0 errors 0 warnings (never ran); `validate_iods` 2025b 53 errors (all the
allowlisted C9) and current 53 errors. The package was installed; its `dciodvfy.exe` sits in
`$S/venv/Scripts`, which was not on `PATH`. In CI the pip scripts folder is on `PATH`, so the job is not
affected; locally the protocol's command fails unless the venv is activated.

Second run with `PATH=$S/venv/Scripts:$PATH` and `--rt-dir $OUT/rt/series --out $OUT/validators2`:
exit **0** after 177 s. `summary.md`: "Result: PASS", `dciodvfy` 0 errors and 4,712 warnings over 53
files, `validate_iods --edition 2025b` 53 errors (exit code 53), all the allowlisted C9 (`ROI Observation
Label` unexpected), current edition the same 53; "Errors not in the allowlist: none". Against the
baseline: the 53 `dciodvfy/*.txt`, `findings.json` and `summary.md` are identical; the two
`validate_iods-*.txt` logs differ only in the file paths and in four banner lines of the validator's
one-time DICOM-standard parse ("Creating JSON excerpts from docbook files..."). Verdict: **PASS**;
finding F-4 for the `PATH` dependence.

### 4.4 Nomenclature (protocol 1.4, plan P1.8)

`$S/venv/Scripts/python.exe -I tools/nomenclature/check_tg263_names.py --snapshot TemplateSync.Tests/TestData/TG263.2026-09-30.json --out $OUT/nomenclature`
exit 0. The AAPM worksheet downloaded; SHA-256
`5ff0b9e2ebf578793f6fa8f59c2357b3feb61fdc0f87171e9103a0495d93d150`, the value protocol 1.4 and the
baseline record. Result: 246 records; `Structure` exact 96, case-only 3, unmatched 147; violations:
length 7, unique 2, no_spaces 1, charset 2, target/helper/laterality 0. `nomenclature.md` is identical to
`docs/evidence/baseline-e7f0572/nomenclature/nomenclature.md` apart from the "Produced" timestamp.
Verdict: **PASS**.

### 4.5 `dcmdump` cross-check

NOT RUN: DCMTK is not installed on this machine. The pydicom cross-check of four inventories is recorded
in the baseline `SUMMARY.md`.

### 4.6 Orthanc smoke test

NOT RUN locally (no Docker). The artifact `dicom-validation-reports` of the latest `main` CI run
(41718210134, 2026-11-03, `gh run download`) holds `orthanc/orthanc.md`: **PASS**, Orthanc 1.13.0,
57 of 57 files stored (53 RTs and 4 CT slices), 1,178 ROIs compared after read-back, 0 mismatches. The
same artifact's `validators/summary.md` is PASS (dciodvfy 0 errors, 4,712 warnings; validate_iods 53
allowlisted C9 errors) and `realism/realism.md` is PASS with the 9 allowlisted RR8 errors.

## 5. Phase D: tools and workflows

Script `$S/phase-d.sh`, summary `$S/phase-d-summary.txt`, per-command logs under `$S/phase-d/`. The site
used throughout is `_Targets_RNI` (6 ROIs) as built by Phase C; its `DicomTags.txt` and `Paths.txt` are
empty as built, so every series matches.

### 5.1 Command-line runner (`DicomTemplateMakerCSharp`)

Template root `$S/phase-d/root` (the one site plus the built `Ontologies` library), `Paths.txt` pointing
at `$S/phase-d/ct`, a copy of `DicomTemplateMakerGUI/SmallCT`.

| Run | Command | Exit | Result |
| --- | --- | --- | --- |
| 1, six seconds after the CT copy | `dotnet run --project DicomTemplateMakerCSharp -- <root> --once` | 0 | "0 RT(s) written, 0 ROI failure(s), 0 error(s), 1 folder(s) waiting": the newest file was younger than the 10 s settle time, so the folder was skipped as Waiting and the run ended with nothing written (observation O-1) |
| 2, ten seconds later | same | 0 | "Wrote ...\ct\_Targets_RNI_UID2.25.279807263898541607461237963382274624702.dcm (template _Targets_RNI, series 2.25.2798..., 6 ROIs)" |
| delete | `-- <root> --delete-generated` | 0 | "Deleted 1 generated RT file(s); 0 could not be deleted; 0 error(s)"; the 4 CT slices remain |
| 3, files 182 s old | `-- <root> --once` | 0 | 1 RT written, 6 ROIs, 4,632 bytes |
| 4, straight after | same | 0 | "0 RT(s) written"; the RT's size and last-write time are unchanged |
| delete | `-- <root> --delete-generated` | 0 | Deleted 1; 4 CT slices remain |
| unknown option | `-- <root> --bogus` | 2 | "Unknown option: --bogus" and the usage line |
| two folders | `-- <root> <ct> --once` | 2 | "Only one template folder can be given." |
| missing folder | `-- <missing> --once` | 2 | "Template folder not found: ..." |
| no `template_RS.dcm` | a copy of `bin/Debug/net10.0` without the file; `DicomTemplateMakerCSharp.exe <root> --once` | 1 | "The template RT file ...\template_RS.dcm is missing; it must be next to the program." |

Verdict: **PASS**. The README's exit codes and the one-RT-per-series, never-rewrite and delete-only
rules hold. O-1 is the documented settle rule seen through `--once`.

### 5.2 Varian XML round trip (`Xaml_Maker/XamlMakerCsharp`)

`dotnet run --project Xaml_Maker/XamlMakerCsharp -- export <root> $S/phase-d/xml` exit 0, wrote
`_Targets_RNI.xml`; `... import $S/phase-d/xml $S/phase-d/root2` exit 0, wrote `_Targets_RNI` and
`Ontologies`. `All_ROIs.json` before and after differs in six lines only: every ROI's `ContourStyle`
goes from `"contour"` to `""`; names, colours, types, codes and DVH fields are identical. The same loss
is pinned by `DicomTemplateCore.Tests/TestData/Abdomen.import.golden.json` (six ROIs with
`"ContourStyle": ""`): the reader sets the style only from a style-prefixed `ColorAndStyle`
(`VarianXmlReader.cs:158-171`). Verdict: **PASS** as characterisation; no new finding (D6 and P6.4 own
the presentation fields).

### 5.3 `CleaningRTStructureCsharp`

`dotnet run --project CleaningRTStructureCsharp -- <rt> <out>` exit 0 ("Wrote ..."); `inventory` of the
input and output folders, diffed: the only substantive change is `(3006,0016) ContourImageSequence`
in the RT Referenced Series item, 4 items to 0; the remaining diff lines are the masked UID numbers
shifting (`UID#10`, `UID#11` become `UID#6`, `UID#7`) because the four CT UIDs no longer appear. The file
meta and `SOPInstanceUID` lines are unchanged, so the cleaned file carries the same SOP Instance UID as
its source (as D22's recommendation notes). Verdict: **PASS** as characterisation.

### 5.4 `write-airtable --dry-run`

No token in the environment; no request can be made.

| Case | Exit | Output |
| --- | --- | --- |
| `--base appYl68lenTOFqTDh --table tblex7IPsmm8hvVEc` with that base listed in `EVAL_TARGET_BASES` | 3 | "Refused: appYl68lenTOFqTDh is the public TG-263 source base and is never a write target." |
| `--base appNOTLISTED000002` with another base listed | 3 | "Refused: base appNOTLISTED000002 is not listed in EVAL_TARGET_BASES (...)" |
| `--base appEVAL00000000001` (18 characters, a typo of mine) | 2 | "--base should be app followed by 14 letters or digits." (correct validation) |
| `EVAL_TARGET_BASES=appEVAL0000000001`, `--base appEVAL0000000001 --table "Structure Templates" --site _Targets_RNI --dry-run` | 0 | "Dry run against an empty table (no cache in ...); nothing is sent." "_Targets_RNI: 6 create(s), 0 update(s), 0 unchanged, 1 request(s)." "1 write request(s) for 1 site(s), plus the refresh a real write makes first." Wrote `dry-run.json`, `manifest.json` |

Verdict: **PASS**.

### 5.5 Snapshot workflow

`gh run list --workflow refresh-template-snapshot.yml`: scheduled runs on 2026-10-05, 10-12, 10-19,
10-26 and 11-02, all success; the 2026-11-02 log ends "Unchanged: 246 records match
TemplateSnapshots/TG263.json." Not dispatched (quota). The check and pull-request steps of the workflow
have therefore never executed in a real run (STATUS.md P1.4). Verdict: **PASS** (export ran, no change).

### 5.6 Release rehearsal (first half of P0.7)

`dotnet publish DicomTemplateMakerGUI/DicomTemplateMakerGUI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=1.1.0-rc.0 -o $S/phase-d/publish/DicomTemplateMaker`
exit 0 in 22 s. The folder holds `DicomTemplateMakerGUI.exe` (177,628,099 bytes; ProductVersion
`1.1.0-rc.0+08eca396...`), `template_RS.dcm`, `SmallCT\vhm.1001.dcm` to `vhm.1004.dcm`,
`FMA_SNOMEDCT_Key.txt`, `Structure Template.xml` and `TemplateSnapshots\TG263.json` (SHA-256
`059cd49e...eac03c4`, the checkout's CRLF copy), plus five `.pdb` files and `TemplateSync.xml`
(observation O-3). Started from that folder with the working directory set to the scratch template
root: the process was alive after 8 s with the window title "DICOM RT Template Maker",
`%LOCALAPPDATA%\DicomTemplateMaker\ui-settings.json` was created with `templateRoot` = that root
(the "Start in" rule, N10) and `logs\app-20261103.log` appeared. Verdict: **PASS** for the rehearsal.
NOT RUN: the offline install on a clean Windows VM (user). Not done: making a missing snapshot fail the
job (a code change, finding F-3).

## 6. Phase E: the Windows app

Clean state (protocol 1.1): no app instance was running; `%LOCALAPPDATA%\DicomTemplateMaker` did not exist,
so there was nothing to copy or delete; `AIRTABLE_PAT` is not set in the process, user or machine
environment. App under test: the published `1.1.0-rc.0+08eca396...` build of section 5.6, started with
the working directory set to the scratch template root `$S/phase-e/root` (three sites built by Phase C,
`_Targets_RNI`, `AbdPelv_Prostate`, `CNS_Brain`, plus the built `Ontologies` library); scratch CT folder
`$S/phase-e/ct` (copy of SmallCT); bundled snapshot SHA-256 `059cd49e...eac03c4`.

The walkthrough could not be driven on 2026-11-03: after access to the app was granted, the
workstation lock screen (`LockApp`, `LogonUI`) was the foreground window and input is blocked while the
session is locked. Two items were verified from the log and settings files that day.

The remaining nine were driven on 2026-11-04, 07:31-07:46, by a continuation session with computer use.
The instance left running for the continuation (pid 7032) had been stopped by another session's
`Stop-Process` (its log has a startup line at 07:21 and no exit line), so the same `1.1.0-rc.0` exe was
started again with the working directory `$S/phase-e/root` (log
07:33:31, "the saved template folder"). Before that, `$S/phase-e/ct` was written into
`root\_Targets_RNI\Paths.txt` on disk (one line), because the folder pickers do not accept typing in
this setup. The pickers were reached through a temporary junction `%USERPROFILE%\Documents\DTM-scratch`
-> `$S/phase-e`, removed at the end, so the paths the app logged and showed for items 4 and 5 read
`C:\Users\BRA008\Documents\DTM-scratch\...`. The Claude window covered the centre of the screen, so
message boxes (Windows centres them on the screen) were moved into view with Alt+Space before reading.
`AIRTABLE_PAT` stayed unset; no Airtable request was made.

| Item (MODERNIZATION_PLAN.md section 3) | Verdict | What was seen |
| --- | --- | --- |
| Startup and the template folder rule | **PASS** | Log: "Template folder: ...\phase-e\root (the folder the program was started in, now saved)"; `ui-settings.json` has `templateRoot` = that folder; the main window shows it with *Change template folder...*. On 2026-11-04 a restart read it back: "(the saved template folder)" |
| The log folder | **PARTIAL** | `logs\app-20261103.log` and `app-20261104.log` written (startup, every action below, "exits (code 0)"); *Open log folder* not pressed (its button sat under the Claude window) |
| RT generator start/stop and the status line | **PASS** | *Start RT generator (DICOM server)* turns into *Stop RT generator*. Running: "RT generator running. Last scan 07:33:49: 1 RT written and 0 ROIs skipped since start; 0 errors, 0 folders waiting." (the time moves on each 3 s scan; still 1 RT at 07:34:08). After Stop the button reads *Start RT generator (DICOM server)* again and the line "RT generator stopped. Last scan 07:34:11: 1 RT written and 0 ROIs skipped since start; 0 errors, 0 folders waiting." On disk: `_Targets_RNI_UID2.25.279807263898541607461237963382274624702.dcm` (4,632 bytes) beside `vhm.1001.dcm`-`vhm.1004.dcm`; log "Wrote ... (template _Targets_RNI, series 2.25.2798..., 6 ROIs)" |
| *Delete previously generated RTs* | **PASS** | With `_Targets_RNI` ticked: "Delete the RTs generated by 1 template? These folders, and all folders inside them, will be searched: • _Targets_RNI: $S\phase-e\ct. Only files named <template>_UID<series UID>.dcm written by these templates are deleted. They are deleted permanently, not moved to the Recycle Bin; other files are not touched." followed by the warning that a running generator writes them again within seconds; *Yes* / *No* (No is the default). Yes -> "Deleted 1 generated RT." The CT folder holds the four slices only; log "Deleted 1 generated RT(s); 0 could not be deleted; 0 path error(s)." |
| *Delete selected* (Recycle Bin; refusal on network drives) | **PASS**; network-drive refusal NOT RUN (no network drive) | *Copy?* + *Copy selected* on `_Targets_RNI`: "Copied 1 template: • _Targets_RNI → _Targets_RNI_Copy0. The copy has the same ROIs and DICOM requirements but no monitored folders, so the RT generator writes no RTs for it." (the copy's `Paths.txt` is empty; the selection moves to the copy). *Delete?* + *Delete selected*: "Move 1 template folder to the Recycle Bin? • _Targets_RNI_Copy0 (<path>) Their ROIs, monitored folders and DICOM requirements go with them; they can be restored from the Recycle Bin. RTs they generated earlier are not deleted, and no new RTs are written for them. Windows may ask again; if it cannot recycle a folder, it asks before deleting it permanently." (No is the default). Yes -> "Moved 1 template folder to the Recycle Bin." The folder is gone from the root; the Recycle Bin (shell namespace 10) lists `_Targets_RNI_Copy0` from `$S\phase-e\root`, deleted 07:35 |
| *Create folder with loadable RTs* | **PASS**; the planning-system load NOT RUN (needs D15's environment) | Picker "Select where to create the folder with loadable RTs" -> `rt-folder`. Confirmation: "Generate test RTs for 1 template into ...\rt-folder\Template_Output? • _Targets_RNI. A four-slice sample CT is copied into the folder and each template writes one RT for it, so the RTs can be loaded into a planning system to check the templates. Files already in the folder are kept. The templates' monitored folders are not changed and the RT generator is not started." Yes -> "Wrote 1 test RT into ...\rt-folder\Template_Output." On disk: `Template_Output` holds `_Targets_RNI_UID2.25.2798...dcm` and the four slices. (`rt-folder` was not empty: a `_Targets_RNI_UID...dcm` from 2026-11-03 13:42, left by the setup, stayed untouched) |
| Varian XML import and export | **PASS** | Export: picker "Select where to write the Varian XML templates" (it opened in the last output folder) -> `xml-out`; no confirmation step; "Wrote 1 Varian XML file to ...\xml-out." `_Targets_RNI.xml` (3,839 bytes, `ID="_Targets_RNI"`, 6 structures; log "0 kept, 0 failed"). Import: *Load from XMLs*, picker "Select the folder of Varian XML templates to import" -> `xml-in`; report "Imported 1 template into $S\phase-e\root: • Abdomen (17 ROIs)". `root\Abdomen\All_ROIs.json` (14,771 bytes) with empty `Paths.txt` and `DicomTags.txt`; the list shows "Abdomen, 17 ROIs present in template" |
| Template editor (build, rename, save/discard on close) | **PASS** | *Add a new template* opens "New template" (everything but the name disabled, "Enter a name for the new template, then press Build template."). `ZZ_PhaseE` + *Build template* -> the button reads "Template built", the title "Template editor – ZZ_PhaseE", red "No monitored folders: no RTs are written for this template until one is added." Search ontology "Bladder" offers Bladder and GallBladder; Bladder, ROI name `Bladder`, type ORGAN (list: ORGAN, PTV, CTV, GTV, MARKER, AVOIDANCE, ... DOSE_REGION), *Add ROI* -> row added and "Unsaved changes" beside *Save*. *Save* -> the label clears; log "Saved template ZZ_PhaseE (1 ROIs)". *Rename template...* -> "Rename template" with "The folder becomes ...\root\ZZ_PhaseE2." -> *Rename*: title and Folder line follow, the folder is renamed on disk, `All_ROIs.json` unchanged (same MD5). Unticking Include and pressing *Close*: "Unsaved changes — Save the changes to template "ZZ_PhaseE2" before closing? Yes saves them. No discards them. Cancel keeps the window open." (Cancel is the default). No -> closed; log "Discarded unsaved changes to template ZZ_PhaseE2."; the file is unchanged (same MD5, `"Include": true`; reopened, the box is ticked) |
| Paths and ontology editors | **PASS** | `_Targets_RNI` editor: "1 monitored folder, no DICOM requirements." *Edit monitored DICOM paths...* -> "Monitored folders and DICOM requirements – _Targets_RNI", "Monitored folders (1)": `$S\phase-e\ct`. Requirement kind *Series Description*, text `PhaseE check`, *Add DICOM requirement* -> row "Series Description / PhaseE check / Remove"; *Save and close* -> the editor reads "1 monitored folder, 1 DICOM requirement."; `DicomTags.txt` = `Series Description\PhaseE check` (CRLF), `Paths.txt` unchanged; log "Saved template _Targets_RNI: 1 monitored folder(s), 1 DICOM requirement(s)." *Edit ontologies*: names the library file (`...\root\Ontologies\All_Ontologies.json`), *Add from RT Structure file...*, *Change ontology scheme...*, "Add an entry" (Common name, Code value (required), Coding scheme) and the list (A_Aorta 15825003 SCT, ..., Body Outline BODY 99VMS_STRUCTCODE). `ZZ_NoCode` with scheme SCT and no code: red "Enter a code value (required)." and *Add to library* stays disabled. *Close* without saving: no prompt, the library file is unchanged (07:39 timestamp, no `ZZ_NoCode`) |
| Scheme conversion | **PASS** | "Change ontology scheme": "Converts the ontology codes of the ROIs of every template in the main window's list, and of the ontology library, from one coding scheme to the other, using the FMA to SNOMED CT table shipped with the program. Codes without an equivalent are left as they are. You are asked to confirm, with the numbers, before anything changes." *Convert from* and *Convert to* each offer FMA and SNOMED CT. SNOMED CT -> SNOMED CT: red "Choose two different schemes: converting SNOMED CT into itself would corrupt codes that are already SNOMED CT." and *Convert...* stays disabled. Closed with *Close* (the dialog's cancel); nothing converted |
| Both Airtable windows (open and cancel only) | **PASS** | *Load Online Templates* -> "Online Templates": Templates to show "TG-263 (shared)", *Add Airtable table...*, *Remove connection* (disabled), *Refresh* ("Check for changes since the last download (usually one request).") and *Full refresh* ("Download the whole table again (one request per 100 records)."); "246 records · published 2026-09-30 14:46", "Published 34 days ago.", "0 of 53 selected", each template with its ROI count and an orange "already exists" on `_Targets_RNI` and `AbdPelv_Prostate`. The shared source is the snapshot at `raw.githubusercontent.com` with the bundled file as fallback (`SyncSettingsFile.DefaultSnapshotUrl`, `TemplateSourceCatalog.cs:127-129`), so *Refresh* was pressed once: the status is unchanged and `cache\snapshot-TG-263__shared_.json` was written at 07:45:32 with the same `generatedAtUtc` (2026-09-30T21:46:50Z) and `recordCount` 246, plus an `etag` and `lastCheckedUtc`. *Add Airtable table...* -> "Add an Airtable table": Name, Personal access token ("create a personal access token at airtable.com/create/tokens with the data.records:read and data.records:write scopes"), Base id, Table id or name, each "Required"; *Test connection* and *Save* disabled while empty; nothing typed, *Cancel* |

Finding F-1 is confirmed on the 2026-11-04 screens: the main window list reads "Targets_RNI",
"AbdPelvProstate", "CNSBrain", "AAPhaseE" and "ZZPhaseE2", while the editor titles, the row tooltips and
the Online Templates list show the real names (`_Targets_RNI`, `AbdPelv_Prostate`, ...). So the fix is
confined to the main window's row label. Finding F-6 is new (observation O-4, confirmed).

At the end the generator was stopped, the app closed ("exits (code 0)" at 07:46:19), the junction
removed and the scratch folders left in place. State left in them: `root` now also holds `Abdomen` and
`AA_PhaseE`, `_Targets_RNI` carries the `PhaseE check` requirement (so it no longer matches the sample
CT), and `ui-settings.json` keeps `lastOutputFolder` and `lastVarianXmlFolder` pointing at the removed
junction. Neither log has a `[WRN]`, `[ERR]` or `[FTL]` line.

### 6.1 Release-candidate recheck (2026-11-05, `1.1.0-rc.3`)

Run by the second project-manager session at `de002b7`, which is `main` after pull requests 23 to 31.
The run used a detached worktree, with outputs under `$S/rc3`.

| Step | Command | Exit | Result | Verdict |
| --- | --- | --- | --- | --- |
| Build | `dotnet build DicomTemplateMaker.sln` | 0 | 0 warnings, 0 errors (32 s) | PASS |
| Test | `dotnet test --solution DicomTemplateMaker.sln` | 0 | 837 passed, 0 failed (55 s) | PASS |
| Format | `dotnet format DicomTemplateMaker.sln --verify-no-changes` | 0 | no changes (38 s) | PASS |
| Pipeline | `build-sites`, `generate-rts`, `inventory`, `realism` as in the `evaluation-pipeline` skill | 0 each | `build-warnings.json`, `sites.json`, `run-report.json`, the 53 inventories and the realism report identical to `baseline-e7f0572` (manifests excluded) | PASS |
| Snapshot check | `TemplateSnapshotTool check TemplateSnapshots/TG263.json` | 0 | passed; warnings 0 / 1 / 1 / 7 | PASS |
| Validators | `$S/venv/Scripts/python.exe -I tools/dicom-validation/validate_rts.py ...`, with the venv's `Scripts` **not** on `PATH` | 0 | PASS: `dciodvfy` 0 errors and 4,712 warnings; `validate_iods` 2025b 53 errors, all the allowlisted C9. `summary.md`, `findings.json` and the 54 `dciodvfy` files are identical to the baseline. `validate_iods-2025b.txt` differs only by the four first-run banner lines. F-3 is fixed: `dciodvfy` was found beside the interpreter | PASS |
| Publish | the `ci.yml` line with `-p:Version=1.1.0-rc.3` | 0 | 18 s. The folder holds `DicomTemplateMakerGUI.exe` (177,789,891 bytes, ProductVersion `1.1.0-rc.3+de002b7c...`), `FMA_SNOMEDCT_Key.txt`, `LICENSE`, `SmallCT\vhm.1001.dcm` to `vhm.1004.dcm`, `Structure Template.xml`, `TemplateSnapshots\TG263.json` and `template_RS.dcm`. There is no `.pdb` and no `TemplateSync.xml`, so F-4 is fixed | PASS |
| CI on `main` | run 42102552546 | success | Build and test on Windows and Ubuntu, and DICOM validation, green. *Publish release* skipped (no tag) | PASS |

**Windows app (protocol 1.1, with the owner's agreement of 2026-11-05).** Setup:

- `%LOCALAPPDATA%\DicomTemplateMaker` held the four files left by the 2026-11-04 pass. It was copied to
  `$S/rc3/appdata-backup`, verified by SHA-256 and deleted.
- `AIRTABLE_PAT` was unset in process, user and machine scope.
- The `1.1.0-rc.3` exe was started from the shell with the working directory `$S/rc3/app/root`. That
  root holds `_Targets_RNI`, `AbdPelv_Prostate`, `CNS_Brain`, `H&N_LX_HPX_Thyroid`, `ZZ_Rename` (a copy
  of `_Targets_Basic`) and the `Ontologies` library, all from the pipeline run above.
- `_Targets_RNI\Paths.txt` names `$S/rc3/app/ct`, a copy of SmallCT.

| Item | Verdict | What was seen |
| --- | --- | --- |
| Startup and the template folder rule | **PASS** | Log: version `1.1.0-rc.3+de002b7c...`, then "Template folder: $S\rc3\app\root (the folder the program was started in, now saved)". `ui-settings.json` has `templateRoot` = that folder |
| F-1: names in the main window list | **PASS** | The rows read `_Targets_RNI`, `AbdPelv_Prostate`, `CNS_Brain`, `H&N_LX_HPX_Thyroid` and `ZZ_Rename`, with every underscore shown (zoomed screenshot) |
| F-6: rename, then close the editor | **PASS** | On the `ZZ_Rename` row: editor, *Rename template...*, `AA_Renamed`, *Rename*. The title and Folder line follow, and the log says "Renamed template folder ...\ZZ_Rename to ...\AA_Renamed". Closing the editor moves the row at once to second place, below `_Targets_RNI` and above `AbdPelv_Prostate`, with no rescan |
| The log folder (*Open log folder*) | **PASS** | Exactly one new Explorer window opened, at `%LOCALAPPDATA%\DicomTemplateMaker\logs` (read through `Shell.Application`, then closed). Together with the 2026-11-04 log lines, the item is now PASS |
| RT generator start/stop | **PASS** | While running: "RT generator running. Last scan 13:13:25: 1 RT written and 0 ROIs skipped since start; 0 errors, 0 folders waiting." After Stop, the button and the line switch back. `_Targets_RNI_UID2.25.2798...dcm` (4,632 bytes, as on 2026-11-04) is beside the four slices |
| Exit | **PASS** | "exits (code 0)" at 13:13:43. The log has no `[WRN]`, `[ERR]` or `[FTL]` line |

The other section 6 items were not repeated. Since `1.1.0-rc.0` (`git diff 08eca39 de002b7`), the
app's code changed in four places, and every test passes:

- display text: the main window's row label and the editor's Airtable-write button text (F-1, PR 24);
- the row order (F-6, PR 30);
- a test seam in `FileSwap.MoveOver` with unchanged behaviour (F-7, PR 31);
- symbols embedded on publish instead of shipped as `.pdb` files (F-4, PR 25). Afterwards
the run's settings folder was copied to `$S/rc3/appdata-rc3-run` and deleted, and the backup was
restored: four files, SHA-256 identical to before.

**Test-harness note (explains O-6).**

- What happened: the computer-use call that brings an app forward (`open_application`) started a
  second copy of the exe at 13:09 instead. Its working directory was the program folder, and it logged
  "Template folder: ...\publish\DicomTemplateMaker (the program folder)", so its window showed an empty
  list. It was closed at 13:11.
- What that copy saw: not the `ui-settings.json` the first copy had saved. Its log lines went to a
  separate file, also named `app-20261105.log`.
- Cause: the session's shell runs inside the Claude desktop app's MSIX package. There, a new file or
  folder created under `%LOCALAPPDATA%` is redirected to
  `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Local\`; a probe file written from the shell
  appeared there. Computer-use starts programs outside the package. So the first copy kept its settings
  and log in the package's copy, and the second used the real folder.
- The 2026-11-04 second instance (O-6) is the same artifact.
- State after the restore: the package's `LocalCache\Local` holds no `DicomTemplateMaker` folder, so
  the restore went to the real folder.
- For later passes: bring the app forward by clicking its window, never with `open_application`
  (`release-rehearsal` skill).

## 7. Findings, ranked by effect on patients and data

No finding affects the RT Structure Sets the program writes: the generator reproduced the committed
baseline byte for byte and every validator result matched. The findings are display, release, tooling
and documentation defects.

| Id | Defect | Reproduction | Impact | Proposed fix | Test that would pin it |
| --- | --- | --- | --- | --- | --- |
| F-1 | Template names lose an underscore in the main window's template list (WPF `Label` access-key handling) | Open the app on a root holding `_Targets_RNI` and `AbdPelv_Prostate`; the list reads "Targets_RNI", "AbdPelvProstate" (screenshot, section 6; confirm by zoom) | A user reads a wrong template name; TG-263 site names carry meaning in their underscores (`_Targets_*` are the helper sites). No data effect: the folder, `StructureSetLabel` and RTs use the real name | Show names in a `TextBlock` (or escape `_` as `__`) in `AddTemplateRow` and in any other `Label` bound to a template, ROI or ontology name (`AddROIRow`, `AddOntologyRow` to be checked) | WPF code-behind has no test host in this repository; a Presentation-level helper `TemplateRowText.LabelContent(name)` that doubles underscores can be unit-tested, and Phase E confirms the rendering |
| F-2 | The release job only warns when `TemplateSnapshots/TG263.json` is missing from the publish folder (`ci.yml`, `Write-Warning`), so a release without the offline snapshot would ship (plan P0.7) | Publish without the file and read the job log | Offline first runs of a release would have no shared templates until the download succeeds | Make the step fail (`Write-Error` / `exit 1`) when the file is missing; keep the message | No unit test (workflow); verify once on a `v1.1.0-rc.*` tag in the rehearsal, where the file is present and the job passes |
| F-3 | `tools/dicom-validation/validate_rts.py` reports "dciodvfy is not installed" and exits 1 when `dicom3tools` is installed in a venv whose `Scripts` folder is not on `PATH` (section 4.3) | `$S/venv/Scripts/python.exe -I tools/dicom-validation/validate_rts.py ...` without activating the venv | The protocol's local command fails for anyone who runs it with an explicit interpreter; CI is unaffected (pip's scripts folder is on `PATH` there) | Resolve `dciodvfy` and `validate_iods` next to `sys.executable` (`Scripts` on Windows, `bin` elsewhere) before falling back to `PATH`, and name the paths tried in the problem line | A unit test of the resolver with a fake scripts folder (the script has no tests today; `unittest` in `tools/dicom-validation/`) |
| F-4 | The publish output, and so the release zip, includes five `.pdb` symbol files and `TemplateSync.xml` (section 5.6) | `dotnet publish` as in `ci.yml`; list the folder | Larger download; no functional effect | `-p:DebugType=None -p:DebugSymbols=false` on the publish line (or in the GUI csproj for Release), and `GenerateDocumentationFile` off for `TemplateSync` in Release or excluded from publish | Rehearsal listing (no unit test) |
| F-5 | Documentation drift: `README.md:153` says the solution builds on macOS (CI is Windows and Linux); the release-download lines predate the new release job; `MODERNIZATION_PLAN.md` opens with "Phase 0 (audit) complete", its Phase 1 text names .NET Framework 4.8 and netstandard2.0, and its open question 3 is answered in code | Read the files | Misleads readers; part of the release-readiness list | One documentation pull request (plan Appendix A) | None (prose) |
| F-6 | After a template is renamed from its row's editor, the main window list keeps sorting it by its old name until the next full rescan (O-4 confirmed). `AddTemplateRow.template_name`, the sort key of `MainWindow.DisplayRows` (`MainWindow.xaml.cs:434`), is set once in the constructor (`AddTemplateRow.cs:45`), and `EditROIButton_Click` only calls `Refresh()` (`AddTemplateRow.cs:106-112`) | Root with `_Targets_RNI`, `Abdomen`, `AbdPelv_Prostate`, `CNS_Brain`, `ZZ_PhaseE2`; press *Edit ROIs and monitored DICOM paths* on `ZZ_PhaseE2`, *Rename template...* to `AA_PhaseE`, *Close*. The row now reads "AAPhaseE" but stays last, below "CNSBrain"; typing in *Search by template name* and clearing it keeps it last. Any action that rescans (closing *Edit ontologies*, add, copy, delete) puts it second, where it belongs (section 6, 2026-11-04) | Display only: a renamed template is not where a user looks for it in a long list. Search uses the current name ("AA_Ph" finds it, "ZZ_Ph" finds nothing); the folder, the tooltip and later edits use the new path | Read the sort key from the template (`template_name` as a property returning `templateMaker.TemplateName`, or update it in `Refresh()`), and have the main window re-sort after the editor closes, for example an event raised by `AddTemplateRow` after `ShowDialog` that `MainWindow` handles with `UpdateText()` | Move the ordering into a Presentation helper (for example `TemplateRowText.Order(IEnumerable<TemplateMaker>)`, keyed on the live `TemplateName`) with a unit test that renames a template and expects the new order; Phase E rechecks the rename-then-close path |
| F-7 | `TemplateSync.Tests.SnapshotStoreSwapTests.A_target_held_open_for_a_moment_is_replaced_once_it_is_released` is flaky on `windows-latest`: it failed with `System.UnauthorizedAccessException: Access to the path is denied` in the CI runs of pull requests 23 (documentation only) and 25 on 2026-11-04 (runs 41858401769 and 41858697891), while the same commit's Linux job and the other three pull requests passed, and the test passed locally in Phase B | Run the Windows `build-and-test` job repeatedly; about two in five runs of 2026-11-04 failed | A random red gate on every pull request; it was added by PR 20 (`9a62b14`) with the atomic-swap retry it tests, `FileSwap.MoveOver` (`FileSwap.cs:33-37`) already retries on both `IOException` and `UnauthorizedAccessException` within a bounded delay list, so the likely cause is the test's timing (a 100 ms timer-released hold against retries that must total under a second) on a slow shared runner, which the fix session confirms or refutes | Read the test and `FileSwap.MoveOver`; if the production retry ignores `UnauthorizedAccessException`, extend it (the product defect the test was written for); otherwise make the test's hold-and-release deterministic (release on a signal, not a timer) | The same test, made deterministic, plus a unit test that the retry covers `UnauthorizedAccessException` |

Observations, not defects:

- **O-1** `DicomTemplateMakerCSharp --once` exits 0 after writing nothing when a monitored folder's newest
  file is younger than the 10 s settle time (section 5.1, run 1); the log says "1 folder(s) waiting". A
  batch caller cannot tell "nothing to do" from "not yet" without the log. The behaviour is documented;
  whether `--once` should wait out the settle time, or exit with a distinct code, is a design question
  for the maintainers (section 8).
- **O-2** The Varian XML round trip drops `ContourStyle` (section 5.2); pinned by the import golden and
  owned by D6 and P6.4.
- **O-3** `CleaningRTStructureCsharp` keeps the source SOP Instance UID and patient identity (section
  5.3), as D22 records.
- **O-4** `AddTemplateRow.template_name`, the sort key of the template list, is set once and stays stale
  after a rename until the next rescan (`AddTemplateRow.cs:45`, `MainWindow.xaml.cs:434`); whether the
  list order is wrong right after a rename is a Phase E check (N14). **Checked 2026-11-04: it is;
  recorded as finding F-6.** A template renamed inside the editor that built it is listed correctly,
  because closing *Add a new template* rescans.
- **O-5** The snapshot workflow's check and pull-request steps have never run in a real run, because the
  table has not changed since 2026-09-30 (section 5.5).
- **O-6** Two instances of the app can run at once on the same template folder and settings file. In
  section 6 a second instance (started at 07:32:05 by the computer-use bring-forward call while the
  first was running) wrote no line to `app-20261104.log` (no startup, no exit), although the file sink is
  opened with `shared: true` (`App.xaml.cs:93-98`), and its window showed an empty template list for the
  half minute it was looked at. Both were closed, and one fresh instance listed the templates at once.
  Explained on 2026-11-05 (section 6.1): the test harness started the two instances in different
  views of `%LOCALAPPDATA%`, the first inside the Claude desktop app's MSIX package and the second
  outside it, so the second saw no saved template folder and no log file. The empty list and the
  missing log lines are therefore a test artifact. That two instances can run at once still holds.
  Two instances would mean two RT generators on the same folders, so
  whether the app should refuse a second instance is put to the maintainers (section 8). To reproduce:
  start the exe twice and compare the log and the lists.
- **O-7** *Refresh* in Online Templates leaves no line in the log; the only trace is the cache file's new
  `lastCheckedUtc` and `etag` (section 6). A support question such as "when did it last check?" cannot
  be answered from the log.
- **O-8** *Build template* saves the new template folder at once ("Saved template ZZ_PhaseE (0 ROIs)",
  before *Save* is pressed), so a template built and then closed without any ROI stays in the list as an
  empty template. This looks intended (the editor needs the folder for monitored paths) and was not
  driven further.
- **O-9** *Create folder with loadable Varian XMLs* writes without a confirmation and keeps files that
  already exist (log "0 kept, 0 failed"), while *Create folder with loadable RTs* confirms first; both
  only add files, so nothing is lost either way.

## 8. Questions for the maintainers

1. **P0.1 tension.** You decided the leaked tokens are meant to be shared and need no revocation. The
   current code still refuses them: `TemplateSync/Credentials/LeakedTokens.cs` keeps their SHA-256
   fingerprints, the add-table dialog rejects them and legacy token files holding them are retired on
   first start; the README's "Security notice" asks their owners to revoke them; and the modernization
   plan's quota analysis (section 1.2) found one shared token exhausting a Free workspace's 1,000 calls
   a month, which is why the snapshot path exists. Should that code and text stay as they are, or change?
   Nothing was changed.
2. **Phase E.** Unlock the workstation and let this session drive the app (it is still running from
   the publish folder), or run the section 6 checklist yourself and send the PASS/FAIL per item?
   *(Driven by a continuation session on 2026-11-04; section 6. Still open: pressing* Open log folder,
   *the network-drive refusal and the planning-system load.)*
3. **Fixes.** Approve F-1 to F-5 as separate pull requests against `main`, each behind the gate? F-1 is
   confirmed only by one screenshot until Phase E zooms in. *(F-1 confirmed on 2026-11-04. F-6, the
   stale sort order after a rename, is new.)*
4. **O-1.** Should `--once` wait out the settle time (or take a `--settle` option), exit with a distinct
   code when folders are still waiting, or stay as it is?
5. **Snapshot workflow (O-5).** Exercise the pull-request path once by dispatching the workflow on a
   branch with a deliberately altered snapshot (3 Airtable calls), or wait for the next real change?
6. **D2 to D22.** Walk through the remaining decisions one section at a time now?
7. **Deliverable 5.** Write the root `CLAUDE.md` (under 60 lines) and `.claude/skills/` for
   build-and-test, evaluation-pipeline and release-rehearsal, following the UNC_SchedulingAssistant
   pattern?
8. **Second instance (O-6).** Should the app refuse to start a second instance (a named mutex, bringing
   the first window forward), given that two instances run two RT generators on the same folders, and
   the second one's log lines were missing in Phase E?
