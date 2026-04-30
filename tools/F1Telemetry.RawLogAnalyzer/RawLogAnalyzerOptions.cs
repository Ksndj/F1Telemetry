namespace F1Telemetry.RawLogAnalyzer;

/// <summary>
/// Defines the input raw log, optional markdown output path, and optional session filter for offline analysis.
/// </summary>
public sealed record RawLogAnalyzerOptions(string InputPath, string? OutputPath, ulong? SessionUid = null);
