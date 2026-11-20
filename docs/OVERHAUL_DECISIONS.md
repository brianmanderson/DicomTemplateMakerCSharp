# Overhaul decisions log

Companion to `docs/OVERHAUL_PLAN.md`, `docs/EVALUATION_PROTOCOL.md` and `docs/OVERHAUL_CLAIMS.md`.
Written 2026-10-15 against commit `e7f0572`. The plan assumes every recommendation below; steps that
change under another answer are written there as "if Dn = recommended: ..., otherwise: ...".

How to use this file: fill in **Answer** (who, date, choice) for each decision. When an answer differs
from the recommendation, apply the "Otherwise" branch of every step listed under **Changes**.
Step ids (P0.1 ...) refer to `OVERHAUL_PLAN.md`; claim ids (S5, V7, A3 ...) refer to
`OVERHAUL_CLAIMS.md`; A.x and R1-R10 refer to the appendices of `docs/OVERHAUL_PLANNING_PROMPT.md`.

Decisions that block the first implementation phase (start of WS1 and WS2): **D1, D7, D15**. They
were not put to the maintainers interactively because this session was started by another session;
please answer them first.

## Summary

| Id | Question (short) | Recommendation | Blocks |
| --- | --- | --- | --- |
| D1 | Ontology identity and code meaning | (a) identity = (scheme, value), meaning from the concept; targets coded as RT Target + CID 9534 code plus Therapeutic Role; numeric `FMAID` as an equivalent code | P2.1, P4.3 |
| D1b | Source data defects, non-SNOMED codes | Report at build, sanitise per vendor, ask base owners to clean; omit the identification code for "88"/"99" | P1.3, P4.2 |
| D2 | Context attributes and scheme identification | Agree: omit all six context attributes for SCT/FMA not drawn from a context group; Coding Scheme Identification Sequence at top level | P4.1 |
| D3 | Canonical ROI name | TG-263 `Structure` name, language/laterality stored as variants | P3.5, P7.1 |
| D4 | Interpreted-type vocabulary | 25 DICOM terms canonical; map known vendor aliases with a warning; refuse the rest | P4.4, P6.4 |
| D5 | Template_Consider ROIs in generated RTs | Keep them, after Recommend ROIs, marked optional in ROI Description; per-template switch to omit | P4.8 |
| D6 | Varian-only presentation fields | Carry through unchanged now; move to a vendor extension block in the canonical model | P3.5, P7.1 |
| D7 | Canonical base, column spelling, write-target workspace | `appYl68lenTOFqTDh` canonical; its spelling canonical, code reads both; throwaway base in a workspace we own that does not host it | P0.5, P2.8, P3.1 |
| D7b | Sort live reads | Yes: `Record` when present, else `createdTime` | P2.10, P4.9 |
| D8 | Synchronisation vs additive writes | Stay additive; dry run lists remote-only ROIs and sites; no per-site overrides | P3.5 |
| D9 | Record-level copier | No copier now: "Duplicate base" is the faithful copy; the template path is what we evaluate | P3.8 |
| D9b | Which re-upload leg | Both; leg (a) first, leg (b) after P2.2 | P3.7 |
| D10 | `AIRTABLE_PAT` scope, connection test, dry run | Override only a named connection; test checks scopes and columns; show a dry run | P3.9 |
| D11 | CLI as product or test-only | Internal tool now (`TemplateEvaluationTool`, not shipped) | P1.1 |
| D12 | Provenance attributes | Project name and version in place of personal names; empty `ROIInterpreter`; `MANUAL`; `UNAPPROVED` written deliberately | P4.5 |
| D13 | Seed file or build from scratch | Build from scratch once the baseline inventory exists | P4.6 |
| D14 | Transfer syntax, names, labels | Implicit VR LE default; refuse case-insensitive duplicate names; reject colliding labels; full name in `StructureSetName` | P4.6, P4.7 |
| D15 | Which TPS environments and identity convention | Maintainers to answer; plan assumes a non-clinical Eclipse database first | P5.3-P5.5 |
| D16 | Eclipse versions, XML version, conformance statement | Version 1.2 target; 1.1 selectable; obtain 18.x conformance statement and a fresh export | P6.1, P6.3, P6.4 |
| D17 | Structure-code dictionaries and SNOMED for Eclipse | No `StructureCode` for SCT-coded ROIs until the dictionary export; then FMA from numeric `FMAID` | P6.2-P6.4 |
| D18 | Writer strategy, vendor scope, outreach owner | Fix in place now; canonical model plus exporters once two non-Varian exporters are approved | P6, P7 |
| D19 | Clinical Protocol XML in scope? | No; archive the dictionaries, delete the dead pipeline | P6.7 |
| D20 | Modernization plan's deferred items | Do "(no code)"; keep the rest deferred; keep matching semantics with exact-match precedence | P4.2, appendix |
| D21 | Legacy files, licence, third-party files | Archive or delete per list; Apache-2.0; commit no unlicensed or copyleft files; snapshot via PR | P0.3, P0.4, P1.8, P6.1 |
| D22 | `CleaningRTStructureCsharp` | Retire in favour of "Add ROIs from an RT Structure file" | P4.12 |

---

## Data model and codes

### D1. Ontology identity and code meaning

