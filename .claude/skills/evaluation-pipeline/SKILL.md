---
name: evaluation-pipeline
description: >
  How to reproduce the committed evidence baseline and run every validator of
  DicomTemplateMakerCSharp with the internal TemplateEvaluationTool: build the 53 TG-263 sites from
  the frozen snapshot, generate the test RTs on the sample CT, inventory them, run dciodvfy and
  validate_iods, the TPS-realism rules, the snapshot check and the nomenclature report, and diff
  against docs/evidence/baseline-e7f0572. Load it before any evaluation-tool command, before
  judging whether an RT-content change is intended, before touching docs/evidence, and before
  any write-airtable call. Skipping it means outputs written into the repository or the app's
  settings folder, a validator run that fails for PATH reasons, or an Airtable write that spends
  the maintainers' quota.
---

# Evaluation pipeline (docs/EVALUATION_PROTOCOL.md sections 1.3, 1.4, 3.2)

`$OUT` is a scratch folder **outside the repository** (the session scratchpad is fine). Every
command writes `manifest.json` (git SHA, inputs with SHA-256, time) next to its outputs. The tool
never reads or writes `%LOCALAPPDATA%\DicomTemplateMaker`; `--data-dir` is where it keeps the
`Ontologies` library it builds. Expected timings from 2026-11-03: the whole pipeline under one minute,
the validators about three minutes.

```
dotnet build TemplateEvaluationTool -c Release
dotnet run --project TemplateEvaluationTool -c Release --no-build -- build-sites --snapshot TemplateSync.Tests/TestData/TG263.2026-09-30.json --naming default --data-dir $OUT/data --out $OUT/sites
dotnet run --project TemplateEvaluationTool -c Release --no-build -- generate-rts --sites $OUT/sites --images DicomTemplateMakerGUI/SmallCT --out $OUT/rt
dotnet run --project TemplateEvaluationTool -c Release --no-build -- inventory --in $OUT/rt --out $OUT/inventory
dotnet run --project TemplateEvaluationTool -c Release --no-build -- realism --in $OUT/rt --out $OUT/realism --allowlist docs/evidence/validator-allowlist.json
dotnet run --project TemplateSnapshotTool -- check TemplateSnapshots/TG263.json --summary $OUT/check.md
```

Always the frozen fixture, never the live `TemplateSnapshots/TG263.json`, for anything you compare:
the live file hashes differently on CRLF and LF checkouts. `generate-rts` writes the 53 RTs and a
copy of the four CT slices into `$OUT/rt/series`.

## What must match

| Output | Compare with | Expected |
| --- | --- | --- |
| `$OUT/sites/build-warnings.json`, `sites.json` | `docs/evidence/baseline-e7f0572/build-sites/` | identical (warnings empty); 53 sites |
| `$OUT/rt/run-report.json` | `.../generate-rts/run-report.json` | identical; 53 RTs, 1,178 ROIs |
| `$OUT/inventory/*.txt` (53) | `.../inventory/` | identical (UIDs, dates and patient values are masked) |
| `$OUT/realism/realism.md`, `realism.json` | `.../realism/` | PASS; 9 allowlisted RR8 errors (codes 88/99, D1b) |
| `$OUT/check.md` | protocol 1.4 | exit 0; warnings 0 / 1 / 1 / 7 |

Diff with `diff -rq --strip-trailing-cr -x manifest.json`; only the manifests may differ. Any other
difference is a finding, or the intended effect of an RT-content change that then regenerates the
evidence (plan P4.10).

## Validators (Python, pinned)

```
python -m venv $OUT/venv
$OUT/venv/Scripts/pip install "dicom3tools==20260927" "dicom-validator==0.9.0" "pydicom==3.0.2" "pynetdicom==3.0.4" "xlrd==2.0.1"
PATH="$OUT/venv/Scripts:$PATH" $OUT/venv/Scripts/python.exe -I tools/dicom-validation/validate_rts.py --rt-dir $OUT/rt/series --out $OUT/validators --allowlist docs/evidence/validator-allowlist.json
```

The `Scripts` folder must be on `PATH` until finding F-3 (pull request 27) is merged; otherwise the
script says "dciodvfy is not installed" and exits 1. Expected: PASS, `dciodvfy` 0 errors (4,712
warnings), `validate_iods --edition 2025b` 53 errors, all the allowlisted C9. The first `validate_iods`
run parses the DICOM standard (about a minute) and prints four banner lines that are not in the
baseline log.

Nomenclature (needs the network, not in CI):
`$OUT/venv/Scripts/python.exe -I tools/nomenclature/check_tg263_names.py --snapshot TemplateSync.Tests/TestData/TG263.2026-09-30.json --out $OUT/nomenclature`;
worksheet SHA-256 `5ff0b9e2ebf578793f6fa8f59c2357b3feb61fdc0f87171e9103a0495d93d150`; the report
matches the baseline apart from its timestamp.

Orthanc and `dcmdump` are CI-only on the maintainer's machine (no Docker, no DCMTK): read the
`dicom-validation-reports` artifact of the latest `main` run with `gh run download <run-id> --name dicom-validation-reports`
into an empty scratch folder.

## Airtable safety

- `write-airtable` only with `--dry-run` unless the maintainers have done P0.5 and handed over
  `AIRTABLE_EVAL_PAT`; the first live run is the observation run of plan P3.2.
- The tool refuses the public base `appYl68lenTOFqTDh` and any base not in `EVAL_TARGET_BASES`
  (exit 3); a base id is `app` plus 14 letters or digits (an 18-character id is "bad arguments",
  exit 2). A dry run of `_Targets_RNI` against an empty table plans 6 creates in 1 request.
- Never run `TemplateSnapshotTool` export, never dispatch `refresh-template-snapshot.yml`, never
  the root-level Python scripts.
