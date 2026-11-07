# Overhaul and evaluation plan

Written 2026-10-15 against `e7f0572` (main) from
`docs/OVERHAUL_PLANNING_PROMPT.md`. Companions: `OVERHAUL_DECISIONS.md` (D1-D22, recommendations
and answer blanks), `EVALUATION_PROTOCOL.md` (exact procedures), `OVERHAUL_CLAIMS.md` (every vendor and
standards claim relied on, plus the re-check status of every appendix fact).

Status (2026-11-05; `main` at `de002b7`): pull requests 9 to 21, merged 2026-10-16 to 2026-10-18, landed P0.2,
P0.3, P0.6, P1.2-P1.8 and P2.2-P2.9 in full, P1.1 without its `export-table`, `diff-tables` and `fixtures`
commands, P2.1's guard (its second half now follows D1), and the RR1-RR10 half of P2.4. Pull request 29
(2026-11-05) landed P0.4 (Apache-2.0 `LICENSE`). P0.7 is done apart from the clean-VM install: the
publish rehearsal passed at `1.1.0-rc.3`, and a missing snapshot fails the release job (pull request 26).
P0.5, P1.9, P2.10 and everything from P3 on are not started.
Decisions (updated 2026-11-05): all twenty-five answer blanks in `OVERHAUL_DECISIONS.md` are filled
by Brian Anderson (2026-11-03 to 2026-11-05). Every answer accepts the recommendation except D19:
the Clinical Protocol XML is in scope, in a later phase after P6. Facts still pending: the D7 workspace
details, the D15 versions and operators, the D16 Eclipse versions, the D17 dictionaries, the D18
outreach owner and the D21 key-sheet owner. P0.1 was closed by maintainer decision (no revocation).
No step from P3 on has started. Every P4 step may now be scheduled; P3, P5 and P6.2-P6.4 wait on the
pending facts.

Release `v1.1.0` is not a plan step but the release that follows this work. Its status:

- These pull requests are merged:
  - the fixes for the QA findings F-1 to F-7 (23-27, 30, 31);
  - the README note on O-6 (28);
  - the licence (29).
- The release-candidate pass at `de002b7` is green: gate, baseline, validators, publish rehearsal and
  Windows checks.
- Left before the tag: the snapshot workflow run, the clean-VM install, and the user's push of the
  tag.

Where the details are:

- Per-step evidence and the release-readiness list: `docs/STATUS.md`.
- The gate, baseline reproduction, validators, tools, publish rehearsal and Windows checks of
  2026-11-03/04, and the release-candidate recheck of 2026-11-05: `docs/WORKFLOW_TEST_REPORT.md`.
- The system drawing: `docs/ARCHITECTURE.md`.

The request, in four goals: **G1** download the shared TG-263 template as DICOM; **G2** re-upload it to an
Airtable base we control; **G3** test the DICOM; **G4a** a more robust way to make the Varian Eclipse
StructureTemplate XML ("Xaml") files; **G4b** publicly available template formats other vendors ingest.
Priority: G1-G3 before G4; within a goal, steps that produce evidence before refactors.

## 1. Starting point (verified 2026-10-15)

- `dotnet build DicomTemplateMaker.sln`: 0 warnings, 0 errors (.NET SDK 10.0.401). `dotnet test --solution
  DicomTemplateMaker.sln`: 630 passed, 0 failed (Core 112 facts + 20 theories, Presentation 213 + 24,
  Sync 90 + 6). Every test project targets `net10.0`; only `DicomTemplateMakerGUI` is Windows-only.
- `TemplateSnapshots/TG263.json`: 184,255 bytes, SHA-256 `059cd49e731fcc56...eac03c4`, generated
  2026-09-30T21:46:50Z, last changed in `b185504`. 246 records, 53 sites, 55 records in no site, 191 in
  at least one, 1,178 site memberships. Every A.1 count re-measured and holding; the few corrections
  are in claims part 2.
- Live source `appYl68lenTOFqTDh`/`tblex7IPsmm8hvVEc` (read tools only): 246 records, schema as in the
  prompt's Appendix C; the five context columns and `CommonName` are empty in every record; `Id` is empty
  in every record; `FMAID` is filled in all 246 but 82 are "88", 10 are "99" and 20 are labels such as
  "PTV" or "CTVHR", leaving about 134 plausible FMA ids. `Type` offers 24 options (no OAR,
  TREATED_VOLUME, DEVICE; extra `None`, `Control`); `Colors_RGB` 24; `Template_Recommend` 53;
  `Template_Consider` 48; `Structure` has duplicate option names (`_TS_CTVsb`, `_TS_CTV_High`).
- Corrections to the prompt found during re-checking (details in claims part 2): `Include` is false
  only when the JSON key is missing; writes remove the current site from the opposite list
  (`WritePlanner.cs:170-173`); three context attributes are written without Context Identifier
  (`RtStructureBuilder.cs:431-433`); `RemoveIllegalCharacters` runs only in two constructors; the
  delta read depends on the last full download, not cache age; `TemplateSnapshotTool` exits 2 without
  a token (the workflow exits 0); `TemplateSourceItem.WriteTemplatesAsync` is at `:92-118`; any 404,
  including a missing base or table, is treated as a deleted record (`AirtableTableSource.cs:266-272`);
  all 53 site names pass `IsUsableFolderName` (`OnlineTemplatesViewModel.cs:662`), so that invariant
  holds today.
- Corrections from the source checks (claims part 1): Table C.8.8.8.3.1-2 maps interpreted types to
  Therapeutic Role attributes, not to category and identification codes; RT ROI Identification Code
  Sequence allows one item; the ESAPI code-dictionary API is documented from 16.0, not 15.6; the Varian
  System Server 18.x conformance statements are public; the supplement the TG-263 report cites
  (196) is a 2016 draft never finalised; the Airtable error types the client keys on, other than
  `NOT_FOUND` and the rate-limit 429, are not in Airtable's published error list.