**Question.** (a) identity = (scheme, value), Code Meaning taken from the concept ("Planning target
volume"), TG-263 name only in ROI Name; or (b) keep the TG-263 convention (meaning = TG-263 name, one
SCT code shared by dozens of names) and match on (meaning, value, scheme) everywhere. Under (a): how
are targets coded, and does `FMAID` become a second code?

**Recommendation.** (a).
- Targets: Segmented Property Category (130041, DCM, "RT Target") with the generic target code from CID
  9534 as RT ROI Identification Code (228793007 PTV, 228792002 CTV, 228791009 GTV). CID 9580 maps
  130041 to CID 9534, so this is conformant (claim S3). The snapshot already holds exactly these codes,
  so no new data is needed. In addition write Therapeutic Role Category (3010,0064) and Therapeutic
  Role Type (3010,0065), both Type 3, derived from the interpreted type by Table C.8.8.8.3.1-2 ("Therapeutic
  Role Code Mapping": PTV, CTV, GTV, TREATED_VOLUME, IRRAD_VOLUME → 130041 RT Target; OAR, AVOIDANCE,
  CONTROL → 130042 RT Dose Calculation Structure; REGISTRATION → 130746). Correction to the prompt: that
  table populates the role attributes, not the category and identification code. The PS3.17 pattern
  (anatomy as identification code, target as role) needs an anatomy code per target that no source
  column holds; the canonical model (P7.1) stores anatomy and role separately so it can be emitted
  later.
- Code meanings: from PS3.16 where the concept is in a DICOM context group, otherwise curated once into
  the `CommonName` column of the base we control (empty in the source today). The licence terms for
  reusing SNOMED CT meanings outside PS3.16 are a confirm-by item (claim S16).
- `FMAID`: yes as a second code, seeded from the source column, but only for numeric values. RT ROI
  Identification Code Sequence allows a single item, so the FMA code goes in Equivalent Code Sequence
  (0008,0121, Type 3, "codes considered equivalent by the creating system") inside that item (claim S17).
  Verification on 2026-10-15 found `FMAID` filled in all 246 records, of which 82 are "88", 10 are
  "99" and 20 are labels ("CTV", "PTV", "GTVLR", "PTVHR" ...); about 134 are plausible FMA ids. Check
  them against the active Eclipse FMA dictionary (P6.2) before use; do not seed from
  `FMA_SNOMEDCT_Key.txt`.

**Rationale.** PS3.3 section 8.3 says Code Meaning is not to be used as a key (claim S6); (b) writes
items such as (228793007, SCT, "PTV_High") that no receiving system can validate. Today identity is
inconsistent: (meaning, value, scheme) on JSON load (`ROIClass.cs:613-615`), (value, scheme) in the
library merge (`OntologyCodeClass.cs:84`), value only in `TemplateMaker.cs:147, 303, 490`, and
`categorize_folder` re-points ROIs by value alone (A.2 item 1, re-checked). With 40 PTV names sharing
228793007, (a) is the only answer under which matching by value is correct.

**Answer.** Brian Anderson, 2026-11-03 (relayed by the project-manager session): **(a), the
recommendation.** Identity = (scheme, value); Code Meaning from the concept; targets coded Segmented
Property Category (130041, DCM, "RT Target") with the CID 9534 code as RT ROI Identification Code plus
Therapeutic Role Category and Type; numeric `FMAID` as an Equivalent Code. P2.1's second half and P4.3
are unblocked.

**Changes.** Recommended: P2.1 branch (a) (fix in `TemplateEntryMapper` and the meaning table), P4.3,
P7.1 identity rule (scheme, value); P1.8 adds a meaning-coverage report. Otherwise (b): P2.1 branch (b)
(`categorize_folder` and `make_template` match on meaning, value and scheme), P4.3 drops Segmented
Property Category for targets (Therapeutic Role attributes are still written), and the claims list gains
a non-conformance note for every target code.

### D1b. Source data defects and non-SNOMED codes

**Question.** Names with `/`, `^`, a space or U+02C6; duplicate `_TS_CTVsb`; `Type` "Control"; seven
`SchemeCode` values "88"/"99". Clean in Airtable before the baseline is pinned, reject at build time,
or pass through and sanitise per vendor? For "88"/"99": omit the identification code or derive one
from the interpreted type?

**Recommendation.** Pass through, report every defect as a build warning in the harness output
(P1.5), sanitise per vendor at export (P6.4, P7.x), and send the list to the base owners to clean in
their table; automation never writes to `appYl68lenTOFqTDh`. Rejecting at build time would remove
two cervix CTVs, two U+02C6 CTVs, `Hi HU` and a duplicate from every site that lists them. For
"88"/"99": omit the RT ROI
Identification Code (Type 3) and, if D1 adds Segmented Property Category, derive only the category
from the interpreted type via Table C.8.8.8.3.1-1. Omitting a code requires the "(no code)" ROI choice
(D20), because `RtStructureBuilder.Validate` (`:244-292`) rejects ROIs without a code today.
Note: the source `Structure` select also holds two options named `_TS_CTVsb` and two named
`_TS_CTV_High` (distinct option ids), so `typecast` writes of those names are ambiguous.

**Rationale.** The baseline (P1.3) must pin the source as published; cleaning first would make the
baseline describe a table nobody downloaded. Inventing codes is worse than omitting them.

