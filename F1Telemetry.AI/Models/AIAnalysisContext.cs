using F1Telemetry.Analytics.Laps;

namespace F1Telemetry.AI.Models;

/// <summary>
/// Represents the lap, state, and event summaries used to build an AI request.
/// </summary>
public sealed record AIAnalysisContext
{
    /// <summary>
    /// Gets the latest completed lap summary.
    /// </summary>
    public LapSummary? LatestLap { get; init; }

    /// <summary>
    /// Gets the best completed lap summary.
    /// </summary>
    public LapSummary? BestLap { get; init; }

    /// <summary>
    /// Gets the most recent completed laps in reverse chronological order.
    /// </summary>
    public IReadOnlyList<LapSummary> RecentLaps { get; init; } = Array.Empty<LapSummary>();

    /// <summary>
    /// Gets the current remaining fuel in laps.
    /// </summary>
    public float? CurrentFuelRemainingLaps { get; init; }

    /// <summary>
    /// Gets the current fuel in tank.
    /// </summary>
    public float? CurrentFuelInTank { get; init; }

    /// <summary>
    /// Gets the current ERS store energy.
    /// </summary>
    public float? CurrentErsStoreEnergy { get; init; }

    /// <summary>
    /// Gets the current tyre description.
    /// </summary>
    public string CurrentTyre { get; init; } = "-";

    /// <summary>
    /// Gets the current tyre age in laps.
    /// </summary>
    public byte? CurrentTyreAgeLaps { get; init; }

    /// <summary>
    /// Gets the gap to the car ahead in milliseconds.
    /// </summary>
    public ushort? GapToFrontInMs { get; init; }

    /// <summary>
    /// Gets the gap to the car behind in milliseconds.
    /// </summary>
    public ushort? GapToBehindInMs { get; init; }

    /// <summary>
    /// Gets the recent race events in summary form.
    /// </summary>
    public IReadOnlyList<string> RecentEvents { get; init; } = Array.Empty<string>();
}
