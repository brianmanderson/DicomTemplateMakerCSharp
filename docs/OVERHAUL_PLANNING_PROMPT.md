# Plan the overhaul and evaluation of DicomTemplateMakerCSharp

You are the planning lead for the next major phase of this repository. Produce a complete, grounded
plan (not the implementation) for overhauling and evaluating the system, and surface every decision
that only the maintainers can make.

## 0. The request, scope and how to work

The maintainers asked, verbatim:

> I want for us to plan out the complete overhaul and evaluation of our new system. This should
> include steps of downloading the provided template as DICOM, and then re-uploading to a separate
> airtable repo that we have control over. Likewise testing out the DICOM. Is there a better, more
> robust method of making our Xaml files? Currently support is limited to the format at our own
> institution, but there could be publicly available template options that different vendors ingest.

How the request maps onto this prompt (the decisions column lists the tags used in that workstream;
an item may also carry a tag from another row):

| Goal in the request | Workstream | Decisions tagged |
| --- | --- | --- |
| G1 download the provided template as DICOM | WS1 | D1, D1b, D2, D7b, D11, D21 |
| G2 re-upload to a separate Airtable base we control | WS2 | D3, D7, D8, D9, D9b, D10, D11 |
| G3 test the DICOM | WS3 | D2, D4, D5, D12, D13, D14, D15, D21, D22 |
| G4a a more robust way of making the "Xaml" (Varian Eclipse XML) files | WS4 items 1-3, 8 | D4, D16, D17, D18, D19, D21 |
| G4b publicly available template formats other vendors ingest | WS4 items 4-7 | D1, D2, D3, D6, D18, D21 |
| not requested; gating items only | WS5 | D20, D21 |

Priority: G1-G3 before G4; within each goal, steps that produce evidence before refactors.

How to work:
- Investigate read-only (plan mode or equivalent): run read-only commands, the test suite and
  read-only Airtable calls. Do not change code, golden files, Airtable data or GitHub settings. The
  only files you create or edit are the four Markdown deliverables in section 5; leave plan mode to
  write them, and do not commit.
- Order of work: first verify, at most an hour: build, run `dotnet test --solution DicomTemplateMaker.sln`,
  measure `TemplateSnapshots/TG263.json`, read the schema of the source base with the Airtable
  connector (read tools only; section 4 says which tools are off limits). If the connector or a token
  is unavailable, use Appendix C as given and mark every Airtable fact "taken on trust". Then write the
  decisions log, then the plan, then the evaluation protocol, then the claims list (section 5).
- Confidence: Appendix A cites file:line at commit `e7f0572` (main, 2026-10-15). Appendix B labels
  each web claim High, Medium or unknown; an unlabelled Appendix B bullet counts as Medium. Every plan
  step that depends on a Medium or unknown claim, or on any statement about a vendor's import behaviour
  that comes from documentation rather than an observed import, gets a "confirm by" step (document,
  export or import test) before code depends on it. The following Appendix A items are inferences from
  reading code, not observed behaviour: additive-only writes (A.3), BOM in the XML output (A.5),
  `interpret_RT` throwing on vendor RTs (A.4).
- Every "Plan for" item below that depends on a section-3 decision is the recommended branch, not a
  mandate. It is tagged with its decision number(s); write such steps as "if Dn = recommended: ...,
  otherwise: ..." and never schedule them unconditionally.
- Done means: the four files exist and nothing else changed; every plan step names its goal, its
  decision tags and its automation level (CI any-OS / local any-OS / local Windows / manual TPS);
  every vendor or standards claim the plan relies on is in the claims list; every appendix item you
  used is marked re-checked or taken on trust.

## 1. What the system does today

Templates (ROI names, colours, DICOM interpreted types, FMA/SNOMED codes) are stored as folders
(`All_ROIs.json` + shared `Ontologies/All_Ontologies.json`). Four conversions exist: Airtable table to
template folder (and back), Varian Eclipse StructureTemplate XML to template folder (and back),
template folder to DICOM RT Structure Set, and an existing RT Structure Set to template folder.

```
Airtable table ◄──► TemplateSync (cache, snapshot, WritePlanner) ◄──► template folder
                                                                          │
Varian Eclipse StructureTemplate XML ◄──► DicomTemplateCore ──────────────┤
                                                                          ▼
                                  DICOM RTSTRUCT with empty ROIs (one per planning series)
```

Key projects: `DicomTemplateMakerGUI` (WPF), `DicomTemplateMaker.Presentation` (view models, tested),
`DicomTemplateCore` (RT generation, Varian XML, Airtable mapping, tested), `ROIOntologyClass` (data
model), `TemplateSync` (Airtable client, tested only against a fake HTTP handler), three CLIs
(`DicomTemplateMakerCSharp`, `Xaml_Maker/XamlMakerCsharp`, `CleaningRTStructureCsharp`),
`TemplateSnapshotTool` + `.github/workflows/refresh-template-snapshot.yml` (publishes the shared TG-263
table to `TemplateSnapshots/TG263.json`). `README.md` is accurate for CLI, settings and Airtable-budget
behaviour; its release-download and macOS claims are not (A.6). `docs/MODERNIZATION_PLAN.md` records
the previous audit (phases 0-3 are done although its header still says "Phase 0 complete"; its
sections 3 and 7 list deferred items and open questions that are still open).

Build and test with `dotnet build DicomTemplateMaker.sln`, `dotnet test --solution DicomTemplateMaker.sln`,
`dotnet format DicomTemplateMaker.sln --verify-no-changes`. Warnings are errors. 630 test cases pass;
none touch the network, a real TPS, or a DICOM validator.

## 2. Workstreams

For each workstream the plan must give phases, concrete steps with file-level targets, acceptance
criteria, automation level, and the decisions it depends on.

### WS1: Download the shared template and turn it into DICOM (G1)

Goal: a scripted, repeatable path from the published snapshot (`TemplateSnapshots/TG263.json`,
source base `appYl68lenTOFqTDh`) to template folders to RT Structure Sets on the bundled sample CT,
with a committed, normalised inventory of what the DICOM contains.

