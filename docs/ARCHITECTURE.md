# Architecture

How DicomTemplateMakerCSharp works, as five Mermaid diagrams with one paragraph each. Every node is a
project, a file, a class or a workflow step of this repository at `08eca39` (2026-11-03). Companion
documents: `README.md` (what the program does), `docs/STATUS.md` (where the overhaul stands),
`docs/EVALUATION_PROTOCOL.md` (the evidence procedures).

## 1. Components and dependencies

```mermaid
flowchart TB
  subgraph app ["Windows app (the released program)"]
    GUI["DicomTemplateMakerGUI (WPF, net10.0-windows)"]
  end
  subgraph libs ["Platform-neutral libraries (net10.0)"]
    PRES["DicomTemplateMaker.Presentation"]
    CORE["DicomTemplateCore"]
    ROI["ROIOntologyClass"]
    SYNC["TemplateSync"]
  end
  subgraph cli ["Command-line tools"]
    RUNNER["DicomTemplateMakerCSharp (RT generator)"]
    XAML["Xaml_Maker/XamlMakerCsharp (Varian XML import and export)"]
    CLEAN["CleaningRTStructureCsharp"]
    SNAP["TemplateSnapshotTool (maintainers and CI)"]
    EVAL["TemplateEvaluationTool (internal, not shipped)"]
  end
  subgraph tests ["Test projects (xunit v3)"]
    T1["DicomTemplateCore.Tests"]
    T2["DicomTemplateMaker.Presentation.Tests"]
    T3["TemplateSync.Tests"]
    T4["TemplateEvaluationTool.Tests"]
  end
  FO["fo-dicom 5.2.6 (Directory.Packages.props)"]
  GUI --> PRES
  GUI --> CORE
  GUI --> ROI
  GUI --> SYNC
  PRES --> CORE
  PRES --> ROI
  PRES --> SYNC
  CORE --> ROI
  CORE --> SYNC
  RUNNER --> CORE
  XAML --> CORE
  SNAP --> SYNC
  EVAL --> PRES
  T1 --> CORE
  T2 --> PRES
  T3 --> SYNC
  T4 --> EVAL
  CORE -.-> FO
  CLEAN -.-> FO
```

The arrows are the `ProjectReference` entries of the csproj files. `DicomTemplateMakerGUI` is the only
Windows-only project; its code-behind shows what `DicomTemplateMaker.Presentation` decides (view models,
rules such as `NameRules` and `AddRoiRules`, the `RunnerService`, confirmations in `ShellMessages` and
`EditorMessages`, settings in `UiSettingsStore`). `DicomTemplateCore` holds the RT generator
(`DicomTemplateServices/`), the template logic and the Varian XML reader and writer (`Services/`) and the
TPS-realism checker (`Validation/`); it depends on `ROIOntologyClass` (the `ROIClass` and
`OntologyCodeClass` data model and template-folder files) and on `TemplateSync` (Airtable client, snapshot
and cache, write planner, encrypted connection store), because `TemplateEntryMapper` maps Airtable rows to
ROIs. Three command-line tools sit on `DicomTemplateCore` or `TemplateSync`; `TemplateEvaluationTool`
sits on the Presentation library because it drives the same `SiteBuilder` the window uses.
`CleaningRTStructureCsharp` references only fo-dicom. All four test projects run on any OS; the WPF
project is compile-checked on Linux through `EnableWindowsTargeting`.

## 2. Data flow

