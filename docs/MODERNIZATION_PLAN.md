# Modernization plan

Status: Phase 0 (audit) complete. This document records what the audit found, the API-call budget
before and after, the decisions taken, and the order of work. Evidence labels: **documented**
(Airtable/GitHub/NuGet documentation), **measured** (observed in this repository, its history, or
the live table metadata), **inferred** (reasoned, not observed), **unknown**.

## 1. Why the Airtable quota runs out

### 1.1 Facts

| Fact | Value | Basis |
| --- | --- | --- |
| Records in the shared TG-263 table (`appzWlVKRp9TrrTUJ` / `tblltR3aTxlJUwaGa`) | 246 | measured (Airtable metadata, `totalRecordCount`) |
| Free plan allowance | 1,000 API calls per workspace per month, strictly enforced since 2025-01-22, resets on the 1st | documented |
| Team plan allowance | 100,000 per workspace per month | documented |
| Rate limit | 5 requests/s per base, 50 requests/s per token; a 429 requires a 30 s wait | documented |
| Monthly limit exceeded | every request returns HTTP 429 `PUBLIC_API_BILLING_LIMIT_EXCEEDED` | documented |
| Page size | default and maximum 100 records | documented |
| Records per create/update request | at most 10 | documented |
| Legacy `key...` API keys | stopped working on 2024-02-01 | documented |

### 1.2 Root causes (all measured in the code at `dd90dc8`)

1. **N+1 reads.** `ReadAirTable.read_recordsAsync` pages `ListRecords` (which already returns every
   field) and then calls `RetrieveRecord` once per record. One load costs
   `max(1, ceil(R/100)) + R` calls: **249 calls** for the TG-263 table.
2. **Endless loop on a failing page.** `return_recordsAsync` never clears `offset` on an error, so a
   failure on page 2 or later (for example the monthly-limit 429) repeats the same request until the
   process is killed. The client library also retries every 429 three times after 2, 4 and 8 s,
   shorter than Airtable's 30 s penalty, so each logical call can become 4 HTTP requests.
3. **No cache.** Every process pays the full load again: opening *Load Online Templates* reads every
   table, including TG-263; *Load* in the main window reads the writeable tables.
4. **One shared token for everyone.** A personal access token for the TG-263 base was committed to
   the public repository and copied into every build and release. Every install spends the same
   workspace's allowance. Four sessions a month across all users exhaust a Free workspace.
5. **Unbatched writes.** One request per ROI, plus a second request per new ROI to copy the record id
   into an `Id` column, and a PATCH for nearly every existing ROI even when nothing changed.

The audit also found that **every Airtable write has failed since commit `d7d6583`
(2025-12-08)**: two computed `bool` properties added to `AirTableEntry` are picked up by the
reflection-based compare and payload code, which throws `InvalidCastException` for existing records
and sends unknown fields for new ones.

### 1.3 Call budget, before and after

| Journey | Before | After (Phase 1) |
| --- | --- | --- |
| Start the program | 0 | 0 |
| Open online templates, TG-263 | 249 per process | **0 Airtable calls**; at most one conditional HTTPS request per day to GitHub |
| Open online templates, own table of R records, cache under 24 h old | R + ceil(R/100) | 0 |
| Same, cache expired, or **Refresh** | R + ceil(R/100) | usually 1 (changed records only) |
| Weekly full refresh or **Full refresh** | n/a | ceil(R/100) |
| Write a template of n ROIs, c new and u changed | u + 2c (currently always fails) | 1 + ceil(c/10) + ceil(u/10); unchanged ROIs cost 0 |
| Snapshot publication (CI, weekly) | n/a | 3 per run, about 13 per month |
| Failure on a later page | unbounded | 1; the fetch stops and the cached copy is shown |

Worked example, one month with ten users who each open online templates twice: before, 20 × 249
= 4,980 calls against one Free workspace (exhausted after 4 sessions); after, about 13 calls (the
CI export), plus deltas for people who maintain their own tables.

## 2. Decisions

