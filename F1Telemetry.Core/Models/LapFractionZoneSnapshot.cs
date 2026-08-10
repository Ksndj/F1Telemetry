namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents a zone bounded by lap-fraction positions.
/// </summary>
public sealed record LapFractionZoneSnapshot
{
    /// <summary>
    /// Gets the lap fraction where the zone starts.
    /// </summary>
    public float StartLapFraction { get; init; }

    /// <summary>
    /// Gets the lap fraction where the zone ends.
    /// </summary>
    public float EndLapFraction { get; init; }
}
