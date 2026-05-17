namespace F1Telemetry.Analytics.Tracks;

/// <summary>
/// Describes whether a track segment map can be used and how it was sourced.
/// </summary>
public enum TrackSegmentMapStatus
{
    /// <summary>
    /// No usable map is available for the track.
    /// </summary>
    Unsupported = 0,

    /// <summary>
    /// The map is a stable estimated map with broad segment windows.
    /// </summary>
    Estimated = 1,

    /// <summary>
    /// The map is verified against a trusted source.
    /// </summary>
    Verified = 2
}
