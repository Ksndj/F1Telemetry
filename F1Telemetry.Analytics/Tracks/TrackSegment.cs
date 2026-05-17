namespace F1Telemetry.Analytics.Tracks;

/// <summary>
/// Describes a named distance window on a racing circuit.
/// </summary>
public sealed record TrackSegment
{
    /// <summary>
    /// Gets the stable identifier for this segment.
    /// </summary>
    public string SegmentId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user-facing segment name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the segment type.
    /// </summary>
    public TrackSegmentType SegmentType { get; init; }

    /// <summary>
    /// Gets the optional primary corner number associated with the segment.
    /// </summary>
    public int? CornerNumber { get; init; }

    /// <summary>
    /// Gets the estimated segment start distance in metres from the start line.
    /// </summary>
    public float StartDistanceMeters { get; init; }

    /// <summary>
    /// Gets the estimated segment end distance in metres from the start line.
    /// </summary>
    public float EndDistanceMeters { get; init; }

    /// <summary>
    /// Gets the confidence level for this segment definition.
    /// </summary>
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Unknown;

    /// <summary>
    /// Gets data quality warnings that apply to this segment definition.
    /// </summary>
    public IReadOnlyList<DataQualityWarning> Warnings { get; init; } = Array.Empty<DataQualityWarning>();

    /// <summary>
    /// Returns a value indicating whether the supplied lap distance is inside this segment.
    /// </summary>
    /// <param name="lapDistanceMeters">Lap distance in metres from the start line.</param>
    /// <returns><c>true</c> when the distance belongs to the segment; otherwise, <c>false</c>.</returns>
    public bool ContainsDistance(float lapDistanceMeters)
    {
        if (StartDistanceMeters <= EndDistanceMeters)
        {
            return lapDistanceMeters >= StartDistanceMeters && lapDistanceMeters <= EndDistanceMeters;
        }

        return lapDistanceMeters >= StartDistanceMeters || lapDistanceMeters <= EndDistanceMeters;
    }
}
