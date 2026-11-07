# Status ledger

## Handover (2026-11-05)

State on 2026-11-05 after the release-candidate pass, with `main` at `de002b7`. Written by the second
project-manager session. The brief is `docs/PROJECT_MANAGER_PROMPT.md`; the first manager's hand-over
of 2026-11-04 is superseded by this one.

- **Decisions: all answered.** D13-D22 were walked through with Brian Anderson in this session on
  2026-11-04/05 and transcribed (D13-D19 dated 2026-11-04, D20-D22 2026-11-05). All accept the
  recommendation except D19 (Clinical Protocol XML in scope, later phase). Pending facts: section 2.
- **Pull requests: all nine fix pull requests merged on 2026-11-05.** 31 (F-7), 24 (F-1), 30 (F-6),
  26 (F-2), 27 (F-3), 28 (O-6 README note), 29 (Apache-2.0 `LICENSE`, P0.4), then 23 (F-5, `307a375`)
  and 25 (F-4, `de002b7`). CI on `main` at `de002b7` is green (run 42102552546).
- **Release-candidate pass at `de002b7` (`1.1.0-rc.3`), 2026-11-05: all PASS.**
  - Gate: 837 tests.
  - The baseline is reproduced.
  - Validators PASS without `PATH`.
  - The publish folder is right.
  - The app checks pass: *Open log folder*, F-1, F-6 and the generator.
  - Details: `docs/WORKFLOW_TEST_REPORT.md` section 6.1.
  - The app's settings folder was backed up and restored with the owner's agreement.
- **User decisions of 2026-11-05 for the release:** the user dispatches the snapshot workflow by hand
  once the merges are in; O-6 waits until after `v1.1.0` apart from the README note; the overhaul
  documents stay uncommitted until the user asks.
- **Uncommitted in this checkout:** `CLAUDE.md`, `.claude/skills/` (three skills), `docs/STATUS.md`,
  `docs/ARCHITECTURE.md`, `docs/WORKFLOW_TEST_REPORT.md`, `docs/PROJECT_MANAGER_PROMPT.md`, and the
  edits to `docs/OVERHAUL_DECISIONS.md` and `docs/OVERHAUL_PLAN.md` (status paragraph of 2026-11-05).
  The local `main` branch of this checkout is still at `08eca39`.
- **Left before the tag (section 4):**
  - The user dispatches the snapshot workflow, or waits for the scheduled Monday run on 2026-11-09.
    The 2026-11-02 run said "Unchanged: 246 records".
  - The user does the clean-VM install.
  - The user decides whether to commit the overhaul documents before or after the tag.
  - Then the user pushes `v1.1.0` at the release commit.
