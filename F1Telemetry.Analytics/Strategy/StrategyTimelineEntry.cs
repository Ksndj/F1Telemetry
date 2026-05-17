namespace F1Telemetry.Analytics.Strategy;

/// <summary>
/// Represents a compact strategy timeline entry suitable for UI, reports, and AI prompts.
/// </summary>
public sealed record StrategyTimelineEntry
{
    /// <summary>
    /// Gets the associated lap number when known.
    /// </summary>
    public int? LapNumber { get; init; }

    /// <summary>
    /// Gets the timeline category.
    /// </summary>
    public string Category { get; init; } = "-";

    /// <summary>
    /// Gets the short timeline title.
    /// </summary>
    public string Title { get; init; } = "-";

    /// <summary>
    /// Gets the compact timeline detail.
    /// </summary>
    public string Detail { get; init; } = "-";

    /// <summary>
    /// Gets a value indicating whether the entry is directly supported by source data.
    /// </summary>
    public bool IsDataSupported { get; init; } = true;

    /// <summary>
    /// Gets the risk level associated with the entry.
    /// </summary>
    public StrategyRiskLevel RiskLevel { get; init; } = StrategyRiskLevel.Unknown;

    /// <summary>
    /// Gets data quality warnings attached to this timeline entry.
    /// </summary>
    public IReadOnlyList<string> DataQualityWarnings { get; init; } = Array.Empty<string>();
}
