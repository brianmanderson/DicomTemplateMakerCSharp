# DicomTemplateMakerCSharp

Windows tools for building and applying DICOM RT Structure Set templates in radiation oncology contouring workflows. Define a structure template once — ROI names, colors, DICOM interpreted types, and FMA/SNOMED ontology codes — then generate pre-populated RT Structure Set files onto DICOM image series, so contouring starts from a standardized structure list instead of an empty one.

**Program download:** grab the compiled build from the [Releases](../../releases) section.
**Video walkthroughs:** [Our DICOM Playlist](https://www.youtube.com/playlist?list=PLudf8Cfe-LuctPJTTfgb2tDBUvyR-ZOQu) on YouTube.

## Components

| Project | What it is |
| --- | --- |
| `DicomTemplateMakerGUI` | WPF desktop app (the released program). Create/edit templates, assign ontology codes, pull shared template definitions (published TG-263 snapshot or your own Airtable tables), and read/write Varian Eclipse structure-template XML. Includes a folder-watcher service for automated runs. |
| `DicomTemplateMakerCSharp` | Console runner. Scans template folders (each holding a `Paths.txt` plus an `ROIs/` folder of per-ROI text files), then walks the DICOM folder trees listed in each `Paths.txt` and writes an RT Structure file into them. |
| `CleaningRTStructureCsharp` | Console utility for cleaning existing RT Structure files. |
| `ROIOntologyClass` | Shared library: ROI and ontology-code classes, template folder loading (JSON and legacy text formats). |
| `TemplateSync` | Platform-neutral library for online templates: Airtable client, local cache, shared snapshot download, write planning, encrypted connection store. Tested by `TemplateSync.Tests`. |
| `TemplateSnapshotTool` | Maintainer/CI tool that exports the shared TG-263 table to `TemplateSnapshots/TG263.json`. |
| `Xaml_Maker` | Converts templates to/from Varian XML structure templates. |
| `AirTableRecords/` | Bundled per-treatment-site template definitions (e.g. `AbdPelv_Anal`, `AbdPelv_Bladder`) with one text file per ROI. |

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

- Windows, .NET Framework 4.8
- Key NuGet packages: fo-dicom 5.0.3, Newtonsoft.Json, WindowsAPICodePack
- Building the `TemplateSync` library and running its tests needs the .NET SDK (8 or later for the library, 10 for the tests): `dotnet test --project TemplateSync.Tests`
