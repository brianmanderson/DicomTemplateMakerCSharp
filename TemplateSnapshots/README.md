# Published template snapshots

`TG263.json` is the shared TG-263 template table exported from Airtable by
`.github/workflows/refresh-template-snapshot.yml`. The program downloads it from
`https://raw.githubusercontent.com/brianmanderson/DicomTemplateMakerCSharp/main/TemplateSnapshots/TG263.json`
(conditional request, at most once a day unless the user presses Refresh) and bundles a copy in
release builds for offline first runs. No Airtable token is needed to read it.

Do not edit `TG263.json` by hand. Change the Airtable table, then run the workflow
(Actions > Refresh TG-263 template snapshot > Run workflow). A changed export must pass
`TemplateSnapshotTool check` and is proposed as a pull request from `snapshot-refresh/<branch>`
with the check summary as its description; merging it publishes the snapshot. Its git history is
the audit trail of changes to the shared templates.

To export and check locally (the export requests every column and keeps only the mapped template
columns, so no exclude list is needed):

```
AIRTABLE_PAT=pat... dotnet run --project TemplateSnapshotTool -- \
  --base appYl68lenTOFqTDh --table tblex7IPsmm8hvVEc --name TG263 \
  --sort-field Record --output TemplateSnapshots/TG263.json
dotnet run --project TemplateSnapshotTool -- check TemplateSnapshots/TG263.json --summary check.md
```

`check` exits 5 when a gate fails: fewer than 240 records or `recordCount` not matching them, a
record without `Structure`, `SchemeCode` or `Scheme`, or a `Colors_RGB` option that is not
`name:r,g,b` with 0-255. It also reports, without failing, site names that cannot be folder names,
duplicate `Structure` names (exact and ignoring case), `Type` values outside the 25 DICOM RT ROI
Interpreted Types, and codes that are not valid for their scheme (SCT: SCTID with Verhoeff check
digit; FMA: integer). On the 2026-09-30 snapshot those warnings number 0, 1, 1 and 7.