| Decision | Choice | Why |
| --- | --- | --- |
| Shared TG-263 read path | Publish a JSON snapshot in the repository, refreshed by a scheduled GitHub Action; the app downloads it anonymously with ETag and bundles a copy | Removes Airtable calls and tokens from every install. Verified: raw.githubusercontent.com returns a strong ETag, answers 304 and allows `max-age=300` (measured). |
| Airtable client | Replace the `Airtable` NuGet client with a small `HttpClient`-based client in a new `TemplateSync` library | The library's built-in 429 retry is shorter than Airtable's penalty and cannot tell the billing limit from the rate limit; the GUI referenced 1.2.0 while `packages.config` said 1.8.0; a thin client is testable with a fake `HttpMessageHandler` and brings no transitive dependencies into the .NET Framework 4.8 build. |
| Field selection | Request only mapped columns; if Airtable answers 422 `UNKNOWN_FIELD_NAME`, drop that column, remember it, and retry | The live TG-263 table has no `CommonName` or `RGB` column (measured), so a fixed field list would make every read fail. |
| Delta refresh | `OR(IS_AFTER(LAST_MODIFIED_TIME(), DATETIME_PARSE(t)), IS_AFTER(CREATED_TIME(), DATETIME_PARSE(t)))` with `t` = server `Date` of the previous fetch minus 2 minutes | Uses Airtable's clock, so local clock skew cannot skip changes; `CREATED_TIME()` covers never-edited records (inferred safety). Deleted records are caught by the weekly full refresh. |
| Writes | Refresh changes first, plan the minimal creates/updates, send 10 per request, update the cache from the responses, never write `Id` | Avoids re-adding sites other people removed; one refresh call is cheaper than a lost edit. If the table needs an `Id` column, make it a `RECORD_ID()` formula field. |
| Credentials | Windows DPAPI (current user) in `%LOCALAPPDATA%\DicomTemplateMaker`, `AIRTABLE_PAT` override, legacy files migrated and deleted | Tokens were plain text next to the exe and in releases. |
| Record order in the snapshot | Server-side sort by the autonumber `Record` column | Airtable's default order is unspecified (documented as unspecified); order decides duplicate resolution and ROI order within a template. |
| Colour parse failures | Keep the ROI in red and report it | The old code silently dropped the ROI from the template, which is the more dangerous failure. |
| `DVH_Type_Index` / `DVH_ContourStyle` on read | Still ignored, as before | Applying them would change generated templates; left for the maintainers to decide (open question 1). |
| Phase order | 0, 1, 3, 2 | Phase 1 must ship before Phase 3. Doing Phase 2 after the migration avoids hand-editing `packages.config` references for logging and MVVM packages twice. |
| Branching | One branch and pull request, one or more commits per phase | The session may push to a single branch; each phase's commits build and pass tests on their own, so Phase 1 can be cherry-picked and released alone. |

## 3. Work by phase

### Phase 1: Airtable access (shippable on .NET Framework 4.8)
- `TemplateSync` library (netstandard2.0): client with per-base throttle, bounded retries,
  quota/rate-limit distinction; list-only paging; cache with atomic writes; delta and full refresh;
  snapshot source; write planner and batching; encrypted connection store; settings file.
- GUI: replaces `ReadAirTable` with awaitable sources; Refresh/Full refresh; cache age shown;
  connection test before saving a table; masked token entry; confirmation before removing a table.
- Security: token file untracked, legacy key removed, `.gitignore` guards.
- Tests: `TemplateSync.Tests` (xunit v3) with a fake HTTP handler and fake clock.
- CI: `refresh-template-snapshot.yml` and `TemplateSnapshotTool`.

### Phase 3: platform and build
See section 5. Done: every project is SDK-style in one solution (`DicomTemplateMaker.sln`); the GUI
targets `net10.0-windows` and everything else `net10.0`; duplicated sources live once in
`DicomTemplateCore`; SimpleITK and WindowsAPICodePack are gone; nullable warnings are fixed, not
suppressed; CI builds and tests on Windows and Linux and publishes tagged releases.

### Phase 2: stability and usability
See section 6.

## 4. Actions only the maintainers can take

