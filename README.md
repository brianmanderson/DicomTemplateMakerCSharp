# DicomTemplateMakerCSharp

Windows tools for building and applying DICOM RT Structure Set templates in radiation oncology contouring workflows. Define a structure template once — ROI names, colors, DICOM interpreted types, and FMA/SNOMED ontology codes — then generate pre-populated RT Structure Set files onto DICOM image series, so contouring starts from a standardized structure list instead of an empty one.

**Program download:** grab the compiled build from the [Releases](../../releases) section.
**Video walkthroughs:** [Our DICOM Playlist](https://www.youtube.com/playlist?list=PLudf8Cfe-LuctPJTTfgb2tDBUvyR-ZOQu) on YouTube.

## Components

| Project | What it is |
| --- | --- |
| `DicomTemplateMakerGUI` | WPF desktop app (the released program, `net10.0-windows`). Create and edit templates, assign ontology codes, pull shared template definitions (published TG-263 snapshot or your own Airtable tables), read and write Varian Eclipse structure-template XML, and run the folder-watching RT generator. |
| `DicomTemplateCore` | Platform-neutral library (`net10.0`) with the template logic shared by the app and the tools: RT Structure Set generation, DICOM series discovery (header-only fo-dicom reads), template folders, Varian XML import and export, Airtable record mapping. Tested by `DicomTemplateCore.Tests`. |
| `ROIOntologyClass` | Platform-neutral library: ROI and ontology-code classes, template folder loading (JSON and legacy text formats). |
| `TemplateSync` | Platform-neutral library for online templates: Airtable client, local cache, shared snapshot download, write planning, encrypted connection store. Tested by `TemplateSync.Tests`. |
| `DicomTemplateMakerCSharp` | Command-line RT generator: applies every template under a folder to the DICOM folders listed in each template's `Paths.txt`. |
| `Xaml_Maker/XamlMakerCsharp` | Command-line converter between template folders and Varian XML structure templates. |
| `CleaningRTStructureCsharp` | Command-line utility that empties the referenced-image lists (Contour Image Sequences) of an RT Structure file, e.g. to make a reusable template RT. |
| `TemplateSnapshotTool` | Maintainer/CI tool that exports the shared TG-263 table to `TemplateSnapshots/TG263.json`. |
| `AirTableRecords/` | Bundled per-treatment-site template definitions (e.g. `AbdPelv_Anal`, `AbdPelv_Bladder`). |

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
dotnet run --project DicomTemplateMakerCSharp -- <template-folder> [--once] [--delete-generated]
dotnet run --project Xaml_Maker/XamlMakerCsharp -- import <folder-of-xml-files> <template-folder>
dotnet run --project Xaml_Maker/XamlMakerCsharp -- export <template-folder> <xml-output-folder>
dotnet run --project CleaningRTStructureCsharp -- <input-RS.dcm> <output.dcm>
```

## Releases

GitHub Actions (`.github/workflows/ci.yml`) builds and tests every push and pull request on Windows
and Linux and checks formatting. Pushing a tag such as `v1.1.0` also publishes a self-contained,
single-file Windows x64 build to the GitHub release for that tag. Run the TG-263 snapshot workflow
first so the release carries an offline copy of the shared templates.