Plan for:
1. A headless harness (console project or xunit "evaluation" tests) [D11]. Building sites from a
   source today exists only inside `OnlineTemplatesViewModel` (private build logic;
   `TemplateSourceCatalog`'s public constructor is pinned to `%LOCALAPPDATA%`). Decide between a
   supported CLI (`build-from-snapshot`, `write-to-airtable`) and an internal test host over
   `SnapshotStore` → `AirTableEntry.FromRecord` → `TemplateIndex.Build` →
   `TemplateEntryMapper.ToRoiWrapper` → `TemplateMaker.make_template` →
   `DicomTemplateRunner`/`TestRtGenerator`. The harness must write every per-site build warning
   (dropped ROIs, red fallback colours, nameless records, unusable site names) and the
   `RunReport` of RT generation to JSON: the GUI shows them once, capped at 30 lines, and does not
   log them.
2. Reproducibility: the harness reads `TemplateSnapshots/TG263.json` at a recorded commit SHA (the GUI
   has no override for the snapshot file or the data directory; `sharedSnapshotUrl` is https-only), and
   every manual GUI run starts from a documented clean state (delete `%LOCALAPPDATA%\DicomTemplateMaker`,
   unset `AIRTABLE_PAT`, reset `templateRoot` in `ui-settings.json`).
3. Two tests: (a) a frozen copy of the 2026-09-30 snapshot under test data with the A.1 facts pinned
   exactly; (b) checks on the live `TG263.json` that also run inside the snapshot workflow. Only the
   record count (at least 240) and parse checks (every record has `Structure`, `SchemeCode`, `Scheme`;
   every `Colors_RGB` parses) gate publication; the invariants that A.1 shows failing today (every
   site name a usable folder name; no duplicate `Structure`; every `Type` a DICOM defined term; every
   `SchemeCode` syntactically valid for its scheme) run as warnings until D1b is resolved and the
   source is cleaned [D1b].
4. A guard against the code-meaning drift in `TemplateMaker.categorize_folder` (A.2 item 1) before any
   snapshot-built template is opened and saved in the app [D1]. Under D1 option (a) the change is in
   `TemplateEntryMapper` and the snapshot data; under option (b) the change is in
   `categorize_folder`/`make_template` (match on meaning, value and scheme). Plan both branches; until
   D1 is answered, the harness must not open-and-save templates through the app.
5. The code-item policy per scheme [D2], and this sequence constraint: make `interpret_RT` tolerant
   (TryGetString for the nine code attributes, missing code sequences, ROIs without observations) and
   add a generate-then-read-back equality test before dropping the Varian context attributes from SCT
   codes, otherwise the program can no longer read its own RTs (`RtStructureBuilder.cs:424-433` writes
   context attributes only when `ContextIdentifier` is set; `TemplateMaker.cs:107-123` throws when one
   is absent).
6. A policy for the seven snapshot records whose `SchemeCode` is not a SNOMED CT identifier ("88" on
   zPTV_High, zPTV_Mid, zPTVopt, Eye_Origin_L/R; "99" on Fid, Hi HU) [D1b]: recommended, omit the
   Type 3 identification code rather than invent one, or derive category and identification from the
   interpreted type via PS3.3 Table C.8.8.8.3.1-1 (for example MARKER → (130666, DCM, "Radiotherapy
   Fiducial")).
7. Two committed attribute inventories of every generated RT (dcmdump/drtdump or pydicom; all tags
   with VR and length, file meta and transfer syntax; volatile values such as SOP/Series Instance UIDs,
   dates and Implementation Class UID replaced by placeholders): (a) a baseline captured at `e7f0572`
   before any change, annotated with the A.4 defects it contains and treated as a characterisation
   record, not as correct; (b) after each phase that changes RT content, a regenerated inventory
   reviewed under the `UPDATE_GOLDEN=1` regeneration rule of section 4, with the diff listed in the plan.
8. A nomenclature check over the snapshot: every `Structure`/`TG_263` name and `FMAID` compared with
   the AAPM 20170815 worksheet (recommended: fetched at test time, not committed [D21]) and parsed by a
   TG-263 grammar validator (UAB-RO TG263-Toolkit, licence to confirm); non-compliant names reported in
   the baseline.
9. Acceptance: ROI order in every generated RT is Recommend ROIs first, then Consider ROIs, each group
   in snapshot `Record` order [D7b]; the RT contains exactly the ROIs the harness reports as built;
   the WS3 validators pass.
10. Snapshot provenance: AAPM edition and worksheet SHA-256 in the JSON header (it already carries base
    and table ids and `generatedAtUtc`), tagged snapshot releases, and an AAPM-page edition check.

### WS2: Re-upload the templates to an Airtable base we control (G2)

Goal: exercise the write path end to end against a throwaway base, read the result back, and prove
field by field what survives. The write path has never run against live Airtable since the Phase 1
rewrite, and the implementation it replaced had been failing silently for months.

Plan for:
1. Both readings of "re-upload", as separate legs [D9b]: (a) snapshot → template folders → own base
   (today's write path); (b) generated RT → template via `RtStructureSetFile.TryInterpret` /
   `TemplateMaker.interpret_RT` → own base, so that the DICOM produced in WS1 is what gets uploaded.
   Leg (b) is blocked for vendor RTs today, and for this program's own RTs as soon as WS1 item 5 drops
   the context attributes (A.4); the only RT→Airtable uploader in the repository is the legacy
   `WriteToAirTable.py` with a different schema, which must be retired or ported.
2. Two fresh targets, both in a workspace owned by us (plan per D7) that does not host the public base
   [D7]: (a) an Airtable "Duplicate base" of the source with records (we are collaborators, so no
   share link is needed and it costs zero API calls; confirm whether automations, select-option colours
   and `Record` values carry over) to exercise the update/merge path against the real select options;
   (b) an empty base created with the Metadata API (`POST /v0/meta/bases` needs a `workspaceId` and a
   PAT with `schema.bases:write`; `autoNumber` and select choices with colours can be declared at
   creation) to exercise creates and `typecast` option creation. Never reuse an existing base from
   Appendix C.
3. The target schema (A.3): `Structure`, `Template_Recommend`, `Template_Consider` are mandatory for
   writes; `Colors_RGB` and `Template_*` must be multiple selects; in the source `Structure`, `Type` and
   `TG_263*` are single selects (267 `Structure` options for 246 records; `Type` also offers `None`,
   `Control` and `CONTROL`). Pre-declare the 53 site options and the named colour options with colours:
   `typecast`-created options get Airtable-assigned colours and the update-field endpoint is not
   documented to add choices. Add an autonumber `Record` column for deterministic exports.
4. A backup of the source before anything else: Airtable's own Duplicate base, or a raw unfiltered
   `GET /v0/{base}/{table}` of every field saved verbatim. `TemplateSnapshotTool` keeps only the 26
   mapped columns (it drops `FMAID`, `Id`, the reversed-language columns and the base's other two
   tables) and is not a restore point.
5. A headless write driver [D11] that reuses `TemplateSourceItem.WriteTemplatesAsync`
   (`DicomTemplateMaker.Presentation/Services/TemplateSourceItem.cs:85-111`, which maps `ROIClass` to
   `LocalRoi` and calls `AirtableTableSource.WriteTemplatesAsync`), constructing
   `AirtableTableSource` + `AirtableHttpClient` + `SnapshotStore` directly as
   `TemplateSnapshotTool/Program.cs:26-36` does for reads. The driver hard-refuses
   `appYl68lenTOFqTDh`/`tblex7IPsmm8hvVEc` as a target. The writes must come from this code path. At
   execution time (not during planning) the Airtable connector may only create the throwaway schema,
   read the target back as an independent check, and clean up.
6. The first live run as an observation run: one small site; every non-2xx body captured by a logging
   `DelegatingHandler` on the `HttpClient` handed to `AirtableHttpClient` (Authorization redacted).
   Provoke each error shape the code keys on and record the real bodies in the `AirtableJson` fakes:
   a target lacking `RGB` (POST/PATCH `UNKNOWN_FIELD_NAME`, regex `TableFetcher.cs:37`); a target
   record deleted between runs (error type for a missing record, `AirtableTableSource.cs:266-272`); a
   second run with the same cache directory within 7 days (the delta `filterByFormula` is only sent
   then; a 422 `INVALID_FILTER_BY_FORMULA` would be swallowed into a cache warning that blocks every
   write); an array sent to a text column in a scratch table (`INVALID_VALUE_FOR_COLUMN`, unhandled).
7. Read-back and diff: `TableExporter.ExportAsync` on the target (its own `--exclude-fields`, `Record`
   as sort field), keyed on `Structure` over the 26 mapped columns, against a fresh unfiltered export
   of the source taken the same day (the committed snapshot is a secondary baseline). The expected
   differences are A.3 R1-R10; the plan says for each whether the overhaul fixes it or the diff
   tolerates it [D3, D8, D9].
8. Idempotency: records are matched by exact `Structure` against the table refreshed at the start of a
   session, so re-running an unchanged site costs one refresh and no writes; created records are merged
   into the cache per batch and the cache is saved even on failure; a lost create response is not
   retried but is picked up by the next refresh. Keep this. Acceptance criterion after fixing A.3 R3
   and R10: a second run of the driver sends zero create or update requests. Consider `performUpsert`
   only after deciding what to do with duplicate `Structure` values (it fails on multiple matches).
9. A quota budget measured from the snapshot with `WritePlanner` semantics: publishing all 53 sites
   through `WriteTemplatesAsync` into an empty base is 191 creates + 987 site-membership updates
   (about 165 calls, plus one failed request per mapped column the target lacks); into a duplicate that
   already holds the 246 records about 40; a record-level copy about 28; read-back 3. State the
   workspace plan (Free: 1,000 calls per workspace per month, one 30-day grace period, then blocked;
   Team: 100,000, then throttled to 2 req/s, which the client would see as rate limiting, not as the
   billing 429 it keys on) and the number of full runs it allows. Metadata calls count. Run the live
   round trip on manual dispatch only and log `WriteResult.ApiCalls` per run.
10. The column-name divergence between the two real TG-263 bases (Appendix C): the public base names
    the reversed-language columns `TG_263RSpanish`/`TG_263RFrench`, has `CommonName`, and lacks
    `MappingResourceUID` and `RGB`; `appzWlVKRp9TrrTUJ` (the base the tests still name) uses the code's
    `TG_263SpanishR`/`TG_263FrenchR`, has `MappingResourceUID`, no `CommonName`, and `Scheme`/`SchemeCode`
    as single selects. Do not rename the `AirTableEntry` properties: plan a per-table column alias (read
    either spelling; write the spelling the table has, learned from `schema.bases:read` or from the
    existing 422 probe) and put the canonical spelling to us [D7].
11. Snapshot export robust to schema drift: the hand-maintained `--exclude-fields` list was derived on
    2026-09-30; the source has since gained `ContextGroupVersion`, `MappingResource`,
    `ContextIdentifier`, `MappingResourceName` and `ContextUID` (still excluded) but every cell in them,
    and in `CommonName` (exported, never excluded), is empty, so re-deriving the list changes nothing
    today; the Varian context values in the snapshot-built templates come from `AirTableEntry`
    defaults applied to absent cells (A.1). Make the export fetch all columns (`Normalize` already
    keeps only mapped ones) or derive the list from `schema.bases:read`.
12. One manual Windows pass through the WPF flow (Add Airtable table with a pasted URL, Test
    connection, Save; the `Airtable write?` tick and Write to Airtable; MakeTemplateWindow write, which
    saves the template first, so run it only after the WS1 item 4 guard is in place or on a
    non-snapshot template) recording the exact confirmation and result texts and the app log under
    `%LOCALAPPDATA%\DicomTemplateMaker\logs`; note the 30-line warning cap (`MainWindow.xaml.cs:1241`),
    the tick reset after each write and the non-resumable partial failure; plan the connection-test
    and dry-run changes from there [D10].

### WS3: Test the DICOM (G3)

Goal: validator evidence, a TPS-less transfer and read-back smoke test, and a treatment-planning-system
acceptance matrix.

Plan for:
1. A CI validation stage over every RT the tests generate: fo-dicom `DicomDataset.Validate()`;
   `dciodvfy -new` (`pip install dicom3tools`; `-new` only selects the parsable message format; all
   output is on stderr; gate on parsed `Error -` lines and confirm the exit code empirically; record the
   dicom3tools version); `validate_iods --edition 2025b` (`pip install dicom-validator`; pinned to 2025b
   deliberately because it is the last edition in which the three ROI sequences are Type 1 and an RT
   with zero ROIs fails; document that reason next to the pin).
2. A project-specific "TPS-realism" checker for what vendors require beyond the standard: at least one
   ROI; identical ROI-number sets across Structure Set ROI / ROI Contour / RT ROI Observations; Contour
   Sequence absent (not empty) for template ROIs; exactly one referenced study and series with every CT
   SOP instance listed; interpreted type from the 25 PS3.3 C.8.8.8 defined terms (not
   `InterpretedTypes.cs`, which lacks OAR, BRACHY_CHNL_SHLD and DEVICE); the name and label rules of
   D14 (ROI names unique ignoring case and trailing spaces and at most 16 characters per TG-263;
   `StructureSetLabel` collisions across templates) [D14]; code-value syntax per scheme (SCT: 6-18
   digits, valid partition, Verhoeff check digit; FMA: integer; `CodeValue` over 16 characters must move
   to `LongCodeValue`); Position Reference Indicator present whenever the Frame of Reference module is
   written; the IHE-RO BRTO-II "RT Structure Set for Basic Interoperability" content module (fetch TF
   Vol 3).
3. An Orthanc service container in CI (`jodogne/orthanc`; C-STORE via `python -m pynetdicom storescu`)
   as a smoke test for association negotiation and an independent parse of names, colours, types and
   codes read back from `/instances/{id}/simplified-tags`; it is not a conformance oracle.
4. The conformance and provenance gaps A.4 C1-C13, each tagged with its decision
   [D2, D4, D5, D12, D13, D14].
5. The TPS acceptance matrix [D15], with a manual protocol per system: import CT and RT through the
   same path with UID preservation (no de-identification gateway); record TPS version and
   conformance-statement revision; read back programmatically where possible (Eclipse: an ESAPI
   research-mode script listing `Structure.Id`, `IsEmpty`, `DicomType`, `Color`, `StructureCode`;
   RayStation: `ImportDicomDataFromPath` then `ScriptableDicomExport` and a fo-dicom diff; ProKnow: SDK
   download); re-export the structure set and diff names, colours, types, codes and ROI count against
   the input (the Gooding 2026 method); and the import-then-"save as structure template" experiment in
   RayStation with a script-recorder capture to learn whether a template-creation API exists. Fixture
   families per system: an all-ORGAN template; a targets template with `^` and `/` names; a name over
   16 characters; the legacy `AbdPelv_Prostate_Intact`/`_Postop` label-prefix pair; SCT-, FMA- and
   99VMS_STRUCTCODE-coded ROIs; a `Control`/`BODY`-typed ROI; an `Include=false` ROI; empty ROIs of
   every snapshot type including EXTERNAL, MARKER, BRACHY_SRC_APP, IRRAD_VOLUME and, for RayStation,
   contour-less BOLUS/FIXATION/SUPPORT (FSN #167168); case-only duplicate names; a non-ASCII patient
   name. Pass criterion per cell: loaded; name unchanged; colour unchanged; type mapped to what; code
   visible. Eclipse is the first target because it is the only system with a native template file the
   project writes; the matrix must contain a row for every system in B.1 and B.2 with status
   "available / vendor contact / no access", not only for those currently reachable (the maintainers'
   public RayStation scripting repositories suggest RayStation access).
6. A realistic, de-identified planning CT (50 or more slices) for the matrix in addition to the 4-slice
   `SmallCT`, and a fictitious test-patient identity (section 4).
7. Runner edge cases with tests or a documented limitation: a series split across sub-directories (one
   RT per directory today), Enhanced multi-frame CT/MR, images declaring several ISO 2022 character
   sets, Secondary Capture multi-frame; a harden-or-retire decision for `CleaningRTStructureCsharp` [D22].
8. A fixture set for the RT→template reader: a bare-SCT RT from this program, a de-identified copy of
   `DicomTemplateMakerGUI/Test.dcm`, and one RayStation or MIM export [D21].
9. A short conformance description of the generated RT (modules; which attributes come from images,
   template, ROI or constants; UID, character-set, transfer-syntax and empty-contour policy; the Frame
   of Reference module written deliberately) kept next to the inventories [D13, D14].

### WS4: Make the Varian XML path robust, and evaluate vendor-neutral formats (G4a, G4b)

Goal: recommend, with evidence, one of the three writer strategies in D18: fix the writer in place
(items 1-3), rebuild it as one exporter behind a canonical model (item 6), or keep it and add other
carriers (items 4-5). The items describe what each option needs so the maintainers can choose [D18].

Plan for:
1. Fixing the writer against real Eclipse exports (A.5, B.1) [D16, D17]: Version 1.2 by default
   (Version on root and `Preview`, `TreatmentSite`) with 1.1 selectable for older releases (the AAPM
   files are reported as 1.1, unverified); per-scheme `CodeSchemeVersion` (FMA 3.2 and 99VMS_STRUCTCODE
   1.0 are confirmed in two institutions' exports, ours and rtpros; RADLEX 3.8 only in our 2022 file;
   SNOMED has no public evidence); an explicit DICOM↔Varian `VolumeType` map (ORGAN↔Organ,
   EXTERNAL↔BODY, CONTROL↔Control, decisions for AVOIDANCE, MARKER, CAVITY, None) [D4]; the
   `<Identification>` element that is dropped when `CodeValue` is empty, using the real shape (always
   write `<Identification><VolumeID/><VolumeCode/><VolumeType>..</VolumeType><VolumeCodeTable/>` and
   add `<StructureCode>` only when a code exists; StrukturMall's 2025 Version 1.2 templates carry no
   `StructureCode` at all); tolerant `ColorAndStyle` parsing (named colours, `RGB255192203`,
   `RGB  0255191`, `RGB 0 0 255`, style-prefixed names truncated near 16 characters) with reported, not
   silent, fallbacks; an XSD or schema check.
2. SNOMED for Eclipse [D17]: the only SNOMED dictionary documented in ESAPI 15.6-17.0 is `SRT`
   ("SNOMED RT structure scheme": alphanumeric SNOMED-RT identifiers, deprecated in DICOM PS3.16); the
   snapshot holds numeric SNOMED CT concept IDs. An Eclipse-bound export therefore needs a value
   crosswalk (PS3.16 Annex O covers only DICOM-used concepts; otherwise a maintained SCT→SRT table or
   a stored second code per ROI) or conversion to FMA, never a designator swap alone. Codes selected in
   the Eclipse UI or through ESAPI must exist in the site's RT Administration structure-code
   dictionary: plan a dictionary-conformance step that exports the active dictionaries with an ESAPI
   script (`Application.StructureCodes.Fma/RadLex/Srt/VmsStructCode`, keys and versions) and checks
   every TG-263 code against them before any Eclipse import test. Whether the XML importer validates
   codes, and whether 18.x adds `SCT`, is unknown: confirm by importing a one-structure SCT template
   and a one-structure SRT template into our Eclipse.
3. A golden corpus beyond our own two files: AAPM `EclipseStructureTemplates.zip` (contents reported by
   one research pass as five Version 1.1 files, March 2018, 30-51 structures each; unverified; AAPM
   states no licence), StrukturMall (MIT, Version 1.2, 2025), rtpros (Apache-2.0, Protocol 1.12), plus
   fresh exports from our Eclipse. Recommended for the unlicensed files: fetch them at test time into an
   isolated folder behind a `TG263_CORPUS_DIR` variable with the maintainers' go-ahead and do not
   commit them [D21]. The existing goldens are characterisation files that pin known-wrong behaviour;
   schedule a reviewed `UPDATE_GOLDEN=1` regeneration and say which goldens change.
4. DICOM RTSTRUCT with empty ROIs as the universal carrier, hardened to the current standard [D1, D2]
   (Segmented Property Category from CID 9580, identification code from the CID mapped for that
   category, laterality via Segmented Property Type Modifier, therapeutic role codes per Table
   C.8.8.8.3.1-2, `SCT` with numeric IDs, FMA with its registered designator, a top-level Coding Scheme
   Identification Sequence as Eclipse writes). The survey in B.1 and B.2 found exactly two
   vendor-published structure-template file formats, Varian StructureTemplate XML and ProKnow's
   structure-set template CSV, plus script-based paths (RayStation Python, Pinnacle `.Script`, ESAPI)
   and community interchange files (wrssc `TG-263.xml`, dcmqi JSON, TotalSegmentator CSV). Everything
   else (RayStation GUI templates, Brainlab, MIM, Monaco, Accuray, the AI-contouring vendors) ingests
   RTSTRUCT only or has no public specification.
5. Cheap additional exporters where a documented ingestion path exists [D18]: ProKnow structure-set
   template CSV (`Name,Type,R,G,B`) and a ProKnow Renaming-Rules synonym CSV from the alias columns; a
   RayStation Python module (`case.PatientModel.CreateRoi(Name, Color, Type)` + `OrganData.OrganType`);
   an idempotent Pinnacle `.Script` with the named-colour map and character blacklist; a dcmqi/3D
   Slicer terminology JSON validated with the dcmqi validator; a flat CSV using the TotalSegmentator
   attribute-path convention; an ESAPI "apply template" script (`AddStructure(StructureCodeInfo)`,
   StrukturMall pattern) as the robust Eclipse alternative to XML import. Cross-validate against the
   wrssc `TG-263.xml` (GPLv3; recommended: compare, do not bundle [D21]).
6. A versioned, vendor-neutral canonical JSON schema for templates [D1, D3, D6]: stable ROI id;
   canonical TG-263 name plus language and laterality variants; anatomy code and therapeutic role
   separately (D1), with several codes per concept (FMA, SCT, SRT where mapped, 99VMS, RADLEX) under a
   (scheme, value) identity rule replacing the editor's value-only uniqueness; template metadata;
   vendor extension blocks for Eclipse DVH/TypeIndex fields; per-vendor mapping tables (interpreted-type
   vocabulary, colour representation, name length and character limits); TG-263 writer rules as tests
   (16 characters or fewer, case-insensitive uniqueness, `z`/`_` prefix for non-evaluation structures);
   and a migration from `All_ROIs.json` and the legacy text folders.
7. What not to invest in now (DICOM SEG, Supplement 147 RT Segment Annotation: no TPS accepts them as
   templates) and a vendor-outreach list for formats with no public specification: Radformation
   `.structures`, MVision ConfigUI, MIM contour label templates, Limbus templates, Siemens Organ
   Database, Manteia, Mirada, Therapanacea, Carina, Varian Ethos; and the TPS vendors whose documents are
   gated: Elekta (Monaco 6.x conformance statement, any structure-template feature, scripting licence),
   MIM (conformance statement, template import), Accuray (Precision/iDMS conformance), Philips
   (Pinnacle Evolution ROI templates). Confirm-by step: look up the DICOM supplement the TG-263 report
   cites as a planned vendor-neutral template format (recorded as Supplement 196; unverified) on the
   DICOM supplements index.
8. Housekeeping that belongs to G4a: rename `Xaml_Maker` (nothing in it is XAML); route
   `XamlMakerCsharp` through `VarianXmlBatch` so the CLI gets `NameRules`, atomic overwrite and the
   path-traversal guard; delete unused `AddROI(string)`/`ReturnUppercaseWord`; move the Core classes out
   of the `DicomTemplateMakerGUI.Services` namespace; drop or relocate the unused shipped
   `DicomTemplateMakerGUI/Structure Template.xml`; decide the fate of the dead Python/notebook Clinical
   Protocol pipeline and whether Clinical Protocol XML is in scope [D19] (if yes, the skeleton must be a
   protocol exported from our Eclipse, Protocol 1.10-1.12, not the 2019 Version 1.9 sample).

### WS5: Gating items only (not requested)

Keep in the plan only the items that gate WS1-WS4: confirming revocation of the four published tokens
and replacing the legacy release assets before the first live Airtable run; a LICENSE before any golden
corpus or snapshot release is published; the publication gate on the snapshot workflow (WS1 item 3)
before WS1 relies on it; rehearsing the never-run release job on a `v1.1.0-rc` tag, installing the zip
on a clean Windows VM and confirming it runs offline with `template_RS.dcm`, `SmallCT`,
`FMA_SNOMEDCT_Key.txt` and the bundled snapshot (today a missing snapshot only warns, `ci.yml:76-78`).
Everything else from A.6 goes into a "hygiene backlog" appendix of the plan. The modernization plan's
deferred items are decided in D20 (keep/do/drop in the decisions log, not implemented here) [D20, D21].

## 3. Decisions you must put to us

Do not wait for answers. Write the decisions log with your recommendation and rationale, plan on the
recommended answer, and mark the steps that change under the alternative. If the session is
interactive you may ask at most the three decisions that block the first implementation phase (the
start of WS1 and WS2): D1, D7, D15. Everything else goes to the log.

Data model and codes
- D1. Ontology identity and code meaning. Option (a): identity = (scheme, value), the meaning taken
  from the concept ("Planning target volume", "Right eyeball"), the TG-263 name carried only in ROI
  Name. Option (b): keep the TG-263 convention (meaning = TG-263 name, one SCT code shared by dozens of
  target names) and match ontologies on (meaning, value, scheme) everywhere. Recommendation (a), because
  PS3.3 section 8.3 requires Code Meaning to be a meaning, synonym or translation of the coded term and
  never a key, so (b) produces non-conformant code items (228793007 / "PTV_High"), and today's
  matching by meaning (`ROIClass.cs:597-626`) uses the meaning as a key. Under (a) also decide, for
  targets, whether the target code is written as the identification code (CID 9534) under Segmented
  Property Category (130041, DCM, "RT Target", from CID 9580), or as Therapeutic Role Type with the
  anatomy as the identification code (PS3.17 worked example), which means the canonical JSON needs
  anatomy code plus role, not one code per ROI. Should `FMAID` (populated in the source base but
  dropped by the 26-column mapping) become a second code per ROI, seeded from the source column rather
  than from `FMA_SNOMEDCT_Key.txt`?
- D1b. Source data defects (names with `/`, `^`, spaces, U+02C6; duplicate `_TS_CTVsb`; `Type` values
  outside the DICOM terms such as `Control`/`None`): clean in the Airtable table before the baseline is
  pinned, reject at build time, or pass through and sanitise per vendor? For the seven non-SNOMED codes
  ("88", "99"): omit the identification code, or derive it from the interpreted type via Table
  C.8.8.8.3.1-1 (WS1 item 6)?
- D2. Context attributes: Context Identifier is Type 3 and Mapping Resource / Context Group Version are
  required only with it, so the conformant default is to omit all six for SCT and FMA codes that were
  not drawn from a named context group, keep `VMS011`/`99VMS` only for codes that came from a Varian
  dictionary (99VMS_STRUCTCODE, Varian-exported FMA; Siemens Organs RT writes VMS011 for every scheme
  it supports, a precedent for FMA/RADLEX), and add a DICOM context identifier (for example CID 9534 for
  a target identification code) only where membership is verified. Scheme identification:
  `CodingSchemeVersion` in the item (FMA 3.2) and a top-level Coding Scheme Identification Sequence
  (FMA 2.16.840.1.113883.6.119, SCT 2.16.840.1.113883.6.96, 99VMS_STRUCTCODE with Varian's UID) as
  Eclipse writes, rather than `CodingSchemeUID` per item. Agree?
- D3. Canonical ROI name: the TG-263 name with language/laterality as stored variants (so a re-upload
  matches the source row), or the renamed display name as today?
- D4. Interpreted-type vocabulary: the 25 DICOM defined terms canonical, with explicit per-vendor maps
  (Eclipse's 14 ESAPI types have no OAR; Monaco's 2017 statement lists EXTERNAL/ORGAN/BOLUS on import
  and SUPPORT on export; Varian XML `BODY`/`Organ`/`None`; the table's `Control`/`None`); should the
  generator refuse, warn or map?
- D5. Should Template_Consider ROIs (`Include=false` once A.2 item 6 is fixed; today every
  JSON-loaded ROI has `Include=false`) be left out of generated RTs?
- D6. Apply `DVH_Type_Index`/`DVH_ContourStyle` on read, or move Varian-only presentation fields to a
  vendor extension block?

Airtable
- D7. Which base is canonical for the shared template (`appYl68lenTOFqTDh` feeds the snapshot;
  `appzWlVKRp9TrrTUJ` is the base the modernization plan and the tests still name) and which column
  spelling (`TG_263RSpanish` vs `TG_263SpanishR`) is canonical? Who owns each, on which plan, and in
  which workspace (and on which plan) should the WS2 write-target base live? Is `appYl68lenTOFqTDh` the
  "online database maintained by TG-263U1 members" cited in Anderson et al. 2024, and may its share
  link enable "Copy base"? Should the WS2 write-target base, if it becomes a lasting team base, get its
  own snapshot workflow (Airtable cannot be read without a token)?
- D7b. Should live tables be read with an explicit sort (`Record` autonumber or `createdTime`) so ROI
  order and duplicate resolution match the snapshot? Today live reads have no sort
  (`AirtableTableSource.cs:329`).
- D8. Should writes become a true synchronisation (remove sites, prune colours, delete orphans) or stay
  additive? Should per-site overrides replace one shared record per structure (last writer wins)?
- D9. Should the re-upload be a record-level copier that preserves all columns (including `FMAID`, `Id`,
  the language columns), or go through template folders as today (lossy, A.3 R1-R10)?
- D9b. Is the re-upload meant to start from the generated DICOM (leg b), from the template folders (leg
  a), or both?
- D10. Should `AIRTABLE_PAT` override only a named connection? Should the connection test check write
  scope and the required columns before the first write, and should a dry-run preview
  (`WritePlan.RequestCount`, creates/updates per site) be shown before the first request?
- D11. Is a supported CLI (`build-from-snapshot`, `write-to-airtable`) a product feature or test-only?

DICOM
- D12. Provenance: what replaces `Manufacturer "UCSD Residency"`, `ManufacturerModelName
  "Universal_RT_Creator"`, `SoftwareVersions "Hello_from_Brian_M_Anderson"`, `SeriesDescription
  "Anonymized"` and `ROIInterpreter "Brian_Mark_Anderson"` (PN, Type 2; recommend empty)? Should
  `SeriesDescription`/`StructureSetName` carry the template name plus a "TG-263 template, empty
  structures, not for clinical use" marker; should `ApprovalStatus UNAPPROVED` be written deliberately;
  should `ROIGenerationAlgorithm` be `MANUAL` (what Eclipse writes for contour-less ROIs)?
- D13. Keep `template_RS.dcm` as a curated minimal seed, or build RTs from scratch (losing the "template
  file unchanged" and CLI "template missing" behaviours)?
- D14. Transfer syntax: Implicit VR Little Endian as the deliberate default for RT objects (MIM is
  reported to negotiate only Implicit VR LE for RT, B.2 Medium, confirm by a MIM association test; both
  Varian and RayStation files in the repo are Implicit VR LE; Explicit VR caps `ContourData` at 65,534
  bytes) with Explicit VR LE as an option for file-only workflows, instead of today's accidental
  inheritance from the seed. Policy for names over 16 characters, duplicate names ignoring case, and
  templates sharing a 16-character label prefix (`StructureSetName` for the full name?).
- D15. Which non-clinical TPS environments can we import into (Varian test/research ARIA-Eclipse
  database, RayStation test database, vendor sandboxes), which versions, who performs the imports, and
  what test-patient identity convention does each site require?

Varian and vendors
- D16. Which Eclipse/ARIA versions must the XML load in; is Version 1.2 the target; can we obtain the
  Varian System Server 18.x conformance statement (MyVarian) and a fresh template export?
- D17. Which structure-code dictionaries are active in our RT Administration (FMA, RADLEX, SRT,
  99VMS_STRUCTCODE) and is the SRT dictionary populated? Given that, should SCT-coded templates be
  exported to Eclipse via an SCT→SRT crosswalk, via FMA (`FMA_SNOMEDCT_Key.txt`: 421 many-to-one rows,
  the converter rewrites every template globally and keeps the Varian context attributes, and
  `OntologyEntryRules` forbids one value in two schemes), or with no `StructureCode`?
- D18. Writer strategy: fix the Varian writer in place, rebuild it as one exporter behind a canonical
  model, or keep it and add other carriers (WS4 goal)? Which vendors are in scope for native exporters
  versus RTSTRUCT only, and who contacts the vendors in WS4 item 7?
- D19. Is Clinical Protocol XML generation in scope?

Repository
- D20. The modernization plan's deferred items, keep/do/drop each: persistent header cache; on-disk
  change detection in editors; "(no code)" ROI choice; rename leaving old RTs; Varian share settings
  window; template matching semantics (keep the two-way substring, or narrow it, knowing "H&N" against
  "H&N Planning CT" would stop matching).
- D21. Keep, archive or delete: `Paper/` (20 MB), root Python scripts, `Xaml_Maker` notebooks,
  `AirTableRecords/`, `Test.dcm`, `Test_*_key.txt`, `ontology.pkl`; which licence; which third-party
  files (AAPM zip and worksheet, wrssc `TG-263.xml`, vendor RT exports) may be committed and under what
  licence (recommended: none of the unlicensed or copyleft ones); snapshot workflow via pull request;
  who owns the Google Sheet behind `FMA_SNOMEDCT_Key.txt` and should regeneration continue?
- D22. Keep and harden `CleaningRTStructureCsharp`, or retire it in favour of "Add ROIs from an RT
  Structure file"?

## 4. Constraints

- Patient identity. `DicomTemplateMakerGUI/SmallCT` carries a real person's name as `PatientName`
  ("Brian Mark Anderson", `PatientID` 0003141592654, study "Visible Human Male") and
  `RtStructureBuilder.cs:26-30` copies PatientName, PatientID and PatientBirthDate into every RT. Never
  import generated RTs or the sample CT into a clinical patient database; use test or research databases
  only. Before any TPS import, re-identify the sample CT (and therefore every RT built on it) with an
  obviously fictitious test patient (for example `PatientName ZZTEST^TG263TEMPLATE`, a site-reserved
  `PatientID` prefix, `PatientIdentityRemoved YES`, `DeidentificationMethod` naming the tool); the plan
  must include the step that fixes the bundled CT or replaces it.
- De-identification of fixtures. No vendor or clinical RTSTRUCT enters the repository or the test
  fixtures without documented PS3.15 Annex E de-identification (consistent UID replacement; institution,
  station, operator and dates removed). `DicomTemplateMakerGUI/Test.dcm` today carries the institution's
  address, the ARIA server `StationName`, an operator name and real ARIA UIDs; `CleaningRTStructureCsharp`
  is not a de-identifier.
- Airtable safety. Never write to the public source base. The evaluation token is a PAT scoped to the
  throwaway base only (`data.records:read`, `data.records:write`, plus `schema.bases:read/write` if the
  base is API-created) with no access to `appYl68lenTOFqTDh`, stored as a separate secret (for example
  `AIRTABLE_EVAL_PAT`) and mapped into `AIRTABLE_PAT` only inside the driver's step; the snapshot job's
  read-only secret is never upgraded. If you set `AIRTABLE_PAT` for `curl` checks, do not launch the GUI
  in the same environment (A.3: it overrides every stored connection's token). During planning, do not
  call the Airtable connector's create, update or delete tools (section 0 and Appendix C refer here).
- Tokens. Nothing token-shaped in the repository (push protection is off and four tokens already leaked
  through history and old release assets).
- Observation first. The error-shape assumptions in A.3 are unverified live; the first live run is an
  observation run with logging, not a bulk write.
- Goldens. Changing Varian XML output or the generated RT requires a reviewed `UPDATE_GOLDEN=1`
  regeneration; the plan says which goldens change and why.
- Intended use. The README has no intended-use statement; the plan must add one (what has and has not
  been verified in which TPS versions) and make the objects self-describing (D12).
- Automation level. Separate Windows-only steps (WPF, DPAPI, Recycle Bin, TPS imports) from steps that
  run on any OS (all tests, the CLIs, validators, Orthanc).

## 5. Deliverables

1. `docs/OVERHAUL_PLAN.md` (300-500 lines): phases in execution order, each with goal, steps
   (file-level), acceptance criteria, automation level, decision tags, risk and an effort range in
   engineer-days; the critical path; a hygiene backlog appendix.
2. `docs/EVALUATION_PROTOCOL.md` (200-300 lines): the exact procedures for the snapshot baseline, the
   Airtable round trip (schema, driver, diff keyed on `Structure`, expected differences by A.3 R-id,
   quota budget, error-shape provocation), the DICOM validation stage, and the TPS acceptance matrix (a
   table to fill in per system with the fixture families and pass criteria of WS3 item 5).
3. `docs/OVERHAUL_DECISIONS.md`: the section-3 questions, your recommendation and rationale, blanks for
   our answers, and the plan steps each answer changes.
4. `docs/OVERHAUL_CLAIMS.md`: every vendor or standards claim the plan relies on, with source URL or
   file:line, confidence, and its "confirm by" step.

---

## Appendix A: Verified findings in the code (file:line at commit e7f0572)

### A.1 The published snapshot (`TemplateSnapshots/TG263.json`, generated 2026-09-30)
- 246 records; 53 sites across `Template_Recommend`/`Template_Consider`; 55 records in neither.
- Every record has `Scheme = "SCT"` and no context values (the workflow excludes the six context
  columns; the five that now exist in the source are empty in every record anyway), so
  `AirTableEntry.FromRecord` back-fills the Varian defaults (`VMS011`, `99VMS`, `20161209`,
  `1.2.246.352.7.2.11`, `1.2.246.352.7.1.1`, "Varian Medical Systems") into every SNOMED code
  (`TemplateSync/Model/AirTableEntry.cs:17-26, 62-68, 91-128`; `refresh-template-snapshot.yml:50-66`).
- The source table has a populated `FMAID` column and an `Id` column that are not among the 26 mapped
  columns; the snapshot, the GUI and `TemplateSnapshotTool` drop them; `CommonName` is exported but
  empty in every record.
- Duplicate `Structure` "_TS_CTVsb" (two records, Red vs Navy Blue, both in no site, so
  `TemplateIndex`'s first-wins rule at `TemplateIndex.cs:75` is not exercised; the pair matters for
  `WritePlanner`, which keys remote records by `Structure`); two names contain U+02C6 instead of `^`;
  names with `/`, `^` and spaces; seven `Structure` names and six site names longer than 16 characters;
  one `Type` "Control"; seven records with codes that are not SNOMED CT identifiers ("88" on
  zPTV_High, zPTV_Mid, zPTVopt, Eye_Origin_L, Eye_Origin_R; "99" on Fid, Hi HU), every other code
  passing SCTID length, partition and Verhoeff checks; code 228793007 shared by 40 PTV-like names,
  228792002 by 41 CTV-like, 228791009 by 14 GTV-like.
- Snapshot sorted by the autonumber `Record`; live tables are read with no sort
  (`AirtableTableSource.cs:329`), so ROI order and duplicate resolution can differ.
- No test loads the committed `TG263.json`; the only publication guard is "refuse if the table shrank
  by half" (`TemplateSnapshotTool/Program.cs:52-56`); the workflow commits straight to unprotected
  `main`; GitHub disables the cron after 60 idle days; the step exits 0 when the secret is unset.

### A.2 Data model defects
1. Code-meaning drift (critical under either answer to D1). `TemplateMaker.categorize_folder`
   re-points every ROI at the first in-memory ontology with the same `CodeValue` regardless of meaning
   or scheme (`DicomTemplateCore/Services/TemplateMaker.cs:486-497`) and `make_template` persists it
   (`:303-319`: by-value dedup at `:303`, library merge at `:315`, `SaveROIsToFolder` at `:319`). With
   the snapshot's shared SCT codes, every PTV in a template would take the code meaning of whichever
   PTV was listed first the next time the template is opened and saved.
2. Varian context group written into generated RTs whenever the ontology carries one
   (`RtStructureBuilder.cs:424-433`), which every SNOMED code from the snapshot does because of the
   defaults (`OntologyCodeClass.cs:297-302`, `AirTableEntry.cs:17-26`), and every Varian-imported
   FMA/RADLEX code does (`VarianXmlReader.cs:227`).
3. Interpreted type is free text end to end: Varian `BODY`/`None` imported verbatim
   (`VarianXmlReader.cs:143`), upper-cased but unchecked at RT time (`RtStructureBuilder.cs:251-254,
   438`); `Editors/InterpretedTypes.cs:16-21` holds 22 terms (no OAR, BRACHY_CHNL_SHLD, DEVICE) and only
   editors use it.
4. Identity rules are inconsistent: ontology dedup by (meaning, value, scheme) on JSON load
   (`ROIClass.cs:613-615`), by (value, scheme) in the library merge (`OntologyCodeClass.cs:84`), by
   value only in `TemplateMaker`/migration/`interpret_RT`; ROI dedup ordinal in migration, Airtable and
   RT import but case-insensitive in the editor (`NameRules.cs:152`).
5. `color_string` is a persisted derivation rebuilt only when empty (`ROIClass.cs:420-421, 600-603`);
   `RGB`, `R_DVH/G_DVH/B_DVH`, `dvh_color_string` are redundant persisted fields that can disagree.
6. `Include` is false for JSON-deserialised ROIs but true in every other constructor
   (`ROIClass.cs:422, 588-591`); the RT generator ignores `Include` entirely.
7. Language/laterality choice renames `ROIName` in place at build time and the original `Structure` is
   not stored (`ROIClass.cs:768-823`; `OnlineTemplatesViewModel.cs:627-653`).
8. `RemoveIllegalCharacters` strips `< > : " / \ | ? *` from every code meaning
   (`OntologyCodeClass.cs:384-411`), so "CTV1^GTV/Cervix/Uterus" gets a meaning that differs from its
   name and from the Airtable row.
9. No JSON Schema, schema version, template metadata or stable ROI id; the canonical model is
   Varian-shaped (`TypeIndex`, `ContourStyle`, `DVHLine*`, 99VMS defaults).

### A.3 Airtable write path (`TemplateSync`)
- Never run live since the rewrite; tests use `TemplateSync.Tests/Fakes/FakeHttpHandler.cs` with
  replies fabricated by `Echo` (`AirtableTableSourceTests.cs:464-473`).
- Required columns and types: `AirtableTableSource.cs:252-264`; arrays sent for `Colors_RGB` and
  `Template_*` (`WritePlanner.cs:126,133,141,177,192`); `typecast:true` always
  (`AirtableTableSource.cs:186, :202` → `AirtableHttpClient.cs:105, :123`); other missing columns cost
  one failed request each, then are dropped with a warning (`AirtableTableSource.cs:372-404`), capped at
  30 distinct lines in the GUI (`MainWindow.xaml.cs:1241`).
- Additive-only (inferred from the absence of any removal branch): iterates local ROIs only
  (`WritePlanner.cs:76-107`), unions `Colors_RGB` (`:189`), never removes a site or deletes a record.
  Scalar columns are one record per `Structure` shared by all 53 sites; last writer wins.
- Pre-write refresh must succeed or nothing is written (`AirtableTableSource.cs:106-110`); a fetch
  failure on the delta path is returned as a cache warning (`:334-336`) which `:106-110` turns into
  "Nothing was written"; the delta `filterByFormula` (`TableFetcher.cs:139-143`) has never been sent live.
- Error-shape assumptions: regex `Unknown field names?:\s*"(?<name>[^"]+)"` (`TableFetcher.cs:37`);
  deleted-record types 404 / `ROW_DOES_NOT_EXIST` / `MODEL_ID_NOT_FOUND` / `NOT_FOUND`
  (`AirtableTableSource.cs:266-272`); billing 429 detected by substring `BILLING`/`monthly`
  (`AirtableHttpClient.cs:317-322`); no request or response body logging anywhere.
- `AIRTABLE_PAT` overrides the token of every stored connection (`AirtableConnectionStore.cs:112-122`).
- The connection test is a read of one record (`TemplateSourceCatalog.cs:156-169`).
- No CLI write entry point; the GUI callers are `MainWindow.xaml.cs:1172-1232` (`WriteToAirTable_Click`,
  call at `:1203`) and `MakeTemplateWindow.xaml.cs:622`, both through
  `TemplateSourceItem.WriteTemplatesAsync` (`TemplateSourceItem.cs:85-111`), which is reusable headlessly.
- Column spelling: the code maps `TG_263SpanishR`/`TG_263FrenchR` (`AirTableEntry.cs:41`); the source
  base names them `TG_263RSpanish`/`TG_263RFrench`; `appzWlVKRp9TrrTUJ` uses the code's spelling
  (Appendix C).

Expected round-trip differences through the template-folder path (`TemplateEntryMapper.cs:107-136`,
`WritePlanner.cs:180-202`):
- R1. `TG_263`, `TG_263R`, `TG_263Spanish/French` and the reversed forms are never written back.
- R2. The target's `CommonName` is written as `RemoveIllegalCharacters(Structure)` via `CodeMeaning`
  while the source column is empty, so every row gains a value and names with `/ ^ < > : " \ | ? *` differ.
- R3. `Colors_RGB` named options come back as `Auto:r,g,b`; every colour change adds another option.
- R4. `RGB` is written unpadded (`6,82,255` vs `006,082,255`); the source has no `RGB` column.
- R5. The six context columns are written with the Varian constants even for SCT codes.
- R6. `DVH_Type_Index` and `DVH_ContourStyle` are written but never read.
- R7. Language/laterality renames at build time break the exact-name match, creating new records.
- R8. Writes are additive-only; removed ROIs and sites persist on the target.
- R9. `FMAID` and `Id` are lost unless the copier is record-level.
- R10. `Colors_RGB` is compared as a sequence with the local colour first, so two sites colouring a
  shared structure differently flip `RGB` and reorder `Colors_RGB` on every alternate write; ordinal
  `Structure` matching makes `Brain` and `brain` two records.

### A.4 RT generation (`DicomTemplateCore/DicomTemplateServices/RtStructureBuilder.cs`)
- Every RT is a clone of `DicomTemplateMakerGUI/template_RS.dcm` (a RayStation anonymised export,
  Implicit VR Little Endian). Inherited and shipped in every RT: `SoftwareVersions
  "Hello_from_Brian_M_Anderson"`, `SeriesDescription "Anonymized"`, `SeriesNumber 1`, `InstanceNumber
  1`, `OperatorsName` empty, `ApprovalStatus UNAPPROVED`, `PositionReferenceIndicator` empty;
  `StructureSetName`, `InstitutionName` and `StationName` are absent. `DeidentificationMethod`,
  `PatientIdentityRemoved` and `StudyDescription` are copied from the images or removed (`:33-36`).
  Transfer syntax follows the clone via `new DicomFile(rt)` (`:240`).
- Frame of Reference module (U in the current RT Structure Set IOD, PS3.3 A.19.3 note 2):
  `FrameOfReferenceUID` is written (`:166`) and the Type 2 Position Reference Indicator is inherited
  from the seed untested.
- Code item: `CodeMeaning`, `CodeValue`, `CodingSchemeDesignator` verbatim; context attributes only when
  `ContextIdentifier` is set; no `CodingSchemeVersion`, no Coding Scheme Identification Sequence, no
  `LongCodeValue` path for values over 16 characters (`:420-433`).
- Validation present: series UIDs and consistency, per-ROI name/type/code parts, SH label. Absent: any
  IOD validator, interpreted-type vocabulary, duplicate ROI names, 16-character ID limits.
- `interpret_RT` reads all nine code attributes with `GetString` and throws when one is absent
  (`TemplateMaker.cs:107-123`; inferred from fo-dicom semantics, not executed), so RTs from other
  vendors and this program's own bare-SCT output cannot seed a template.
- Series split across sub-directories get one RT per directory (`DicomParser.cs:11-14, 27-52`).
- `CleaningRTStructureCsharp` is untested, throws on missing sequences and only empties
  `ContourImageSequence` (patient identity, contours and UIDs stay).
- Test RT generation only on the 4-slice `SmallCT` (Explicit VR LE, real person's name as patient); a
  second run into the same `Template_Output` writes nothing (`TestRtGeneratorTests.cs:42-54`).
- `DicomTemplateMakerGUI/Test.dcm`: a real ARIA RTM 16.1 export (institution name and address,
  `StationName "ro-ariadb-v"`, operator name, real ARIA UIDs), referenced by no code; the only in-repo
  evidence of what Eclipse writes (`ROIGenerationAlgorithm MANUAL` for contour-less ROIs, Implicit VR
  LE, ISO_IR 192, FMA 3.2 with `VMS011` context, top-level Coding Scheme Identification Sequence, no
  ROI Observation Label).

Conformance and provenance gaps:
- C1. Varian context group asserted on SNOMED codes [D2].
- C2. `BODY`/`NONE` interpreted types leak from Varian imports; nothing checks the 25 defined terms [D4].
- C3. `ROIGenerationAlgorithm "SEMIAUTOMATIC"` on contour-less ROIs (`:411`); Eclipse writes `MANUAL` [D12].
- C4. Hard-coded `Manufacturer "UCSD Residency"`, `ManufacturerModelName "Universal_RT_Creator"`,
  `ROIInterpreter "Brian_Mark_Anderson"` (`:21-23, :183-184, :439`) [D12].
- C5. Seed inheritance listed above; `SeriesDescription "Anonymized"` is what a TPS user sees [D12, D13].
- C6. Transfer syntax by accident [D14].
- C7. `StructureSetLabel` = first 16 characters of the template name (`:180-182`); the legacy
  `AirTableRecords/AbdPelv_Prostate_Intact` and `_Postop` folders collide; all 53 snapshot sites truncate
  to distinct labels although six exceed 16 characters; `StructureSetName` never set [D14].
- C8. No `CodingSchemeVersion`, Coding Scheme Identification Sequence or `LongCodeValue` [D2].
- C9. Retired `ROIObservationLabel` still written, truncated to 16 (`:442`).
- C10. `interpret_RT` intolerant of minimal code items (sequence constraint in WS1 item 5).
- C11. `Include=false` ROIs still written [D5].
- C12. Duplicate ROI names not rejected; names over 16 characters only warned about in editors [D14].
- C13. Frame of Reference module inherited rather than written: keep it for single-FoR structure sets
  but write `FrameOfReferenceUID` and Position Reference Indicator deliberately and assert the UID
  equals the one in Referenced Frame of Reference Sequence and every Structure Set ROI item [D13, D14].

### A.5 Varian XML (`DicomTemplateCore/Services/VarianXmlWriter.cs`, `VarianXmlReader.cs`)
- Writer hard-codes root `Version="1.1"` (`:30`), `CodeSchemeVersion="3.2"` for every scheme (`:195,
  :316`), `ApprovalStatus="Unapproved"`, `Description="Auto-generated xml file"`, no `TreatmentSite`,
  no `Preview Version`, home-made unpadded dates (`:28-39`). `Name` attribute = code meaning (`:178`);
  structure `ID` = `ROIName` (`:171`); template `Preview ID` = folder name (`:117`), which on import is
  the Preview ID with spaces turned into underscores (`VarianXmlReader.cs:283`).
- `<Identification>` (and therefore `<VolumeType>`) is omitted when `CodeValue == ""` (`:190-198`); the
  reader then skips the structure on re-import (`VarianXmlReader.cs:142-143`).
- `VolumeType` written verbatim from DICOM terms (`ORGAN`, `EXTERNAL`, `AVOIDANCE`) while real files use
  `Organ`, `BODY`, `Control`, `None` (`:185`); the reader keeps Varian spelling (`:143`).
- Colour: unknown names become red with no report (`VarianXmlReader.cs:159-203`; the goldens enshrine
  red for `Kidney_R` and `x Artifact`); fixed-width RGB only; `ContourStyle` default lowercase `contour`
  and the `KnownColor` enumeration includes system colours (`VarianXmlWriter.cs:137-149`;
  `ROIClass.cs:423`).
- Unknown `DVHLineStyle` exports as an empty element (`:216-235`); `CodeSchemeVersion`, `VolumeCode`,
  `SearchCT*`, `EUD/TCP`, approval history and treatment site are dropped on import.
- XML written through `XmlWriter.Create(path)` defaults (`:52`), so UTF-8 BOM plus declaration
  (inferred; the goldens' normaliser strips it); of the two real samples `Abdomen.xml` has a declaration
  and `Structure Template.xml` none. `XmlWriter` is not disposed on exception (`:50-55`). The reader
  never checks the root element or version.
- No XSD; goldens are characterisation (`VarianXmlRoundTripTests.cs:8-12`) and pin a 1.2→1.1 downgrade
  and the `CodeSchemeVersion` rewrite.
- `Xaml_Maker/XamlMakerCsharp/Program.cs` bypasses `VarianXmlBatch` guards (no `NameRules`, path
  traversal via Preview ID, non-atomic overwrite) and has no tests.
- `Xaml_Maker/*.py`, `*.ipynb`: dead 2019-2022 Clinical Protocol pipeline (another author, missing
  `XMLconversion` module, `//kodiak/physics` shares); reusable only for its dictionaries (its Protocol
  1.9 skeleton is superseded, WS4 item 8).

### A.6 CI, release and hygiene
- `ci.yml`'s release job has never run (tags are v1.0.0-v1.0.4, legacy ClickOnce builds that the
  modernization plan records as embedding tokens; confirm before deleting assets); README promises
  token-free self-contained releases that do not exist yet and macOS builds with no macOS CI leg.
- Leaked tokens remain in history and old assets; secret-scanning push protection is off; no LICENSE;
  `main` unprotected; no dependabot or SECURITY.md; the two workflows pick the SDK differently.
- Tests: 415 `[Fact]` + 50 `[Theory]` across three projects (630 cases); Presentation tests locate data
  by walking up to the `.sln`.
- Dead or legacy: root Python scripts with personal paths and an in-place DICOM rewriter (`test.py`),
  `WriteToAirTable.py` with a different schema, `ontology.pkl`, `FMAID To SNOMED_Updated.xlsx`,
  `Test_Base_key.txt`/`Test_Table_key.txt` (a third party's test base), `Paper/` (20 MB),
  `AirTableRecords/` (52 legacy template folders plus `Ontologies/`; 50 of 52 `Paths.txt` point at
  `O:\DICOM\BMA_Export\Single_Image`, two are empty; FMA codes; old site names).
- Deferred from the modernization plan (D20): persistent header cache, on-disk change detection in
  editors, "(no code)" ROI choice, rename leaving old RTs, Varian share settings window, matching
  semantics.

## Appendix B: Research findings (web, 2026-10-15; confidence noted)

### B.1 Varian Eclipse
- Current StructureTemplate exports are Version 1.2 with `Preview Version="1.2"` (public 2023-2025
  exports: StrukturMall, rtpros; our 2022 `Abdomen.xml`). High. Which release introduced 1.2 and
  whether 18.x emits newer: unknown; confirm by exporting from our Eclipse.
- Real exports pair each scheme with its own `CodeSchemeVersion`: FMA 3.2 and 99VMS_STRUCTCODE 1.0 (our
  2019/2022 exports and rtpros 2023), RADLEX 3.8 (our 2022 file only); no public export contains an SRT
  code. High for FMA/99VMS, Medium for RADLEX.
- `ColorAndStyle` variants in the wild: names (`Blue`, `LemonGreen`), `RGB255192203`, `RGB  0255191`,
  `RGB 0 0 255`, style-prefixed names truncated near 16 characters. High.
- Code-less structures in real exports keep `<Identification>` with empty `VolumeID`, `VolumeCode`,
  `VolumeCodeTable` and a populated `VolumeType`, omitting `StructureCode`; StrukturMall's 2025 templates
  carry no `StructureCode` at all. High (file shape); whether such files import is Medium (StrukturMall
  applies them through ESAPI, not through the XML importer).
- ESAPI 15.6-17.0 structure-code dictionaries: `FMA`, `RADLEX`, `SRT` ("SNOMED RT structure scheme"),
  `99VMS_STRUCTCODE`; no `SCT`. `StructureSet.AddStructure(StructureCodeInfo)` creates coded structures;
  `AddStructure(dicomType, id)` accepts AVOIDANCE, CAVITY, CONTRAST_AGENT, CTV, EXTERNAL, GTV,
  IRRAD_VOLUME, ORGAN, PTV, TREATED_VOLUME, SUPPORT, FIXATION, CONTROL, DOSE_REGION (no OAR); no ESAPI
  API creates or imports templates. High. Whether 18.x accepts `SCT`, and whether the XML importer
  validates codes against RT Administration: unknown (public ESAPI docs stop at 17.0).
- Codes selected in the Eclipse UI or through ESAPI must be active in the site's RT Administration
  Structure Dictionary (AAPM TG-263 report: "Each structure requires selection of a label from the
  Structure Dictionary"). High. Whether template import validates codes: unknown.
- Template XML lives on the image server under `va_data$\programdata\vision\templates\{protocol,
  objective,plan}` (a structure folder is unconfirmed). Medium.
- Public corpus (Medium unless stated): AAPM `EclipseStructureTemplates.zip` (linked from the TG-263
  supplemental page; no date, version or licence stated; one research pass reports five Version 1.1
  files, unverified); StrukturMall (MIT; ESAPI apply script using `CanAddStructure`/`AddStructure` and
  `RGB r g b` colours; High); rtpros (Apache-2.0; builds Protocol 1.12 from CSV; High); Squint (xsd.exe
  C# model of the protocol XML); WUSTL ClinicalTemplateReader (read-only, restrictive licence, do not
  vendor).
- Varian publishes clinical-goal XML downloads but no TG-263 structure templates; the System Server 18.1
  DICOM conformance statement is password-protected (MyVarian). High.
- Third-party experience (tomo_export, Eclipse 18.1): referenced CT SOP instances must exist; empty
  `StructureSetLabel` rejected; SH 16-character limit enforced. Medium.

### B.2 Other vendors
- RayStation: structure templates are database objects edited in "Structure template management"; no
  public file import or export; scripting `case.PatientModel.CreateRoi(Name, Color, Type, ...)` plus
  `OrganData.OrganType`; templates applied via `LoadTemplatePatientModel`/`CreateStructuresFromTemplate`.
  v2026 DICOM conformance statement (RSL-D-RS-v2026-DCS-EN-1.0, 2026-05-28): RTSTRUCT import converts
  to an internal representation; must reference a single image series whose images are imported prior
  to or together with the RTSTRUCT; Structure Set ROI and ROI Contour item counts must match and at
  least one item is required; ROI names may be renamed on collision; Contour Sequence is Type 3 on
  import, so contour-less ROIs are permitted by the table but acceptance is not described (confirm by
  importing our empty-ROI RT); `RT ROI Identification Code Sequence` appears in neither the import nor
  the export tables; export writes `ROIGenerationAlgorithm SEMIAUTOMATIC`. High. Field Safety Notice
  #167168 (28 Nov 2025; versions 10B to v2025 SP1; fixed December 2025): dose invalidation fails for
  ROIs with no contours that have a material override or are of type Bolus, Fixation or Support,
  evidence that contour-less ROIs are a normal state. Community: wrssc `TG-263.xml` (GPLv3; 982 entries
  with name, FMAID, interpreted type, RGB) is the closest public analogue of our schema.
- Brainlab RT Elements 4.7 (DICOM Conformance Statement Rev 10, 10 June 2026, section 8.1.2.3.7):
  imports Segmented Property Category Code Sequence and RT ROI Identification Code Sequence with
  "Schemes supported: SCT, SRT, FMA"; exports the identification code as SRT; imports interpreted types
  EXTERNAL, PTV, CTV, GTV, AVOIDANCE, SUPPORT (ORGAN only on export); object model Name/Type/Role; no
  template file. High. Contour-less and ORGAN handling unknown.
- Elekta ProKnow DS: structure-set template CSV (`Name,Type,R,G,B` minimum) upload and download in
  Organization Settings; REST endpoints exist but are not in the SDK; draft API `create_roi(name, color,
  type)`; renaming rules (synonym lists). High. The cheapest real "repository we control" for a
  write-path test; whether codes survive upload and re-export is untested.
- Elekta Monaco: no structure template format (plan templates only); RTSTRUCT must be imported with its
  axial images; the 2017 conformance statement lists interpreted types EXTERNAL, ORGAN, BOLUS on import
  and SUPPORT in addition on export; reads no codes; current version unknown; a 14-character name limit
  is reported but unverified. Medium.
- MIM: contour label templates configurable (TG-263 supported) but documentation is login-only and no
  file format was found; MIM accepts RT objects over the network only as Implicit VR LE (Explicit VR's
  16-bit length limit on DS `ContourData`). Medium.
- Pinnacle: no template file; `.Script`/HotScript adds ROIs with Pinnacle's named colours only; import
  renames invalid or duplicate names and strips a documented character list. High.
- Accuray: RTSTRUCT import only; no public conformance statement. Medium.
- AI-contouring vendors: Limbus writes placeholder ROIs with configurable codes
  (`ROIGenerationAlgorithm MANUAL`); Siemens AI-Rad Companion Organs RT (VA6x conformance statement, June
  2024): Organ Database rows hold name, colour, type, method, group and one code from FMA, RADLEX or
  99VMS_STRUCTCODE, and the RTSTRUCT writes `99VMS` / `20161209` / `VMS011` context for every scheme
  (no SCT documented); Radformation `.structures`, MVision ConfigUI and Manteia templates have no public
  specification. High for Limbus and Siemens; Medium for the rest.

### B.3 Standards
- DICOM permits ROIs with no contours. Structure Set ROI Sequence and RT ROI Observations Sequence are
  Type 3 from edition 2025c and ROI Contour Sequence from 2025d (all three were Type 1 through 2025b);
  the current edition (2026d) also allows an RT Structure Set "used without structures". RayStation
  v2026 still requires at least one ROI. High.
- RT ROI Observations: Segmented Property Category (0062,0003) Type 3 from CID 9580; RT ROI
  Identification Code Sequence (3006,0086) Type 3 with the CID mapped for the category (C.8.8.8.3;
  Table C.8.8.8.3.1-1 maps interpreted types to category and identification codes, Table C.8.8.8.3.1-2
  maps PTV/CTV/GTV to RT Target (130041, DCM, in CID 9580) plus SCT 228793007/228792002/228791009 from
  CID 9534); 25 interpreted-type defined terms. Code Sequence Macro: `CodeValue` 1C (16 characters or
  fewer) else `LongCodeValue`; `CodingSchemeVersion` 1C; Context Identifier Type 3 with Mapping Resource
  and Context Group Version 1C. PS3.3 8.3: Code Meaning is the concept's meaning, never a key. High.
- PS3.16: `SCT` = 2.16.840.1.113883.6.96 (numeric concept IDs); `SRT` shares the UID, SNOMED-RT style
  values, deprecated; FMA = 2.16.840.1.113883.6.119. CP-2608 adds Tracking ID/UID per ROI. High.
- IHE-RO has no structure-template profile (a 2009 use case was never finished); BRTO-II defines an RT
  Structure Set content module (fetch TF Vol 3). High. The TG-263 report cites a DICOM supplement as a
  planned vendor-neutral template format (recorded as Supplement 196; unknown).
- HL7 mCODE `TG263CM` ConceptMap maps TG-263 names to SNOMED (draft, known errors). Medium.
- Do not invest in DICOM SEG or Supplement 147 RT Segment Annotation as TPS template carriers (no TPS
  surveyed accepts them; no toolkit implements Sup 147); keep the data model mappable to RT Segment
  Annotation. High for RayStation and ProKnow (SEG absent from their conformance statements), Medium
  for the rest.
- Open tooling (Medium unless stated): dcmqi/3D Slicer terminology JSON (High); TotalSegmentator SNOMED
  mapping CSV convention (High); PyMedPhys `pymedphys-tg263/1` JSON with provenance and a monthly AAPM
  edition check (High); AAPM worksheet (717 structures, 594 FMA IDs, 2017 edition; no stated licence;
  TG-263U1 chartered through 2027 to publish French and Spanish names; High); NRG structure-names
  spreadsheet (2023); UAB-RO TG263-Toolkit (C# name grammar; licence to confirm); `tg263` PyPI; Gooding
  2026 twelve-system DICOM round-trip study (High).

### B.4 Validation and test tooling (all High: package pages and documentation read directly)
- `dciodvfy` (`pip install dicom3tools`, 20260927 wheels for Windows, Linux and macOS; all output on
  stderr; the package says dciodvfy returns 1 when it reports an error but advises reading the output).
- `dicom-validator` `validate_iods --edition <ed>` (exit code = error count). fo-dicom
  `DicomDataset.Validate()`. DCMTK `drtdump`/`dcmdump` (`choco install dcmtk` / apt). dcm4che
  `dcmvalidate` needs a hand-written RTSTRUCT IOD XML (low priority). DVTk is Windows-only and partly paid.
- Orthanc (`jodogne/orthanc`, ports 4242/8042) as a CI service; `python -m pynetdicom storescu`;
  `/instances/{id}/simplified-tags`; Orthanc Stone rejects mismatched ROI sequence counts.
- Viewers: 3D Slicer + SlicerRT (scriptable), Weasis, OHIF 3.11; dicompyler is archived.

### B.5 Airtable API (all High: Airtable's own documentation)
- 5 requests/s per base and 50/s per token (429, wait 30 s); 10 records per create or update request;
  list pages of 100; monthly caps per workspace: Free 1,000 (one 30-day grace period, then blocked until
  the month resets), Team 100,000 (overage throttled to 2 req/s, not blocked), Business and Enterprise
  unlimited; metadata calls count.
- `typecast:true` creates a single- or multi-select choice when none matches exactly (choice colours
  Airtable-assigned); `update-field` is not documented to add choices; create-table/create-field accept
  `autoNumber` and select choices with colours. Metadata API: `POST /v0/meta/bases` (needs
  `workspaceId`, `schema.bases:write`, Workspace creator), `.../tables`, `.../fields`.
- `performUpsert` with 1-3 `fieldsToMergeOn` fails on multiple matches and may be throttled separately.
- Collaborators can duplicate a base directly (records and comments optional, interfaces copied, synced
  tables lose their sync, record ids are new); share-link "Copy base" is for non-collaborators and needs
  the owner's toggle. Every Airtable API read needs a token; there is no anonymous read.

## Appendix C: Airtable bases in the account (read via the Airtable connector, 2026-10-15)

| Base | Name | Notes |
| --- | --- | --- |
| `appYl68lenTOFqTDh` | TG263_PublicAvailable | Snapshot source; schema below. Two more tables ("Copies of deleted", "choose your own color"). |
| `appzWlVKRp9TrrTUJ` | TG263BMABase | The base the modernization plan and `AirtableTableSourceTests.cs:14-15` name; the leaked `patQ` token pointed at it; schema below. |
| `appTUL6ZaSepTawFw` | Structure Naming | Old wide base: per-site singleSelect columns, personal colour columns, Location/Subsite/Intent/Protocol. |
| `appAZeazznL7m4fpq` | Structure Naming v2.1 | Newer wide schema with renamed and added sites (AbdPelv_SBRT, Bone, Sarcoma, CNS_Spine, Thorax_Mesothelioma, BreastPBI/WBI/RNI); dated "v2 (April 2026)" copies exist. |
| others | TG263_Base (x2), TG263_BaseCopy, TG263_BaseBMA (x2), UNC_Template, testing | Ownership and consumers unknown: not throwaway candidates; do not write to them. |

Schema of `appYl68lenTOFqTDh` / `tblex7IPsmm8hvVEc` "Structure Templates":
- `Record` (autoNumber, primary), `CommonName`, `Type`, `Structure`, `Colors_RGB`, `FMAID`, `SchemeCode`,
  `TG_263`, `TG_263R`, `TG_263French`, `TG_263RFrench`, `TG_263Spanish`, `TG_263RSpanish`, `Scheme`,
  `Template_Recommend`, `Template_Consider`, `DVH_Color`, `DVH_Style`, `DVH_Width`, `DVH_Type_Index`,
  `DVH_ContourStyle`, `Id`, `ContextGroupVersion`, `MappingResource`, `ContextIdentifier`,
  `MappingResourceName`, `ContextUID`. No `RGB`, no `MappingResourceUID`.
- Types: `Structure`, `Type`, `TG_263*` singleSelect; `Colors_RGB`, `Template_Recommend`,
  `Template_Consider` multipleSelects; the rest single-line text.

Schema of `appzWlVKRp9TrrTUJ` / `tblltR3aTxlJUwaGa` "Table1":
- `Record` (autoNumber, primary), `Type`, `Structure`, `Colors_RGB`, `FMAID`, `SchemeCode` (singleSelect),
  `TG_263`, `TG_263R`, `TG_263French`, `TG_263Spanish`, `TG_263SpanishR`, `TG_263FrenchR`, `Scheme`
  (singleSelect), `Template_Recommend`, `Template_Consider`, `DVH_*` (five columns),
  `ContextGroupVersion`, `MappingResource`, `MappingResourceName`, `ContextIdentifier`,
  `MappingResourceUID`, `ContextUID`, `Id`. No `CommonName`, no `RGB`.

The Airtable connector in this environment can list bases, read schemas and records, and also create,
update and delete tables, fields and records. During planning use only its read tools (section 4).
