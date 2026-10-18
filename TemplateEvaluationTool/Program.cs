using TemplateEvaluationTool;

// Internal evaluation tool (docs/EVALUATION_PROTOCOL.md section 1.3). Never writes to %LOCALAPPDATA%\DicomTemplateMaker.
// Every command writes manifest.json next to its outputs. Only write-airtable without --dry-run makes network calls.
//
//   dotnet run --project TemplateEvaluationTool -- build-sites --snapshot <TG263.json> --naming default --data-dir <dir> --out <sites>
//   dotnet run --project TemplateEvaluationTool -- generate-rts --sites <sites> --images <ct folder> --out <rt> [--template-rs <file>]
//   dotnet run --project TemplateEvaluationTool -- inventory --in <rt> --out <inventory>
//   dotnet run --project TemplateEvaluationTool -- realism --in <rt> --out <realism> [--allowlist docs/evidence/validator-allowlist.json]
//   EVAL_TARGET_BASES=appXXXXXXXXXXXXXX AIRTABLE_PAT=pat... dotnet run --project TemplateEvaluationTool -- write-airtable \
//       --sites <sites> --base <appId> --table <tblId or name> --cache-dir <dir> --out <dir> [--site <name>]... [--dry-run]
//
// write-airtable refuses the public TG-263 base appYl68lenTOFqTDh and table tblex7IPsmm8hvVEc, and any base not listed in
// EVAL_TARGET_BASES (comma-separated app ids), before any request. The token comes from AIRTABLE_PAT only; --dry-run needs
// none and sends nothing. Every request of a real write is logged to <out>/requests.jsonl without header values.
//
// Exit codes: 0 done, 1 the build stopped (realism: an error is not allowlisted; write-airtable: Airtable reported an error),
// 2 bad arguments (write-airtable: or no token), 3 the sample CT could not be copied (write-airtable: target or token refused).
const string Usage = "Usage: build-sites --snapshot <file> --naming default --data-dir <dir> --out <dir> | generate-rts --sites <dir> --images <dir> --out <dir> [--template-rs <file>] | inventory --in <dir> --out <dir> | realism --in <dir> --out <dir> [--allowlist <file>]"
    + " | write-airtable --sites <dir> --base <appId> --table <tblId or name> --cache-dir <dir> --out <dir> [--site <name>]... [--dry-run]";

if (args.Length == 0)
{
    Console.Error.WriteLine(Usage);
    return 2;
}

string[] rest = args.Skip(1).ToArray();
try
{
    return args[0] switch
    {
        "build-sites" => BuildSitesCommand.Run(rest),
        "generate-rts" => GenerateRtsCommand.Run(rest),
        "inventory" => InventoryCommand.Run(rest),
        "realism" => RealismCommand.Run(rest),
        "write-airtable" => WriteAirtableCommand.Run(rest),
        _ => throw new UsageException("Unknown command '" + args[0] + "'."),
    };
}
catch (UsageException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine(Usage);
    return 2;
}
