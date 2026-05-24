namespace F1Telemetry.Analytics.Tracks;

/// <summary>
/// Normalizes lap distances so wrapped or negative values can be compared against segment windows.
/// </summary>
public static class LapDistanceNormalizer
{
    /// <summary>
    /// Wraps a lap distance into the [0, lapLength) range when a valid lap length is available.
    /// </summary>
    /// <param name="lapDistanceMeters">The raw lap distance in metres.</param>
    /// <param name="lapLengthMeters">The lap length in metres.</param>
    /// <returns>The normalized lap distance, or the raw value when normalization is unavailable.</returns>
    public static float Normalize(float lapDistanceMeters, float? lapLengthMeters)
    {
        if (lapLengthMeters is not > 0 || !float.IsFinite(lapDistanceMeters) || !float.IsFinite(lapLengthMeters.Value))
        {
            return lapDistanceMeters;
        }

        var length = lapLengthMeters.Value;
        var normalized = lapDistanceMeters % length;
        if (normalized < 0)
        {
            normalized += length;
        }

        return normalized >= length ? 0f : normalized;
    }

    /// <summary>
    /// Returns a sortable distance relative to a segment start, preserving wrapped segment order.
    /// </summary>
    /// <param name="lapDistanceMeters">A normalized lap distance in metres.</param>
    /// <param name="segment">The segment used as the ordering window.</param>
    /// <param name="lapLengthMeters">The lap length in metres.</param>
    /// <returns>A segment-relative distance suitable for ordering samples inside the segment.</returns>
    public static float ToSegmentRelativeDistance(float lapDistanceMeters, TrackSegment segment, float? lapLengthMeters)
    {
        ArgumentNullException.ThrowIfNull(segment);

        if (segment.StartDistanceMeters <= segment.EndDistanceMeters || lapLengthMeters is not > 0)
        {
            return lapDistanceMeters;
        }

        return lapDistanceMeters < segment.StartDistanceMeters
            ? lapDistanceMeters + lapLengthMeters.Value
            : lapDistanceMeters;
    }

    /// <summary>
    /// Calculates the shortest absolute distance between two lap positions.
    /// </summary>
    /// <param name="firstDistanceMeters">The first normalized lap distance.</param>
    /// <param name="secondDistanceMeters">The second normalized lap distance.</param>
    /// <param name="lapLengthMeters">The lap length in metres.</param>
    /// <returns>The shortest distance in metres.</returns>
    public static float CircularDistance(float firstDistanceMeters, float secondDistanceMeters, float? lapLengthMeters)
    {
        var direct = Math.Abs(firstDistanceMeters - secondDistanceMeters);
        if (lapLengthMeters is not > 0)
        {
            return direct;
        }

        var length = lapLengthMeters.Value;
        return Math.Min(direct, Math.Abs(length - direct));
    }
}
