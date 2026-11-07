# CLAUDE.md

## What this is

Windows tools that turn radiation-oncology structure templates (TG-263 names, colours, interpreted
types, SNOMED/FMA codes) into DICOM RT Structure Sets with ROI definitions and no contours. One .NET 10
solution, `DicomTemplateMaker.sln`: the WPF app `DicomTemplateMakerGUI` (the released program,
code-behind only), the platform-neutral libraries `DicomTemplateMaker.Presentation` (rules, view
models, runner service), `DicomTemplateCore` (RT generator, template logic, Varian XML, realism
checker), `ROIOntologyClass` (data model, template files) and `TemplateSync` (Airtable client,
snapshot, cache, write planner), the command-line tools `DicomTemplateMakerCSharp` (RT generator),
`Xaml_Maker/XamlMakerCsharp` (Varian XML), `CleaningRTStructureCsharp`, `TemplateSnapshotTool`
(maintainers and CI) and the internal `TemplateEvaluationTool`, and four xunit v3 test projects.
`docs/ARCHITECTURE.md` draws it; `README.md` says what the program does.

## Workflow rules

- **Pull requests target `main`** (decided 2026-11-03; `dev` is unused). Never push to `main`.
- **The gate is local and in CI:** `dotnet build DicomTemplateMaker.sln`, `dotnet test --solution
  DicomTemplateMaker.sln`, `dotnet format DicomTemplateMaker.sln --verify-no-changes`. Warnings are
  errors, nullable is on. A change is not done until all three pass (the `build-and-test` skill).
- **Never ease a fixture, golden, threshold or allowlist entry to make something pass.**
  `UPDATE_GOLDEN=1` only when a change to RT or Varian XML output is intended, with the diff
  reviewed; `docs/evidence/validator-allowlist.json` may only shrink.
- **Airtable quota is the maintainers':** no `TemplateSnapshotTool` export, no workflow dispatch, no
  `write-airtable` without `--dry-run`, never the root-level Python scripts. Nothing token-shaped in
  files, logs, docs or chat.
- Decisions D1-D22 are the maintainers' (`docs/OVERHAUL_DECISIONS.md`): transcribe answers, never
  fill a blank, never schedule a decision-tagged plan step on your own.

## The skills are the operational manual

Three tracked skills in `.claude/skills/`; their descriptions say when to load them:

- `build-and-test`: before the first build of a session (a running app locks the DLLs,
  MSB3027/MSB3021) and for any build, test, format or golden question.
- `evaluation-pipeline`: reproducing the committed baseline, running the validators, the realism
  rules, the snapshot check and the nomenclature report; what must match `docs/evidence/`.
- `release-rehearsal`: the publish command, what the release folder must hold, the clean-state rule
  for the app's settings folder, and the release-readiness list before a `v*` tag.

## Where the plan lives

- `docs/STATUS.md`: the ledger of every overhaul step, decision and release-readiness item.
- `docs/OVERHAUL_PLAN.md` (steps P0-P7), `docs/OVERHAUL_DECISIONS.md`, `docs/EVALUATION_PROTOCOL.md`,
  `docs/OVERHAUL_CLAIMS.md`; `docs/WORKFLOW_TEST_REPORT.md` holds the latest end-to-end test;
  `docs/MODERNIZATION_PLAN.md` is the previous overhaul (all phases done; its deferred items are D20).

## Pitfalls

- Exclude `obj/`, `bin/` and `.claude/worktrees/` when searching.
- `TemplateSnapshots/TG263.json` hashes differently on CRLF and LF checkouts; compare against the
  frozen fixture `TemplateSync.Tests/TestData/TG263.2026-09-30.json` (`-text`, byte-stable).
- The evaluation tool never reads or writes `%LOCALAPPDATA%\DicomTemplateMaker`; keep `--data-dir`
  and `--out` outside the repository. Generated RTs and copied CTs never enter the repository.
- `Get-Process DicomTemplateMakerGUI | Stop-Process` stops every instance on the machine, including
  another session's test instance; stop only processes whose path is under your own checkout.
- No Docker and no DCMTK on the maintainer's machine: Orthanc runs in CI only; `dciodvfy` comes from
  `pip install dicom3tools` and must be on `PATH` (or beside the interpreter once F-3 is merged).