## 2. Conventions

- Step id `Pn.m`. **Goal** G1, G2, G3, G4a, G4b or WS5 (gating). **Dec** lists decision tags.
- **Automation**: `CI` = CI on any OS (ubuntu job unless noted); `Local` = local, any OS (includes
  manual web-UI work); `Win` = local Windows (WPF, DPAPI, Recycle Bin); `TPS` = manual work in a
  treatment-planning system.
- A step tagged with a decision is the recommended branch: "if Dn = rec: ...; otherwise: ...". No such
  step is scheduled until its decision is answered or the maintainers accept the recommendation.
- Evidence (inventories, validator reports, diffs, run logs without secrets) is committed under
  `docs/evidence/<phase>/`. Raw Airtable exports stay outside the repository (the source base's
  "Copies of deleted" table has a `Collaborators` field).
- Goldens change only through a reviewed `UPDATE_GOLDEN=1` regeneration (`DicomTemplateCore.Tests/
  TestFolder.cs:34-44`); each step that changes RT or XML output names the goldens it changes.
- Safety rules from the prompt's section 4 hold in every phase: no automated write to
  `appYl68lenTOFqTDh`; no generated RT or sample CT in a clinical database; nothing token-shaped in the
  repository; the first live Airtable run is an observation run.

## 3. Phases in execution order

### P0. Gates and safety (WS5, constraints)

Goal: nothing live (Airtable writes, TPS imports, releases, published corpora) until these hold.
Effort 2-4 engineer-days plus maintainer time. Risk: token revocation cannot be confirmed, which blocks
P3 but not P1/P2.

| Id | Goal | Step and file-level targets | Dec | Auto | Acceptance |
| --- | --- | --- | --- | --- | --- |
| P0.1 | WS5 | Confirm revocation of the four leaked tokens (Airtable token pages); inspect and replace or delete the v1.0.0-v1.0.4 release assets; enable secret-scanning push protection | D21 | Local (manual) | Maintainer note in `docs/evidence/p0/` with date per token (no token text); assets gone or replaced |
| P0.2 | G3 | Re-identify `DicomTemplateMakerGUI/SmallCT` (4 files): if D15 gives a site convention use it, otherwise `PatientName ZZTEST^TG263TEMPLATE`, `PatientID ZZTEST-0001`, empty birth date, `PatientIdentityRemoved YES`, `DeidentificationMethod` naming the script, consistent new `2.25.` UIDs; one-off `tools/reidentify_sample_ct.py` kept for audit; guard test `DicomTemplateCore.Tests/SampleCtIdentityTests.cs` | D15 | Local, CI | Guard passes; all 630 tests pass unchanged (tests compare CT to RT, none hard-code the name); the shipped copy (GUI csproj:41) is fictitious |
| P0.3 | G3 | `DicomTemplateMakerGUI/Test.dcm` (ARIA RTM 16.1 export, institution address, station, operator): remove, or replace with a PS3.15 Annex E de-identified copy | D21 | Local | `git grep` finds no institution or station string; de-identification record if replaced |
| P0.4 | WS5 | Add `LICENSE` before any golden corpus, snapshot release or evidence bundle is published | D21 | Local (manual) | File present; README links it |
| P0.5 | G2 | Evaluation Airtable setup: target workspace per D7 (must not host `appYl68lenTOFqTDh`, because a token that may create bases needs workspace-wide access, claim A9); PAT `AIRTABLE_EVAL_PAT` with `data.records:read/write`, `schema.bases:read/write`, scoped to that workspace only; stored as an environment secret | D7 | Local (manual) | `GET /v0/meta/whoami` lists the scopes; a read of `appYl68lenTOFqTDh` with this token is refused |
| P0.6 | G3 | First intended-use statement in `README.md`: what the RTs contain, that no TPS import has been verified at `e7f0572`, test databases only for evaluation | D12 | Local | Reviewed README section |
| P0.7 | WS5 | Rehearse the never-run release job on `v1.1.0-rc.1` after P0.2-P0.4; install the zip on a clean Windows VM offline; confirm `template_RS.dcm` (until P4.6), `SmallCT`, `FMA_SNOMEDCT_Key.txt`, bundled snapshot; make a missing snapshot fail `ci.yml:76-78` | D21 | CI (windows), Win | Release created; VM checklist signed; not on the evaluation critical path |

Exit: P0.1-P0.6 done before any live Airtable write (P3) or TPS import (P5); P0.7 before any release.
Goldens: none.

### P1. Evidence baseline (behaviour of `e7f0572`)

Goal: G1 and G3 evidence before any behaviour change. Effort 7-11 days. Risk: the extraction in P1.1
changes behaviour; mitigated by the characterisation test written first.

