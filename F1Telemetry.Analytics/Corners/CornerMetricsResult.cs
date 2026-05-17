using F1Telemetry.Analytics.Tracks;

namespace F1Telemetry.Analytics.Corners;

/// <summary>
/// Represents the output of corner metrics extraction for a lap.
/// </summary>
public sealed record CornerMetricsResult
{
    /// <summary>
    /// Gets the track id used for extraction, when known.
    /// </summary>
    public sbyte? TrackId { get; init; }

    /// <summary>
    /// Gets the track name from the selected segment map.
    /// </summary>
    public string TrackName { get; init; } = "Unknown";

    /// <summary>
    /// Gets the support and source status of the selected map.
    /// </summary>
    public TrackSegmentMapStatus MapStatus { get; init; }

    /// <summary>
    /// Gets the confidence level for the extraction result.
    /// </summary>
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Unknown;

    /// <summary>
    /// Gets extracted corner summaries.
    /// </summary>
    public IReadOnlyList<CornerSummary> Corners { get; init; } = Array.Empty<CornerSummary>();

    /// <summary>
    /// Gets data quality warnings that apply to the extraction result.
    /// </summary>
    public IReadOnlyList<DataQualityWarning> Warnings { get; init; } = Array.Empty<DataQualityWarning>();
}
