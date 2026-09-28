using TemplateSync.Airtable;
using TemplateSync.Credentials;
using TemplateSync.Storage;
using TemplateSync.Sync;

// Usage:
//   AIRTABLE_PAT=pat... dotnet run --project TemplateSnapshotTool -- \
//       --base appXXXXXXXXXXXXXX --table tblXXXXXXXXXXXXXX --name TG263 \
//       --output TemplateSnapshots/TG263.json [--exclude-fields CommonName,RGB] [--sort-field Record] [--allow-shrink]
//
// Exit codes: 0 written or unchanged, 2 bad arguments, 3 Airtable error, 4 refused (table shrank by half or more).
var options = ParseArguments(args);
if (options == null)
{
    Console.Error.WriteLine("Usage: --base <appId> --table <tblId> --name <name> --output <file> [--exclude-fields a,b] [--sort-field <column>] [--allow-shrink]");
    return 2;
}

string? token = Environment.GetEnvironmentVariable(AirtableConnectionStore.TokenEnvironmentVariable);
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine($"Set the {AirtableConnectionStore.TokenEnvironmentVariable} environment variable to a token with data.records:read on the base.");
    return 2;
}

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
var api = new AirtableHttpClient(http, token, options: new AirtableClientOptions
{
    UserAgent = "DicomTemplateMaker-SnapshotTool",
    Notify = message => Console.WriteLine(message),
});

TableSnapshot fresh;
try
{
    fresh = await TableExporter.ExportAsync(api, options.BaseId, options.TableId, options.Name, options.ExcludeFields, options.SortField == null ? null : new[] { options.SortField }, null, new Progress<string>(Console.WriteLine), CancellationToken.None);
}
catch (AirtableException ex)
{
    Console.Error.WriteLine("Airtable error: " + ex.Message);
    return 3;
}

TableSnapshot? existing = SnapshotStore.TryLoadFile(options.Output);
if (existing != null && TableExporter.SameRecords(existing, fresh))
{
    Console.WriteLine($"Unchanged: {fresh.Records.Count} records match {options.Output}.");
    WriteGitHubOutput("changed", "false");
    return 0;
}

if (existing != null && !options.AllowShrink && fresh.Records.Count * 2 <= existing.Records.Count)
{
    Console.Error.WriteLine($"Refusing to replace {existing.Records.Count} records with {fresh.Records.Count}. Re-run with --allow-shrink if this is intended.");
    return 4;
}

SnapshotStore.WriteFile(options.Output, fresh);
Console.WriteLine($"Wrote {fresh.Records.Count} records to {options.Output}" + (existing == null ? "." : $" (previously {existing.Records.Count})."));
WriteGitHubOutput("changed", "true");
return 0;

static void WriteGitHubOutput(string key, string value)
{
    string? path = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
    if (!string.IsNullOrEmpty(path))
    {
        File.AppendAllText(path, $"{key}={value}{Environment.NewLine}");
    }
}

static Options? ParseArguments(string[] args)
{
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    bool allowShrink = false;
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--allow-shrink")
        {
            allowShrink = true;
        }
        else if (args[i].StartsWith("--", StringComparison.Ordinal) && i + 1 < args.Length)
        {
            values[args[i][2..]] = args[++i];
        }
        else
        {
            return null;
        }
    }

    if (!values.TryGetValue("base", out string? baseId) || !AirtableIds.IsBaseId(baseId)
        || !values.TryGetValue("table", out string? tableId) || string.IsNullOrWhiteSpace(tableId)
        || !values.TryGetValue("name", out string? name) || string.IsNullOrWhiteSpace(name)
        || !values.TryGetValue("output", out string? output) || string.IsNullOrWhiteSpace(output))
    {
        return null;
    }

    string[] exclude = values.TryGetValue("exclude-fields", out string? list)
        ? list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        : Array.Empty<string>();
    string? sortField = values.TryGetValue("sort-field", out string? sort) && !string.IsNullOrWhiteSpace(sort) ? sort : null;
    return new Options(baseId, tableId, name, output, exclude, sortField, allowShrink);
}

internal sealed record Options(string BaseId, string TableId, string Name, string Output, string[] ExcludeFields, string? SortField, bool AllowShrink);