| Id | Goal | Step and file-level targets | Dec | Auto | Acceptance |
| --- | --- | --- | --- | --- | --- |
| P1.1 | G1 | Harness. First a characterisation test in `OnlineTemplatesViewModelTests.cs` that builds 3 sites through the view model from the frozen snapshot (P1.3) and hashes each `All_ROIs.json`. Then extract `SiteBuilder` (public, `DicomTemplateMaker.Presentation/Services/SiteBuilder.cs`) from the private `BuildTemplates` (:362), `TryMergeCodes` (:403), `ApplyNamingChoice` (:626), `TemplatePath` (:656) and `IsUsableFolderName` (:662) of `OnlineTemplatesViewModel`; it returns every warning (record id, site, kind) instead of the 30-line `ListLimited` text (:709). If D11 = rec: new console project `TemplateEvaluationTool/` (in the .sln, not in the release zip) with `build-sites`, `generate-rts` (via `TestRtGenerator.Run`, `RunReport` to JSON), `inventory`, `write-airtable`, `export-table`, `diff-tables`, `fixtures`; otherwise the same commands in `DicomTemplateMakerCSharp` | D11 | CI | Hashes identical before and after extraction; the tool writes `build-warnings.json`, `run-report.json`, `manifest.json` |
| P1.2 | G1 | Reproducibility: every run records snapshot path, SHA-256 and git SHA in `manifest.json`; `--data-dir` keeps the tool away from `%LOCALAPPDATA%`; manual GUI runs follow the clean-state checklist (protocol 1.1) | - | CI, Win | Two runs on the same inputs give identical outputs apart from masked UIDs and dates |
| P1.3 | G1 | Frozen fixture: copy `TG263.json` at `b185504` to `TemplateSync.Tests/TestData/TG263.2026-09-30.json`; `SnapshotFactsTests.cs` pins the A.1 facts exactly (protocol 1.2 lists them) | D1b | CI | Test green; any change to the fixture fails it |
| P1.4 | G1, WS5 | Publication gate: `TemplateSnapshotTool check <file>`. Gates: at least 240 records; every record has `Structure`, `SchemeCode`, `Scheme`; every `Colors_RGB` parses. Warnings until D1b is resolved: usable folder names, unique `Structure`, `Type` in the 25 terms, `SchemeCode` valid for its scheme. `refresh-template-snapshot.yml`: run `check` before committing, open a pull request instead of pushing to `main` (D21), fail on manual dispatch when the secret is missing, take the SDK from `global.json` | D1b, D21 | CI | Branch run opens a PR with the check summary; a fixture with 120 records fails |
| P1.5 | G1 | Baseline build: all 53 sites from the frozen snapshot with the default naming choice; one RT per site on the P0.2 CT; outputs to `docs/evidence/baseline-e7f0572/` | - | CI | Every site built or listed with its reason; RT ROI count per site recorded next to the harness's built count |
| P1.6 | G1, G3 | Baseline inventory: `inventory` writes every element including file meta (tag, keyword, VR, VM, length, value; nested sequences), UIDs replaced by `UID#n` in order of first use (equalities stay visible), dates, times, Implementation Class UID/Version and patient attributes masked; `SUMMARY.md` annotates A.4 C1-C13 where they appear; one file cross-checked with `dcmdump` and pydicom | - | CI | 53 inventories committed as characterisation, not as correct |
| P1.7 | G3 | Validators in report mode over the baseline: fo-dicom `Validate()`, `dciodvfy -new`, `validate_iods --edition 2025b` and the current edition (protocol 3) | - | CI | Reports committed; each finding mapped to a C-id or given a new id; seeds the allowlist of P2.3 |
| P1.8 | G1 | Nomenclature: fetch the AAPM worksheet at test time (record URL and SHA-256), compare every `Structure`/`TG_263` name; check names with in-repo TG-263 rules (at most 16 characters, case-insensitive uniqueness, `z`/`_` prefixes, laterality suffixes), written in-repo because UAB-RO TG263-Toolkit has no licence file (claim T6); opt-in `Network` test category | D21 | Local | Non-compliant names listed in the baseline |
| P1.9 | G2 | Source backup: (1) "Duplicate base" of the source with records into the evaluation workspace, kept untouched; (2) `export-table --raw` of all three tables, every field, saved outside the repository with SHA-256 | D7 | Local (manual) | Counts 246 plus the two small tables; hashes in `docs/evidence/p1/` |

Exit: `docs/evidence/baseline-e7f0572/` holds 53 inventories, warnings, run report, validator reports
and the nomenclature report; `SnapshotFactsTests` green; the workflow publishes through a gated pull
request; backup hashes recorded. Goldens: none changed; new characterisation files only.

### P2. Safety fixes and test infrastructure

Goal: remove the blockers for P3 and P4. Effort 9-14 days. Risk: P2.2 widens what the importer accepts;
covered by the round-trip test.

| Id | Goal | Step and file-level targets | Dec | Auto | Acceptance |
| --- | --- | --- | --- | --- | --- |
| P2.1 | G1 | Code-meaning drift guard (A.2 item 1). First a failing test: open and save a snapshot-built site with several PTVs sharing 228793007 through `TemplateMaker.make_template` (`:292-319`) and assert each ROI keeps its meaning. Guard valid under both D1 answers: `categorize_folder` (`:486-497`), the by-value dedup (`:303`) and `interpret_RT` re-pointing (`:145-152`) match on (meaning, value, scheme). If D1 = rec (a): the guard is temporary and P4.3 replaces TG-263-name meanings with concept meanings in `TemplateEntryMapper`, after which matching may use (scheme, value); otherwise (b): the guard is final and is extended to the library merge (`OntologyCodeClass.cs:84`) and `AddOntologyIfNotExists` (`:266`). Until this lands the harness never opens and saves a template | D1 | CI | Test passes; P1.5 rerun gives identical inventories |
| P2.2 | G1, G3 | Tolerant `interpret_RT` (C10): `TemplateMaker.cs:96-128` uses TryGet for the nine code attributes (`:112-120`) and the three sequences (`:96, :102, :125`); ROIs without observations or codes become code-less ROIs (needs D20 "(no code)") or are skipped with a reason; `RtStructureSetFile.TryInterpret` (`:73-74`) reports `NullReferenceException`-class failures as reasons. Round-trip test: generate, interpret, compare name, colour, type and code for all 53 baseline RTs plus a bare-SCT RT built in the test | D20 | CI | 53/53 equal; bare-SCT RT reads; must land before P4.1 |
| P2.3 | G3 | CI validation stage: job `dicom-validation` (ubuntu) runs the evaluation tests that write RTs, then pinned `dicom3tools` and `dicom-validator`; gate on parsed `Error -` lines and validator exit codes; `--edition 2025b` pinned with the reason in a comment; fo-dicom `Validate()` inside the tests; `docs/evidence/validator-allowlist.json` from P1.7, each entry tagged with a C-id, may only shrink | - | CI | Job green; a test RT with mismatched ROI numbers fails it |
| P2.4 | G3 | TPS-realism checker `DicomTemplateCore/Validation/RtRealismChecker.cs` (rules in protocol 3.3), run over every generated RT; usable by the generator for D4's refuse/warn | D4, D14 | CI | Positive and negative test per rule; baseline violations in the allowlist |
| P2.5 | G3 | Orthanc smoke: service container in the validation job; C-STORE with `python -m pynetdicom storescu`; compare `/instances/{id}/simplified-tags` names, colours, types and codes with the inventory | - | CI (Linux only) | All RTs stored and read back equal; documented as not a conformance oracle |
| P2.6 | G2 | Logging `DelegatingHandler` in `TemplateEvaluationTool` on the `HttpClient` handed to `AirtableHttpClient`: method, URL, status, full body for non-2xx, request body for writes, `Authorization` redacted, JSONL output | D11 | CI | Unit tests with the existing `FakeHttpHandler`; no header value ever written |
| P2.7 | G2 | Write driver `write-airtable --base --table --sites --cache-dir --dry-run`: builds `AirtableHttpClient` + `AirtableTableSource` (cache via `SnapshotStore`) and calls the same mapping and write as `TemplateSourceItem.WriteTemplatesAsync` (`:92-118`, mapping `:107`, call `:111`); refuses `appYl68lenTOFqTDh`/`tblex7IPsmm8hvVEc` and any base not in `EVAL_TARGET_BASES`; logs `WriteResult.ApiCalls` and `WritePlan.RequestCount` | D11 | CI | Refusal tests; dry run prints creates and updates per site |
| P2.8 | G2 | Column alias: `AirTableEntry` (`:34-43`) reads `TG_263SpanishR`/`TG_263RSpanish` and the French pair; writes the spelling the table has (schema read with `schema.bases:read`, else the unknown-field probe at `TableFetcher.cs:37`); property names unchanged; canonical spelling per D7 | D7 | CI | Tests against both real schemas |
| P2.9 | G1, G2 | Export robust to schema drift: `TemplateSnapshotTool` requests all columns (no `fields[]`) and lets `Normalize` keep the mapped ones; drop `MISSING_COLUMNS` (`refresh-template-snapshot.yml:51, :65`) | - | CI | Export of a recorded response is byte-identical to today's snapshot |
| P2.10 | G1, G2 | Live reads sorted: `AirtableTableSource.cs:329` sends `sort` on `Record` when the table has it, else sorts by `createdTime` then id | D7b | CI | Live and snapshot builds give the same ROI order on a recorded response |

