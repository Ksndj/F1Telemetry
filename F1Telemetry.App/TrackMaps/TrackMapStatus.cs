namespace F1Telemetry.App.TrackMaps;

/// <summary>
/// Describes whether the corner-analysis track map can be drawn and why it may be empty.
/// </summary>
public enum TrackMapStatus
{
    /// <summary>
    /// Motion coordinates may still arrive for the active session.
    /// </summary>
    WaitingMotionData,

    /// <summary>
    /// The selected historical session does not contain Motion coordinates.
    /// </summary>
    MissingMotionData,

    /// <summary>
    /// Motion coordinates exist but are too sparse to draw a trustworthy map.
    /// </summary>
    InsufficientTrackPoints,

    /// <summary>
    /// A track outline exists but the selected corner has no usable distance range.
    /// </summary>
    MissingCornerRange,

    /// <summary>
    /// Motion coordinates are ready for drawing.
    /// </summary>
    Ready
}
