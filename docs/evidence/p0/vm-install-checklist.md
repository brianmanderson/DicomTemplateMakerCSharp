# P0.7c: offline install on a clean Windows VM

Plan step P0.7c (`docs/OVERHAUL_PLAN.md`, release-readiness row in `docs/STATUS.md` section 4). Only the
repository owner can run it. Fill in every box and "What you saw" line by hand; the results are
transcribed here verbatim.

## The build under test

| | |
| --- | --- |
| Commit | `main` at `de002b7cde000041de751589edef47b4eda6cd76` |
| Version | `1.1.0-rc.4` (exe ProductVersion `1.1.0-rc.4+de002b7cde000041de751589edef47b4eda6cd76`) |
| Built | 2026-11-07, the `release` job's publish line from `.github/workflows/ci.yml` with `-p:Version=1.1.0-rc.4`, zipped with `Compress-Archive -Path publish/DicomTemplateMaker/*` as the job does |
| Zip | `DicomTemplateMaker-v1.1.0-rc.4-win-x64.zip`, 71,904,364 bytes |
| Zip SHA-256 | `4EA09AC4CD91716E78F4C780285FB9D401AF6F41FA99DF5C3895F001A376274E` |
| Built at | `C:\Users\BRA008\AppData\Local\Temp\claude\C--Users-BRA008-Modular-Projects-DicomTemplateMakerCSharp\ac139809-8102-42ad-b83a-cc0ccc04b717\scratchpad\rc4\DicomTemplateMaker-v1.1.0-rc.4-win-x64.zip` (a temporary folder: copy the zip out before using it) |