1. **Revoke every published token.** The full history and the release downloads hold 4 personal
   access tokens and 4 legacy keys (measured in a full clone and in the v1.0.0 to v1.0.4 release
   assets). Legacy `key...` keys stopped working in 2024; the personal access tokens may still work:

   | Prefix | Base | Where it was published |
   | --- | --- | --- |
   | `patQ` | current TG-263 base `appzWlVKRp9TrrTUJ` | git history, v1.0.3 and v1.0.4 assets |
   | `pat4` | old TG-263 base `appTUL6ZaSepTawFw` | git history, v1.0.0 assets |
   | `patT` | UCSD test base `appczNMj8RE4CKjtp` | git history, v1.0.0 to v1.0.2 assets |
   | `patK` | a third-party base (file `UNC_Template.txt`) | v1.0.0 release assets only |

   Revoke `patQ`, ask the owners of the others to confirm theirs are revoked, then replace or delete
   the release assets. The program retires any legacy token file holding one of these tokens (it
   keeps only their SHA-256 fingerprints) and refuses them in the add-table dialog. Rewriting history
   (`git filter-repo`) is optional once the tokens are dead.
2. Add the `AIRTABLE_PAT` repository secret (read-only, TG-263 base only) and run the snapshot
   workflow once before building the next release.
3. Turn on GitHub secret scanning and push protection.

## 5. Phase 3 inventory

