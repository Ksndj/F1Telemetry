namespace F1Telemetry.Analytics.Strategy;

/// <summary>
/// Represents the compact output of the V3 stint strategy analyzer.
/// </summary>
public sealed record StrategyAnalysisResult
{
    /// <summary>
    /// Gets the inferred stint summaries.
    /// </summary>
    public IReadOnlyList<StintSummary> Stints { get; init; } = Array.Empty<StintSummary>();

    /// <summary>
    /// Gets compact timeline entries derived from stints and race events.
    /// </summary>
    public IReadOnlyList<StrategyTimelineEntry> Timeline { get; init; } = Array.Empty<StrategyTimelineEntry>();

    /// <summary>
    /// Gets analysis-level data quality warnings.
    /// </summary>
    public IReadOnlyList<string> DataQualityWarnings { get; init; } = Array.Empty<string>();
}