Contents (checked before zipping): `DicomTemplateMakerGUI.exe` (177,789,891 bytes), `FMA_SNOMEDCT_Key.txt`,
`LICENSE`, `SmallCT\vhm.1001.dcm` to `vhm.1004.dcm`, `Structure Template.xml`, `template_RS.dcm`,
`TemplateSnapshots\TG263.json` (SHA-256 `059CD49E...EAC03C4`, equal to the checkout's copy). No `.pdb` and no
XML-doc file.

## Suggested layout on the VM

- Unzip to `C:\DTM\DicomTemplateMaker` (the program folder).
- Create an empty folder `C:\DTM\Templates` (the template folder) and `C:\DTM\Out` (for the test RTs).
- Settings and logs go to `%LOCALAPPDATA%\DicomTemplateMaker` (`ui-settings.json`, `logs\app-<yyyyMMdd>.log`).

Start the exe by double-clicking it in Explorer (not from a shortcut with a "Start in" folder), so the
first-run rule below is the one being tested.

## Checklist

Tester: ______________________  Date: ______________  VM: Windows ___ (10 / 11), build ___________

### 1. Clean VM: Windows 10 or 11, no .NET runtime, network disabled

Check: a fresh VM or snapshot; `C:\Program Files\dotnet` does not exist, and `dotnet --list-runtimes` in a
command prompt says "not recognized". The network adapter is disconnected in the hypervisor (not only in
Windows); `ping 8.8.8.8` and opening any web page fail. `%LOCALAPPDATA%\DicomTemplateMaker` does not exist.

- [ ] PASS  [ ] FAIL
- What you saw: ____________________________________________________________

### 2. Copy and unzip

Copy the zip onto the VM (shared folder, ISO or a disk, with the network still off). Check its SHA-256 in
PowerShell: `Get-FileHash .\DicomTemplateMaker-v1.1.0-rc.4-win-x64.zip` must print
`4EA09AC4CD91716E78F4C780285FB9D401AF6F41FA99DF5C3895F001A376274E`. Unzip to `C:\DTM\DicomTemplateMaker`
(Explorer's *Extract all*). The folder holds the exe, `LICENSE`, `template_RS.dcm`, `SmallCT` (4 files),
`FMA_SNOMEDCT_Key.txt`, `Structure Template.xml` and `TemplateSnapshots\TG263.json`, nothing else.

- [ ] PASS  [ ] FAIL
- What you saw (hash, file list): ____________________________________________________________

### 3. Start the exe

Double-click `DicomTemplateMakerGUI.exe`. Windows SmartScreen may show "Windows protected your PC" (the exe is
not signed and came from another machine); if so, note it, choose *More info* then *Run anyway*. The main
window opens with no error dialog and no request to install .NET. *Help!* or the About window shows version
`1.1.0-rc.4`.

- [ ] PASS  [ ] FAIL
- What you saw (SmartScreen? start time? version?): ____________________________________________________________

### 4. First-run template folder rule

With nothing saved and the exe started from its own folder, the template folder is the program folder and
it is not saved. Expected: the top of the main window shows `C:\DTM\DicomTemplateMaker`; the template list is
empty; `%LOCALAPPDATA%\DicomTemplateMaker\logs\app-<date>.log` has the line
"Template folder: C:\DTM\DicomTemplateMaker (the program folder). Window settings: ...", without "now saved".
Then press *Change template folder...* and choose `C:\DTM\Templates`; the main window shows that folder.

- [ ] PASS  [ ] FAIL
- What you saw (folder shown, log line): ____________________________________________________________

### 5. Bundled TG-263 templates under *Load Online Templates*, with no network

Press *Load Online Templates*. Expected: the "Online Templates" window opens without a long hang or an error
box; Templates to show is "TG-263 (shared)"; the status reads "246 records · published 2026-09-30 <local time>
(copy shipped with the program)"; the list shows the TG-263 templates (53) with their ROI counts. Pressing
*Refresh* may report that it could not connect; the list must stay. Write down any message exactly.

- [ ] PASS  [ ] FAIL
- What you saw (status line, count, any message): ____________________________________________________________

### 6. Create one template

In the Online Templates window tick one template (for example `_Targets_RNI`) and press *Build selected
templates*. Expected: the window reports it built, and the main window lists `_Targets_RNI` with its ROI
count; `C:\DTM\Templates` now holds `_Targets_RNI` and `Ontologies`. (Optional: open the template's editor,
check the ROIs are listed, close it.)

- [ ] PASS  [ ] FAIL
- What you saw: ____________________________________________________________

### 7. Generate an RT on the bundled SmallCT

Tick `_Targets_RNI` in the main window and press *Create folder with loadable RTs*; choose `C:\DTM\Out` and
answer *Yes*. Expected: "Wrote 1 test RT into C:\DTM\Out\Template_Output."; that folder holds
`vhm.1001.dcm` to `vhm.1004.dcm` (copied from the bundled `SmallCT`) and one
`_Targets_RNI_UID2.25.<digits>.dcm` of a few kilobytes.

- [ ] PASS  [ ] FAIL
- What you saw (message, file name and size): ____________________________________________________________

### 8. *Open log folder*

Press *Open log folder*. Expected: one Explorer window opens at `%LOCALAPPDATA%\DicomTemplateMaker\logs`,
holding `app-<date>.log`. Open the log: it has the startup line with version `1.1.0-rc.4+de002b7c...` and
lines for the actions above. Note any line marked `[WRN]`, `[ERR]` or `[FTL]`.

- [ ] PASS  [ ] FAIL
- What you saw (folder, any WRN/ERR/FTL line): ____________________________________________________________

### 9. Close and restart keeps the template folder

Close the main window, then double-click the exe again. Expected: the main window shows `C:\DTM\Templates`
and lists `_Targets_RNI`; the log has a new startup line
"Template folder: C:\DTM\Templates (the saved template folder) ..." and the previous session ends with
"exits (code 0)". Close the app again.

- [ ] PASS  [ ] FAIL
- What you saw: ____________________________________________________________

### Overall

- [ ] PASS (all nine)  [ ] FAIL
- Anything else you noticed: ____________________________________________________________

## Results (transcribed verbatim)

### 2026-11-13: run on the maintainer's workstation, not a clean VM (does not count for P0.7c)

The owner sent three logs from `%LOCALAPPDATA%\DicomTemplateMaker\logs` (`app-20261103.log`,
`app-20261104.log`, `app-20261113.log`); no PASS/FAIL boxes were filled in. The 2026-11-03 and 2026-11-04
logs are the earlier Phase E passes (`1.1.0-rc.0`/`rc.1`). The rc.4 lines, verbatim:

```
2026-11-13 08:54:24.582 -08:00 [INF] DicomTemplateMakerGUI.App: DICOM RT Template Maker 1.1.0-rc.4+de002b7cde000041de751589edef47b4eda6cd76 starting on Microsoft Windows 10.0.19045 (.NET 10.0.13, "X64"). Program folder: C:\Users\BRA008\AppData\Local\Temp\claude\C--Users-BRA008-Modular-Projects-DicomTemplateMakerCSharp\ac139809-8102-42ad-b83a-cc0ccc04b717\scratchpad\rc4\publish\DicomTemplateMaker\. Working directory: C:\Users\BRA008\AppData\Local\Temp\claude\C--Users-BRA008-Modular-Projects-DicomTemplateMakerCSharp\ac139809-8102-42ad-b83a-cc0ccc04b717\scratchpad\rc4\publish\DicomTemplateMaker.
2026-11-13 08:54:24.620 -08:00 [INF] DicomTemplateMakerGUI.App: Template folder: C:\Users\BRA008\AppData\Local\Temp\claude\C--Users-BRA008-Modular-Projects-DicomTemplateMakerCSharp\c9a3d172-c373-4551-997f-215ab20349dd\scratchpad\phase-e\root (the saved template folder). Window settings: C:\Users\BRA008\AppData\Local\DicomTemplateMaker\ui-settings.json.
2026-11-13 08:54:32.035 -08:00 [INF] DicomTemplateMakerGUI.App: DICOM RT Template Maker exits (code 0).
2026-11-13 08:55:18.013 -08:00 [INF] DicomTemplateMakerGUI.App: DICOM RT Template Maker 1.1.0-rc.4+de002b7cde000041de751589edef47b4eda6cd76 starting on Microsoft Windows 10.0.19045 (.NET 10.0.13, "X64"). Program folder: C:\Users\BRA008\AppData\Local\Temp\claude\C--Users-BRA008-Modular-Projects-DicomTemplateMakerCSharp\ac139809-8102-42ad-b83a-cc0ccc04b717\scratchpad\rc4\. Working directory: C:\Users\BRA008\AppData\Local\Temp\claude\C--Users-BRA008-Modular-Projects-DicomTemplateMakerCSharp\ac139809-8102-42ad-b83a-cc0ccc04b717\scratchpad\rc4.
2026-11-13 08:55:18.043 -08:00 [INF] DicomTemplateMakerGUI.App: Template folder: C:\Users\BRA008\AppData\Local\Temp\claude\C--Users-BRA008-Modular-Projects-DicomTemplateMakerCSharp\c9a3d172-c373-4551-997f-215ab20349dd\scratchpad\phase-e\root (the saved template folder). Window settings: C:\Users\BRA008\AppData\Local\DicomTemplateMaker\ui-settings.json.
2026-11-13 09:54:57.383 -08:00 [INF] DicomTemplateMakerGUI.MainWindow: Template folder changed to C:\DTM\Templates.
2026-11-13 09:55:34.682 -08:00 [INF] DicomTemplateMakerGUI.DicomTemplateServices.DicomTemplateRunner: Wrote C:\DTM\Templates\Template_Output\_Targets_RNI_UID2.25.279807263898541607461237963382274624702.dcm (template _Targets_RNI, series 2.25.279807263898541607461237963382274624702, 6 ROIs).
2026-11-13 09:55:34.684 -08:00 [INF] DicomTemplateMakerGUI.MainWindow: Wrote 1 test RT(s) into C:\DTM\Templates\Template_Output for 1 template(s).
```

Reading (2026-11-13), against the checklist:

- Not a clean VM: the program folder is the build scratchpad on the maintainer's workstation (Windows
  10.0.19045, the same build as the workstation), and `%LOCALAPPDATA%\DicomTemplateMaker` was not clean,
  so items 1 and 2 were not run and item 4 could not be tested: both starts read the 2026-11-03 saved
  folder `...\phase-e\root` instead of falling back to the program folder. Whether the network was off
  is not in the log.
- Item 3 (start): the rc.4 exe started twice with the right version, and the first session exited with
  code 0.
- Items 5 and 6: no log line (building from the Online Templates window does not log), but `C:\DTM\Templates`
  holds `_Targets_RNI` (`All_ROIs.json`, `DicomTags.txt`, `Paths.txt`) and `Ontologies\All_Ontologies.json`.
- Item 7: one test RT, `_Targets_RNI`, 6 ROIs, with the four `vhm` slices, written into
  `C:\DTM\Templates\Template_Output` (the output folder chosen was the template folder, not `C:\DTM\Out`).
- Item 8: not shown by the log.
- Item 9: the second session has no "exits" line and there is no later start, so the restart was not
  logged. `ui-settings.json` now holds `"templateRoot": "C:\\DTM\\Templates"` (the change was saved);
  no app instance was running at 2026-11-13 when this was checked.
- No `[WRN]`, `[ERR]` or `[FTL]` line.

Side effect: the workstation's own `ui-settings.json` now points at `C:\DTM\Templates` instead of the
Phase E scratch root.

P0.7c stays not done until the checklist is run on a clean VM.