**Answer.** Brian Anderson, 2026-11-04 (relayed by the project-manager session): **accept the
recommendation.** Pass through; report every defect as a build warning; sanitise per vendor at export;
send the defect list to the base owners; for "88"/"99" omit the identification code and derive only the
Segmented Property Category from the interpreted type.

**Changes.** Recommended: P1.4 runs the invariants as warnings; P4.2 omits codes. Otherwise "clean
first": P1.3 pins a cleaned snapshot and P1.4 turns the invariants into gates after the cleanup.
Otherwise "reject": P2.1 adds build-time rejection with a reported list. Otherwise "derive": P4.2
writes identification codes from Table C.8.8.8.3.1-1.

### D2. Context attributes and coding scheme identification

**Question.** Omit Context Identifier, Context UID, Mapping Resource (and Name, UID) and Context Group
Version for SCT and FMA codes not drawn from a named context group; keep `VMS011`/`99VMS` only for
codes from a Varian dictionary; add a DICOM context identifier only where membership is verified;
write `CodingSchemeVersion` in the item and a top-level Coding Scheme Identification Sequence rather
than `CodingSchemeUID` per item?

**Recommendation.** Agree, with two qualifications.
1. Re-check found that `RtStructureBuilder.cs:424-429` gates only Context Identifier, Context Group
   Version and Mapping Resource on Context Identifier; Context UID, Mapping Resource Name and Mapping
   Resource UID are written whenever they have a value (`:431-433`). The fix must clear all six at
   mapping time (`AirTableEntry.cs:17-26` defaults, `OntologyCodeClass.cs:297-302`), not just the
   identifier.
2. For FMA codes bound for Eclipse, keep `VMS011` until P5.3 shows whether Eclipse needs it: Eclipse
   writes `VMS011` on FMA 3.2 (`DicomTemplateMakerGUI/Test.dcm`, claim V13) and Siemens Organs RT does
   the same (claim O8).

**Rationale.** Context Identifier is Type 3 and the mapping attributes are 1C on it (claim S5); a
Varian context group asserted on a SNOMED CT code is a false statement (A.4 C1).

**Answer.** Brian Anderson, 2026-11-04 (relayed by the project-manager session): **accept the
recommendation with both qualifications.** Clear all six context attributes at mapping time for SCT
and FMA codes not drawn from a named context group; keep `VMS011` only for Varian-dictionary codes
and, until the P5.3 Eclipse import test, on FMA codes bound for Eclipse; write `CodingSchemeVersion`
and one top-level Coding Scheme Identification Sequence.

**Changes.** Recommended: P4.1 as written, sequenced after P2.2. Otherwise: P4.1 keeps the context
attributes and only adds `CodingSchemeVersion`; P2.2 still lands; the A.4 C1 entry stays open in the
conformance description (P4.11).

### D3. Canonical ROI name

**Question.** Store the TG-263 name, with language and laterality as stored variants (so a re-upload
matches the source row), or keep the renamed display name as today?

**Recommendation.** TG-263 `Structure` name canonical; variants (`TG_263R`, Spanish, French, reversed
forms) stored per ROI and chosen at export. Today the choice renames `ROIName` in place
(`ROIClass.cs:768-824`, `OnlineTemplatesViewModel.cs:626-654`) and the original is not stored, which
causes R7 (new records on re-upload).

**Answer.** Brian Anderson, 2026-11-04 (relayed by the project-manager session): **accept the
recommendation.** The TG-263 `Structure` name is canonical; language and laterality forms are stored
per ROI as variants chosen at export; P3.5 fixes R1 and R7; the P7.1 schema has `name` plus `variants`.

**Changes.** Recommended: P3.5 fixes R7 and R1; P7.1 schema has `name` plus `variants`. Otherwise: the
diff (P3.4) tolerates R7, and the evaluation uses only the default (non-renamed) choice.

### D4. Interpreted-type vocabulary

**Question.** The 25 DICOM defined terms canonical with explicit per-vendor maps; should the
generator refuse, warn or map?

**Recommendation.** Canonical = the 25 terms (claim S4). The generator maps known aliases with a
warning (`BODY`→EXTERNAL, `Organ`→ORGAN, `Control`→CONTROL, `None`→empty, since RT ROI Interpreted
Type is Type 2) and refuses anything else. Per-vendor maps live with each exporter: Varian XML
(EXTERNAL↔BODY, ORGAN↔Organ, CONTROL↔Control; AVOIDANCE, MARKER, CAVITY need a confirm-by export),
ESAPI's 14 types (no OAR), Monaco's import list (claim O3). Replace `Editors/InterpretedTypes.cs`
(22 terms, no OAR, BRACHY_CHNL_SHLD, DEVICE) with one shared list.
The source `Type` select offers 24 options: 21 DICOM terms plus `None`, `Control` and `CONTROL`
(no OAR, TREATED_VOLUME or DEVICE); records use only 10 values, one of them "Control".

**Answer.** Brian Anderson, 2026-11-04 (relayed by the project-manager session): **accept the
recommendation.** The 25 DICOM RT ROI Interpreted Type terms are canonical; the generator maps known
aliases with a warning (`BODY` to EXTERNAL, `Organ` to ORGAN, `Control` to CONTROL, `None` to empty)
and refuses anything else; per-vendor maps live with each exporter; one shared 25-term list replaces
the editor's 22-term list.