Exit: drift test, round-trip test, `dicom-validation` job (validators, realism checker, Orthanc) green
on `main`; the driver's refusal tests pass; no live Airtable call made yet. Goldens: none (P2.1 must
leave the baseline inventories byte-identical).

### P3. Airtable round trip (G2)

Goal: run the write path live against throwaway bases, read back, prove field by field what survives.
Effort 6-10 days; calendar bounded by the workspace's monthly quota. Risk: error bodies differ from the
fakes; that is what P3.2 is for.

| Id | Goal | Step and file-level targets | Dec | Auto | Acceptance |
| --- | --- | --- | --- | --- | --- |
| P3.1 | G2 | Targets: (a) "Duplicate base" of the source with records (manual, zero calls; record what carried over: automations, option colours, `Record` values); (b) empty base created with `POST /v0/meta/bases` and the schema of protocol 2.1 (53 site options and 24 colour options declared with colours, `Record` autonumber, `RGB` absent on purpose). The connector may create this schema and clean up, nothing else | D7 | Local | Both bases listed in `EVAL_TARGET_BASES`; schema read back equals protocol 2.1 |
| P3.2 | G2 | Observation run (protocol 2.3): one small site into (b); provoke unknown field, deleted record, delta read within 7 days of the last full read, array into a text column (scratch table); record real bodies in `TemplateSync.Tests/Fakes/AirtableJson.cs`; fix `TableFetcher.cs:37`, `AirtableTableSource.cs:266-272` or `AirtableHttpClient.cs:317-322` where shapes differ; separate a missing base or table from a deleted record | - | Local (live), CI | Every provoked shape has a test using its real body |
| P3.3 | G2 | Leg (a): all 53 sites into (b), then into (a); `ApiCalls` per run against the dry-run estimate | D9b | Local (live) | Estimate within 10% of actual; logs in `docs/evidence/p3/` |
| P3.4 | G2 | Read-back and diff: `TableExporter.ExportAsync` on each target (`Record` sort) and a same-day raw export of the source; `diff-tables` keyed on `Structure` over the 26 mapped columns (`FMAID`, `Id` informational); every difference classified by R-id (protocol 2.4); the connector reads 10 records as an independent check | D3, D8, D9 | Local | No unclassified difference |
| P3.5 | G2 | Fix the R items per decisions: R1 and R7 (if D3 = rec: write variants, keep canonical name; otherwise tolerated); R2 (if D1 = rec: `CommonName` = concept meaning; otherwise stop writing it); R3 (write the existing named option when the colour matches one, never create `Auto:`, `TemplateEntryMapper.cs:128`); R4 (pad `RGB`, `:129`); R5 (context columns per D2); R6 (D6); R10 (set comparison of `Colors_RGB` at `WritePlanner.cs:189-190`, case-only duplicate warning at `:64, :75`); R8 and R9 tolerated under D8 and D9 = rec | D1, D2, D3, D6, D8, D9 | CI | Payload tests in `TemplateSync.Tests`; rerun of P3.4 shows only tolerated items |
| P3.6 | G2 | Idempotency: second driver run on unchanged sites | D8 | Local (live), CI | Zero create or update requests; one refresh |
| P3.7 | G2 | Leg (b): RTs from P1.5 and P4 → `RtStructureSetFile.TryInterpret` → template folders → driver → a fresh table in (b); diff against leg (a); retire `WriteToAirTable.py` | D9b, D21 | Local (live), CI | Same as leg (a) apart from documented items; after P2.2 |
| P3.8 | G2 | If D9 = rec: skipped. Otherwise: `copy-table` (raw read, batched creates with `typecast`) and its diff (only record ids and `Record` may differ) | D9 | CI, Local | As stated |
| P3.9 | G2 | Manual WPF pass (protocol 2.6), then D10: `AIRTABLE_PAT` limited to a named connection (`AirtableConnectionStore.cs:112-122`); connection test adds `whoami` scopes and a column check (`TemplateSourceCatalog.cs:156-169`); dry-run preview before the first request in `MainWindow.xaml.cs:1172-1232` and `MakeTemplateWindow.xaml.cs:622`; one warning cap for both windows | D10 | Win, CI | Recorded texts and log in `docs/evidence/p3/`; Presentation tests for the new logic |
| P3.10 | G2 | `.github/workflows/airtable-roundtrip.yml`: `workflow_dispatch` only, environment with required reviewer, `AIRTABLE_EVAL_PAT` mapped into `AIRTABLE_PAT` only in the driver step, logs uploaded as artifacts, never scheduled | D7 | CI | A dispatch run reproduces P3.3-P3.6 |

