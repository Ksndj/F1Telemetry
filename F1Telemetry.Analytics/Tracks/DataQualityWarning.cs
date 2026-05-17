namespace F1Telemetry.Analytics.Tracks;

/// <summary>
/// Lists non-fatal data quality issues that can reduce analytics confidence.
/// </summary>
public enum DataQualityWarning
{
    /// <summary>
    /// The current track is not supported by the selected segment map provider.
    /// </summary>
    UnsupportedTrack = 0,

    /// <summary>
    /// No usable lap samples were available for the requested calculation.
    /// </summary>
    MissingSamples = 1,

    /// <summary>
    /// Available samples are too sparse for precise corner-level values.
    /// </summary>
    LowSampleDensity = 2,

    /// <summary>
    /// No reference or best lap samples were supplied for comparison.
    /// </summary>
    MissingReferenceLap = 3,

    /// <summary>
    /// The active segment map is estimated rather than verified from official telemetry.
    /// </summary>
    EstimatedTrackMap = 4,

    /// <summary>
    /// One or more samples are missing lap-distance data.
    /// </summary>
    MissingLapDistance = 5,

    /// <summary>
    /// One or more samples are missing lap-time data.
    /// </summary>
    MissingTimingSamples = 6,

    /// <summary>
    /// One or more samples are missing speed data.
    /// </summary>
    MissingSpeedSamples = 7,

    /// <summary>
    /// One or more samples are missing throttle data.
    /// </summary>
    MissingThrottleSamples = 8,

    /// <summary>
    /// One or more samples are missing brake data.
    /// </summary>
    MissingBrakeSamples = 9,

    /// <summary>
    /// One or more samples are missing steering data.
    /// </summary>
    MissingSteeringSamples = 10
}