**Changes.** Recommended: P4.4 (map + refuse), P6.4 (Varian map). Otherwise "warn only": P4.4 writes
unknown terms verbatim with a warning and the TPS matrix (P5.2) adds a fixture per unknown term.
Otherwise "refuse": P4.4 refuses `BODY`/`None` too and the Varian importer maps on import.

### D5. Template_Consider ROIs in generated RTs

**Question.** Should Consider ROIs (`Include=false`) be left out of generated RTs?

**Correction to the prompt.** `Include` is serialised; it is false only when `All_ROIs.json` lacks the
key (field default at `ROIClass.cs:422`, parameterless constructor at `:588-591`). Snapshot-built ROIs
get `Include` from `TemplateIndex` (true for Template_Recommend, false for Template_Consider,
`TemplateIndex.cs:18-19`, `TemplateEntryMapper.cs:90`), and the RT generator never reads it.

**Recommendation.** Keep writing Consider ROIs, ordered after Recommend ROIs (WS1 item 9), and mark
them with ROI Description (3006,0028, Type 3 in the Structure Set ROI Sequence, claim S18) "optional
(TG-263 Consider)"; add a per-template switch "omit
optional ROIs", default off. Treat a missing `Include` key in legacy JSON as true. Rationale: dropping
them changes what current users receive; a coded optional structure is more useful present than
re-created by hand; contour-less ROIs are a normal TPS state (claim R2).

**Answer.** Brian Anderson, 2026-11-04 (relayed by the project-manager session): **accept the
recommendation.** Keep writing Consider ROIs after the Recommend ROIs, each with ROI Description
"optional (TG-263 Consider)"; add the per-template "omit optional ROIs" switch, default off; a legacy
template without the `Include` key reads as true.

**Changes.** Recommended: P4.8, P4.9. Otherwise "omit": P4.8 filters on `Include`; inventories (P4.10)
lose the Consider ROIs; the TPS fixture "Include=false ROI" (P5.2) expects absence.

### D6. Varian-only presentation fields

**Question.** Apply `DVH_Type_Index` and `DVH_ContourStyle` on read, or move Varian-only fields to a
vendor extension block?

**Recommendation.** Now: read them and carry them through unchanged (fixes R6, which is a loss today
because `ToRoiWrapper` ignores them, `TemplateEntryMapper.cs:65-88`). In the canonical model (P7.1):
a `vendor.varian` block holding them plus `ContourStyle`, `DVHLine*`, `TypeIndex`; only the Varian
exporter reads it.

**Answer.** Brian Anderson, 2026-11-04 (relayed by the project-manager session): **accept the
recommendation.** Read `DVH_Type_Index` and `DVH_ContourStyle` and carry them through unchanged now
(fixes R6); in the P7.1 canonical model a `vendor.varian` block holds them with `ContourStyle`, the
DVH line fields and `TypeIndex`, read only by the Varian exporter.

**Changes.** Recommended: P3.5 (R6), P7.1. Otherwise "apply on read": P3.5 maps them onto `ROIClass`
fields used by the Varian writer and no extension block is created.

## Airtable

### D7. Canonical base, column spelling, ownership and write-target workspace

**Question.** Which base is canonical (`appYl68lenTOFqTDh` feeds the snapshot; `appzWlVKRp9TrrTUJ` is
named by the modernization plan and `AirtableTableSourceTests.cs:14-15`)? Which spelling
(`TG_263RSpanish` vs `TG_263SpanishR`)? Who owns each base, on which plan, and in which workspace should
the throwaway base live? Is `appYl68lenTOFqTDh` the "online database maintained by TG-263U1 members"
cited in Anderson et al. 2024, and may its share link enable "Copy base"? Should a lasting team base
get its own snapshot workflow?

**Recommendation.**
- Canonical: `appYl68lenTOFqTDh` (it is what the app downloads; it has the French/Spanish columns and
  an empty `CommonName` that D1 can fill). Tests stop naming `appzWlVKRp9TrrTUJ` except as a fixture
  of the other spelling.
- Spelling: the canonical base's (`TG_263RSpanish`, `TG_263RFrench`). The code reads both and writes
  what the table has (P2.8); `AirTableEntry` property names stay.
- Workspace: the connector shows this account as owner of "UCSD" (`wspifarJT1yil05P9`) and
  "UCSD_Templates" (`wspjbARddZacIq3Ob`) and with create permission on "Structure Naming by Disease
  Site" (`wspUgUKmVn9W2SyZz`); it has only "create" permission on every base, including
  `appYl68lenTOFqTDh`. Read tools do not show which workspace hosts which base. Recommended: the
  throwaway bases go in whichever owned workspace does not host `appYl68lenTOFqTDh`; please name it and
  its plan (Free 1,000 calls/month vs Team 100,000, claim A2).
- Lasting team base: no snapshot workflow until it is decided to be lasting.

**Answer.** Brian Anderson, 2026-11-03 (relayed by the project-manager session): **accept the
recommendation, details to follow.** Canonical base `appYl68lenTOFqTDh`; spelling: the canonical
base's (`TG_263RSpanish`, `TG_263RFrench`), the code reads both; owner/plan of each base: pending;
target workspace and plan: a workspace the maintainers own that does not host the canonical base, name
and plan pending; Anderson 2024 base: pending; share-link Copy base: pending. The pending facts are
asked for when P0.5 or P3 is next; P1, P2 and P4 do not wait on them.

