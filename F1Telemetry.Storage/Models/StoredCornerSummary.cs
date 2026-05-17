namespace F1Telemetry.Storage.Models;

/// <summary>
/// Represents a persisted corner-level summary for a completed lap.
/// </summary>
public sealed record StoredCornerSummary
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
    /// Gets the analyzed lap number.
    /// </summary>
    public int LapNumber { get; init; }

    /// <summary>
    /// Gets the corner number within the track definition.
    /// </summary>
    public int CornerNumber { get; init; }

    /// <summary>
    /// Gets the corner display name.
    /// </summary>
    public string CornerName { get; init; } = "-";

    /// <summary>
    /// Gets the corner start distance in metres.
    /// </summary>
    public float? StartDistanceMeters { get; init; }

    /// <summary>
    /// Gets the corner apex distance in metres.
    /// </summary>
    public float? ApexDistanceMeters { get; init; }

    /// <summary>
    /// Gets the corner end distance in metres.
    /// </summary>
    public float? EndDistanceMeters { get; init; }

    /// <summary>
    /// Gets the entry speed in KPH.
    /// </summary>
    public double? EntrySpeedKph { get; init; }

    /// <summary>
    /// Gets the apex speed in KPH.
    /// </summary>
    public double? ApexSpeedKph { get; init; }

    /// <summary>
    /// Gets the exit speed in KPH.
    /// </summary>
    public double? ExitSpeedKph { get; init; }

    /// <summary>
    /// Gets the minimum speed in KPH.
    /// </summary>
    public double? MinSpeedKph { get; init; }

    /// <summary>
    /// Gets the maximum brake input observed in the corner.
    /// </summary>
    public double? MaxBrake { get; init; }

    /// <summary>
    /// Gets the average throttle input observed in the corner.
    /// </summary>
    public double? AverageThrottle { get; init; }

    /// <summary>
    /// Gets the average steering input observed in the corner.
    /// </summary>
    public double? AverageSteering { get; init; }

    /// <summary>
    /// Gets the estimated time loss in milliseconds.
    /// </summary>
    public double? TimeLossInMs { get; init; }

    /// <summary>
    /// Gets the human-readable advice text for the corner.
    /// </summary>
    public string AdviceText { get; init; } = "-";

    /// <summary>
    /// Gets optional structured details for future analyzers.
    /// </summary>
    public string? PayloadJson { get; init; }

    /// <summary>
    /// Gets the row creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
}
