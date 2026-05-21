namespace F1Telemetry.App.TrackMaps;

/// <summary>
/// Holds a normalized single-lap track map generated from player Motion coordinates.
/// </summary>
public sealed record TrackMapSnapshot
{
    /// <summary>
    /// Gets the game session UID that produced the trajectory.
    /// </summary>
    public string SessionUid { get; init; } = string.Empty;

    /// <summary>
    /// Gets the game track id when known.
    /// </summary>
    public int? TrackId { get; init; }

    /// <summary>
    /// Gets the lap number used for the track outline.
    /// </summary>
    public int LapNumber { get; init; }

    /// <summary>
    /// Gets the normalized trajectory points.
    /// </summary>
    public IReadOnlyList<TrackMapPoint> Points { get; init; } = Array.Empty<TrackMapPoint>();

    /// <summary>
    /// Gets the user-facing source label.
    /// </summary>
    public string Source { get; init; } = "Motion 轨迹";

    /// <summary>
    /// Gets the user-facing quality label.
    /// </summary>
    public string Quality { get; init; } = "Low";

    /// <summary>
    /// Gets the structured drawing status for the map.
    /// </summary>
    public TrackMapStatus Status { get; init; } = TrackMapStatus.WaitingMotionData;

    /// <summary>
    /// Gets the warning or empty-state text for this snapshot.
    /// </summary>
    public string WarningText { get; init; } = "等待 Motion 数据";

    /// <summary>
    /// Gets a value indicating whether this snapshot can be drawn.
    /// </summary>
    public bool HasDrawableMap => Points.Count > 0;
}