**Changes.** Recommended: P0.5, P2.8, P3.1, quota table in `EVALUATION_PROTOCOL.md` section 2.5.
Otherwise (`appzWlVKRp9TrrTUJ` canonical): the snapshot workflow (`refresh-template-snapshot.yml`)
switches base and table ids and its exclude list, P1.3's frozen fixture is re-captured, and the alias
writes `TG_263SpanishR`. If the target workspace is on Free: one full empty-base run per month plus
observation runs (protocol 2.5).

### D7b. Sort live reads

**Question.** Read live tables with an explicit sort (`Record` autonumber or `createdTime`) so ROI order
and duplicate resolution match the snapshot?

**Recommendation.** Yes. Send `sort[0][field]=Record` when the table has a `Record` autonumber (both
real bases do); otherwise sort client-side by `createdTime`, then record id. Today live reads pass no
sort (`AirtableTableSource.cs:329`); only `TableExporter` sorts (`--sort-field Record`,
`refresh-template-snapshot.yml:65`). Verified: the snapshot's first and last records are `Record` 1 and
`Record` 325 in the live table, and `createdTime` is monotonic in the snapshot.

**Answer.** Brian Anderson, 2026-11-04 (relayed by the project-manager session): **accept the
recommendation.** Live reads send `sort[0][field]=Record` when the table has a `Record` autonumber,
otherwise sort client-side by `createdTime` then record id; P2.10 lands and the P4.9 acceptance applies
to live and snapshot builds.

**Changes.** Recommended: P2.10, P4.9 acceptance. Otherwise: P4.9 acceptance applies to snapshot
builds only and live builds are documented as unordered.

### D8. Synchronisation vs additive writes; per-site overrides

**Question.** True synchronisation (remove sites, prune colours, delete orphans) or additive? Per-site
overrides instead of one shared record per structure?

**Recommendation.** Stay additive; add a dry-run section listing remote-only ROIs and sites for a
human to prune. Correction: writes are not strictly additive: `WritePlanner.cs:170-173` removes the
current site from the opposite list (Recommend vs Consider) of a written ROI. No deletes exist
(`IAirtableApi` has list, create and update only). No per-site overrides; warn in the dry run when a
write changes a scalar column of a record that other sites list.

**Answer.** Brian Anderson, 2026-11-04 (relayed by the project-manager session): **accept the
recommendation.** Writes stay additive; the dry run lists remote-only ROIs and sites for a human to
prune; no per-site overrides, and the dry run warns when a write changes a scalar column of a record
other sites list. P3.5 tolerates R8 and fixes R10; P3.9 shows the dry run.

**Changes.** Recommended: P3.5 (R8 tolerated, R10 fixed), P3.9 dry run. Otherwise "sync": a new
`WritePlanner` removal branch, a delete endpoint, and an idempotency acceptance that also checks
removals; the driver gains a confirmation for deletes.

### D9. Record-level copier

**Question.** A copier that preserves all columns, or re-upload through template folders (lossy)?

**Recommendation.** No copier now. The faithful copy is Airtable's "Duplicate base" (zero API calls,
claim A6); the template-folder path is the product write path and is what G2 evaluates. Revisit only
if a lasting team base must be kept in step with the public one.

**Answer.** Brian Anderson, 2026-11-04 (relayed by the project-manager session): **accept the
recommendation.** No record-level copier now; "Duplicate base" is the faithful copy; P3.8 is skipped
and R9 is tolerated in the round-trip diff.

**Changes.** Recommended: P3.8 is skipped; R9 is tolerated in the diff. Otherwise: P3.8 builds
`TemplateEvaluationTool copy-table` (raw `GET` of every field, batched creates with `typecast`), with
its own diff that expects zero differences except record ids and `Record` values.

### D9b. Which re-upload leg

**Question.** Re-upload from the generated DICOM (leg b), from template folders (leg a), or both?

**Recommendation.** Both: leg (a) first (no code dependency), leg (b) after P2.2 makes `interpret_RT`
tolerant. Leg (b) is what proves the DICOM from WS1 carries the template.

**Answer.** Brian Anderson, 2026-11-04 (relayed by the project-manager session): **both, the
recommendation.** Leg (a) first (P3.3), leg (b) after it (P3.7); `WriteToAirTable.py` is retired.

**Changes.** Recommended: P3.3 and P3.7. Otherwise only one of them; `WriteToAirTable.py` is retired in
either case.

### D10. `AIRTABLE_PAT` scope, connection test, dry run

**Question.** Should `AIRTABLE_PAT` override only a named connection? Should the connection test check
write scope and required columns? Should a dry run be shown before the first request?

**Recommendation.** Yes to all three. `AIRTABLE_PAT` today overrides every stored connection
(`AirtableConnectionStore.cs:112-122`); restrict it to the connection named in a second variable. The
test (`TemplateSourceCatalog.cs:156-169`, one record, token as entered) adds `GET /v0/meta/whoami`
(returns the token's scopes, claim A8) and a column check (schema read when the token has
`schema.bases:read`, else the existing unknown-field probe). The dry run shows
`WritePlan.RequestCount` (`WritePlanner.cs:35-38`, used only by tests today) and creates/updates per
site.

**Answer.** Brian Anderson, 2026-11-04 (relayed by the project-manager session): **accept all
three.** (a) `AIRTABLE_PAT` overrides only the connection named in a second variable; (b) the
connection test adds the `whoami` scopes and a column check; (c) both windows show the dry run
(`RequestCount`, creates and updates per site) before the first request, with one shared warning cap.

