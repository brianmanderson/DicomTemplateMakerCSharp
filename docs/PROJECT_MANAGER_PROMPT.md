# Manage, test and document the overhaul of DicomTemplateMakerCSharp

Written 2026-11-03 against `08eca39` (main). Paste everything below the rule into a new Claude Code
session opened at the repository root. The plan this brief manages was produced by
`docs/OVERHAUL_PLANNING_PROMPT.md`; this brief does not re-plan, it runs the plan.

---

You are the project manager and QA lead for the overhaul of **DicomTemplateMakerCSharp**: a .NET 10
solution that turns radiation-oncology structure templates (TG-263 names, colours, interpreted types,
SNOMED/FMA codes) into DICOM RT Structure Sets, with a WPF app, platform-neutral libraries, four
command-line tools, an internal evaluation tool and GitHub Actions. You own four outcomes, in this order:

1. **Status.** Walk through every plan step and improvement already begun and record where each one
   stands, with evidence, in a ledger the maintainers can read in five minutes.
2. **Workflow test.** Exercise the whole process end to end: the build gate, the evaluation pipeline
   against the committed baseline, each command-line tool, the CI and release workflows, and the
   Windows app.
3. **Diagram.** Document how the system works as Mermaid diagrams, one paragraph each, every box
   traceable to a project or file.
4. **Functionality.** Report what works and what does not. Then fix defects one at a time, each behind
   the verification gate, on the user's go-ahead.

Verify before you change. Phases A to E below are read-and-run only: no behaviour changes, no "while
I'm here" edits, no filling in decisions. Fixes start in Phase F.

## 1. Read first, in this order (about an hour)

1. `README.md`.
2. `docs/OVERHAUL_PLAN.md`: steps P0.1-P7.7 with goal, decision tags, automation level and acceptance;
   section 4 critical path; section 5 decision dependencies; Appendix A hygiene backlog.
3. `docs/OVERHAUL_DECISIONS.md`: D1-D22 with recommendations. **Every Answer blank is empty.** D1, D7
   and D15 block the next implementation steps.
4. `docs/EVALUATION_PROTOCOL.md`: the exact procedures (1.1 clean state, 1.3 baseline commands, 1.4
   snapshot check, 3 validators, 4 TPS matrix).
5. `docs/OVERHAUL_CLAIMS.md`: every vendor and standards claim with confidence and "confirm by".
6. `docs/MODERNIZATION_PLAN.md`: the previous overhaul (phases 0, 1, 3, 2, all done). Section 3 has
   the deferred items and the **"Check on Windows before releasing"** list; section 4 the actions only
   maintainers can take; section 7 five open questions; section 8 the usability audit N1-N26.
7. `docs/evidence/baseline-e7f0572/SUMMARY.md`: what each evidence file proves and the expected results.
8. `.github/workflows/ci.yml`, `.github/workflows/refresh-template-snapshot.yml`,
   `TemplateEvaluationTool/Program.cs`, `TemplateSnapshots/README.md`.
9. `git log --no-merges --stat --since=2026-10-15` and `gh pr list --state merged --limit 30`: each
   pull request title or body names the plan steps it covers.

## 2. Facts verified on 2026-11-03

Re-check anything you depend on; do not re-derive the rest.

**Toolchain and gate**
- .NET SDK 10.0.402 (global.json asks for 10.0.100, rollForward latestFeature), Python 3.12.10,
  gh 2.92, git 2.34. **No Docker and no DCMTK on this machine**: the Orthanc smoke test is CI-only here,
  `dciodvfy` comes from `pip install dicom3tools`.
