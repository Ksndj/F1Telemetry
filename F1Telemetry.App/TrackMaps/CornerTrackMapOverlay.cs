namespace F1Telemetry.App.TrackMaps;

/// <summary>
/// Describes the highlighted corner section on top of a normalized track map.
/// </summary>
public sealed record CornerTrackMapOverlay
{
    /// <summary>
    /// Gets the displayed corner name.
    /// </summary>
    public string CornerName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the highlighted corner start distance when known.
    /// </summary>
    public float? StartLapDistance { get; init; }

    /// <summary>
    /// Gets the highlighted corner end distance when known.
    /// </summary>
    public float? EndLapDistance { get; init; }

    /// <summary>
    /// Gets the highlighted normalized points.
    /// </summary>
    public IReadOnlyList<TrackMapPoint> HighlightPoints { get; init; } = Array.Empty<TrackMapPoint>();

    /// <summary>
    /// Gets the normalized marker X coordinate when available.
    /// </summary>
    public double? MarkerX { get; init; }

    /// <summary>
    /// Gets the normalized marker Y coordinate when available.
    /// </summary>
    public double? MarkerY { get; init; }

    /// <summary>
    /// Gets a value indicating whether the highlighted position is estimated.
    /// </summary>
    public bool IsEstimated { get; init; }

    /// <summary>
    /// Gets the warning or empty-state text for the overlay.
    /// </summary>
    public string WarningText { get; init; } = string.Empty;
}
