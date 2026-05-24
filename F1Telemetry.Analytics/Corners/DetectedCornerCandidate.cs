using F1Telemetry.Analytics.Tracks;

namespace F1Telemetry.Analytics.Corners;

/// <summary>
/// Describes an automatically detected corner window derived from lap samples.
/// </summary>
public sealed record DetectedCornerCandidate
{
    /// <summary>
    /// Gets the stable detected segment identifier.
    /// </summary>
    public string SegmentId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user-facing detected corner name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the primary corner number when known.
    /// </summary>
    public int? CornerNumber { get; init; }

    /// <summary>
    /// Gets a compact corner label when known.
    /// </summary>
    public string CornerLabel { get; init; } = string.Empty;

    /// <summary>
    /// Gets the detected segment type.
    /// </summary>
    public TrackSegmentType SegmentType { get; init; }

    /// <summary>
    /// Gets the detected start distance in metres.
    /// </summary>
    public float StartDistanceMeters { get; init; }

    /// <summary>
    /// Gets the detected end distance in metres.
    /// </summary>
    public float EndDistanceMeters { get; init; }

    /// <summary>
    /// Gets the confidence level for the detected window.
    /// </summary>
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Unknown;

    /// <summary>
    /// Gets warnings that explain reduced confidence.
    /// </summary>
    public IReadOnlyList<DataQualityWarning> Warnings { get; init; } = Array.Empty<DataQualityWarning>();

    /// <summary>
    /// Converts the detected candidate to the existing track segment abstraction.
    /// </summary>
    /// <returns>A track segment that can be reused by corner metrics and map overlays.</returns>
    public TrackSegment ToTrackSegment()
    {
        return new TrackSegment
        {
            SegmentId = SegmentId,
            Name = DisplayName,
            SegmentType = SegmentType,
            CornerNumber = CornerNumber,
            StartDistanceMeters = StartDistanceMeters,
            EndDistanceMeters = EndDistanceMeters,
            Confidence = Confidence,
            Warnings = Warnings
        };
    }
}
