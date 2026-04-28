namespace F1Telemetry.RawLogAnalyzer;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = ParseArgs(args, out var showHelp);
            if (showHelp)
            {
                PrintUsage();
                return options is null ? 1 : 0;
            }

            var analyzer = new RawLogAnalyzerService();
            var result = await analyzer.AnalyzeAsync(options!).ConfigureAwait(false);

            Console.WriteLine($"Analyzed {result.TotalLines} JSONL lines.");
            Console.WriteLine($"Parsed packets: {result.ParsedPacketCount}.");
            Console.WriteLine($"Sessions: {result.Sessions.Count}.");
            Console.WriteLine($"Report: {result.ReportPath}");
            Console.WriteLine(
                $"Issues: invalidJson={result.InvalidJsonLineCount}, invalidBase64={result.InvalidBase64LineCount}, lengthMismatch={result.PayloadLengthMismatchCount}, unknownPacketIds={result.UnknownPacketIdCount}, unsupportedKnownPacketIds={result.UnsupportedPacketIdCount}, parseFailures={result.PacketParseFailureCount}.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Raw log analysis failed: {ex.Message}");
            return 1;
        }
    }

    private static RawLogAnalyzerOptions? ParseArgs(string[] args, out bool showHelp)
    {
        showHelp = false;
        if (args.Length == 0)
        {
            showHelp = true;
            return null;
        }

        string? inputPath = null;
        string? outputPath = null;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "-h":
                case "--help":
                    showHelp = true;
                    return new RawLogAnalyzerOptions(string.Empty, null);
                case "--input":
                    inputPath = ReadValue(args, ref index, "--input");
                    break;
                case "--output":
                    outputPath = ReadValue(args, ref index, "--output");
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{arg}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("Missing required --input path.");
        }

        return new RawLogAnalyzerOptions(inputPath, outputPath);
    }

    private static string ReadValue(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{name} requires a value.");
        }

        index++;
        return args[index];
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project .\\tools\\F1Telemetry.RawLogAnalyzer\\F1Telemetry.RawLogAnalyzer.csproj -- --input <raw-log.jsonl> [--output <analysis.md>]");
    }
}