**Changes.** Recommended: P3.9. Otherwise: P3.9 records the current behaviour only and the protocol's
clean-state rule (unset `AIRTABLE_PAT`) stays mandatory.

### D11. CLI as product feature or test-only

**Recommendation.** Internal tool now: a `TemplateEvaluationTool` console project beside
`TemplateSnapshotTool`, in the solution and CI but not in the release zip (`ci.yml` publishes only the
GUI). Promote commands to the product CLI after P7 if wanted.

**Answer.** Brian Anderson, 2026-11-04 (relayed by the project-manager session): **keep internal,
the recommendation.** `TemplateEvaluationTool` stays an internal console project in the solution and
CI, not in the release zip; promotion to the product command line is possible after P7.

**Changes.** Recommended: P1.1. Otherwise (product): P1.1 adds the commands to
`DicomTemplateMakerCSharp`, the release job packages it, and the README documents them.

## DICOM

### D12. Provenance attributes

**Question.** What replaces `Manufacturer "UCSD Residency"`, `ManufacturerModelName
"Universal_RT_Creator"`, `SoftwareVersions "Hello_from_Brian_M_Anderson"`, `SeriesDescription
"Anonymized"`, `ROIInterpreter "Brian_Mark_Anderson"`? Markers in `SeriesDescription` and
`StructureSetName`? `ApprovalStatus UNAPPROVED` deliberately? `ROIGenerationAlgorithm MANUAL`?

**Recommendation.** `Manufacturer` and `ManufacturerModelName` "DicomTemplateMaker";
`SoftwareVersions` = informational version plus git SHA; `ROIInterpreter` empty (PN, Type 2);
`SeriesDescription` = "TG-263 template: <template name>" (LO, 64); `StructureSetName` = template
name; `StructureSetDescription` = "Empty structures from template <name>, source <snapshot SHA or
table>"; `ApprovalStatus UNAPPROVED` written deliberately; `ROIGenerationAlgorithm MANUAL` (what Eclipse
writes for contour-less ROIs, claim V13, and what Limbus writes for placeholder ROIs, claim O7;
RayStation exports SEMIAUTOMATIC, claim R5). "Not for clinical
use" belongs only in evaluation fixtures (P5.2): the runner's purpose is to put these RTs on real
planning series (README line 3), so a blanket marker would be false in production.

**Answer.** Brian Anderson, 2026-11-04 (relayed by the project-manager session): **accept the
recommendation.** `Manufacturer` and `ManufacturerModelName` "DicomTemplateMaker"; `SoftwareVersions` =
version plus git SHA; `ROIInterpreter` empty; `SeriesDescription` "TG-263 template: <name>";
`StructureSetName` = template name; `StructureSetDescription` naming template and source;
`ApprovalStatus UNAPPROVED` deliberate; `ROIGenerationAlgorithm MANUAL`; no clinical-use marker in
production RTs.

**Changes.** Recommended: P4.5. Otherwise: P4.5 uses the maintainers' strings; the inventory diff
(P4.10) is the review point either way.

### D13. Seed file or build from scratch

**Recommendation.** Build from scratch after the baseline inventory (P1.6) lists every inherited
attribute: each module written deliberately, transfer syntax chosen by D14. Loses the "template file
unchanged" behaviour and the CLI's "template missing" error; both become obsolete. `template_RS.dcm`
(RayStation export, Implicit VR LE, ISO_IR 100, `Manufacturer "RaySearch Laboratories"`,
`StructureSetLabel "RS: Unapproved"`, one ROI) is then removed from the package.

**Answer.** Brian Anderson, 2026-11-04 (in the project-manager session): **build from scratch, the
recommendation.** Each module is written deliberately against the P1.6 baseline inventory, the transfer
syntax as D14 chooses; `template_RS.dcm` leaves the package, and with it the "template file unchanged"
behaviour and the command line's "template missing" error.

**Changes.** Recommended: P4.6. Otherwise "curated seed": P4.6 rewrites `template_RS.dcm` to a
minimal reviewed seed (inventory committed beside it) and adds a test that fails when the seed gains
attributes.

### D14. Transfer syntax, long names, duplicates, label collisions

**Recommendation.** Implicit VR Little Endian as the deliberate default for RT objects (claims O4, S9;
the Varian and RayStation files in the repository are Implicit VR LE), Explicit VR LE as an option.
Names over 16 characters (7 snapshot names): warn at build, allowed in DICOM (ROI Name is LO), refused
by the Eclipse exporter unless an alias under 17 characters exists. Duplicate names ignoring case and
trailing spaces: refuse at generation (A.4 C12). `StructureSetLabel` = first 16 characters (unchanged; a third
party reports Eclipse 18.1 truncating a 22-character label and rejecting an empty one, claim V12);
reject a template set whose labels collide (the legacy `AbdPelv_Prostate_Intact`/`_Postop` pair) with
a message naming both; full name in `StructureSetName`.

**Answer.** Brian Anderson, 2026-11-04 (in the project-manager session): **accept the
recommendation.** Implicit VR Little Endian is the default, Explicit VR Little Endian an option; names
over 16 characters warn at build and are refused by the Eclipse exporter unless an alias under 17
characters exists; names that duplicate ignoring case and trailing spaces are refused at generation;
`StructureSetLabel` is the first 16 characters and colliding labels are rejected with a message naming
both; the full name goes in `StructureSetName`.

