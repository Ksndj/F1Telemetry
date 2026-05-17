namespace F1Telemetry.Storage.Models;

/// <summary>
/// Represents the selected per-lap four-wheel tyre wear sample for post-race trend charts.
/// </summary>
public sealed record StoredLapTyreWearTrendPoint
{
    /// <summary>
    /// Gets the player lap number represented by this point.
    /// </summary>
    public int LapNumber { get; init; }

    /// <summary>
    /// Gets the sample index selected for this lap.
    /// </summary>
    public int SampleIndex { get; init; }

    /// <summary>
    /// Gets the time when the selected sample was captured.
    /// </summary>
    public DateTimeOffset SampledAt { get; init; }

    /// <summary>
    /// Gets the front-left tyre wear percentage.
    /// </summary>
    public float FrontLeft { get; init; }

    /// <summary>
    /// Gets the front-right tyre wear percentage.
    /// </summary>
    public float FrontRight { get; init; }

    /// <summary>
    /// Gets the rear-left tyre wear percentage.
    /// </summary>
    public float RearLeft { get; init; }

    /// <summary>
    /// Gets the rear-right tyre wear percentage.
    /// </summary>
    public float RearRight { get; init; }
}
