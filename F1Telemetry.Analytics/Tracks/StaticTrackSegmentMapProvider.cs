namespace F1Telemetry.Analytics.Tracks;

/// <summary>
/// Provides stable V3 seed segment maps for the first supported tracks.
/// </summary>
public sealed class StaticTrackSegmentMapProvider : ITrackSegmentMapProvider
{
    private static readonly IReadOnlyDictionary<sbyte, TrackSegmentMap> Maps = new Dictionary<sbyte, TrackSegmentMap>
    {
        [0] = CreateEstimatedMap(
            trackId: 0,
            trackName: "Australia",
            lapLengthMeters: 5_280f,
            CreateSegment("aus-t1-t2", "Turns 1-2", TrackSegmentType.Chicane, 1, 260f, 520f),
            CreateSegment("aus-t3-t4", "Turns 3-4", TrackSegmentType.CornerComplex, 3, 700f, 1_000f),
            CreateSegment("aus-t6-t7", "Turns 6-7", TrackSegmentType.CornerComplex, 6, 1_480f, 1_760f),
            CreateSegment("aus-t9-t10", "Turns 9-10", TrackSegmentType.Chicane, 9, 2_300f, 2_650f),
            CreateSegment("aus-t11-t12", "Turns 11-12", TrackSegmentType.CornerComplex, 11, 3_000f, 3_330f),
            CreateSegment("aus-t13-t14", "Turns 13-14", TrackSegmentType.CornerComplex, 13, 3_750f, 4_080f)),
        [2] = CreateEstimatedMap(
            trackId: 2,
            trackName: "Shanghai",
            lapLengthMeters: 5_450f,
            CreateSegment("sha-t1-t4", "Turns 1-4", TrackSegmentType.CornerComplex, 1, 300f, 960f),
            CreateSegment("sha-t6", "Turn 6", TrackSegmentType.Corner, 6, 1_520f, 1_780f),
            CreateSegment("sha-t7-t8", "Turns 7-8", TrackSegmentType.CornerComplex, 7, 1_980f, 2_350f),
            CreateSegment("sha-t9-t10", "Turns 9-10", TrackSegmentType.CornerComplex, 9, 2_560f, 2_910f),
            CreateSegment("sha-t11-t13", "Turns 11-13", TrackSegmentType.CornerComplex, 11, 3_120f, 3_720f),
            CreateSegment("sha-t14-t16", "Turns 14-16", TrackSegmentType.CornerComplex, 14, 4_360f, 4_950f)),
        [13] = CreateEstimatedMap(
            trackId: 13,
            trackName: "Suzuka",
            lapLengthMeters: 5_810f,
            CreateSegment("suz-t1-t2", "Turns 1-2", TrackSegmentType.CornerComplex, 1, 260f, 620f),
            CreateSegment("suz-esses", "Esses", TrackSegmentType.CornerComplex, 3, 780f, 1_580f),
            CreateSegment("suz-degner", "Degner", TrackSegmentType.CornerComplex, 8, 2_020f, 2_380f),
            CreateSegment("suz-hairpin", "Hairpin", TrackSegmentType.Corner, 11, 2_900f, 3_220f),
            CreateSegment("suz-spoon", "Spoon", TrackSegmentType.CornerComplex, 13, 3_820f, 4_320f),
            CreateSegment("suz-130r", "130R", TrackSegmentType.Corner, 15, 4_940f, 5_250f),
            CreateSegment("suz-casio", "Casio Triangle", TrackSegmentType.Chicane, 16, 5_440f, 5_780f))
    };

    /// <inheritdoc />
    public TrackSegmentMap GetMap(sbyte? trackId)
    {
        if (trackId is null)
        {
            return TrackSegmentMap.CreateUnsupported(null, "No track id was available in the current session state.");
        }

        return Maps.TryGetValue(trackId.Value, out var map)
            ? map
            : TrackSegmentMap.CreateUnsupported(trackId, $"Track id {trackId.Value} is not supported by the static V3 segment map provider.");
    }

    private static TrackSegmentMap CreateEstimatedMap(
        sbyte trackId,
        string trackName,
        float lapLengthMeters,
        params TrackSegment[] segments)
    {
        return new TrackSegmentMap
        {
            TrackId = trackId,
            TrackName = trackName,
            Status = TrackSegmentMapStatus.Estimated,
            StatusReason = "Estimated sparse V3 map with broad corner windows; do not treat distances as official braking or apex markers.",
            LapLengthMeters = lapLengthMeters,
            Segments = segments,
            Confidence = ConfidenceLevel.Low,
            Warnings = new[] { DataQualityWarning.EstimatedTrackMap }
        };
    }

    private static TrackSegment CreateSegment(
        string segmentId,
        string name,
        TrackSegmentType segmentType,
        int cornerNumber,
        float startDistanceMeters,
        float endDistanceMeters)
    {
        return new TrackSegment
        {
            SegmentId = segmentId,
            Name = name,
            SegmentType = segmentType,
            CornerNumber = cornerNumber,
            StartDistanceMeters = startDistanceMeters,
            EndDistanceMeters = endDistanceMeters,
            Confidence = ConfidenceLevel.Low,
            Warnings = new[] { DataQualityWarning.EstimatedTrackMap }
        };
    }
}