```mermaid
flowchart LR
  AT["Airtable table appYl68lenTOFqTDh / tblex7IPsmm8hvVEc (shared TG-263)"]
  WF["refresh-template-snapshot.yml: TemplateSnapshotTool export, check, pull request"]
  SNAPJ["TemplateSnapshots/TG263.json on main"]
  RAW["raw.githubusercontent.com (conditional GET with ETag, at most daily)"]
  SRC["SnapshotTableSource (TemplateSync/Sync)"]
  CACHE["cache\\TG263.json in the app data folder (SnapshotStore)"]
  BUNDLED["TemplateSnapshots\\TG263.json in the program folder (offline copy)"]
  OWN["Your own Airtable table (token entered in Add Airtable table)"]
  ATS["AirtableTableSource + AirtableHttpClient (delta reads, cached table)"]
  WP["TemplateSourceItem.WriteTemplatesAsync + WritePlanner (refresh, then batched creates and updates)"]
  VM["OnlineTemplatesViewModel and SiteBuilder (Presentation/Services)"]
  TF["Template folder: {root}\\{site}\\All_ROIs.json, Paths.txt, DicomTags.txt and {root}\\Ontologies\\All_Ontologies.json"]
  RUN["DicomTemplateRunner (Core) via RunnerService in the app or the DicomTemplateMakerCSharp tool"]
  RSB["RtStructureBuilder (clones the seed template_RS.dcm)"]
  RT["{template}_UID{SeriesInstanceUID}.dcm written beside the images"]
  TPS["Planning system (import of CT and RT)"]
  VX["Varian StructureTemplate XML files"]
  VR["VarianXmlReader and VarianXmlWriter (Core/Services) via VarianXmlBatch in the app or XamlMakerCsharp"]
  RTIN["An existing RT Structure Set"]
  INT["RtStructureSetFile.TryInterpret then TemplateMaker.interpret_RT (Add ROIs from an RT)"]
  AT --> WF --> SNAPJ --> RAW --> SRC
  BUNDLED --> SRC
  SRC <--> CACHE
  SRC --> VM
  OWN <--> ATS
  ATS --> VM
  VM --> TF
  TF --> WP --> OWN
  TF --> RUN --> RSB --> RT --> TPS
  VX <--> VR
  VR <--> TF
  RTIN --> INT --> TF
```

Templates reach the program by three routes and leave it by two. The shared TG-263 table is never read
by the program directly: the weekly workflow exports it to `TemplateSnapshots/TG263.json`, the program
downloads that file over HTTPS with an ETag (`SnapshotTableSource`, at most once a day or on Refresh),
keeps it in `cache\` and falls back to the copy bundled with the release, so browsing the shared templates
costs no Airtable calls. A user's own table goes through `AirtableTableSource`, which pages the table once,
then fetches only changed records (`TableFetcher`, `filterByFormula`) and keeps the result in the same
cache; writes go back through `WritePlanner` in batches of ten after a refresh. Both sources feed
`OnlineTemplatesViewModel`, whose `SiteBuilder` writes one template folder per site (`All_ROIs.json` plus
`Paths.txt` and `DicomTags.txt`, and the shared code library `Ontologies\All_Ontologies.json`). The
generator (`DicomTemplateRunner`) reads those folders and writes one RT Structure Set per planning series
next to the images, which the planning system imports with the CT. The side path converts template folders
to and from Varian StructureTemplate XML (`VarianXmlReader`, `VarianXmlWriter`); the reverse path builds a
template from an existing RT's ROI names, colours, types and codes (`RtStructureSetFile.TryInterpret`).

## 3. The generator's decisions for one series

```mermaid
flowchart TD
  A["For each template and each folder in its Paths.txt (DicomTemplateRunner.RunOnce or RunAsync, every 3 s)"] --> B{"Folder exists?"}
  B -- no --> S1["Skipped: PathNotFound"]
  B -- yes --> C{"Folder can be listed? (an IOException is transient)"}
  C -- no --> S2["Skipped: Waiting, looked at again next cycle"]
  C -- yes --> D{"Gave up after 5 transient failures, or still backing off?"}
  D -- yes --> S3["Skipped: PreviouslyFailed or BackingOff (retried when the files change)"]
  D -- no --> E{"Same file fingerprint and template signature as when last processed, outputs still exist?"}
  E -- yes --> S4["Skipped: Unchanged (PreviouslyFailed when errors were reported then)"]
  E -- no --> F{"Fingerprint unchanged since the previous scan and newest file at least 10 s old? (SettleTime)"}
  F -- no --> S5["Skipped: Waiting for the files to settle"]
  F -- yes --> G["DicomParser: header-only reads, image objects only, grouped by SeriesInstanceUID in slice order"]
  G --> H["For each series in the folder"]
  H --> I{"Series Instance UID usable in a file name?"}
  I -- no --> E1["Error: invalid UID, no RT"]
  I -- yes --> J{"{template}_UID{uid}.dcm already exists?"}
  J -- yes --> K{"Does it reference every image of the series?"}
  K -- no --> E2["Error: RT incomplete; delete it and it is written again"]
  K -- yes --> S6["Skipped: AlreadyDone"]
  J -- no --> L{"PlanningImages.WhyNotPlanningSeries: Modality CT, MR or PT, not Secondary Capture, not a localizer?"}
  L -- no --> S7["Skipped: NoMatch, with the reason"]
  L -- yes --> M{"TemplateMatcher.Matches(DicomTags.txt requirements, Series Description, Study Description)?"}
  M -- no --> S8["Skipped: NoMatch (blank descriptions never match; otherwise two-way substring, ignoring case)"]
  M -- yes --> N["RtStructureBuilder.Build(slices, template name, ROIs, now)"]
  N --> O{"All slices share one Patient ID, Study Instance UID and Frame of Reference?"}
  O -- no --> E3["Error: the series mixes patients, studies or frames; no RT"]
  O -- yes --> P["Per ROI: Validate (name, interpreted type, code value, scheme and meaning) and CanEncode in the RT's character set"]
  P --> Q{"At least one ROI writable?"}
  Q -- no --> E4["Error: an RT is never written with zero ROIs; each ROI failure is reported"]
  Q -- yes --> R["SaveAtomically, then Written (ROI failures still reported in the RunReport)"]
