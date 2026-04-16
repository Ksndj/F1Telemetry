namespace F1Telemetry.Analytics.Laps;

/// <summary>
/// Represents a completed-lap summary derived from player lap samples.
/// </summary>
public sealed record LapSummary
{
    /// <summary>
    /// Gets the completed lap number.
    /// </summary>
    public int LapNumber { get; init; }

    /// <summary>
    /// Gets the completed lap time in milliseconds.
    /// </summary>
    public uint? LapTimeInMs { get; init; }

    /// <summary>
    /// Gets the sector 1 time in milliseconds.
    /// </summary>
    public uint? Sector1TimeInMs { get; init; }

    /// <summary>
    /// Gets the sector 2 time in milliseconds.
    /// </summary>
    public uint? Sector2TimeInMs { get; init; }

    /// <summary>
    /// Gets the sector 3 time in milliseconds.
    /// </summary>
    public uint? Sector3TimeInMs { get; init; }

    /// <summary>
    /// Gets the average speed across all available samples.
    /// </summary>
    public double? AverageSpeedKph { get; init; }

    /// <summary>
    /// Gets the estimated fuel usage across the lap in litres.
    /// </summary>
    public float? FuelUsed { get; init; }

    /// <summary>
    /// Gets the estimated ERS energy usage across the lap in joules.
    /// </summary>
    public float? ErsUsed { get; init; }

    /// <summary>
    /// Gets the tyre wear delta across the lap in percentage points.
    /// </summary>
    public float? TyreWearDelta { get; init; }

    /// <summary>
    /// Gets a value indicating whether the completed lap is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the tyre description at lap start.
    /// </summary>
    public string StartTyre { get; init; } = "-";

    /// <summary>
    /// Gets the tyre description at lap end.
    /// </summary>
    public string EndTyre { get; init; } = "-";

    /// <summary>
    /// Gets a value indicating whether the lap started in pit.
    /// </summary>
    public bool StartedInPit { get; init; }

    /// <summary>
    /// Gets a value indicating whether the lap ended in pit.
    /// </summary>
    public bool EndedInPit { get; init; }

    /// <summary>
    /// Gets the time when the summary was closed.
    /// </summary>
    public DateTimeOffset ClosedAt { get; init; }
}
