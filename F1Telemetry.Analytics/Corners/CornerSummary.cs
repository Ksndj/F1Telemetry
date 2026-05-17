using F1Telemetry.Analytics.Tracks;

namespace F1Telemetry.Analytics.Corners;

/// <summary>
/// Summarizes driver inputs and timing for one corner segment.
/// </summary>
public sealed record CornerSummary
{
    /// <summary>
    /// Gets the track segment represented by this corner summary.
    /// </summary>
    public TrackSegment Segment { get; init; } = new();

    /// <summary>
    /// Gets the speed at the first usable sample in the segment.
    /// </summary>
    public double? EntrySpeedKph { get; init; }

    /// <summary>
    /// Gets the lowest speed observed in the segment.
    /// </summary>
    public double? MinSpeedKph { get; init; }

    /// <summary>
    /// Gets the speed at the last usable sample in the segment.
    /// </summary>
    public double? ExitSpeedKph { get; init; }

    /// <summary>
    /// Gets the maximum brake input observed in the segment.
    /// </summary>
    public double? MaxBrake { get; init; }

    /// <summary>
    /// Gets the first distance where throttle is reapplied after the slowest point.
    /// </summary>
    public float? ThrottleReapplyDistanceMeters { get; init; }

    /// <summary>
    /// Gets the maximum absolute steering input observed in the segment.
    /// </summary>
    public double? MaxSteering { get; init; }

    /// <summary>
    /// Gets the observed segment duration in milliseconds.
    /// </summary>
    public int? SegmentTimeInMs { get; init; }

    /// <summary>
    /// Gets the reference or best-lap segment duration in milliseconds, when supplied.
    /// </summary>
    public int? ReferenceSegmentTimeInMs { get; init; }

    /// <summary>
    /// Gets the time lost to the reference or best lap in milliseconds, when supplied.
    /// </summary>
    public int? TimeLossToReferenceInMs { get; init; }

    /// <summary>
    /// Gets the confidence level for this corner summary.
    /// </summary>
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Unknown;

    /// <summary>
    /// Gets data quality warnings that apply to this corner summary.
    /// </summary>
    public IReadOnlyList<DataQualityWarning> Warnings { get; init; } = Array.Empty<DataQualityWarning>();
}