Exit: every provoked error shape is a recorded fake; leg (a) and leg (b) diffs have no unclassified
difference; the idempotency run sends zero writes; `ApiCalls` logged per run. Goldens: the fabricated
`Echo` replies give way to recorded bodies in `TemplateSync.Tests`.

### P4. RT conformance hardening (G3, with WS1 items 5, 6, 9, 10)

Goal: close A.4 C1-C13 per the decisions; every RT-changing step regenerates the inventories.
Effort 10-16 days. Risk: TPS behaviour changes (for example Eclipse wanting `VMS011`); P5 tests it.

| Id | Goal | Step and file-level targets | Dec | Auto | Acceptance |
| --- | --- | --- | --- | --- | --- |
| P4.1 | G1, G3 | Code item (C1, C8). If D2 = rec: clear all six context attributes for SCT codes at mapping time (`AirTableEntry.cs:17-26`, `OntologyCodeClass.cs:292, 297-302`, `TemplateEntryMapper.cs:120-125`), keep `VMS011` on FMA bound for Eclipse until P5.3; builder writes the context group as a unit (`RtStructureBuilder.cs:420-433`); `CodingSchemeVersion` where PS3.16 requires it; top-level Coding Scheme Identification Sequence; `LongCodeValue` above 16 characters. Otherwise: only `CodingSchemeVersion` and the sequence | D2 | CI | P2.2 round trip still 53/53; validators clean for C1/C8 |
| P4.2 | G1 | Non-SNOMED codes (WS1 item 6). If D1b = rec: the seven "88"/"99" codes are omitted; `RtStructureBuilder.Validate` (`:244-292`) accepts code-less ROIs; Add ROI offers "(no code)" (D20). Otherwise: derive codes from Table C.8.8.8.3.1-1 | D1b, D20 | CI | Seven ROIs written without identification code; editor test |
| P4.3 | G1, G4b | Category, identification and role (WS4 item 4). Both branches: Therapeutic Role Category/Type (3010,0064/0065) from the interpreted type by Table C.8.8.8.3.1-2; Segmented Property Category for EXTERNAL, MARKER, BRACHY_SRC_APP by Table C.8.8.8.3.1-1. If D1 = rec (a): targets get category (130041, DCM, "RT Target") with the CID 9534 code as identification code (CID 9580 maps 130041 to 9534); anatomy codes get a CID 7150 category after a confirm-by read of CID 7150; concept meanings from `DicomTemplateCore/Ontology/ConceptMeanings.json` (PS3.16, else curated `CommonName`); numeric `FMAID` added to the mapped columns and written in Equivalent Code Sequence (0008,0121) inside the identification item. Otherwise (b): no category for targets, meanings stay TG-263 names | D1 | CI | Inventory shows role on every mapped type and category where the tables give one; validators clean |
| P4.4 | G3 | Interpreted types (C2). If D4 = rec: one 25-term list `DicomTemplateCore/Ontology/InterpretedTypes.cs` replacing `Presentation/Editors/InterpretedTypes.cs`; alias map with warnings in `RtStructureBuilder.cs:251-254, 438` and `VarianXmlReader.cs:143`; refuse unknown terms. Otherwise per D4 | D4 | CI | `BODY`/`None`/`Control` fixtures produce the mapped term and a warning |
| P4.5 | G3 | Provenance (C3, C4, C5, C9) per D12: constants at `RtStructureBuilder.cs:21-23, 183-184, 411, 439`; drop `ROIObservationLabel` (`:442`); `SeriesDescription`, `StructureSetName`, `StructureSetDescription`, `ApprovalStatus` written deliberately | D12 | CI | Inventory diff shows exactly these attributes changing |
| P4.6 | G3 | Seed and syntax (C5, C6, C13). If D13 = rec: build from scratch in `RtStructureBuilder` (replace the clone at `:127` and `new DicomFile(rt)` at `:240`), remove `template_RS.dcm` from the runner (`DicomTemplateRunner.cs:111, 483-490`) and the csproj copies, retire the "template missing" tests; transfer syntax per D14; Frame of Reference module written deliberately (UID at `:166`, Position Reference Indicator present and empty) with an assertion that the UID equals Referenced Frame of Reference and every Structure Set ROI item. Otherwise: curated seed plus a test that fails when it gains attributes | D13, D14 | CI | Inventory lists no attribute without a source in the conformance description |
| P4.7 | G3 | Names and labels (C7, C12) per D14: refuse duplicate names ignoring case and trailing spaces; warn above 16 characters; reject colliding `StructureSetLabel`s across a template set; full name in `StructureSetName` | D14 | CI | Fixtures for each rule |
| P4.8 | G3 | `Include` (C11, A.2 item 6). If D5 = rec: missing JSON key reads as true (`ROIClass.cs:422, 588-591`); Consider ROIs written with ROI Description "optional (TG-263 Consider)"; per-template "omit optional ROIs" switch, default off. Otherwise: filter on `Include` | D5 | CI | Inventory shows the description on Consider ROIs only |
| P4.9 | G1 | ROI order and content (WS1 item 9): Recommend ROIs first, then Consider, each in snapshot `Record` order; the RT holds exactly the ROIs the harness reports as built | D7b, D5 | CI | Test over all 53 sites |
| P4.10 | G1, G3 | Regenerated inventories after each of P4.1-P4.9 that changes RT content; reviewed `UPDATE_GOLDEN=1` diff summarised in `docs/evidence/inventory-changes.md`. Goldens changed: the RT inventories; if D4 = rec also `Abdomen.import.golden.json` and `StructureTemplate.import.golden.json` (imported `BODY` becomes EXTERNAL) | - | CI | Every changed line traced to a step |
| P4.11 | G3 | `docs/RT_CONFORMANCE.md` next to the inventories: modules; source of each attribute (images, template, ROI, constant); UID, character-set, transfer-syntax and empty-contour policy; Frame of Reference written deliberately | D13, D14 | Local | Reviewed |
| P4.12 | G3 | Runner edge cases with tests or documented limits: series split across sub-directories (one partial RT per directory, `DicomTemplateRunner.cs:50-55, 537-539, 673, 724`), Enhanced multi-frame CT/MR, several ISO 2022 character sets, multi-frame Secondary Capture. If D22 = rec: remove `CleaningRTStructureCsharp`; otherwise harden it (null checks at `RTCleaner.cs:24-33`, new SOP Instance UID, tests) | D22 | CI | Each case has a test or a README limitation |
| P4.13 | G1 | Snapshot provenance (WS1 item 10): header gains AAPM edition and worksheet SHA-256 (`schemaVersion` 2); tagged snapshot releases after P0.4; monthly AAPM-page edition check | D21 | CI | Workflow annotates edition changes |