| Item | Finding (measured unless marked) | Plan |
| --- | --- | --- |
| Duplicated sources | `ROIClass.cs` ×4; `DicomSeriesReader`, `DicomTemplateRunner`, `OntologyCodeClass`, `TemplateMaker`, `VarianXmlReader`, `VarianXmlWriter` ×2. The GUI and `ROIOntologyClass` copies are current (2025); the others are 2023 forks. | Keep the current copies in a new platform-neutral `DicomTemplateCore` library; delete the forks and a dead fourth `ROIClass` in the GUI. |
| Console runner `DicomTemplateMakerCSharp` | net5.0 (out of support), hard-coded developer path, cannot read today's JSON templates, depends on out-of-repo SimpleITK and on `BMAnderson.Util.DicomFolderParser` (a net5.0 copy of `DicomParser.cs`). Does not build from a clean clone. | Rebuild as a thin command-line tool over `DicomTemplateCore` (`[folder] [--once] [--delete-generated]`). |
| `Xaml_Maker/XamlMakerCsharp` | netcoreapp3.1, `Program.cs` references undefined variables and does not compile; its classes are 2023 forks. | Rebuild as `import`/`export` commands over `DicomTemplateCore`. |
| `CleaningRTStructureCsharp` | net5.0, hard-coded paths. | net10.0 tool taking input and output paths. |
| SimpleITK | Not in the repository or its history: an external `..\..\SimpleITK` HintPath (SimpleITK 1.2.0, 2019, Windows x64 only). Used only for series grouping, slice order, a few header tags and the slice count (by decoding the whole volume as Float32). | Replace with header-only fo-dicom reads: group by SeriesInstanceUID, keep image objects only, order along the slice normal (GDCM's rule). Removes the native DLL and the x64 restriction and lets the runner be tested on any OS. |
| fo-dicom | 5.0.2/5.0.3 → 5.2.6. One break: `DicomTag.ROIObservationLabel` is `ROIObservationLabelRETIRED` since 5.1. fo-dicom returns values with DICOM padding, which its own validation rejects for TM. | Rename; trim padding when copying tags (what GDCM returned). |
| WPF in `ROIOntologyClass` | One using, 2 fields and 4 `[JsonIgnore]` Color/Brush properties; used in 4 lines of GUI code. `System.Windows.Forms` reference unused. | Make the library `net10.0` (platform-neutral); build brushes in the GUI. |
| WindowsAPICodePack | 7 dialog call sites; the package only resolves on net10.0-windows through a compatibility fallback. | Built-in `Microsoft.Win32.OpenFolderDialog`/`OpenFileDialog`. |
| Package versions | csproj HintPaths, `packages.config` and `App.config` disagree (Airtable 1.2.0 vs 1.8.0, System.Text.Json 6.0.6 vs 6.0.10, Newtonsoft.Json 13.0.1 vs 13.0.4); a clean restore of `dd90dc8` should fail. | SDK-style projects with Central Package Management (`Directory.Packages.props`); binding redirects are no longer needed. |
| Target framework | .NET 8 LTS ends 2026-11-10 (documented); .NET 10 is the current LTS. | `net10.0-windows` for the GUI, `net10.0` for everything else. |
| Build items | Every non-Compile item and property of the GUI and `ROIOntologyClass` projects inventoried; `Pictures/*.JPG` are byte-identical unused copies of `Windows/*.JPG`; resources and settings files are empty; ClickOnce and bootstrapper settings are obsolete. | Reproduce the used items; drop the dead ones; version and company from `Directory.Build.props`. |
| Tests and CI | None. | `DicomTemplateCore.Tests`: Varian XML round-trip golden files (captured before any change to that code), an end-to-end RT generation test on the bundled public-domain CT sample, mapping tests. GitHub Actions on Windows and Linux: build (warnings as errors), test, `dotnet format`; tagged builds publish a self-contained single-file release. |

## 6. Phase 2 inventory

Measured in the code unless marked; ordered by risk to patients and data.

| Area | Finding | Plan |
| --- | --- | --- |
| RT generation | One template RT object is reused for every series; attributes missing from a series keep the previous patient's values. Every RT also inherits the template's `PatientIdentityRemoved=YES` and 2019 dates. | Start each RT from a fresh copy of the template; copy identity-removed status from the images. |
| RT generation | In a folder with more than one series only the first matching series gets an RT (`KeyNotFoundException` after the first save; `run_program` never reset). | Decide per series; delete the write-only dictionaries. |
| Template matching | Empty or missing Series/Study Description matches every template with tags (two-way substring test with `""`). | Blank or missing values never match. |
| RT content | `RTReferencedStudySequence` holds the image's SOP Class/Instance UIDs instead of the study reference. | Study Instance UID with the Detached Study Management class `1.2.840.10008.3.1.2.3.1`. |
| RT content | Every generated RT carries the template's File Meta *Media Storage SOP Instance UID*: fo-dicom does not update the meta header when the dataset's SOP Instance UID changes (measured with fo-dicom 5.2.6). | Build each RT as a new `DicomFile` from a copy of the template dataset. |
| RT content | The bundled `template_RS.dcm` holds a placeholder patient (`RS_Template_File`, ID `000`), `PatientIdentityRemoved=YES` and 2019 dates. Tags an image lacks keep these values. | Type 2 patient/study tags the image lacks are written empty; identity-removed tags follow the image; creation and structure-set dates are the generation time. |
| RT content | Per-ROI failures are swallowed, so RTs are written with ROIs missing. | Log and report; do not write an RT with zero ROIs. |
| Crashes | 10 `async void` methods (3 are event handlers); the background runner is `async void` in a `Task`, so any exception there ends the process; no global exception handlers. | Global handlers with logging; runner as a cancellable `Task` with a try/catch per cycle and per folder. |
| Error handling | 26 bare `catch` blocks, no logging anywhere, errors shown as button labels. | Logging to a rolling file in `%LOCALAPPDATA%\DicomTemplateMaker\logs`; typed catches; user-visible messages. |
| Data loss | A corrupt `All_ROIs.json` loads as an empty template and the next save overwrites it; legacy files that fail to parse are deleted after migration; writes are not atomic while the runner reads them every 3 s. | Atomic writes (temp file + replace); treat parse failures as errors; never delete files that failed to parse. |
| UI safety | "Delete previously generated RTs" runs on the UI thread with `Thread.Sleep`, also generates RTs, and relabels and enables the "Delete selected" (templates) button. | Run off the UI thread, delete only, fix the button, confirm deletions. |
| Folder watcher | `FolderWatcher` leaks one `FileSystemWatcher` per folder per cycle and only waits for `Changed`; every unprocessed folder costs a fixed 3 s per template per cycle. | Settle-time rule based on file sizes and times; bounded retry with back-off for `IOException`. |
| Paths | Data files resolved from the working directory (`.\template_RS.dcm`, `.\FMA_SNOMEDCT_Key.txt`, `.\SmallCT`). | Resolve from the program folder. |
| Dead code | `Running.txt` and `Built_from_RTs.txt` blocks (the dead Airtable code was removed in Phase 1). | Delete. |
| Usability | See section 8. | Critical and high findings, plus the cheap wins. |

## 7. Open questions
1. Should `DVH_Type_Index` and `DVH_ContourStyle` be applied when reading templates from Airtable?
   The write path stores them, the read path has always ignored them.
2. Scalar fields (colour, DVH style, ontology) live on one record per structure shared by every site,
   so writing one template changes them for all sites (last writer wins). This is unchanged; should
   per-site overrides exist?
3. Should the snapshot workflow open a pull request for review instead of committing to `main`?
4. Which Airtable plan is the TG-263 workspace on, and what scopes does the leaked `patQ` token have?
5. Template matching stays a case-insensitive substring test in both directions (only blank values
   stop matching), because existing templates depend on it. Should it become "description contains
   the requirement" only?

## 8. Usability audit

Read from every window and row class at `7719d1b`; findings are measured in the code unless marked.

| Id | Severity | Finding | Plan |
| --- | --- | --- | --- |
| N1 | critical | *Delete previously generated RTs* relabels and enables the *Delete selected* button, which permanently deletes template folders, bypassing its "Delete?" tick. | Fix the button references; confirm. |
| N2 | high | Deleting generated RTs runs on the UI thread and also generates RTs where none exist. | Delete-only, off the UI thread, confirmation and result count. |
| N3 | high | Online-template build, Varian XML import and the default builder overwrite a same-name template and wipe its `Paths.txt` and `DicomTags.txt`. | Confirm; keep the existing paths and requirements. |
| N4 | high | `All_Ontologies.json` is rewritten with only the current template's codes when the library was not loaded first. | Merge into the library. |
| N5 | high | *Build Template!* has no name or existence checks. | Reuse the rename checks with a visible reason. |
| N6 | high | ROI renames are saved one keystroke late. | Set the name before saving. |
| N7 | high | New ROIs get the first ontology in the list unless changed. | No preselection; explicit "(no code)". |
| N8 | high | The RT generator hides skipped ROIs and matches blank descriptions. | Section 6 runner fixes; status line. |
| N9 | high | *Create folder with loadable RTs* selects every template when none is selected, writes its output folder into each `Paths.txt`, and starts a runner that cannot be stopped. | Confirm; one-off run; stop button. |
| N10 | high | The working directory is the template root, and the controls that show it are hidden. | Saved root, else the program folder; show and change it. |
| N11 | medium | Bulk actions include rows hidden by the search; template deletion has no confirmation. | Count hidden rows; confirm with names; Recycle Bin. |
| N12 | medium | Varian XML export defaults to a hard-coded site share and overwrites silently; import skips failures silently. | Setting, empty by default; confirm overwrites; import report. |
| N13 | medium | No global exception handler; many handlers do unguarded file and DICOM work. | Handlers with logging; readable messages. |
| N14 | medium | *Rename* is enabled before the template exists and leaves rows pointing at the old folder. | Enable after creation; rebuild rows. |
| N15 | medium | *Change Ontology Scheme* allows From = To, which corrupts codes in every template. | Disable; confirm with counts. |
| N16 | medium | *Edit Ontologies* does not require a code value and signals errors by colour only. | Require it; text reasons; warn when in use. |
| N17 | medium | Corrupt or invalid template folders are skipped or loaded empty without a message. | Per-folder problems shown to the user. |
| N18 | medium | Scans and exports block the UI thread; the ontology file is rewritten once per template on every rebuild. | Load once; background work with a busy state. |
| N19 | medium | Writing to Airtable from the editor has no confirmation; *Select all* includes hidden rows. | Confirm; visible rows only. |
| N20 | low | Add-table validation gaps; a same-name connection is replaced silently. | Inline validation; confirm; accept a pasted table URL. |
| N21 | low | Dead expiry check (`OutDatedWindow`, `Running.txt`) and an unreachable build-from-RTs block. | Delete. |
| N22 | low | No folder is remembered between dialogs. | Remember folders in the user settings. |
| N23 | low | The paths editor clips rows, accepts duplicates and keeps edits after closing with X. | Fix. |
| N24 | low | Window titles and typos. | Fix. |
| N25 | low | Name length and duplicate checks are missing; the template search is case-sensitive. | Warnings; case-insensitive search. |
| N26 | info | `TemplateWindow` and the default-template window are unreachable. | Delete. |

The open question from this audit, template matching semantics, is in section 7.
