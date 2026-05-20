namespace F1Telemetry.App.TrackMaps;

/// <summary>
/// Represents a player-car trajectory point projected into normalized track-map coordinates.
/// </summary>
public sealed record TrackMapPoint
{
    /// <summary>
    /// Gets the lap distance in metres.
    /// </summary>
    public float LapDistance { get; init; }

    /// <summary>
    /// Gets the raw world X coordinate.
    /// </summary>
    public float X { get; init; }

    /// <summary>
    /// Gets the raw world Z coordinate.
    /// </summary>
    public float Z { get; init; }

    /// <summary>
    /// Gets the normalized X coordinate in the 0..1 UI range.
    /// </summary>
    public double NormalizedX { get; init; }

    /// <summary>
    /// Gets the normalized Y coordinate in the 0..1 UI range.
    /// </summary>
    public double NormalizedY { get; init; }
}
