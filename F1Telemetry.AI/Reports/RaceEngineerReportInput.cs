using F1Telemetry.Analytics.Corners;
using F1Telemetry.Analytics.Strategy;

namespace F1Telemetry.AI.Reports;

/// <summary>
/// Contains compressed V3 race evidence that is safe to use for post-race engineer reporting.
/// </summary>
public sealed record RaceEngineerReportInput
{
    /// <summary>
    /// Gets the user-facing session summary.
    /// </summary>
    public string SessionSummary { get; init; } = "-";

    /// <summary>
    /// Gets compact lap summary lines.
    /// </summary>
    public IReadOnlyList<string> LapSummaries { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets inferred stint summaries.
    /// </summary>
    public IReadOnlyList<StintSummary> Stints { get; init; } = Array.Empty<StintSummary>();

    /// <summary>
    /// Gets conditional strategy advice items.
    /// </summary>
    public IReadOnlyList<StrategyAdvice> StrategyAdvices { get; init; } = Array.Empty<StrategyAdvice>();

    /// <summary>
    /// Gets corner-level summaries.
    /// </summary>
    public IReadOnlyList<CornerSummary> CornerSummaries { get; init; } = Array.Empty<CornerSummary>();

    /// <summary>
    /// Gets compact key event lines.
    /// </summary>
    public IReadOnlyList<string> KeyEvents { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets data-quality warnings that must be surfaced in the report.
    /// </summary>
    public IReadOnlyList<string> DataQualityWarnings { get; init; } = Array.Empty<string>();
}
