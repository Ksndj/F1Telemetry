namespace F1Telemetry.Storage.Models;

/// <summary>
/// Represents a persisted lap row.
/// </summary>
public sealed record StoredLap
{
    /// <summary>
    /// Gets the auto-incremented row identifier.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// Gets the associated session identifier.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the lap number.
    /// </summary>
    public int LapNumber { get; init; }

    /// <summary>
    /// Gets the lap time in milliseconds.
    /// </summary>
    public int? LapTimeInMs { get; init; }

    /// <summary>
    /// Gets the sector 1 time in milliseconds.
    /// </summary>
    public int? Sector1TimeInMs { get; init; }

    /// <summary>
    /// Gets the sector 2 time in milliseconds.
    /// </summary>
    public int? Sector2TimeInMs { get; init; }

    /// <summary>
    /// Gets the sector 3 time in milliseconds.
    /// </summary>
    public int? Sector3TimeInMs { get; init; }

    /// <summary>
    /// Gets a value indicating whether the lap was valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the average speed in KPH.
    /// </summary>
    public double? AverageSpeedKph { get; init; }

    /// <summary>
    /// Gets the estimated fuel used in litres.
    /// </summary>
    public float? FuelUsedLitres { get; init; }

    /// <summary>
    /// Gets the estimated ERS usage.
    /// </summary>
    public float? ErsUsed { get; init; }

    /// <summary>
    /// Gets the starting tyre label.
    /// </summary>
    public string StartTyre { get; init; } = "-";

    /// <summary>
    /// Gets the ending tyre label.
    /// </summary>
    public string EndTyre { get; init; } = "-";

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
}
