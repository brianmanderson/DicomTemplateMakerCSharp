# Published template snapshots

`TG263.json` is the shared TG-263 template table exported from Airtable by
`.github/workflows/refresh-template-snapshot.yml`. The program downloads it from
`https://raw.githubusercontent.com/brianmanderson/DicomTemplateMakerCSharp/main/TemplateSnapshots/TG263.json`
(conditional request, at most once a day unless the user presses Refresh) and bundles a copy in
release builds for offline first runs. No Airtable token is needed to read it.

Do not edit `TG263.json` by hand. Change the Airtable table, then run the workflow
(Actions > Refresh TG-263 template snapshot > Run workflow). Its git history is the audit trail of
changes to the shared templates.

To export locally:

```
AIRTABLE_PAT=pat... dotnet run --project TemplateSnapshotTool -- \
  --base appYl68lenTOFqTDh --table tblex7IPsmm8hvVEc --name TG263 \
  --exclude-fields CommonName --sort-field Record --output TemplateSnapshots/TG263.json
```