Exit: the validator allowlist holds only entries a decision keeps; inventories regenerated and reviewed
after each step; `docs/RT_CONFORMANCE.md` written. Goldens: RT inventories; both `*.import.golden.json`
if D4 = rec.

### P5. TPS acceptance (G3)

Goal: evidence that the RTs load, keep names, colours, types and codes in real systems. Effort 6-12
days of engineering plus site time; lead time is access (D15). Risk: no access; G3 then rests on
validators and Orthanc, and the intended-use statement says so.

| Id | Goal | Step and file-level targets | Dec | Auto | Acceptance |
| --- | --- | --- | --- | --- | --- |
| P5.1 | G3 | Realistic planning CT (50+ slices) from a public collection whose licence allows reuse, de-identified, given the P0.2 identity; fetched at test time unless the licence allows committing | D15, D21 | Local | Licence recorded; identity checked by the guard test |
| P5.2 | G3 | Fixture families (protocol 4.2) generated by `fixtures` on both CTs, `SeriesDescription` marked "evaluation, not for clinical use" | D5, D14 | CI | All families pass P2.3 except where a family is deliberately invalid |
| P5.3 | G3 | Eclipse first (protocol 4.3): import CT and RT through one path with UID preservation; ESAPI research-mode read-back (`tools/esapi/ListStructures.cs`); re-export and diff | D15, D16, D17 | TPS | Matrix row filled for every family |
| P5.4 | G3 | RayStation (protocol 4.3): scripted import, `ScriptableDicomExport`, fo-dicom diff; "save as structure template" experiment with the script recorder | D15 | TPS | Row filled; template-API finding recorded |
| P5.5 | G3 | Every other system in the matrix (protocol 4.1): status available / vendor contact / no access; run what is available (ProKnow sandbox, MIM association test for Implicit VR) | D15, D18 | TPS, Local | No blank row |
| P5.6 | G3 | Reader fixtures (WS3 item 8): bare-SCT RT from this program, the P0.3 de-identified `Test.dcm`, one de-identified RayStation or MIM export; `interpret_RT` tests | D21 | CI | All three read |
| P5.7 | G3 | Intended-use statement updated with verified systems and versions and known limits | D12 | Local | README section reviewed |

Exit: the matrix (protocol 4.1) has no blank row and a complete Eclipse row; the README states what
was verified where. Goldens: none.

### P6. Varian StructureTemplate XML (G4a)

Goal: a writer and reader that match real Eclipse exports, proven by import tests. Effort 8-14 days.
Risk: Eclipse import rules are undocumented; every rule below has a confirm-by import in P6.3.

