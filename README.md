# DicomTemplateMakerCSharp

Windows tools for building and applying DICOM RT Structure Set templates in radiation oncology contouring workflows. Define a structure template once — ROI names, colors, DICOM interpreted types, and FMA/SNOMED ontology codes — then generate pre-populated RT Structure Set files onto DICOM image series, so contouring starts from a standardized structure list instead of an empty one.

**Program download:** grab the compiled build from the [Releases](../../releases) section.
**Video walkthroughs:** [Our DICOM Playlist](https://www.youtube.com/playlist?list=PLudf8Cfe-LuctPJTTfgb2tDBUvyR-ZOQu) on YouTube.

## Components

| Project | What it is |
| --- | --- |
| `DicomTemplateMakerGUI` | WPF desktop app (the released program, `net10.0-windows`). Create and edit templates, assign ontology codes, pull shared template definitions (published TG-263 snapshot or your own Airtable tables), read and write Varian Eclipse structure-template XML, and run the folder-watching RT generator. |
| `DicomTemplateMaker.Presentation` | Platform-neutral library (`net10.0`) behind the app's windows: view models, the RT generator service, confirmations and reports, settings, template-folder choice and validation rules. No WPF types, so it is tested on any OS by `DicomTemplateMaker.Presentation.Tests`. |
| `DicomTemplateCore` | Platform-neutral library (`net10.0`) with the template logic shared by the app and the tools: RT Structure Set generation, DICOM series discovery (header-only fo-dicom reads), template folders, Varian XML import and export, Airtable record mapping. Tested by `DicomTemplateCore.Tests`. |
| `ROIOntologyClass` | Platform-neutral library: ROI and ontology-code classes, template folder loading (JSON and legacy text formats). |
| `TemplateSync` | Platform-neutral library for online templates: Airtable client, local cache, shared snapshot download, write planning, encrypted connection store. Tested by `TemplateSync.Tests`. |
| `DicomTemplateMakerCSharp` | Command-line RT generator: applies every template under a folder to the DICOM folders listed in each template's `Paths.txt`. |
| `Xaml_Maker/XamlMakerCsharp` | Command-line converter between template folders and Varian XML structure templates. |
| `CleaningRTStructureCsharp` | Command-line utility that empties the referenced-image lists (Contour Image Sequences) of an RT Structure file, e.g. to make a reusable template RT. |
| `TemplateSnapshotTool` | Maintainer/CI tool that exports the shared TG-263 table to `TemplateSnapshots/TG263.json`. |
| `AirTableRecords/` | Bundled per-treatment-site template definitions (e.g. `AbdPelv_Anal`, `AbdPelv_Bladder`). |

## Generating RT Structure Sets

Each template is a folder under the template folder holding `All_ROIs.json`, a `Paths.txt` listing the
DICOM folders to watch, and an optional `DicomTags.txt` with Series/Study Description requirements.
The generator (the app's *Start RT generator*, or the `DicomTemplateMakerCSharp` command-line tool)
writes `<template>_UID<SeriesInstanceUID>.dcm` next to each matching image series:

- **One RT per series.** Every series in a folder is decided on its own; a series whose RT exists is
  skipped. Patient and study attributes come from that series only (missing ones are written empty),
  and each RT gets its own UIDs. A series whose images do not all share one Patient ID, Study Instance
  UID and Frame of Reference gets no RT, and the error says why.
- **Planning images only.** CT, MR and PET series get an RT; localizers (scouts, topograms), Secondary
  Capture objects such as dose reports, and other modalities are skipped.
- **Matching.** A template without requirements matches every series. Otherwise, ignoring case, a
  description matches when it contains a requirement or is contained in one; a blank or missing
  description never matches. Because the test works both ways, a short description such as "T2"
  matches a requirement "Prostate T2 AX": write requirements that a description will contain.
- **Incomplete transfers.** A folder is processed once its files have not changed for 10 seconds.
  Files still being written are retried with back-off. An existing RT is never replaced; if images
  arrive after a series' RT was written (a transfer that paused for longer than that), the RT is
  reported as incomplete until the file is deleted, and the generator then writes it again.
- **Problems are reported, not hidden.** An ROI that cannot be written (for example a name the images'
  character set cannot hold, or an ontology code without a code value or scheme) is left out and
  reported; an RT is never written with zero ROIs; an unreadable template is reported and skipped. A
  folder that gets no RT because of a problem stays listed in the app's status bar until it is fixed.
- **Deleting generated RTs** removes only `<template>_UID*.dcm` files. A running generator writes an RT
  for every matching series that has none, so it would write them again within seconds: the app stops
  the generator for the delete and leaves it stopped. To keep the RTs deleted, remove the folders from
  the template's monitored folders (or change its requirements) before starting the generator again.
- **Copies of a template** ("Copy selected") get no monitored folders, so they write no RTs until
  folders are added in their editor.

Command-line tool: `DicomTemplateMakerCSharp [template-folder] [--once] [--delete-generated]`. The
template folder defaults to the current folder; without `--once` it scans every 3 seconds until
Ctrl+C. It exits with 0 on success (including a watch stopped with Ctrl+C); 1 when a `--once` or
`--delete-generated` run hit errors or was stopped with Ctrl+C before it finished, or when the template
RT file (`template_RS.dcm`, next to the program) is missing, in which case nothing is generated; and 2
for bad arguments (an unknown option, more than one folder, or a folder that does not exist).

## Where the program keeps its files

- **Templates and ontologies:** the template folder shown in the main window. It is saved and used
  again at the next start. Without a saved folder, the program uses the folder it was started in
  (a shortcut's "Start in") when that folder holds templates and the program folder does not, and
  saves that choice; otherwise it uses its own folder. When the saved folder is missing (a disconnected
  drive), it is kept for next time and this session uses the program folder, with a note at startup.
  Use *Change template folder* to choose another one.
- **Logs:** `%LOCALAPPDATA%\DicomTemplateMaker\logs`: a new file every day or when a file reaches
  10 MB, and the newest 14 files are kept. *Open log folder* in the main window opens it. Include the
  latest log when reporting a problem.
- **Settings and online-template cache:** `%LOCALAPPDATA%\DicomTemplateMaker` (`ui-settings.json` for
  the template folder and remembered folders, `settings.json` for online templates, encrypted Airtable
  connections, cached tables). A damaged `ui-settings.json` is copied to `ui-settings.json.bak` before
  it is replaced.

## Online templates and Airtable

The program reads templates from two kinds of online source. Both are cached on your computer, so
opening the program and browsing templates normally makes **no** network calls at all.

- **Shared TG-263 templates (no account needed).** These are published as
  [`TemplateSnapshots/TG263.json`](TemplateSnapshots/) and downloaded over HTTPS at most once a day,
  or when you press **Refresh**. A copy ships with each release for offline use. No Airtable token
  is involved, so using the program never spends anyone's Airtable API allowance.
- **Your own Airtable tables (read and write).** In *Load Online Templates*, choose
  *Add Airtable table...* and enter a name, a personal access token, the base id (`app...`) and
  the table id (`tbl...`). The program tests the connection with a single request before saving.

### How Airtable is used

| Action | Airtable API calls |
| --- | --- |
| Start the program, open the online-templates window | 0 (cached copy) |
| Cached copy older than 24 hours, or **Refresh** | usually 1 (only changed records are fetched) |
| **Full refresh**, or automatically once a week | 1 per 100 records |
| Write a template | 1 refresh + 1 per 10 changed ROIs; unchanged ROIs cost nothing |

The previous version fetched every record individually on every load (about 250 calls for the
246-record TG-263 table), which exhausted the Free plan's 1,000 calls per month after a few loads.

Tokens are encrypted for your Windows account (DPAPI) under
`%LOCALAPPDATA%\DicomTemplateMaker`, together with the cached tables and an optional
`settings.json`. The `AIRTABLE_PAT` environment variable, if set, overrides stored tokens. Old
plain-text files in the `AirTables` folder are moved into encrypted storage and deleted on first
start. Create tokens at https://airtable.com/create/tokens with the smallest scopes that work:
`data.records:read`, plus `data.records:write` only for tables you write to, limited to the one base.

Optional `settings.json` keys: `cacheTimeToLiveHours` (default 24), `fullRefreshIntervalDays`
(default 7), `snapshotCheckIntervalHours` (default 24) and `sharedSnapshotUrl`.

### Maintainers: publishing the shared TG-263 snapshot

`.github/workflows/refresh-template-snapshot.yml` exports the TG-263 Airtable table weekly (or on
demand from the Actions tab) and commits `TemplateSnapshots/TG263.json` when it changes. It costs
one API call per 100 records. Add a repository secret `AIRTABLE_PAT` holding a token with
`data.records:read` on the TG-263 base only, then run the workflow once so the snapshot exists
before the next release. See [`TemplateSnapshots/README.md`](TemplateSnapshots/README.md).

The shared base can still be copied for your own use from https://airtable.com/shrk6XcfLggdTwetI.

### Security notice

Earlier versions of this repository and its release downloads contained Airtable access tokens.
They have been removed from the current code, but they remain in git history and in old release
assets. Anyone who owns one of those tokens should revoke it in Airtable
(https://airtable.com/create/tokens). Removing a file from git does not disable the token.

## Requirements

- **Using the program:** 64-bit Windows 10 or later. Release downloads are self-contained, so no
  .NET runtime needs to be installed.
- **Building:** the .NET 10 SDK (see `global.json`). The whole solution, including the WPF app,
  builds on Windows, Linux and macOS; the app itself runs only on Windows.

## Building and testing

```
dotnet build DicomTemplateMaker.sln
dotnet test --solution DicomTemplateMaker.sln
dotnet format DicomTemplateMaker.sln --verify-no-changes
```

Warnings are errors and nullable reference types are on for every project
(`Directory.Build.props`). Package versions live in `Directory.Packages.props`. The tests include
golden files that pin the Varian XML import and export; set `UPDATE_GOLDEN=1` only when a change to
that output is intended, and review the diff.

Command-line tools:

```
dotnet run --project DicomTemplateMakerCSharp -- [template-folder] [--once] [--delete-generated]
dotnet run --project Xaml_Maker/XamlMakerCsharp -- import <folder-of-xml-files> <template-folder>
dotnet run --project Xaml_Maker/XamlMakerCsharp -- export <template-folder> <xml-output-folder>
dotnet run --project CleaningRTStructureCsharp -- <input-RS.dcm> <output.dcm>
```

## Releases

GitHub Actions (`.github/workflows/ci.yml`) builds and tests every push and pull request on Windows
and Linux and checks formatting. Pushing a tag such as `v1.1.0` also publishes a self-contained,
single-file Windows x64 build to the GitHub release for that tag. Run the TG-263 snapshot workflow
first so the release carries an offline copy of the shared templates.
