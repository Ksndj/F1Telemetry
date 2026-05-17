namespace F1Telemetry.Analytics.Strategy;

/// <summary>
/// Represents compact evidence used by the semi-empirical undercut and overcut risk analyzer.
/// </summary>
public sealed record StrategyRiskInput
{
    /// <summary>
    /// Gets the current lap number when known.
    /// </summary>
    public int? CurrentLapNumber { get; init; }

    /// <summary>
    /// Gets the gap to the car ahead in milliseconds.
    /// </summary>
    public double? GapToCarAheadMs { get; init; }

    /// <summary>
    /// Gets the gap to the car behind in milliseconds.
    /// </summary>
    public double? GapToCarBehindMs { get; init; }

    /// <summary>
    /// Gets the estimated pit-loss time in milliseconds.
    /// </summary>
    public double? EstimatedPitLossMs { get; init; }

    /// <summary>
    /// Gets the current tyre label.
    /// </summary>
    public string? CurrentTyre { get; init; }

    /// <summary>
    /// Gets the current tyre age in laps.
    /// </summary>
    public int? CurrentTyreAgeLaps { get; init; }

    /// <summary>
    /// Gets the estimated fresh-tyre pace gain per lap in milliseconds.
    /// </summary>
    public double? FreshTyrePaceGainPerLapMs { get; init; }

    /// <summary>
    /// Gets a value indicating whether pit-exit traffic risk is currently known.
    /// </summary>
    public bool? PitExitTrafficRisk { get; init; }

    /// <summary>
    /// Gets upstream data quality warnings that should be preserved.
    /// </summary>
    public IReadOnlyList<string> DataQualityWarnings { get; init; } = Array.Empty<string>();
}
