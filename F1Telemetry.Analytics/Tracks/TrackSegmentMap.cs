namespace F1Telemetry.Analytics.Tracks;

/// <summary>
/// Describes the available distance-based segmentation for a single track.
/// </summary>
public sealed record TrackSegmentMap
{
    /// <summary>
    /// Gets the F1 game track identifier, when known.
    /// </summary>
    public sbyte? TrackId { get; init; }

    /// <summary>
    /// Gets the track name associated with the map.
    /// </summary>
    public string TrackName { get; init; } = "Unknown";

    /// <summary>
    /// Gets the support and source status for this map.
    /// </summary>
    public TrackSegmentMapStatus Status { get; init; }

    /// <summary>
    /// Gets a human-readable reason for unsupported or reduced-confidence maps.
    /// </summary>
    public string? StatusReason { get; init; }

    /// <summary>
    /// Gets the approximate lap length in metres, when known.
    /// </summary>
    public float? LapLengthMeters { get; init; }

    /// <summary>
    /// Gets the segment definitions for the track.
    /// </summary>
    public IReadOnlyList<TrackSegment> Segments { get; init; } = Array.Empty<TrackSegment>();

    /// <summary>
    /// Gets the confidence level for the map.
    /// </summary>
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Unknown;

    /// <summary>
    /// Gets data quality warnings that apply to the entire map.
    /// </summary>
    public IReadOnlyList<DataQualityWarning> Warnings { get; init; } = Array.Empty<DataQualityWarning>();

    /// <summary>
    /// Creates an unsupported map result for an unknown or unmapped track.
    /// </summary>
    /// <param name="trackId">The requested track identifier, when known.</param>
    /// <param name="reason">The reason the map is unavailable.</param>
    /// <returns>An unsupported segment map with no segment definitions.</returns>
    public static TrackSegmentMap CreateUnsupported(sbyte? trackId, string reason)
    {
        return new TrackSegmentMap
        {
            TrackId = trackId,
            TrackName = trackId is null ? "Unknown track" : $"Track {trackId.Value}",
            Status = TrackSegmentMapStatus.Unsupported,
            StatusReason = reason,
            LapLengthMeters = null,
            Segments = Array.Empty<TrackSegment>(),
            Confidence = ConfidenceLevel.Unknown,
            Warnings = new[] { DataQualityWarning.UnsupportedTrack }
        };
    }
}
