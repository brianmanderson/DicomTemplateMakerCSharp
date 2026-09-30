using CleaningRTStructureCsharp;

// Usage: CleaningRTStructureCsharp <input-RS.dcm> <output.dcm>
if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: CleaningRTStructureCsharp <input-RS.dcm> <output.dcm>");
    return 2;
}

if (!File.Exists(args[0]))
{
    Console.Error.WriteLine($"Input file not found: {args[0]}");
    return 2;
}

var cleaner = new RTCleaner(args[0]);
cleaner.save(args[1]);
Console.WriteLine($"Wrote {args[1]}");
return 0;
