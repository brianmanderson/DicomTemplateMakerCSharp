using TemplateEvaluationTool;

// Internal evaluation tool (docs/EVALUATION_PROTOCOL.md section 1.3). No network calls; never writes to
// %LOCALAPPDATA%\DicomTemplateMaker. Every command writes manifest.json next to its outputs.
//
//   dotnet run --project TemplateEvaluationTool -- build-sites --snapshot <TG263.json> --naming default --data-dir <dir> --out <sites>
//   dotnet run --project TemplateEvaluationTool -- generate-rts --sites <sites> --images <ct folder> --out <rt> [--template-rs <file>]
//   dotnet run --project TemplateEvaluationTool -- inventory --in <rt> --out <inventory>
//
// Exit codes: 0 done, 1 the build stopped, 2 bad arguments, 3 the sample CT could not be copied.
const string Usage = "Usage: build-sites --snapshot <file> --naming default --data-dir <dir> --out <dir> | generate-rts --sites <dir> --images <dir> --out <dir> [--template-rs <file>] | inventory --in <dir> --out <dir>";

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
        _ => throw new UsageException("Unknown command '" + args[0] + "'."),
    };
}
catch (UsageException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine(Usage);
    return 2;
}