- **Scratch** (outside the repository): this session's scratchpad holds `license\`, `rc-preview\`
  and `rc3\` (gate logs, the `1.1.0-rc.3` publish, the evaluation outputs, the validators, the app
  root, and the settings backup and run copy). The release-candidate worktree is
  `.claude/worktrees/rc3` (detached at `de002b7`). The first session's scratch folders are listed in
  its notes in `%APPDATA%\Claude\side-session-notes\local_68f0b7cf-eb57-43ce-8c50-524a53c08e96\`.

Written 2026-11-03 against `08eca39` (main) by Phase A of `docs/PROJECT_MANAGER_PROMPT.md`. It records,
for every step of `docs/OVERHAUL_PLAN.md`, every decision of `docs/OVERHAUL_DECISIONS.md` and the open
items of `docs/MODERNIZATION_PLAN.md`, the status that the evidence supports.

Status values: **done** (acceptance column met, evidence on `main`), **partial** (part of the step landed;
the note says what is missing), **deferred** (kept deferred on purpose), **not started**, **blocked by Dn**
(waits for a decision), **unknown** (only a maintainer can say). "Evidence" names the pull request (PR),
merge commit, test or file that proves it. Everything here was read from the checkout and GitHub on
2026-11-03; nothing was run. Running the gate, the pipeline and the tools is Phases B to E and is recorded
in `docs/WORKFLOW_TEST_REPORT.md`.

Updated the same day after the user's answers (relayed by the project-manager session) to D1, D7, D15 and the
P0.1 question: see section 2, the P0.1 row and the release-readiness list. Updated again on 2026-11-04 with the
answers to the test report's seven questions (section 7).

Pull requests 9 to 21 (merged 2026-10-16 to 2026-10-18) and 22 (2026-11-03) are the only changes since
the plan's base commit `e7f0572`. CI on `main` was green on 2026-11-03 (run 41718210134).

## 1. Overhaul plan steps

### P0. Gates and safety

| Id | Meaning | Status | Evidence | Next action |
| --- | --- | --- | --- | --- |
| P0.1 | Confirm the four leaked tokens are revoked; replace or delete the v1.0.0-v1.0.4 release assets; turn on push protection | **closed by maintainer decision** (2026-11-03) | Brian Anderson, 2026-11-03: "those leaked tokens don't matter, they're meant to be shared". No revocation, no replacement of the v1.0.0-v1.0.4 assets, no history rewrite; push protection was not approved and stays off. GitHub API 2026-11-03: secret scanning enabled, push protection disabled | None. The tension was put to the maintainers once: `TemplateSync/Credentials/LeakedTokens.cs` refuses these tokens and retires legacy token files, and the modernization plan's quota analysis (section 1.2) found one shared token exhausting a Free workspace's 1,000 calls a month. Brian Anderson, 2026-11-04: **keep as is**; no change to that code or to the README security notice, because the app no longer needs a token for the shared templates, so the refusal costs users nothing and protects the shared workspace's quota |
| P0.2 | Fictitious identity for the bundled sample CT | **done** | PR 15 (`e35c5f3`) and PR 21 (`aa7320a`); `SampleCtIdentityTests` (4 tests); `tools/reidentify_sample_ct.py`; the GUI csproj ships `SmallCT\*.dcm`. Identity `ZZTEST^TG263TEMPLATE` / `ZZTEST-0001` because D15 gave no site convention | None unless D15 names a convention |
| P0.3 | Remove the ARIA export `Test.dcm` | **done** | PR 17 (`3a13de3`) removed it; masked inventory kept as `docs/evidence/vendor-samples/aria-rtm-16.1-rtstruct.txt` with a README naming what was masked; `git grep` finds no station or institution value in sources | None |
| P0.4 | `LICENSE` | **done** (PR 29, merged 2026-11-05); Apache-2.0 (D21) | `LICENSE`, the README *License* section, `LICENSE` beside the exe in the release folder | None |
| P0.5 | Evaluation workspace and `AIRTABLE_EVAL_PAT` | **not started** (maintainer); D7 accepted 2026-11-03, workspace name and plan pending | `gh secret list` shows only `AIRTABLE_PAT` (2026-09-30) | Ask for the D7 details when this step is next |
| P0.6 | Intended-use statement in the README | **done** | PR 17 (`3a13de3`, `fb02f13`): README section "Intended use" | Update in P5.7 |
| P0.7 | Release rehearsal, offline clean-VM install, missing snapshot fails the job | **partial** (2026-11-03): the publish rehearsal passed; the job still only warns on a missing snapshot; no clean-VM install yet | `WORKFLOW_TEST_REPORT.md` 5.6: `dotnet publish` as in `ci.yml` with `-p:Version=1.1.0-rc.0` exit 0; `template_RS.dcm`, `SmallCT`, `FMA_SNOMEDCT_Key.txt`, `TemplateSnapshots\TG263.json` beside the exe; the exe starts. `ci.yml` still `Write-Warning`s when the snapshot is missing (finding F-2); no `v1.1.*` tag, the `release` job has never run | Fix F-2; clean-VM install (user); the tagged run itself |

### P1. Evidence baseline

| Id | Meaning | Status | Evidence | Next action |
| --- | --- | --- | --- | --- |
| P1.1 | Harness: `SiteBuilder` extraction and the evaluation tool | **partial** | PR 11 (`ca6283e`): `DicomTemplateMaker.Presentation/Services/SiteBuilder.cs`; characterisation test `OnlineTemplatesViewModelTests.Snapshot_sites_built_with_the_default_naming_choice_match_the_e7f0572_characterisation`; `SiteBuilderTests.The_default_naming_choice_over_the_snapshot_builds_what_the_window_builds`; `TemplateEvaluationTool` in the solution with `build-sites`, `generate-rts`, `inventory` (later `realism`, `write-airtable`). Missing: `export-table`, `diff-tables`, `fixtures` | Build `export-table` and `diff-tables` before P3.4, `fixtures` before P5.2 |
| P1.2 | Reproducibility: manifests, `--data-dir` | **done** | `manifest.json` (git SHA, inputs with SHA-256) in every evidence folder; `PipelineTests.Two_runs_on_the_same_inputs_give_identical_outputs_although_the_rts_differ`; `PipelineTests.No_command_writes_into_the_program_data_folder` | None |
| P1.3 | Frozen fixture and pinned facts | **done** | PR 9 (`bf1037d`): `TemplateSync.Tests/TestData/TG263.2026-09-30.json` (177,534 bytes, `-text` in `.gitattributes`); `SnapshotFactsTests` (11 tests, including `Fixture_is_the_b185504_snapshot_byte_for_byte`) | None |
| P1.4 | Publication gate `TemplateSnapshotTool check`; workflow opens a pull request | **done** (code and tests); the pull-request path has never run live | PR 9: `check` exits 5 on a failed gate; `SnapshotCheckTests` (12 tests, including `A_snapshot_trimmed_to_120_records_fails_with_exit_5`); `refresh-template-snapshot.yml` runs `check`, opens or updates a PR, fails a manual dispatch without the secret, takes the SDK from `global.json`. Every run since (five scheduled, latest 2026-11-02) logged "Unchanged: 246 records", so the check and PR steps were skipped | None; the first changed export exercises it |
| P1.5 | Baseline build of the 53 sites | **done** | `docs/evidence/baseline-e7f0572/build-sites/` and `generate-rts/`: 53 sites, 53 RTs, 1,178 ROIs, `build-warnings.json` empty | Reproduce in Phase C |
| P1.6 | Baseline inventories | **done** | 53 inventories; `SUMMARY.md` annotates C1-C13; pydicom cross-check on four files. `dcmdump` cross-check not run (no DCMTK) | `dcmdump` cross-check when DCMTK is available |
| P1.7 | Validators in report mode | **done** | PR 13 (`1700b48`): `validators/` (`dciodvfy` 0 errors; `validate_iods --edition 2025b` one C9 error per RT); allowlist seeded. The planned fo-dicom `DicomDataset.Validate()` report was not produced | Optional fo-dicom report |
| P1.8 | Nomenclature check against the AAPM worksheet | **done** | PR 18 (`f45b182`): `tools/nomenclature/check_tg263_names.py`; `nomenclature/` report with the worksheet SHA-256. Done as a Python script rather than an opt-in `Network` test category | Re-run in Phase C if the download works |
| P1.9 | Source backup (duplicate base, raw export) | **not started** (maintainer); waits for the D7 details and P0.5 | No `docs/evidence/p1/` | After P0.5 |

### P2. Safety fixes and test infrastructure

| Id | Meaning | Status | Evidence | Next action |
| --- | --- | --- | --- | --- |
| P2.1 | Code-meaning drift guard | **partial**: guard done; second half unblocked by D1 = (a) on 2026-11-03, not started | PR 10 (`503ac8c`): `CodeMeaningDriftTests` (3 tests); the baseline inventories regenerated after it are unchanged (`7c6e829`, `aa7320a`) | Branch (a): P4.3 replaces TG-263-name meanings with concept meanings; the guard stays until then |
| P2.2 | Tolerant `interpret_RT` | **done** | PR 10: `RtRoundTripTests` (`A_snapshot_site_survives_template_rt_and_interpret` over the snapshot sites, `An_rt_with_bare_sct_codes_reads`, `Rois_without_an_observation_or_a_complete_code_are_skipped_with_the_reason`); `RtStructureSetFileTests` | None |
| P2.3 | CI validation stage | **done** | PR 13 (`ca53454`): job `dicom-validation` in `ci.yml`, `tools/dicom-validation/validate_rts.py`, `docs/evidence/validator-allowlist.json` (2 entries: C9, D1b). The negative case is pinned by `RtRealismCheckerTests.RR2_a_duplicate_or_dangling_roi_number_is_an_error`, not by a test of the validator script; fo-dicom `Validate()` inside the tests was not added | None required |
| P2.4 | TPS-realism checker | **done** for RR1-RR10; RR11 (IHE-RO content) not transcribed | PR 16 (`9eb23ba`): `DicomTemplateCore/Validation/RtRealismChecker.cs`; `RtRealismCheckerTests` (positive and negative test per rule); `realism/` report PASS with 9 allowlisted RR8 errors | RR11 when P4 starts |
| P2.5 | Orthanc smoke test | **done** | PR 14 (`ca293c6`): `tools/dicom-validation/orthanc_smoke.py`, service container `jodogne/orthanc:1.13.0`, artifact `dicom-validation-reports` | Read the latest artifact in Phase C |
| P2.6 | Logging `DelegatingHandler` | **done** | PR 19 (`9adf0ed`): `TemplateEvaluationTool/LoggingHandler.cs`; `LoggingHandlerTests` (5 tests, including `No_token_text_ever_reaches_the_file`) | None |
| P2.7 | Write driver `write-airtable` | **done** | PR 19: `WriteAirtableCommand.cs`; `WriteAirtableCommandTests` (9 tests: public base refused, unlisted base refused, token required, dry-run plans) | Dry-run in Phase D |
| P2.8 | Column alias for the Spanish and French columns | **done** | PR 19: `ColumnAliasTests` (9 tests over both real schemas) | None |
| P2.9 | Export robust to schema drift | **done** | PR 9 and PR 19: `SnapshotSourceTests.Export_of_an_all_columns_response_writes_the_frozen_snapshot_byte_for_byte`; the workflow has no exclude list | None |
| P2.10 | Sorted live reads | **not started**; D7b answered 2026-11-04 (recommendation), may be scheduled | `TableFetcher.FetchAsync` takes `sortFields`; `AirtableTableSource.cs:482` passes `null` | Send `sort[0][field]=Record`, else sort by `createdTime` then record id |

Also landed, outside the plan: the atomic file-swap retry (PR 20, `9a62b14`, `SnapshotStoreSwapTests`).

### P3. Airtable round trip

All ten steps are **not started**. Every decision they cite is answered (D1, D2, D3, D6, D8, D9, D9b,
D10, D21; D7 with its workspace details pending), so P3 waits only on facts and access: the D7 workspace
details, then P0.5 and P1.9, and `AIRTABLE_EVAL_PAT` from the user for the live steps. P3.8 (copier) is
skipped (D9); P3.4 also needs the missing `export-table`/`diff-tables` commands of P1.1.

### P4. RT conformance hardening

All thirteen steps are **not started** and none waits on a decision any more: D1, D1b, D2, D4, D5, D7b,
D12, D13, D14, D20, D21 and D22 are answered (all as recommended) and P2.2 is done, so every P4 step
may be scheduled. P4.6 builds the RT from scratch (D13) with Implicit VR LE by default (D14); P4.12
retires `CleaningRTStructureCsharp` (D22). None of P4 is part of `v1.1.0`.

### P5. TPS acceptance

All seven steps are **not started**. Every decision they cite is answered; P5 waits on the D15 facts
(Eclipse test database access, versions, operators) and, for P5.3, the D16/D17 facts. P5.1 and P5.2
could proceed meanwhile per plan section 5.

### P6. Varian StructureTemplate XML

All seven steps are **not started**. Decisions are answered; P6.1 can start (read the public 18.x
conformance statements); P6.2-P6.4 wait on the D16/D17 facts (Eclipse versions, a fresh template export,
the dictionary export). P6.7 proceeds without deleting the Clinical Protocol pipeline: D19 puts it in
scope for a later phase after P6.

### P7. Vendor-neutral carriers

All seven steps are **not started**. D18 (2026-11-04): the canonical model and exporters wait until two
non-Varian exporters are approved; the outreach owner is pending.

## 2. Decisions D1-D22

All twenty-five decisions are answered by Brian Anderson (D1, D7, D15 on 2026-11-03; D1b-D12 on
2026-11-04, relayed by the first project-manager session; D13-D19 on 2026-11-04 and D20-D22 on
2026-11-05 in the second) and transcribed in `docs/OVERHAUL_DECISIONS.md`. Every answer accepts the
recommendation except **D19** (Clinical Protocol XML in scope, in a later phase). Facts still pending
from the maintainers are listed in the last column; the steps that need them wait for them, nothing
else does.

| Id | Question (short) | Answer | Gates | Facts still pending |
| --- | --- | --- | --- | --- |
| D1 | Ontology identity and code meaning | (a), the recommendation (2026-11-03) | P2.1 second half, P3.5 (R2), P4.3 | - |
| D1b | Source data defects, non-SNOMED codes | recommendation (2026-11-04) | P4.2 | - |
| D2 | Context attributes, scheme identification | recommendation with both qualifications (2026-11-04) | P4.1 | - |
| D3 | Canonical ROI name | recommendation (2026-11-04) | P3.5 (R1, R7), P7.1 | - |
| D4 | Interpreted-type vocabulary | recommendation (2026-11-04) | P4.4, P6.4 | - |
| D5 | Consider ROIs in generated RTs | recommendation (2026-11-04) | P4.8, P4.9 | - |
| D6 | Varian-only presentation fields | recommendation (2026-11-04) | P3.5 (R6), P7.1 | - |
| D7 | Canonical base, spelling, write-target workspace | recommendation, details pending (2026-11-03) | P0.5, P1.9, P3 | Target workspace and plan, base owners and plans, Anderson 2024 base, share-link Copy base |
| D7b | Sort live reads | recommendation (2026-11-04) | P2.10, P4.9 acceptance | - |
| D8 | Synchronisation vs additive writes | recommendation (2026-11-04) | P3.5, P3.6 | - |
| D9 | Record-level copier | recommendation: no copier, P3.8 skipped (2026-11-04) | P3.8 | - |
| D9b | Which re-upload leg | both, the recommendation (2026-11-04) | P3.3, P3.7 | - |
| D10 | `AIRTABLE_PAT` scope, connection test, dry run | all three, the recommendation (2026-11-04) | P3.9 | - |
| D11 | CLI as product or internal tool | internal, the recommendation (2026-11-04) | P1.1 placement | - |
| D12 | Provenance attributes | recommendation (2026-11-04) | P4.5, P0.6 wording | - |
| D13 | Seed file or build from scratch | build from scratch, the recommendation (2026-11-04) | P4.6 | - |
| D14 | Transfer syntax, names, labels | recommendation (2026-11-04) | P4.6, P4.7 | - |
| D15 | TPS environments and test identity | Eclipse test database first (2026-11-03) | P5 | Versions, operators, access |
| D16 | Eclipse versions, XML version | recommendation, versions pending (2026-11-04) | P6.1, P6.3, P6.4 | Eclipse versions in clinical use; fresh template export |
| D17 | Structure-code dictionaries, SNOMED in Eclipse | recommendation, facts pending (2026-11-04) | P6.2-P6.4 | Active dictionaries, whether SRT is populated (from the P6.2 export) |
| D18 | Writer strategy, vendor scope, outreach owner | recommendation, owner pending (2026-11-04) | P6, P7 | Outreach owner |
| D19 | Clinical Protocol XML | **in scope, in a later phase** (differs from the recommendation; 2026-11-04) | P6.7 keeps the pipeline; a new phase after P6 | - |
| D20 | Modernization plan's deferred items | recommendation (2026-11-05) | P4.2 "(no code)", matching precedence | - |
| D21 | Legacy files, licence, third-party files | Apache-2.0; files as recommended except the `Xaml_Maker` protocol files stay (D19) (2026-11-05) | P0.4, hygiene backlog, P1.8, P6.1 | Owner of the `FMA_SNOMEDCT_Key.txt` Google Sheet |
| D22 | `CleaningRTStructureCsharp` | retire, the recommendation (2026-11-05) | P4.12 | - |

## 3. Modernization plan (`docs/MODERNIZATION_PLAN.md`)

Graded from the code on 2026-11-03 (file and line, plus the test that pins the behaviour). The six
deferred items are expected to be deferred; D20 says what to do with each.

### 3.1 Deferred items (section 3, Phase 2)

| Item | Status | Evidence | D20 recommendation (unanswered) |
| --- | --- | --- | --- |
| Persistent header cache (first generator cycle re-reads every header) | **deferred** | `DicomTemplateRunner.cs:555-558` keeps `DirectoryFingerprint` in memory only | Keep deferred |
| On-disk change detection in editors | **deferred** | No watcher or reload; `MakeTemplateWindow` and `EditOntologyWindow` save over the file (last save wins) | Keep deferred |
| "(no code)" ROI choice | **deferred** | `AddRoiRules.cs:18, 48-50`: a code is required | Do, in P4.2 (needs D1b) |
| Rename leaves old RTs under the old name | **deferred** | `EditorMessages.RenameConsequences` (`EditorMessages.cs:62-67`) says so; `MakeTemplateWindow.xaml.cs:455` moves the folder only | Keep deferred; document in the README |
| Varian share settings window | **deferred** | `UiSettings.cs:37` (`VarianXmlShare`); tooltips at `MainWindow.xaml:57, 59` say to edit `ui-settings.json` | Keep deferred until P6 lands |
| Template matching semantics | **deferred** | `TemplateMatcher.cs:99-114`: case-insensitive two-way `Contains`, no exact-match precedence; `RunnerMatcherTests.A_short_description_contained_in_a_requirement_still_matches` | Add exact-match precedence and tests |

### 3.2 Open questions (section 7)

| Q | Question | Status | Where it lives now |
| --- | --- | --- | --- |
| 1 | Apply `DVH_Type_Index` and `DVH_ContourStyle` on read? | **open**; still ignored on read (`TemplateEntryMapper.cs:58-120` applies DVH colour, style and width only; both are written at `:180-181`) | D6 |
| 2 | Per-site overrides for shared scalar fields? | **open**; unchanged (`TemplateEntryMapper.ToDesiredEntry`, `:154`) | D8 |
| 3 | Snapshot workflow opens a pull request instead of committing to `main`? | **answered in code** since P1.4 (`refresh-template-snapshot.yml:108-128`); section 7 of the modernization plan is stale | Hygiene fix to the document |
| 4 | Airtable plan of the TG-263 workspace; scopes of the leaked `patQ` token | **open** for the plan (a pending D7 detail); the token question is moot since P0.1 was closed by maintainer decision | D7, P0.1 |
| 5 | Change the two-way substring matching? | **open**; unchanged | D20 |

### 3.3 Usability audit N1-N26 (section 8)

"done" = the planned fix is in the code and a test pins it; "done (no test)" = in the code, nothing pins it.

| Id | Finding (short) | Status | Evidence | Note |
| --- | --- | --- | --- | --- |
| N1 | *Delete previously generated RTs* enabled the template-delete button | done | `MainWindow.xaml.cs:688-738` (`DeleteROIs_Button_Click` no longer touches `Deleted_Selected_Button`; confirms via `ShellMessages.ConfirmDeleteGenerated`); `ShellMessagesTests.Delete_generated_confirmation_names_templates_folders_and_the_permanent_delete` | Button wiring itself untested; Phase E checks it |
| N2 | Deleting generated RTs ran on the UI thread and also generated RTs | done | `MainWindow.xaml.cs:720-730` (`RunBusyAsync`, `StopThenRunAsync(DeleteGenerated)`, result count); `RunnerSeriesTests.Delete_mode_deletes_only_generated_rts_and_writes_nothing`; `RunnerServiceTests.Deleting_generated_rts_stops_the_generator_and_they_are_not_written_again` | |
| N3 | Same-name template builds wiped `Paths.txt` and `DicomTags.txt` | done | `TemplateMaker.cs:413-441` (`SaveSettings` keeps them); confirmations at `OnlineTemplatesViewModel.cs:280-292`, `MainWindow.xaml.cs:1116-1128`, `MakeTemplateWindow.xaml.cs:291-295`; `TemplateMakerSettingsTests.Rebuilding_a_template_keeps_its_paths_and_dicom_requirements_when_the_builder_has_none`; `OnlineTemplatesViewModelTests.Build_confirmation_lists_exactly_the_existing_templates_and_cancel_builds_nothing` | |
| N4 | `All_Ontologies.json` rewritten with only the current template's codes | done | `TemplateMaker.cs:373-378` (`OntologyTools.MergeOntologiesIntoFolder`); `SiteBuilder.cs:240`; `OntologyLibraryTests.A_template_build_keeps_library_entries_it_does_not_use` | |
| N5 | *Build Template!* had no name or existence checks | done | `MakeTemplateWindow.xaml.cs:90, 131-134, 277-317`; `NameRulesTests.Template_names_that_cannot_be_a_folder_are_refused_with_a_reason`; `EditorMessagesTests.Replacing_a_template_says_what_is_replaced_and_what_is_kept` | |
| N6 | ROI renames saved one keystroke late | done (no test) | `AddROIRow.cs:247-258` sets `roi.ROIName` before `changed()` | |
| N7 | New ROIs got the first ontology by default | done (differently, as the plan records) | `AddRoiRules.cs:26-50`; `MakeTemplateWindow.xaml.cs:52, 192-197, 539`; `AddRoiRulesTests.A_code_and_then_a_type_must_be_chosen`; `SeriesRtContentTests.An_roi_without_an_ontology_code_is_left_out_whole` | "(no code)" stays deferred (3.1) |
| N8 | Generator hid skipped ROIs and matched blank descriptions | done | `TemplateMatcher.cs:101`; `RunnerStatus.cs:50`; `MainWindow.xaml.cs:608-653`; `RunnerSeriesTests.A_blank_or_missing_description_does_not_match_a_template_with_requirements`; `RunnerStatusTests.The_log_report_lists_written_rts_skipped_rois_and_errors` | |
| N9 | *Create folder with loadable RTs* selected everything, wrote into `Paths.txt`, unstoppable runner | done | `MainWindow.xaml.cs:741-785` (confirm, one-off `TestRtGenerator.Run`), stop toggle `:656-685`; `TestRtGeneratorTests.Writes_one_rt_per_template_on_the_sample_and_leaves_paths_alone`; `ShellMessagesTests.Test_rt_confirmation_states_count_folder_and_that_paths_are_unchanged` | |
| N10 | Working directory was the template root; controls hidden | done (with the "Start in" addition the plan records) | `App.xaml.cs:49` (`TemplateRootResolver.Resolve`); `MainWindow.xaml.cs:362, 537-599`; `TemplateRootResolverTests.An_existing_saved_folder_wins`, `Without_a_saved_folder_the_program_folder_is_used`, `A_working_directory_with_an_ontologies_folder_is_used_and_saved` | |
| N11 | Bulk actions included hidden rows; no delete confirmation | done | `MainWindow.xaml.cs:479-501, 892-942`; `RecycleBinRule.cs:70`; `ShellMessagesTests.Delete_templates_confirmation_lists_names_folders_and_hidden_ones`; `RecycleBinRuleTests.Folders_without_a_recycle_bin_are_named_with_the_reason`; `TemplateRowTextTests.A_selection_separates_shown_and_hidden_selected_rows` | |
| N12 | Varian export defaulted to a hard-coded share; silent overwrite; silent import failures | done | `UiSettings.cs:37` (empty by default); `MainWindow.xaml.cs:1018-1034, 1054-1066, 1145-1152`; `UiSettingsStoreTests.The_varian_share_is_empty_by_default_and_read_when_set_by_hand`; `VarianXmlBatchTests.Existing_files_are_listed_and_kept_unless_replacing`, `Import_keeps_existing_templates_unless_replacing_and_reports_every_file` | |
| N13 | No global exception handler | done | `App.xaml.cs:42-44, 132`; `MainWindow.xaml.cs:353`; `ShellMessagesTests.Unexpected_errors_point_to_the_log`; `AppLogTests` | Handler wiring untested |
| N14 | *Rename* enabled before the template existed; rows pointed at the old folder | done (no test) | `MakeTemplateWindow.xaml.cs:92, 423-474`; `AddTemplateRow.cs:50-52, 81-83` bind to the shared `TemplateMaker` | `AddTemplateRow.template_name` (sort key, `MainWindow.xaml.cs:434`) is set once at `:45` and stays stale after a rename until the next rescan; Phase E checks whether the sort order is wrong after a rename |
| N15 | *Change Ontology Scheme* allowed From = To | done | `ChangeOntologyWindow.xaml.cs:62-66, 153`; `OntologySchemeConverter.cs:92`; `OntologySchemeConverterTests.Converting_a_scheme_into_itself_is_refused_before_anything_changes`; `EditorMessagesTests.The_conversion_confirmation_and_report_give_the_counts` | |
| N16 | *Edit Ontologies* did not require a code value; colour-only errors | done | `EditOntologyWindow.xaml.cs:90-92, 123-130`; `AddOntologyRow.cs:98-103`; `OntologyEntryRules.cs:18, 104`; `OntologyEntryRulesTests.An_entry_needs_a_name_a_unique_code_value_and_a_scheme` | |
| N17 | Corrupt template folders skipped silently | done | `TemplateLibraryScanner.cs:80-160`; `MainWindow.xaml.cs:399-420` (`ShowProblems`); `TemplateLibraryScannerTests.An_unreadable_template_is_reported_and_not_listed`; `ShellMessagesTests.The_problems_line_counts_each_kind` | |
| N18 | Scans and exports blocked the UI thread; ontology file rewritten per template | done | `TemplateLibraryScanner.cs:80`; `MainWindow.xaml.cs:370-378, 1069-1071`; `TemplateLibraryScannerTests.Codes_used_by_templates_are_added_to_the_library_once`, `Each_template_gets_its_own_copy_of_the_library` | |
| N19 | Airtable write from the editor had no confirmation; *Select all* included hidden rows | done | `MakeTemplateWindow.xaml.cs:599-650`; `MainWindow.xaml.cs:513-527, 1172-1232`; `OnlineTemplatesViewModel.cs:240`; `EditorMessagesTests.The_airtable_confirmation_names_the_table_and_says_shared_fields_change_for_every_site`; `OnlineTemplatesViewModelTests.Select_all_ticks_only_the_rows_the_search_shows` | |
| N20 | Add-table validation gaps; same-name connection replaced silently | done | `AddAirtableTableViewModel.cs`; `AddAirTableTemplate.xaml.cs:18`; `AddAirtableTableViewModelTests.A_pasted_table_address_fills_in_both_ids`, `Saving_a_name_that_is_already_connected_asks_first_and_declining_changes_nothing`, `A_malformed_base_id_is_an_error_with_the_expected_format` | |
| N21 | Dead expiry check and unreachable build-from-RTs block | done | `git grep` finds no `OutDatedWindow`, `Running.txt` or `Built_from_RTs` in tracked `.cs`, `.xaml`, `.csproj` | |
| N22 | No folder remembered between dialogs | done | `FileDialogs.cs:33-39, 53-66`; `UiSettingsStore.cs:89`; `UiSettingsStoreTests.Remembering_the_same_folder_again_does_not_write`; `DialogStartFolderTests.Candidates_are_the_preferred_then_the_remembered_folder_without_blanks_or_repeats` | |
| N23 | Paths editor clipped rows, accepted duplicates, kept edits after X | done | `EditPathsWindow.xaml:31, 48`; `EditPathsWindow.xaml.cs:129, 177-186`; `MonitoredSettingsDraftTests.A_folder_that_is_already_monitored_is_refused`, `Edits_change_only_the_draft_until_applied` | Clipping fix untested; Phase E looks |
| N24 | Window titles and typos | done (no test) | `MainWindow.xaml:8`, `AirTableWindow.xaml:9`, `EditPathsWindow.xaml:8`, `MakeTemplateWindow.xaml.cs:88` | Typos not proofread exhaustively |
| N25 | Missing name-length and duplicate checks; case-sensitive search | done | `NameRules.cs:81, 96, 134`; `TemplateRowText.cs:61`; `NameRulesTests.A_template_name_longer_than_16_characters_gets_a_warning_with_the_label_it_becomes`, `A_duplicate_roi_name_is_refused_ignoring_case`; `TemplateRowTextTests.The_search_ignores_case` | |
| N26 | Unreachable `TemplateWindow` and default-template window | done | No tracked file or class of either name; only `MakeTemplateWindow` and `RenameTemplateWindow` remain | |

### 3.4 "Check on Windows before releasing" (section 3)

Phase E of the brief, against the published `1.1.0-rc.0` build (`docs/WORKFLOW_TEST_REPORT.md`
section 6). On 2026-11-03 the workstation locked once the app was granted, so only the items visible in
the log and settings files were verified; a continuation session drove the other nine on 2026-11-04.
On 2026-11-05 the `1.1.0-rc.3` build at `de002b7` was rechecked (report section 6.1):

- startup PASS;
- *Open log folder* PASS;
- F-1 names PASS;
- F-6 rename-then-close PASS;
- RT generator PASS;
- clean exit with no warning lines.

The other items were not repeated. Since `1.1.0-rc.0`, the app's code changed only in display text and
row order (F-1, F-6), a behaviour-preserving test seam (F-7) and how symbols are published (F-4).

| Item | Status |
| --- | --- |
| Startup and the template folder rule | **PASS** (started in a folder holding templates: that folder was used and saved; read back as "the saved template folder" on restart) |
| The log folder | **PASS** (`logs\app-20261103.log` and `app-20261104.log` written, every action logged, no warning or error line; on 2026-11-05 at `1.1.0-rc.3`, *Open log folder* opened one Explorer window at `%LOCALAPPDATA%\DicomTemplateMaker\logs`) |
| RT generator start/stop and the status line | **PASS** (one `_Targets_RNI_UID*.dcm` written beside the four slices; "RT generator running. Last scan ...: 1 RT written ..." while running, "RT generator stopped. Last scan ..." after Stop) |
| *Delete previously generated RTs* | **PASS** (confirmation says the RTs are deleted permanently; "Deleted 1 generated RT."; the four slices remain) |
| *Delete selected* (Recycle Bin; refusal on network drives) | **PASS** for the Recycle Bin (confirmation names it; the copy is gone from the root and listed in the Recycle Bin); network-drive refusal NOT RUN (no network drive here) |
| *Create folder with loadable RTs* (load the RTs in a planning system) | **PASS** for writing ("Wrote 1 test RT into ...\Template_Output", RT and sample CT on disk); the planning-system load NOT RUN (needs D15's environment) |
| Varian XML import and export | **PASS** (`_Targets_RNI.xml` with 6 structures written; `Abdomen.xml` imported as `Abdomen`, 17 ROIs, `All_ROIs.json` on disk) |
| Template editor (build, rename, save/discard on close) | **PASS** (build, add ROI, Save, rename renames the folder, the close prompt offers Yes / No / Cancel and No leaves the file unchanged) |
| Paths and ontology editors | **PASS** (monitored folder listed; a Series Description requirement saved to `DicomTags.txt`; an ontology entry without a code value refused with "Enter a code value (required).") |
| Scheme conversion | **PASS** (From = To refused with a reason, *Convert...* disabled; closed without converting) |
| Both Airtable windows (open and cancel only) | **PASS** (Online Templates shows TG-263 (shared), 246 records published 2026-09-30, 53 templates; one snapshot *Refresh*, no Airtable call; the add-table dialog opened and cancelled with nothing typed) |

Found on the way: F-1 is confirmed (names without one underscore in the main window list only), and
F-6 is new: a template renamed from its row keeps its old place in the list until the next rescan
(observation O-4). Both are fixed and rechecked at `1.1.0-rc.3`. The report has observations O-6 to O-9.
Among them is a second app instance that wrote no log line (question 8). On 2026-11-05 that instance
was explained as a test artifact: computer-use started it outside the Claude app's MSIX package,
which redirects `%LOCALAPPDATA%` writes made from the session's shell.

### 3.5 Actions only the maintainers can take (section 4)

| Item | Status | Evidence |
| --- | --- | --- |
| 1. Revoke every published token; replace or delete the release assets | **closed by maintainer decision** (will not be done) | See P0.1 |
| 2. Add the `AIRTABLE_PAT` secret and run the snapshot workflow once | **done** | Secret dated 2026-09-30; the workflow has run green weekly through 2026-11-02 |
| 3. Turn on secret scanning and push protection | **partial** | Secret scanning enabled; push protection disabled (GitHub API 2026-11-03) |

## 4. Release-readiness list for `v1.1.0`

Everything the plan and the brief put before a release, as of 2026-11-05. A row is green when it is
done or closed by decision, and the user pushes the tag when every row is green.

- Merged on 2026-11-05: 31 (F-7), 24 (F-1), 30 (F-6), 26 (F-2), 27 (F-3), 28 (O-6 note), 29
  (licence), 23 (F-5) and 25 (F-4).
- Before merging, 23 and 25 were brought up to date with `main` so their CI ran with the F-7 fix. In
  23, the one README conflict with 29 kept both blocks: 23's paragraph, then *License*.
- The release-candidate pass at `de002b7` (`1.1.0-rc.3`) is all PASS (report section 6.1).
- Left:
  - the user's snapshot dispatch;
  - the user's clean-VM install;
  - the user's call on committing the overhaul documents;
  - then the tag.

| Item | Status | Note |
| --- | --- | --- |
| P0.1 tokens revoked, old assets replaced or deleted, push protection on | **closed by maintainer decision** | Brian Anderson, 2026-11-03: the tokens are meant to be shared; push protection stays off |
| P0.4 `LICENSE` present and linked from the README | **merged 2026-11-05** (PR 29) | D21 = Apache-2.0 (2026-11-05). Adds `LICENSE`, a *License* section in the README, and ships `LICENSE` beside the exe in the release folder |
| P0.7a publish rehearsal (`dotnet publish` as in `ci.yml`, data files beside the exe, exe starts) | **done at the release candidate** (`1.1.0-rc.3`, `de002b7`, 2026-11-05) | Report 6.1. The folder holds the exe, `LICENSE`, `template_RS.dcm`, `SmallCT\` (4 slices), `FMA_SNOMEDCT_Key.txt`, `Structure Template.xml` and `TemplateSnapshots\TG263.json`; no `.pdb` or XML-doc file (F-4). The exe started and logged ProductVersion `1.1.0-rc.3+de002b7c...` (first rehearsal: report 5.6, `1.1.0-rc.0`) |
| P0.7b a missing snapshot fails the release job | **merged 2026-11-05** (PR 26) | F-2 |
| P0.7c offline install on a clean Windows VM | **not done** (user) | On the release-candidate zip or the published folder |
| Snapshot workflow run before tagging | **not done** (user) | Decided 2026-11-05: the user dispatches it by hand once the merges are in (all are in now); the manager reads its log (export ran; "Unchanged" or a snapshot PR) before the tag. Last run: the scheduled one of 2026-11-02 (41478177997), "Unchanged: 246 records match TemplateSnapshots/TG263.json"; next scheduled run Monday 2026-11-09. If it opens a snapshot PR, that PR merges before the tag and the release commit moves |
| README corrections of plan Appendix A (release-download claims; the macOS build claim) | **merged 2026-11-05** (PR 23, `307a375`) | F-5 |
| `MODERNIZATION_PLAN.md` header and Phase 1 wording | **merged 2026-11-05** (PR 23) | F-5, same pull request |
| Release folder without `.pdb` and XML-doc files | **merged 2026-11-05** (PR 25, `de002b7`); **confirmed at `1.1.0-rc.3`** | F-4 |
| Template names keep their underscores in the main window | **merged 2026-11-05** (PR 24, `9bec9ea`); **PASS at `1.1.0-rc.3`** | F-1. `_Targets_RNI`, `AbdPelv_Prostate`, `CNS_Brain`, `H&N_LX_HPX_Thyroid` shown with every underscore (report 6.1) |
| Renamed template moves to its new place in the list | **merged 2026-11-05** (PR 30, `819b100`); **PASS at `1.1.0-rc.3`** | F-6. `ZZ_Rename` renamed to `AA_Renamed` from its editor; on closing the editor the row moved at once to second place (report 6.1) |
| Windows CI not flaky | **merged 2026-11-05** (PR 31, `77780e5`) | F-7: the test's timer-based release was the cause, not the product's 0.63 s retry window. The hold is now released on the first refused rename through a test seam on `FileSwap.MoveOver`; assertions kept, plus a Windows check that the rename was refused and two retry tests. Windows job green three times in a row (run 42083419578, attempts 1-3) |
| README says to run one copy of the program at a time | **merged 2026-11-05** (PR 28) | O-6 note, decided 2026-11-05; the single-instance guard itself is after `v1.1.0` |
| Validators found beside the interpreter | **merged 2026-11-05** (PR 27); **confirmed at `de002b7`** | F-3 (tooling; not in the release zip). The validators passed with the venv's `Scripts` not on `PATH` |
| "Check on Windows before releasing" list (section 3.4 above) | **11 of 11 PASS** (2026-11-04, rechecked at `1.1.0-rc.3` on 2026-11-05) | *Open log folder*, F-1, F-6, startup and the generator rechecked at the release candidate (report 6.1). NOT RUN, recorded as such: the network-drive refusal (no network drive here) and the planning-system load (D15 environment) |
| Gate green on `main` | **green at `de002b7`** (2026-11-05) | Local: build 0 warnings, 837 tests passed, format clean. CI run 42102552546: Windows, Ubuntu and DICOM validation green |
| Baseline, validators, tools reproduced at the release commit | **done at `de002b7`** (2026-11-05) | 53 sites; `build-warnings.json`, `sites.json`, `run-report.json`, the 53 inventories and the realism report identical to the baseline; validators PASS with `summary.md`, `findings.json` and the 54 `dciodvfy` files identical; snapshot check passed, warnings 0 / 1 / 1 / 7. No command-line tool's own code changed since `08eca39`. The shared changes are F-7's `FileSwap` seam (same behaviour) and F-3's validator lookup |
| `VersionPrefix` 1.1.0 in `Directory.Build.props` | **done** | |
| Overhaul documents committed (`CLAUDE.md`, `.claude/skills/`, `docs/STATUS.md`, `ARCHITECTURE.md`, `WORKFLOW_TEST_REPORT.md`, `PROJECT_MANAGER_PROMPT.md`, the decisions and plan edits) | **uncommitted, the user's call** | Not selected on 2026-11-05; one documentation pull request when the user asks |

## 5. Hygiene backlog (plan Appendix A), status

| Item | Status |
| --- | --- |
| README release-download and macOS claims | not done (`README.md:5, 150-153, 177-182` still make them) |
| Branch protection on `main`, dependabot, `SECURITY.md`, one SDK rule for both workflows | not done (no protection, no `.github/dependabot.yml`, no `SECURITY.md`; both workflows now use `global.json`, so the SDK item is done) |
| `MODERNIZATION_PLAN.md` header and Phase 1 wording | not done |
| Git history holds the leaked tokens and the CT's original identity | maintainer decision, outside the plan |
| Write-warning cap differs between `MakeTemplateWindow` (uncapped) and `MainWindow` (30) | not done (P3.9 if D10 = rec) |
| Two `FakeHttpHandler` copies; tests walk up to the `.sln` for data | not done (both copies present) |
| `CodeMeaning` setter and JSON load skip `RemoveIllegalCharacters` | not done (decided with D1) |
| `Test_Base_key.txt`, `Test_Table_key.txt`, `ontology.pkl`, `Paper/`, root Python scripts, `AirTableRecords/`, `FMAID To SNOMED_Updated.xlsx` | all still present (D21) |

## 6. Decisions on the test report's questions (Brian Anderson, 2026-11-04, relayed by the project-manager session)

| Question (`WORKFLOW_TEST_REPORT.md` section 8) | Answer | Consequence |
| --- | --- | --- |
| 1. P0.1 tension (`LeakedTokens.cs`, README security notice) | Keep as is | Recorded in the P0.1 row; no code or text change |
| 2. Phase E | The manager session drives the running 1.1.0-rc.0 build through the remaining items; Airtable windows open-and-cancel only | Section 3.4 updated as items pass or fail |
| 3. Fixes F-1 to F-5 | All approved, one branch and one pull request per finding against `main`, failing test first where a test applies, whole gate green; F-1 confirmed first | Opened 2026-11-04 by five Opus 5.5 sessions: F-1 PR 24, F-2 PR 26, F-3 PR 27, F-4 PR 25, F-5 PR 23 (all against `main`, unmerged; PRs 23 and 25 hit the Windows flake F-7 and were rerun). New from Phase E: F-6 (renamed template keeps its old sort position) and F-7 (flaky `SnapshotStoreSwapTests` on `windows-latest`), proposed as further sessions |
| 4. O-1 (`--once` and the settle time) | Not selected: keep today's behaviour | Documented limitation: `DicomTemplateMakerCSharp --once` exits 0 and writes nothing for a folder whose newest file is younger than the 10 s settle time; the log says the folder is waiting. Noted in the README by the F-5 pull request if it does not say so |
| 5. Snapshot workflow pull-request path | Not selected: no dispatch | **Unverified** until a real snapshot change runs it (P1.4 row) |
| 6. D2-D22 walkthrough | Walk through now, one section at a time, answers relayed by the project-manager session | Answers transcribed into `OVERHAUL_DECISIONS.md` as they arrive |
| 7. Deliverable 5 (`CLAUDE.md`, `.claude/skills/`) | Approved; uncommitted until asked; a tracked `.claude/settings.json` only for durable allowlists | Written 2026-11-04, uncommitted: `CLAUDE.md` (under 60 lines) and `.claude/skills/{build-and-test,evaluation-pipeline,release-rehearsal}/SKILL.md`; no `settings.json` (no durable allowlist needed yet) |
| 8. O-6, a second app instance | Decided 2026-11-05 (in the second project-manager session): no single-instance guard before `v1.1.0`; the README says to run one copy at a time | PR 28 (README note); the guard and the missing log lines are investigated after `v1.1.0` |

## 7. Facts re-checked in this session (2026-11-03)

- .NET SDK 10.0.402 (`global.json` 10.0.100, `rollForward latestFeature`).
- Test attributes: Core 143 facts + 22 theories, Presentation 223 + 24, Sync 121 + 12, EvaluationTool 24 + 2;
  `dotnet test --solution` runs 825 cases, all passing (2026-11-03, 69 s).
- Phases B to E of the brief ran on 2026-11-03: see `docs/WORKFLOW_TEST_REPORT.md`. The generator at `08eca39`
  reproduces `docs/evidence/baseline-e7f0572/` byte for byte; the validators, realism rules, snapshot check and
  nomenclature report match; the four command-line tools, the dry run and the publish rehearsal pass.
- Branches: `main` is the integration branch; `origin/dev` behind; three merged `origin/claude/*` branches remain
  (`competent-wilbur-f1c8cc`, `cool-allen-t11tt5`, `overhaul-planning-docs`). No open pull requests.
- CI: last `main` run 2026-11-03 success. The snapshot workflow ran on schedule 2026-10-05, 10-12, 10-19, 10-26
  and 11-02, all green, each logging "Unchanged: 246 records"; `TemplateSnapshots/TG263.json` unchanged since
  `b185504` (2026-09-30).
- Security: secret scanning enabled; push protection, non-provider patterns, validity checks and Dependabot
  security updates disabled; `main` unprotected; no `LICENSE`, no `SECURITY.md`.
- Releases v1.0.0 to v1.0.4 exist; the current release job has never run; `VersionPrefix` 1.1.0.
