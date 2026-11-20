---
name: release-rehearsal
description: >
  How to rehearse and prepare a release of DicomTemplateMakerCSharp: the exact dotnet publish line
  of the release job, what the published folder must contain, how to start the published app in a
  clean state (the %LOCALAPPDATA%\DicomTemplateMaker rule), the release-readiness list in
  docs/STATUS.md and the tag flow. Load it before any dotnet publish, before running the WPF app
  for a check, before proposing a v* tag, and whenever asked whether the project is ready to
  release. Skipping it means a release zip missing the offline snapshot or carrying symbol files,
  an app run that overwrites the maintainer's settings, or a tag pushed before the snapshot
  workflow and the Windows checklist have run.
---

# Release rehearsal (plan P0.7)

## Publish exactly as the release job does

`.github/workflows/ci.yml`, job `release` (runs only on a `v*` tag; it has never run as of
2026-11-04). Rehearse into a scratch folder outside the repository, with a pre-release version:

```
dotnet publish DicomTemplateMakerGUI/DicomTemplateMakerGUI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=1.1.0-rc.N -o $OUT/publish/DicomTemplateMaker
```

About 22 s. The folder must hold, beside `DicomTemplateMakerGUI.exe` (about 178 MB):
`template_RS.dcm`, `SmallCT\vhm.1001.dcm` to `vhm.1004.dcm`, `FMA_SNOMEDCT_Key.txt`,
`Structure Template.xml`, `TemplateSnapshots\TG263.json` (the offline copy of the shared
templates; its SHA-256 equals the checkout's copy) and `LICENSE`, and nothing else: no `.pdb` and no
`TemplateSync.xml` (F-4, fixed 2026-11-05; symbols are embedded on publish). The release job fails
when the snapshot is missing (F-2). Check the exe's `ProductVersion` with
`(Get-Item <exe>).VersionInfo` (expected `1.1.0-rc.N+<git sha>`). Keep worktrees for a rehearsal
under `.claude/worktrees/`: the scratchpad path is too long for the baseline's validator file names.

## Starting the published app (clean state, protocol 1.1)

1. No instance running (`Get-Process DicomTemplateMakerGUI`).
2. `%LOCALAPPDATA%\DicomTemplateMaker` holds the maintainer's settings, connections, caches and logs:
   copy it to the scratch folder, then delete it, only with the owner's agreement (given for the
   2026-11-03 pass). Never start the app from a shell where `AIRTABLE_PAT` is set.
3. Start the exe with the working directory set to a scratch template root that holds templates
   (for example sites built by the `evaluation-pipeline` skill plus their `Ontologies` folder): the
   app uses and saves that folder (`TemplateRootResolver`). Confirm in
   `%LOCALAPPDATA%\DicomTemplateMaker\logs\app-<date>.log` ("Template folder: ... now saved") and
   `ui-settings.json` (`templateRoot`).
4. Driving the windows with computer use: request access to `DicomTemplateMakerGUI.exe` while it
   runs. **Never call `open_application` on it.** That starts a second copy outside the Claude
   app's MSIX package, with the program folder as its template folder, instead of bringing the
   running one forward (2026-11-04 and 2026-11-05, observation O-6). Bring the window forward by
   clicking it, or by closing the windows in front of it.
   - The folder pickers count as the Windows shell (click-only), so set `Paths.txt` on disk or reach
     scratch folders through a junction under `Documents`.
   - Check *Open log folder* without an Explorer grant: list `Shell.Application` windows before and
     after pressing it, then `.Quit()` the new one.
   - The checklist is `docs/MODERNIZATION_PLAN.md` section 3, "Check on Windows before releasing".
     The passes are in `docs/WORKFLOW_TEST_REPORT.md` section 6 (2026-11-04) and 6.1 (`1.1.0-rc.3`).
5. The session's shell runs inside the Claude desktop app's MSIX package. A new file or folder that
   it, or a program started from it, creates under `%LOCALAPPDATA%` lands in
   `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Local\` and is seen merged with the real
   folder. To put the settings folder back:
   - copy the backup only into a real folder that already exists, or check afterwards that the
     package's `LocalCache\Local` holds no `DicomTemplateMaker`;
   - compare SHA-256 against the backup.

## Before the tag

Work the release-readiness list in `docs/STATUS.md` (section 4) to the end. In short:

- Gate green on `main`; the baseline, validators and tools reproduced at the release commit.
- The snapshot workflow has run (scheduled Mondays; a dispatch spends 3 Airtable calls and needs
  the owner's go) so the release carries a current offline snapshot.
- The Windows checklist is PASS at the release candidate; the publish rehearsal passed; the offline
  install on a clean Windows VM is the owner's step.
- `VersionPrefix` in `Directory.Build.props` is the release version (1.1.0 today).
- Open findings that touch the release (F-2, F-4) are merged.

The tag is the owner's to push: `git tag v1.1.0 <sha>` and `git push origin v1.1.0` start the
release job, which creates the GitHub release with the zip. Never push a tag yourself.