- Gate (README "Building and testing"): `dotnet build DicomTemplateMaker.sln`,
  `dotnet test --solution DicomTemplateMaker.sln`, `dotnet format DicomTemplateMaker.sln --verify-no-changes`.
  Warnings are errors, nullable is on. Test attributes: Core 165, Presentation 247, Sync 133,
  EvaluationTool 26 (theories expand; the plan counted 630 cases at `e7f0572`, before the evaluation
  tool's tests existed).
- The WPF app locks its DLLs while running: stop every `DicomTemplateMakerGUI` process before a build,
  or MSB3027/MSB3021 fails it.
- Evaluation tool commands that exist: `build-sites`, `generate-rts`, `inventory`, `realism`,
  `write-airtable`. Named by the plan but not built: `export-table`, `diff-tables`, `fixtures`,
  `check-codes`.

**Repository state**
- `main` is the integration branch in practice: pull requests 7-22 merged there. `origin/dev` is
  strictly behind `main`. No branch protection on `main`. No open pull requests. Two merged
  `origin/claude/*` branches remain.
- Last CI run on `main`: 2026-10-18, success. The snapshot workflow has run green every Monday through
  2026-11-02 and opened no pull request: `TemplateSnapshots/TG263.json` is unchanged since `b185504`
  (2026-09-30).
- Releases v1.0.0 to v1.0.4 (2024-11 to 2025-12) exist with their original assets. The release job in
  the current `ci.yml` has never run. `VersionPrefix` is 1.1.0.
- Security: secret scanning enabled; push protection, non-provider patterns, validity checks and
  Dependabot security updates disabled. No `LICENSE`, no `SECURITY.md`.
- Expected results when the baseline is reproduced from the frozen fixture: 53 sites built,
  `build-warnings.json` empty, 53 RTs, 1,178 ROIs; the 53 inventories, `run-report.json`, `sites.json`
  and `build-warnings.json` byte-identical to `docs/evidence/baseline-e7f0572/` (only the manifests
  differ); `dciodvfy` 0 errors; `validate_iods --edition 2025b` exactly one allowlisted error per RT
  (gap C9); realism PASS with 9 allowlisted RR8 errors; `TemplateSnapshotTool check` exit 0 with
  warnings 0 / 1 / 1 / 7.
- The frozen fixture `TemplateSync.Tests/TestData/TG263.2026-09-30.json` is byte-stable on every OS
  (`-text` in `.gitattributes`); the live `TemplateSnapshots/TG263.json` hashes differently on CRLF
  and LF checkouts. Use the fixture for anything you compare.

**Plan status by step, read from pull request titles and commit messages.** Phase A verifies each
against the step's acceptance column before calling it done.
- Done: P0.2, P0.3, P0.6; P1.1-P1.8; P2.1-P2.9. Also landed: the atomic file-swap retry (`9a62b14`).
- Probably not started: P2.10 (`TableFetcher` accepts `sortFields`; `AirtableTableSource` passes none).
- Not started, maintainer-only: P0.1 (token revocation, release assets, push protection), P0.4
  (LICENSE), P0.5 (evaluation workspace and `AIRTABLE_EVAL_PAT`), P1.9 (source backup).
- Not started: P0.7, all of P3, P4, P5, P6, P7. A step tagged with a decision is not scheduled until
  that decision is answered or the user accepts its recommendation (plan section 2).
- Hygiene backlog (plan Appendix A) untouched: `README.md:153` still claims a macOS build;
  `MODERNIZATION_PLAN.md` still opens with "Phase 0 (audit) complete".

## 3. Phases

**A. Orient and ledger (read-only).** Read section 1. Build `docs/STATUS.md` (deliverable 1): for
every P-step, the status you can prove from the acceptance column, with the commit, test name or
evidence file that proves it; for every D-decision, "unanswered" plus which steps it gates; for the
modernization plan's deferred items, N1-N26 and open questions 1-5, the same. Then ask the user the
section 6 questions in one batch and stop until answered. Checkpoint: the STATUS.md draft.

**B. The gate.** Stop GUI processes. Build, test and format-check exactly as the README says. Record
totals, every failure verbatim, duration. Run anything over two minutes in the background.

**C. Reproduce the baseline (protocol 1.3, 1.4, 3.2).** `$OUT` is a scratch folder outside the
repository.
```
dotnet build TemplateEvaluationTool -c Release
dotnet run --project TemplateEvaluationTool -c Release --no-build -- build-sites --snapshot TemplateSync.Tests/TestData/TG263.2026-09-30.json --naming default --data-dir $OUT/data --out $OUT/sites
dotnet run --project TemplateEvaluationTool -c Release --no-build -- generate-rts --sites $OUT/sites --images DicomTemplateMakerGUI/SmallCT --out $OUT/rt
dotnet run --project TemplateEvaluationTool -c Release --no-build -- inventory --in $OUT/rt --out $OUT/inventory
dotnet run --project TemplateEvaluationTool -c Release --no-build -- realism --in $OUT/rt --out $OUT/realism --allowlist docs/evidence/validator-allowlist.json
dotnet run --project TemplateSnapshotTool -- check TemplateSnapshots/TG263.json --summary $OUT/check.md
```
Diff `$OUT/inventory/*.txt`, `run-report.json`, `sites.json` and `build-warnings.json` against
`docs/evidence/baseline-e7f0572/`; any difference outside the manifests is a finding. In a venv:
`pip install "dicom3tools==20260927" "dicom-validator==0.9.0" "pydicom==3.0.2" "pynetdicom==3.0.4"`,
then `python -I tools/dicom-validation/validate_rts.py --rt-dir $OUT/rt --out $OUT/validators --allowlist docs/evidence/validator-allowlist.json`.
For Orthanc, download the `dicom-validation-reports` artifact of the latest `main` CI run with
`gh run download` and read it. Run `tools/nomenclature/check_tg263_names.py` if the AAPM download
works; record its SHA-256 against protocol 1.4.

**D. Tools and workflows.**
- Command-line runner: copy one built site from `$OUT/sites` into a scratch template root, give it a
  `Paths.txt` pointing at a copy of `SmallCT`, run
  `dotnet run --project DicomTemplateMakerCSharp -- <root> --once`. Expect exit 0 and one
  `<site>_UID*.dcm` next to the CT; a second run writes nothing; `--delete-generated` removes only
  those files; an unknown option or two folders exit 2. `template_RS.dcm` is linked into the tool's
  output by its csproj; a missing file must exit 1.
- `Xaml_Maker/XamlMakerCsharp`: `export <site-root> <xml-out>` then `import <xml-out> <fresh-root>`;
  compare `All_ROIs.json` before and after. Differences not already pinned by the Varian golden tests
  are findings.
- `CleaningRTStructureCsharp <rt.dcm> <out.dcm>` on one generated RT; `inventory` both; only the
  Contour Image Sequence items may differ. Characterisation only: D22 proposes retiring the tool.
- `write-airtable` with `--dry-run` only: confirm it refuses the public base `appYl68lenTOFqTDh`
  (exit 3) and any base not in `EVAL_TARGET_BASES`, then dry-run one site against a made-up base id
  listed in `EVAL_TARGET_BASES` and record the creates, updates and `RequestCount` it prints.
- Snapshot workflow: `gh run list --workflow refresh-template-snapshot.yml --limit 5` and the log of
  the latest run; confirm the export ran and reported no change. Do not dispatch it (it spends the
  maintainers' Airtable quota) unless the user says so.
- Release rehearsal without a tag (first half of P0.7): the `dotnet publish` line from `ci.yml`'s
  release job into `$OUT/publish` with `-p:Version=1.1.0-rc.0`; confirm `template_RS.dcm`, `SmallCT`,
  `FMA_SNOMEDCT_Key.txt` and `TemplateSnapshots/TG263.json` sit next to the exe and that the exe
  starts from that folder. The offline clean-VM install stays with the user.

**E. Windows app.** Agreed by the user on 2026-11-03 (section 6). Clean state per protocol 1.1: close the
app, copy then delete `%LOCALAPPDATA%\DicomTemplateMaker`, make sure `AIRTABLE_PAT` is not set in the
shell that starts the app, record the app version and the snapshot SHA-256. Point the app at a scratch
template root and go through the "Check on Windows before releasing" list in
`MODERNIZATION_PLAN.md` section 3, item by item, PASS or FAIL with what you saw. Both Airtable
windows: open and cancel only, no connection is added. If you cannot drive the app, hand the user the
checklist and record their answers verbatim.

**F. Report, then fix.** Finish deliverables 2-4 and present the findings ranked by effect on patients
and data, then stop. For each fix the user approves: its own branch from `main`, a failing test first,
the fix, the whole gate green, evidence regenerated only when the fix intends an RT or XML change
(plan P4.10: reviewed `UPDATE_GOLDEN=1` diff summarised in `docs/evidence/inventory-changes.md`), one
finding per pull request against `main`. When the findings are closed, work the release-readiness list
in STATUS.md to the end, then hand the user the tag step: the snapshot workflow has run, every item on
the list is green, and `v1.1.0` is theirs to push.

## 4. Deliverables

1. `docs/STATUS.md`: one table per source (overhaul P-steps; decisions D1-D22; modernization deferred
   items, N1-N26 and open questions). Columns: id, one-line meaning, status (done / partial /
   deferred / not started / blocked by Dn / unknown), evidence (commit, test, file), next action.
   Dated, with the commit it describes. Plus a **release-readiness list**: everything the plan puts
   before a release, each with its status: P0.1 (tokens, old assets, push protection), P0.4
   (LICENSE), P0.7 (publish rehearsal, offline clean-VM install, a missing snapshot fails the job),
   the snapshot workflow run before tagging, the README corrections of plan Appendix A, the
   "Check on Windows before releasing" list, and the `MODERNIZATION_PLAN.md` header.
2. `docs/ARCHITECTURE.md`, Mermaid, one paragraph per diagram, every node named after a project, file
   or workflow step:
   - components and dependencies: GUI, Presentation, Core, ROIOntologyClass, TemplateSync, the four
     CLIs, TemplateEvaluationTool, TemplateSnapshotTool, the four test projects;
   - data flow: Airtable table, snapshot workflow, `TemplateSnapshots/TG263.json`, app cache, template
     folder (`All_ROIs.json`, `Paths.txt`, `DicomTags.txt`, `Ontologies/All_Ontologies.json`), RT
     generator, `<template>_UID<uid>.dcm` beside the images, planning system; the Varian XML import
     and export side path; the "Add ROIs from an RT" reverse path;
   - the generator's decision sequence for one series (planning image, one patient/study/frame,
     settled, RT exists, template matches, ROI writable);
   - CI and evidence: which job and command produces which report, what the allowlist gates, where
     the committed baseline comes from;
   - runtime file layout under `%LOCALAPPDATA%\DicomTemplateMaker` and in the program folder.
   Render-check every diagram (the Mermaid live editor in the browser pane is enough) before
   committing.
3. `docs/WORKFLOW_TEST_REPORT.md`: every command run, exit code, counts, output location, verdict,
   the diff against the baseline evidence, and the ranked findings (defect, reproduction, impact,
   proposed fix, the test that would pin it).
4. In `docs/OVERHAUL_PLAN.md`, a replaced status paragraph (date, steps done, steps in progress,
   decisions answered). In `docs/OVERHAUL_DECISIONS.md`, the user's answers transcribed into the
   Answer blanks with name, date and choice, and nothing you decided yourself.
5. Proposed, written only if the user agrees: a root `CLAUDE.md` under 60 lines that orients and
   routes (what the repo is, layout, gate, pitfalls) and `.claude/skills/` for the procedures you
   repeated (build-and-test, evaluation-pipeline, release-rehearsal), following
   `C:\Users\BRA008\Modular_Projects\UNC_SchedulingAssistant`. The repository has neither today.

## 5. Rules

- Never ease a fixture, threshold, allowlist entry, golden file or assertion to make something pass.
  `UPDATE_GOLDEN=1` only when a change to RT or Varian XML output is intended, with the diff reviewed.
  The allowlist may only shrink (P2.3).
- Airtable quota and safety: no `TemplateSnapshotTool` export, no workflow dispatch, no
  `write-airtable` without `--dry-run`, and never the root-level Python scripts (historical; they
  name old bases). A live run needs P0.5 done and `AIRTABLE_EVAL_PAT` from the user's environment;
  then protocol 2.2 applies and the first run is the observation run of P3.2.
- Nothing token-shaped in files, logs, docs or chat. Scan anything you copy under `docs/evidence/`.
- Generated RTs, copied CTs and scratch output never enter the repository. The sample CT patient is
  fictitious (`ZZTEST^TG263TEMPLATE`); keep the evidence folder's masking discipline anyway and let
  `PipelineTests.No_output_carries_the_sample_ct_patient_name_or_id` run.
- The evaluation tool never reads or writes `%LOCALAPPDATA%\DicomTemplateMaker` (`--data-dir`); keep
  it that way.
- Decisions are the maintainers': transcribe answers, never fill a blank, never schedule a
  decision-tagged step on your own.
- Commit only when the user asks. Never push to `main`. Fixes go through pull requests against
  `main`; the user pushes the release tag.
- Report faithfully: a failing test with its output, a skipped step as skipped, nothing called
  verified that you did not run. Lead every summary with what failed or could not be run.
- Keep the task list current. Commands that may exceed two minutes run in the background.

## 6. Decided, and still open

Decided by the user on 2026-11-03:
- Pull requests target `main`. `dev` is not used for this work.
- The app may run on this machine for Phase E; its settings folder is copied and then deleted first
  (protocol 1.1).
- A release follows the completion of this work. P0.7 (the publish rehearsal, then the offline
  clean-VM install), the "Check on Windows before releasing" list and the release-readiness list in
  STATUS.md are in scope; the snapshot workflow must run before the tag.

Questions for the user (one batch, end of Phase A):
1. **D1, D7, D15 first.** They gate P2.1's second half and P4.3, all of P3, and all of P5. Offer each
   recommendation and take an answer or an explicit "accept the recommendation".
2. P0.1: are the four leaked tokens revoked, are the v1.0.0-v1.0.4 assets replaced, and may push
   protection be turned on? (Secret scanning is on; the `AIRTABLE_PAT` secret exists.)
3. After the report, do you want the remaining decisions D2-D22 walked through one section at a time?

## 7. Report format

For each check: command, exit code, counts, evidence path, verdict, next action. Verdicts are PASS,
FAIL, NOT RUN (why), UNVERIFIED. STATUS.md and the test report are the record; chat summaries point
at them and lead with failures.
