namespace F1Telemetry.RawLogAnalyzer;

/// <summary>
/// Defines the input raw log and optional markdown output path for offline analysis.
/// </summary>
public sealed record RawLogAnalyzerOptions(string InputPath, string? OutputPath);