```

`DicomTemplateRunner` (`DicomTemplateCore/DicomTemplateServices/DicomTemplateRunner.cs`) visits every
folder named in every template's `Paths.txt` once per cycle and records each outcome in a `RunReport`
(written RTs, skipped folders and series with a `SkipReason`, ROI failures, errors), which the app's status
line and log and the command-line tool's exit code are built from. A folder is only read when its files
have stopped changing (`DirectoryFingerprint`: names, sizes and last-write times, generated RTs excluded)
and its newest file is at least `SettleTime` old; a folder that fails with a transient I/O error is retried
with back-off up to five times and then until its files change. `DicomParser` reads headers only and groups
image objects by `SeriesInstanceUID`, so every series in a folder is decided on its own. An RT that already
exists is never replaced, but one that does not reference every image of its series is reported as
incomplete. `PlanningImages` and `TemplateMatcher` decide whether a series gets an RT at all;
`RtStructureBuilder` then clones `template_RS.dcm`, copies the patient and study attributes from that
series only (missing ones written empty), gives the RT new UIDs and writes one Structure Set ROI, ROI
Contour and RT ROI Observations item per ROI whose name, type and code are complete and encodable. The
file is written to a temporary name and moved into place.

## 4. CI and evidence

```mermaid
flowchart TB
  subgraph ci ["ci.yml: push to main, pull requests, tags v*, manual dispatch"]
    J1["build-and-test (windows-latest and ubuntu-latest): dotnet restore, build -c Release, test --solution, format --verify-no-changes on Linux"]
    subgraph J2 ["dicom-validation (ubuntu-latest, service container jodogne/orthanc:1.13.0)"]
      V1["dotnet build TemplateEvaluationTool -c Release"]
      V2["build-sites --snapshot TemplateSync.Tests/TestData/TG263.2026-09-30.json --naming default (53 sites)"]
      V3["generate-rts --images DicomTemplateMakerGUI/SmallCT (53 RTs, 1,178 ROIs)"]
      V4["tools/dicom-validation/validate_rts.py: dciodvfy -new (dicom3tools 20260927) and validate_iods --edition 2025b gated, current edition reported (dicom-validator 0.9.0)"]
      V5["realism --allowlist: RtRealismChecker rules RR1 to RR10"]
      V6["tools/dicom-validation/orthanc_smoke.py: C-STORE the 53 RTs and 4 CT slices with pynetdicom, read names, colours, types and codes back over REST"]
      V7["artifact dicom-validation-reports: validators/, realism/, orthanc/"]
      V1 --> V2 --> V3 --> V4 --> V5 --> V6 --> V7
    end
    J3["release (tags v* only, after build-and-test): dotnet publish the GUI as a self-contained single-file win-x64 build, zip, gh release create"]
  end
  AL["docs/evidence/validator-allowlist.json: validate_iods C9 (retired ROI Observation Label) and realism RR8 D1b (code values 88 and 99); entries may only be removed"]
  AL -. gates the errors of .-> V4
  AL -. gates the errors of .-> V5
  subgraph ev ["Committed baseline docs/evidence/baseline-e7f0572/ (the same commands run locally, 2026-10-15 to 2026-10-18)"]
    E1["build-sites/: build-warnings.json (empty), sites.json, manifest.json"]
    E2["generate-rts/: run-report.json (UIDs and times masked), manifest.json"]
    E3["inventory/: one masked element inventory per RT (53)"]
    E4["validators/, realism/, nomenclature/: the reports of the same tools, plus the AAPM worksheet check"]
  end
  subgraph wf ["refresh-template-snapshot.yml: Mondays 06:17 UTC, or manual dispatch"]
    W1["Is the AIRTABLE_PAT secret present? (a dispatch fails without it, a scheduled run only warns)"]
    W2["TemplateSnapshotTool export --sort-field Record (3 Airtable calls for 246 records)"]
    W3{"Snapshot changed?"}
    W4["TemplateSnapshotTool check: at least 240 records, Structure, SchemeCode and Scheme present, Colors_RGB parse; warnings for folder names, duplicates, Type, SCTIDs"]
    W5["Push snapshot-refresh/{branch}, open or update the pull request with the check summary, dispatch ci.yml on it"]
    W6["End: Unchanged"]
    W1 --> W2 --> W3
    W3 -- yes --> W4 --> W5
    W3 -- no --> W6
  end