**Changes.** Recommended: P4.6, P4.7. Otherwise: the chosen syntax and policies replace these in
P4.6/P4.7 and in the realism checker (P2.4).

### D15. TPS environments, versions, operators, test identity

**Question.** Which non-clinical TPS environments can we import into, which versions, who performs the
imports, and what test-patient identity convention does each site require?

**Recommendation.** Needs the maintainers. The plan assumes: a non-clinical Eclipse/ARIA database
(test or research) at our institution first; RayStation research or test database if available
(the maintainers' public RayStation scripting repositories suggest access); ProKnow sandbox as the
cheapest additional system. Identity: `ZZTEST^TG263TEMPLATE`, `PatientID` with a site-reserved test
prefix, unless a site convention exists.

**Answer.** Brian Anderson, 2026-11-03 (relayed by the project-manager session): **Eclipse test
database first, the recommendation.** Systems/versions: a test or research Eclipse/ARIA database first,
RayStation and a ProKnow sandbox if available; versions pending. Operators: pending. Identity
convention per site: `ZZTEST^TG263TEMPLATE` / `ZZTEST-0001` (the P0.2 identity).

**Changes.** Every P5 step; P0.2 uses the site convention when given.

## Varian and vendors

### D16. Eclipse/ARIA versions and XML version

**Recommendation.** Target Version 1.2 (root and `Preview`, with `TreatmentSite`); 1.1 selectable.
Minimum supported Eclipse = the versions in clinical use at participating sites (to fill). Correction
to the prompt: the Varian System Server 18.0 and 18.1 DICOM conformance statements are public (linked
from the vendor's interoperability page, claim V11), so read them in P6.1 rather than requesting them;
take a fresh template export from our Eclipse before changing the writer.

**Answer.** Brian Anderson, 2026-11-04 (in the project-manager session): **accept the recommendation,
versions pending.** Target Version 1.2 with 1.1 selectable; P6.1 reads the public Varian System Server
18.0/18.1 conformance statements; a fresh template export from our Eclipse comes before the writer
changes. Versions in clinical use: pending; statement read: not yet (P6.1); fresh export: pending.

**Changes.** P6.1, P6.3, P6.4. If only 1.1 systems are in scope: P6.4 keeps 1.1 as default.

### D17. Structure-code dictionaries and SNOMED for Eclipse

**Question.** Which dictionaries are active in our RT Administration and is SRT populated? Export
SCT-coded templates via an SCT→SRT crosswalk, via FMA, or with no `StructureCode`?

**Recommendation.** No `StructureCode` for SCT-coded ROIs in Eclipse XML until the dictionary export
(P6.2) and the one-structure import tests (P6.3) are done (StrukturMall's 2025 templates carry none,
claim V4). Then: FMA from the numeric `FMAID` values that exist in the active FMA dictionary;
SRT only if the SRT dictionary is populated and a sourced crosswalk exists; never a designator swap
(SRT values are SNOMED-RT identifiers, claim S7). The `FMA_SNOMEDCT_Key.txt` converter stays an editor
tool: it rewrites templates globally, keeps the Varian context attributes, and `OntologyEntryRules`
forbids one value in two schemes (`OntologyEntryRules.cs:37-41`, broader than stated: any duplicate
value is rejected).

**Answer.** Brian Anderson, 2026-11-04 (in the project-manager session): **accept the recommendation,
facts pending.** No `StructureCode` for SCT-coded ROIs in Eclipse XML until P6.2 and P6.3 are done;
then FMA from the numeric `FMAID` values present in the active FMA dictionary; SRT only with a populated
SRT dictionary and a sourced crosswalk, never a designator swap; `FMA_SNOMEDCT_Key.txt` stays an editor
tool. Active dictionaries: pending; SRT populated: pending (both from the P6.2 export).

**Changes.** P6.2-P6.4. Otherwise "crosswalk": P6.2 sources and commits an SCT→SRT table with its
provenance. Otherwise "via key file": P6.4 uses `FMAID_SNOMED_OntologyClass` per export, not per
template.

### D18. Writer strategy, vendor scope, outreach owner

**Recommendation.** Fix the Varian writer in place now (P6); build the canonical model (P7.1) and put
exporters behind it once two non-Varian exporters are approved; RTSTRUCT with empty ROIs stays the
universal carrier. Native exporters in scope: Varian XML and an ESAPI apply-template script; ProKnow
structure-set CSV only after a sandbox confirms the format (its documentation is gated and the
`Name,Type,R,G,B` shape is unverified, claim O2); RayStation script if P5.4 has access. All other
vendors: RTSTRUCT only. Outreach owner: a maintainer to name. The vendor-neutral template format the
TG-263 report cites is DICOM Supplement 196 "Segmentation Creation Template", a 2016 working-group
draft that was never finalised (claim S11): nothing to adopt.

**Answer.** Brian Anderson, 2026-11-04 (in the project-manager session): **accept the recommendation,
outreach owner pending.** Strategy: fix the Varian writer in place now (P6), canonical model and
exporters (P7.1) once two non-Varian exporters are approved, RTSTRUCT with empty ROIs the universal
carrier. Vendors: Varian XML and an ESAPI apply-template script; ProKnow CSV after a sandbox confirms
the format; a RayStation script if P5.4 has access; RTSTRUCT only for the rest. Outreach owner: pending.

**Changes.** P6, P7. Otherwise "rebuild now": P7.1 moves before P6.4 and the Varian writer is rewritten
behind it. Otherwise "keep and add": P6.4 shrinks to the defects that block imports.

### D19. Clinical Protocol XML

**Recommendation.** Out of scope. Archive `Xaml_Maker/ClinicalProtocolDictionaries.py` if its tables
are wanted, delete the rest of the 2019-2022 pipeline with D21. If in scope later, start from a
protocol exported by our Eclipse (Protocol 1.10-1.12), not `Clinical Protocol Template.xml` (1.9).

**Answer.** Brian Anderson, 2026-11-04 (in the project-manager session): **in scope, in a later phase**
(differs from the recommendation, so the "Otherwise" branch applies). A new phase after P6, with its own
fresh protocol export from our Eclipse and its own corpus; P6.7 does not delete the Clinical Protocol
pipeline.

**Changes.** P6.7. Otherwise: a new phase after P6 with its own fresh export and corpus.

## Repository

### D20. Modernization plan's deferred items

| Item | Recommendation |
| --- | --- |
| Persistent header cache | Keep deferred |
| On-disk change detection in editors | Keep deferred |
| "(no code)" ROI choice | Do, in P4.2: needed for D1b's omitted codes |
| Rename leaving old RTs | Keep deferred; document in the README |
| Varian share settings window | Keep deferred until P6 lands |
| Template matching semantics | Keep two-way substring; add exact-match precedence and tests ("H&N" vs "H&N Planning CT" still matches) |

**Answer.** Brian Anderson, 2026-11-05 (in the project-manager session): **accept the recommendation.**
"(no code)" is done in P4.2; template matching keeps the two-way substring and gains exact-match
precedence with tests; the rename item stays deferred and is documented in the README; the header
cache, on-disk change detection and the Varian share settings window (until P6 lands) stay deferred.

### D21. Legacy files, licence, third-party files

| Item | Recommendation |
| --- | --- |
| `Paper/` (21 MB, 48 files) | Move to a release asset or separate repository, then delete |
| Root Python scripts (9) | Delete; port nothing (`WriteToAirTable.py` uses a different schema and is superseded by P3.7) |
| `Xaml_Maker` notebooks, `.py`, `.docx`, `.xlsx` | Delete with D19 |
| `AirTableRecords/` (53 folders, 50 `Paths.txt` pointing at `O:\DICOM\BMA_Export\Single_Image`) | Archive; tests use `DicomTemplateCore.Tests/TestData/Legacy` |
| `DicomTemplateMakerGUI/Test.dcm` | Remove, or replace by a PS3.15 Annex E de-identified copy (P0.3) |
| `Test_Base_key.txt`, `Test_Table_key.txt` | Delete (a third party's base and table ids, not tokens) |
| `ontology.pkl`, `FMAID To SNOMED_Updated.xlsx` | Delete after confirming no generator needs them |
| Licence | Apache-2.0 (explicit patent grant; compatible with reusing MIT or Apache-2.0 material) |
| Third-party files | Commit none that are unlicensed (AAPM zip and worksheet) or copyleft (wrssc `TG-263.xml`, GPLv3); fetch at test time behind `TG263_CORPUS_DIR`; MIT/Apache-2.0 files only with attribution and only if needed |
| Snapshot workflow | Open a pull request instead of pushing to `main` |
| `FMA_SNOMEDCT_Key.txt` Google Sheet | Maintainers to name the owner; stop regenerating until D17 is answered |

**Answer.** Brian Anderson, 2026-11-05 (in the project-manager session): **Apache-2.0; the files as
recommended, except that the `Xaml_Maker` protocol files stay.** Licence Apache-2.0 (P0.4, before
`v1.1.0`). `Paper/` to a release asset or separate repository, then deleted; the nine root Python
scripts deleted; `AirTableRecords/` archived; `Test_Base_key.txt` and `Test_Table_key.txt` deleted;
`ontology.pkl` and `FMAID To SNOMED_Updated.xlsx` deleted once nothing needs them; no unlicensed or
copyleft third-party file committed; the snapshot workflow's pull request stays (done since P1.4).
Because D19 puts the Clinical Protocol XML in scope for a later phase, the `Xaml_Maker` notebooks,
`.py`, `.docx` and `.xlsx` files are kept until that phase decides what it reuses. Owner of the
`FMA_SNOMEDCT_Key.txt` Google Sheet: pending.

**Changes.** P0.3, P0.4, P1.8, P6.1, P6.7, hygiene backlog.

### D22. `CleaningRTStructureCsharp`

**Recommendation.** Retire. It is untested, throws `NullReferenceException` on a missing sequence
(`RTCleaner.cs:24-33`), only empties Contour Image Sequences and keeps patient identity and UIDs; the
GUI's "Add ROIs from an RT Structure file" covers the use case once P2.2 lands.

**Answer.** Brian Anderson, 2026-11-05 (in the project-manager session): **retire, the
recommendation.** P4.12 removes `CleaningRTStructureCsharp`; "Add ROIs from an RT Structure file"
covers the use case.

**Changes.** P4.12. Otherwise: P4.12 adds null checks, new SOP Instance UIDs and tests, and states in
its help text that it is not a de-identifier.
