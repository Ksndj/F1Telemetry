namespace F1Telemetry.Analytics.Tracks;

/// <summary>
/// Describes how much confidence downstream analytics should place in a computed result.
/// </summary>
public enum ConfidenceLevel
{
    /// <summary>
    /// Confidence cannot be assigned because required data is unavailable.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The result is based on sparse, estimated, or incomplete data.
    /// </summary>
    Low = 1,

    /// <summary>
    /// The result is based on enough data for directional guidance, but not precise comparison.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// The result is based on dense data and a verified map.
    /// </summary>
    High = 3
}
