---
name: build-and-test
description: >
  How to build, test and format-check DicomTemplateMakerCSharp (the .NET 10 solution
  DicomTemplateMaker.sln: WPF app, libraries, command-line tools, four xunit v3 test projects).
  Load it BEFORE the first dotnet build of a session, before running or filtering tests, before
  dotnet format, when a build fails with MSB3027/MSB3021 file-lock errors, and whenever a golden
  file (Varian XML round trip, import JSON) or the validator allowlist seems to need updating.
  Skipping it means a build broken by a running app instance, a test run that stops another
  session's test app, or a golden regenerated without the required review.
---

# Build, test and format

## The gate (run all three, in this order, from the repository root)

```
dotnet build DicomTemplateMaker.sln
dotnet test --solution DicomTemplateMaker.sln
dotnet format DicomTemplateMaker.sln --verify-no-changes
```

Measured 2026-11-03 on the maintainer's machine: 38 s, 69 s (825 tests), 33 s. Warnings are errors
and nullable reference types are on for every project (`Directory.Build.props`); package versions
are central (`Directory.Packages.props`). CI (`.github/workflows/ci.yml`) runs the same commands in
Release on `windows-latest` and `ubuntu-latest` (format on Linux only), plus the `dicom-validation`
job (the `evaluation-pipeline` skill). Run anything over two minutes in a background tool call.

## Before any build: release the file locks

A running `DicomTemplateMakerGUI` keeps its DLLs loaded, so a build of the same output folder fails
with MSB3027/MSB3021. Stop only instances that run from **your** checkout:

```powershell
Get-Process DicomTemplateMakerGUI -ErrorAction SilentlyContinue |
  Where-Object { $_.Path -like "$PWD\*" } | Stop-Process -Force
```

Never `Get-Process DicomTemplateMakerGUI | Stop-Process` without the path filter: it kills every
instance on the machine, including another session's test instance (this happened on 2026-11-04).

## Tests

- The runner is Microsoft.Testing.Platform (`global.json`: `"test": { "runner":
  "Microsoft.Testing.Platform" }`), so use `dotnet test --solution`, not `dotnet test` on a project
  with VSTest flags. One project: `dotnet test --project DicomTemplateCore.Tests`. One class or
  method: `dotnet test --solution DicomTemplateMaker.sln -- --filter-method "*SnapshotStoreSwapTests*"`
  (check `--help` of the test executable if the filter syntax has changed).
- Test projects run on any OS; the WPF app is compile-checked only. Rules the windows follow are
  tested in `DicomTemplateMaker.Presentation.Tests`; a UI defect usually gets a Presentation helper
  plus its test, and the code-behind calls the helper.
- Former flake (finding F-7, fixed 2026-11-05 by PR 31): `SnapshotStoreSwapTests.A_target_held_open_for_a_moment_is_replaced_once_it_is_released`
  failed now and then on `windows-latest` with `UnauthorizedAccessException`; its hold is now released
  on the first refused rename through the `FileSwap.MoveOver` test seam, not on a timer. If it fails
  again, treat it as a regression, not a flake, and never loosen the test.
- Both Presentation and Core tests find their data by walking up to the `.sln`; run them from a
  checkout, not from a copied `bin` folder.

## Goldens and fixtures

- Varian XML goldens: `DicomTemplateCore.Tests/TestData/*.golden.xml` and `*.import.golden.json`.
  They change only through a reviewed `UPDATE_GOLDEN=1 dotnet test ...` when a change to that output
  is intended; summarise the diff in the pull request.
- RT content is characterised by `docs/evidence/baseline-e7f0572/inventory/*.txt`; a change to what
  the generator writes must regenerate the inventories (plan P4.10) with the diff reviewed and
  summarised in `docs/evidence/inventory-changes.md`.
- `TemplateSync.Tests/TestData/TG263.2026-09-30.json` is byte-pinned (`SnapshotFactsTests`,
  `-text` in `.gitattributes`): never edit it.
- `docs/evidence/validator-allowlist.json` may only lose entries.

## Formatting

`dotnet format DicomTemplateMaker.sln --verify-no-changes` must print nothing. To fix, run it without
the flag and review the diff; `.editorconfig` rules apply to every project.