```

Three jobs and one more workflow produce the evidence. `build-and-test` is the gate every pull request
must pass (warnings are errors, formatting is checked on Linux). `dicom-validation` rebuilds the 53 TG-263
sites from the frozen fixture, not from the live snapshot, generates one RT per site on the sample CT and
runs two independent validators, the realism rules and an Orthanc transfer; an error passes only when an
entry of `docs/evidence/validator-allowlist.json` names the conformance gap (C-id) or pending decision that
explains it, and the allowlist may only shrink. The `release` job runs only for a `v*` tag and has not run
yet; it warns, but does not fail, when the bundled snapshot is missing. The committed baseline under
`docs/evidence/baseline-e7f0572/` was produced by the same commands locally and is what any later change
to RT content is diffed against; its inventories mask UIDs, dates, times and every patient attribute so
they can be committed. The snapshot workflow is the only thing that spends Airtable quota (three calls a
week); a changed export is proposed as a pull request from `snapshot-refresh/<branch>` rather than pushed.

## 5. Runtime file layout

```mermaid
flowchart LR
  subgraph data ["App data folder: LOCALAPPDATA\\DicomTemplateMaker (AppPaths.DataDirectory, TemplateSourceCatalog.AppDataDirectory)"]
    D1["ui-settings.json and ui-settings.json.bak: template folder, remembered dialog folders, Varian XML share (UiSettingsStore)"]
    D2["settings.json (optional): cacheTimeToLiveHours, fullRefreshIntervalDays, snapshotCheckIntervalHours, sharedSnapshotUrl (SyncSettingsFile)"]
    D3["airtable-connections.json: named connections with DPAPI-encrypted tokens (AirtableConnectionStore, DpapiTokenProtector)"]
    D4["cache\\{name}.json: the shared snapshot and each table's cached copy with ETag and sync cursor (SnapshotStore)"]
    D5["logs\\: Serilog files, a new file daily or at 10 MB, newest 14 kept (AppLog)"]
  end
  subgraph prog ["Program folder: AppContext.BaseDirectory (the release zip)"]
    P1["DicomTemplateMakerGUI.exe (self-contained single file)"]
    P2["template_RS.dcm: the seed every RT is cloned from"]
    P3["SmallCT\\vhm.1001.dcm to vhm.1004.dcm: sample CT with the fictitious patient ZZTEST^TG263TEMPLATE"]
    P4["FMA_SNOMEDCT_Key.txt: table for Change Ontology Scheme"]
    P5["Structure Template.xml: Varian sample"]
    P6["TemplateSnapshots\\TG263.json: bundled offline snapshot"]
  end
  subgraph root ["Template folder: the saved folder, else the program folder (TemplateRootResolver)"]
    R1["{template}\\All_ROIs.json: the ROIs (ROIClass.RoisFileName)"]
    R2["{template}\\Paths.txt: monitored DICOM folders (RunnerTemplate.PathsFileName)"]
    R3["{template}\\DicomTags.txt: Series and Study Description requirements (RunnerTemplate.DicomTagsFileName)"]
    R4["Ontologies\\All_Ontologies.json: the shared code library (OntologyCodeClass.LibraryFileName)"]
    R5["{template}\\ROIs\\*.txt: legacy text format, migrated to All_ROIs.json on load"]
  end
```

The program writes user state only under `%LOCALAPPDATA%\DicomTemplateMaker`: window settings
(`ui-settings.json`, copied to `.bak` before a damaged one is replaced), the optional online-template
settings, the encrypted Airtable connections (the `AIRTABLE_PAT` environment variable overrides every stored
token), the snapshot and table caches, and the logs. It reads its data files from its own folder, never from
the working directory; the five files listed there are copied next to the exe by the GUI csproj and kept
outside the single-file bundle so they can be opened by path. Templates live in the template folder shown
in the main window: one sub-folder per template with the three files above, plus the shared `Ontologies`
library at the root; a template whose `All_ROIs.json` is missing is migrated from the legacy `ROIs\*.txt`
files, and files that fail to parse are never deleted or saved over. The evaluation tool never touches the
app data folder: every command takes `--data-dir` or writes only under its `--out` folder.