| Id | Goal | Step and file-level targets | Dec | Auto | Acceptance |
| --- | --- | --- | --- | --- | --- |
| P6.1 | G4a | Corpus and documents: read the public Varian System Server 18.0/18.1 DICOM conformance statements (claim V11) for RTSTRUCT import rules; fresh exports from our Eclipse (D16); StrukturMall (MIT) and rtpros (Apache-2.0) committed with attribution only if needed, else fetched; AAPM `EclipseStructureTemplates.zip` fetched at test time into `TG263_CORPUS_DIR`, never committed; reader characterisation over all files with fallbacks reported | D16, D21 | Local, CI | Corpus manifest with licence per file |
| P6.2 | G4a | Dictionary export: ESAPI script listing `Application.StructureCodes` (FMA, RadLex, Srt, VmsStructCode: keys, versions; API documented from ESAPI 16.0, claim V5); `check-codes` compares every snapshot code and numeric `FMAID` with it | D17 | TPS, Local | Coverage table per dictionary |
| P6.3 | G4a | Confirm-by imports into non-clinical Eclipse: one-structure templates coded SCT, SRT, FMA and none; Version 1.1 vs 1.2; code-less `<Identification>` shape; each VolumeType alias | D16, D17 | TPS | Accept or reject recorded per file |
| P6.4 | G4a | Writer and reader (`VarianXmlWriter.cs`, `VarianXmlReader.cs`): Version 1.2 default with `Preview Version` and `TreatmentSite`, 1.1 selectable (`:30`); `CodeSchemeVersion` per scheme (`:195, :316`); VolumeType map (`:185`; reader `:143`); `<Identification>` always written with empty `VolumeID`/`VolumeCode`/`VolumeCodeTable`, `StructureCode` only when a code exists and D17 allows (`:190-198`); colours written as real exports do, no system colours (`:137-149`); padded dates (`:28-39`); explicit `XmlWriterSettings` and `using` (`:50-55`); reader checks root element and version, keeps `CodeSchemeVersion` (`:152`), parses all observed `ColorAndStyle` forms and reports fallbacks instead of silent red (`:159-203`); hand-written XSD from the corpus; remove unused `AddROI(string)` and `ReturnUppercaseWord` | D4, D16, D17 | CI | Corpus files round-trip; P6.3 imports accepted |
| P6.5 | G4a | Golden regeneration: `Abdomen.roundtrip.golden.xml` (Version 1.2 kept, per-scheme versions), `StructureTemplate.roundtrip.golden.xml` (Identification, VolumeType), both `*.import.golden.json` (reported colour fallbacks, `Kidney_R` and `x Artifact` no longer red where parseable) | D4 | CI | Reviewed diff |
| P6.6 | G4a | ESAPI apply-template script (StrukturMall pattern: `CanAddStructure`, `AddStructure(StructureCodeInfo)`) reading the canonical JSON | D18 | TPS | Applied in non-clinical Eclipse; matrix row |
| P6.7 | G4a | Housekeeping: rename `Xaml_Maker` and its CLI; route `XamlMakerCsharp/Program.cs` (`:31`, `:57`) through `VarianXmlBatch` (NameRules, atomic writes, traversal guard); move the five Core files out of namespace `DicomTemplateMakerGUI.Services`; drop `Structure Template.xml` from GUI content (csproj:42), keep the test link; delete the Clinical Protocol pipeline if D19 = rec | D19, D21 | CI | Tests for the CLI path; traversal test |

Exit: every corpus file round-trips with reported fallbacks only; the P6.3 imports are accepted or the
writer defaults follow what Eclipse accepted. Goldens: the four Varian XML goldens (P6.5).

### P7. Vendor-neutral carriers (G4b)

Goal: the formats other vendors ingest, built only where an ingestion path is documented and testable.
Effort 10-20 days depending on D18. Risk: vendor documentation differs from behaviour; each exporter
has a confirm-by step before code depends on it.

| Id | Goal | Step and file-level targets | Dec | Auto | Acceptance |
| --- | --- | --- | --- | --- | --- |
| P7.1 | G4b | Canonical JSON schema `schemas/template.schema.json` (versioned): stable ROI id, canonical name plus variants (D3), anatomy code and role (D1), several codes under a (scheme, value) identity, metadata, vendor blocks (D6), per-vendor mapping tables, TG-263 writer rules as tests; migration from `All_ROIs.json` and legacy text folders; mappable to RT Segment Annotation. If D18 = rebuild now: before P6.4 | D1, D3, D6, D18 | CI | Migration of the 53 sites lossless against the inventories |
| P7.2 | G4b | ProKnow structure-set template CSV and renaming-rules CSV. Confirm first: download a template CSV from a ProKnow sandbox to learn the real columns (documentation is gated, claim O2); build only then; confirm by upload and re-download | D18 | TPS, CI | Round trip equal |
| P7.3 | G4b | RayStation script module (`CreateRoi`, `OrganData.OrganType`); confirm in P5.4's environment | D18 | CI, TPS | ROIs created with names, colours, types |
| P7.4 | G4b | Pinnacle `.Script` (named colours, character blacklist, idempotent); confirm with a site that runs Pinnacle or leave unbuilt | D18 | CI, TPS | As stated |
| P7.5 | G4b | dcmqi/3D Slicer terminology JSON (validated by the dcmqi validator) and a TotalSegmentator-style CSV | D18 | CI | Validator passes |
| P7.6 | G4b | Cross-validate names, FMA ids, types and colours against wrssc `TG-263.xml`, fetched at test time, never bundled | D21 | Local | Difference report |
| P7.7 | G4b | `docs/VENDOR_OUTREACH.md`: formats with no public specification and gated documents (WS4 item 7); record that the supplement the TG-263 report cites is Supplement 196 "Segmentation Creation Template", a 2016 draft never finalised (claim S11), and ask WG-07 whether it is dormant; record DICOM SEG and Supplement 147 as not invested in | D18 | Local | Owner and status per vendor |

Exit: each built exporter confirmed through its vendor path; `docs/VENDOR_OUTREACH.md` has an owner and
status per vendor. Goldens: new exporter goldens only, from confirmed vendor files.

## 4. Critical path and schedule

```
Week 0   answers to D1, D7, D15; P0.1 token revocation; request TPS access and an Eclipse export (D15, D16)
Path A   P0.2 -> P1.1 -> P1.5 -> P1.6 -> P2.2 -> P4.1..P4.10 -> P5.2 -> P5.3 -> P6.3 -> P6.4 -> P6.5
Path B   P0.1 + P0.5 -> P2.6 -> P2.7 -> P3.1 -> P3.2 -> P3.3 -> P3.4 -> P3.5 -> P3.6 -> P3.7 (needs P2.2)
Off-path P0.7, P1.8, P2.5, P4.11-P4.13, P5.5-P5.7, P6.6-P6.7, P7
```

| Phase | Effort (engineer-days) | Can start when |
| --- | --- | --- |
| P0 | 2-4 | now |
| P1 | 7-11 | P0.2 for P1.5 |
| P2 | 9-14 | P1.1; P2.1 needs D1 only for its second half |
| P3 | 6-10 | P0.1, P0.5, P2.6-P2.8 |
| P4 | 10-16 | P2.2; each step needs its decision |
| P5 | 6-12 plus site time | P4.10, D15 |
| P6 | 8-14 | P6.1-P6.3 any time after D16; P6.4 after P6.3 |
| P7 | 10-20 | D18 |
| Total | 58-101 | about 12-20 weeks for one engineer, 8-12 weeks for two (A and B in parallel) |

