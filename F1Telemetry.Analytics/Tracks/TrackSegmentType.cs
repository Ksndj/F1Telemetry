namespace F1Telemetry.Analytics.Tracks;

/// <summary>
/// Describes the driving character of a track segment.
/// </summary>
public enum TrackSegmentType
{
    /// <summary>
    /// The segment type is not known.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The segment is primarily a straight or full-throttle zone.
    /// </summary>
    Straight = 1,

    /// <summary>
    /// The segment is a single corner or short bend.
    /// </summary>
    Corner = 2,

    /// <summary>
    /// The segment combines multiple closely connected corners.
    /// </summary>
    CornerComplex = 3,

    /// <summary>
    /// The segment is a chicane or direction-change sequence.
    /// </summary>
    Chicane = 4
}