The longest external lead time is TPS access and the fresh Eclipse export; both are requested in week 0
so that P5.3 and P6.3 are not waiting when P4 finishes.

## 5. Decision dependencies

Steps tagged with an unanswered decision are not scheduled (section 2). This table says which steps each
decision gates and what can proceed meanwhile.

| Decision | Gates | Proceeds meanwhile |
| --- | --- | --- |
| D1 | P2.1 second half, P3.5 (R2), P4.3 | P2.1 guard (valid under both answers), P1 |
| D1b | P4.2 | P1.4 runs the invariants as warnings |
| D2 | P4.1 | P2.2 |
| D3 | P3.5 (R1, R7), P7.1 | P3.1-P3.4 with the default naming choice |
| D4 | P4.4, P6.4 | P2.4 reports unknown types |
| D5 | P4.8, P4.9 | Everything else in P4 |
| D6 | P3.5 (R6), P7.1 | - |
| D7 | P0.5, P1.9, P2.8, P3 entirely | P1, P2.1-P2.7 |
| D7b | P2.10 | - |
| D8 | P3.5, P3.6 acceptance | P3.1-P3.4 |
| D9, D9b | P3.7, P3.8 | Leg (a) |
| D10 | P3.9 code changes | P3.9 manual pass |
| D11 | P1.1 tool placement | Characterisation test of P1.1 |
| D12 | P4.5, P0.6 wording | - |
| D13, D14 | P4.6, P4.7 | P4.1-P4.5 |
| D15 | P0.2 identity convention, P5 entirely | P5.1, P5.2 |
| D16, D17 | P6.1-P6.4 | P6.7 |
| D18 | P6.6, P7 | P6.1-P6.5 |
| D19 | P6.7 deletions | Rest of P6.7 |
| D20 | P4.2 "(no code)" | - |
| D21 | P0.1, P0.3, P0.4, P1.4 (pull request), P1.8, P6.1 | P1.1-P1.3, P1.5-P1.7 |
| D22 | P4.12 | - |

## 6. Risks

| Risk | Effect | Mitigation |
| --- | --- | --- |
| D1 answered (b) | Code items stay non-conformant (meaning = TG-263 name) | P2.1 guard still prevents drift; P4.11 records the non-conformance |
| Workspace on the Free plan | One evaluation cycle (about 400 calls) per month | Observation run first; repeat runs against the duplicate (about 40 calls) |
| No TPS access | G3 rests on validators and Orthanc only | P5.5 statuses and the intended-use statement say so |
| Eclipse rejects Version 1.2, code-less `<Identification>` or SCT | Writer defaults change | P6.3 before P6.4; 1.1 selectable |
| SNOMED meaning licence | D1 (a) meanings limited to PS3.16 | Curate meanings in `CommonName` of our base; claim S16 |
| Live error bodies differ from fakes | Silent misclassification (for example 404 for a wrong table read as a deleted record) | P3.2 records real bodies before bulk runs |
| Source table changes during evaluation | Diffs mix two states | Same-day raw export (P1.9) for every diff |

## Appendix A. Hygiene backlog (not gating)

- README: correct the release-download claims (`README.md:5, 85, 125-127, 132-133, 159-164`) and the
  macOS claim (`:134-135`; the CI matrix is windows-latest and ubuntu-latest only, `ci.yml:23`).
- Branch protection on `main`; dependabot; `SECURITY.md`; one SDK selection rule for both workflows.
- `docs/MODERNIZATION_PLAN.md`: header still says "Phase 0 (audit) complete"; Phase 1 text mentions
  .NET Framework 4.8 and netstandard2.0.
- Git history still holds the leaked tokens and the sample CT's original identity; rewriting history
  is a maintainer decision outside this plan.
- `MakeTemplateWindow.xaml.cs:626-629` shows all write warnings while `MainWindow.xaml.cs:1241` caps at 30
  (unified in P3.9 if D10 = rec).
- Two `FakeHttpHandler` copies (TemplateSync.Tests, Presentation.Tests); Presentation and Core tests both
  walk up to the `.sln` to find data (`Fakes/Templates.cs:33-44`, `TemplateLegacyMigrationTests.cs:246`).
- `CodeMeaning` setter and JSON load skip `RemoveIllegalCharacters` while constructors apply it
  (`OntologyCodeClass.cs:384-411`): one rule for all paths, decided with D1.
- `Test_Base_key.txt`, `Test_Table_key.txt`, `ontology.pkl`, `Paper/`, root Python scripts,
  `AirTableRecords/`: per D21.
- Modernization-plan deferred items kept deferred under D20.

## Appendix B. Trace from the prompt to plan steps

| Prompt item | Steps |
| --- | --- |
| WS1 items 1-10 | P1.1; P1.2; P1.3, P1.4; P2.1; P2.2, P4.1; P4.2; P1.6, P4.10; P1.8; P1.5, P4.9; P4.13 |
| WS2 items 1-12 | P3.3, P3.7; P3.1; P3.1; P1.9; P2.7; P2.6, P3.2; P3.4; P3.6; P3.3 and protocol 2.5; P2.8; P2.9; P3.9 |
| WS3 items 1-9 | P1.7, P2.3; P2.4; P2.5; P4.1-P4.8; P5.2-P5.5; P5.1; P4.12; P5.6; P4.11 |
| WS4 items 1-8 | P6.4; P6.2, P6.3; P6.1, P6.5; P4.3, P7.1; P6.6, P7.2-P7.6; P7.1; P7.7; P6.7 |
| WS5 | P0.1, P0.4, P1.4, P0.7; D20, D21 in the decisions log; Appendix A |
| Section 4 constraints | P0.2 (identity), P0.3 and P5.6 (de-identification), P0.5, P2.7, P3.10 (Airtable safety), P0.1 (tokens), P3.2 (observation first), P4.10 and P6.5 (goldens), P0.6 and P5.7 (intended use), column "Auto" (automation level) |
