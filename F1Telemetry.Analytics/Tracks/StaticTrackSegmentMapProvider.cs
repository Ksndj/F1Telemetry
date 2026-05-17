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
            CreateSegment("suz-casio", "Casio Triangle", TrackSegmentType.Chicane, 16, 5_440f, 5_780f)),
        [26] = CreateEstimatedMap(
            trackId: 26,
            trackName: "Zandvoort",
            lapLengthMeters: 4_259f,
            CreateSegment("zan-t1", "Tarzan", TrackSegmentType.Corner, 1, 220f, 520f),
            CreateSegment("zan-t3", "Hugenholtz", TrackSegmentType.Corner, 3, 760f, 1_080f),
            CreateSegment("zan-t7-t8", "Turns 7-8", TrackSegmentType.CornerComplex, 7, 1_620f, 2_040f),
            CreateSegment("zan-t11-t12", "Hans Ernst", TrackSegmentType.Chicane, 11, 2_780f, 3_160f),
            CreateSegment("zan-t13-t14", "Turns 13-14", TrackSegmentType.CornerComplex, 13, 3_540f, 4_060f)),
        [11] = CreateEstimatedMap(
            trackId: 11,
            trackName: "Monza",
            lapLengthMeters: 5_793f,
            CreateSegment("mon-t1-t2", "Prima Variante", TrackSegmentType.Chicane, 1, 520f, 940f),
            CreateSegment("mon-t4-t5", "Seconda Variante", TrackSegmentType.Chicane, 4, 1_920f, 2_300f),
            CreateSegment("mon-lesmo", "Lesmo", TrackSegmentType.CornerComplex, 6, 2_560f, 3_060f),
            CreateSegment("mon-ascari", "Ascari", TrackSegmentType.Chicane, 8, 4_120f, 4_660f),
            CreateSegment("mon-parabolica", "Parabolica", TrackSegmentType.Corner, 11, 5_120f, 5_680f)),
        [20] = CreateEstimatedMap(
            trackId: 20,
            trackName: "Baku",
            lapLengthMeters: 6_003f,
            CreateSegment("bak-t1-t2", "Turns 1-2", TrackSegmentType.CornerComplex, 1, 260f, 760f),
            CreateSegment("bak-t3-t4", "Turns 3-4", TrackSegmentType.CornerComplex, 3, 1_100f, 1_620f),
            CreateSegment("bak-castle", "Castle", TrackSegmentType.CornerComplex, 8, 2_420f, 3_060f),
            CreateSegment("bak-t15-t16", "Turns 15-16", TrackSegmentType.CornerComplex, 15, 3_820f, 4_420f),
            CreateSegment("bak-t18-t20", "Turns 18-20", TrackSegmentType.CornerComplex, 18, 4_760f, 5_520f)),
        [12] = CreateEstimatedMap(
            trackId: 12,
            trackName: "Singapore",
            lapLengthMeters: 4_940f,
            CreateSegment("sin-t1-t3", "Turns 1-3", TrackSegmentType.CornerComplex, 1, 220f, 720f),
            CreateSegment("sin-t5-t7", "Turns 5-7", TrackSegmentType.CornerComplex, 5, 1_160f, 1_760f),
            CreateSegment("sin-t9-t10", "Turns 9-10", TrackSegmentType.CornerComplex, 9, 2_100f, 2_640f),
            CreateSegment("sin-t14-t16", "Turns 14-16", TrackSegmentType.CornerComplex, 14, 3_260f, 3_920f),
            CreateSegment("sin-t17-t19", "Turns 17-19", TrackSegmentType.CornerComplex, 17, 4_020f, 4_560f)),
        [15] = CreateEstimatedMap(
            trackId: 15,
            trackName: "Austin",
            lapLengthMeters: 5_513f,
            CreateSegment("aus2-t1", "Turn 1", TrackSegmentType.Corner, 1, 260f, 620f),
            CreateSegment("aus2-esses", "Esses", TrackSegmentType.CornerComplex, 3, 1_080f, 2_020f),
            CreateSegment("aus2-t11", "Turn 11", TrackSegmentType.Corner, 11, 2_520f, 2_900f),
            CreateSegment("aus2-stadium", "Stadium", TrackSegmentType.CornerComplex, 12, 3_780f, 4_520f),
            CreateSegment("aus2-t19-t20", "Turns 19-20", TrackSegmentType.CornerComplex, 19, 4_820f, 5_360f)),
        [19] = CreateEstimatedMap(
            trackId: 19,
            trackName: "Mexico",
            lapLengthMeters: 4_304f,
            CreateSegment("mex-t1-t3", "Turns 1-3", TrackSegmentType.CornerComplex, 1, 460f, 980f),
            CreateSegment("mex-t4-t5", "Turns 4-5", TrackSegmentType.CornerComplex, 4, 1_360f, 1_740f),
            CreateSegment("mex-esses", "Esses", TrackSegmentType.CornerComplex, 7, 2_160f, 2_920f),
            CreateSegment("mex-stadium", "Stadium", TrackSegmentType.CornerComplex, 12, 3_220f, 3_820f),
            CreateSegment("mex-peraltada", "Peraltada", TrackSegmentType.Corner, 17, 3_880f, 4_220f)),
        [16] = CreateEstimatedMap(
            trackId: 16,
            trackName: "Brazil",
            lapLengthMeters: 4_309f,
            CreateSegment("bra-senna-s", "Senna S", TrackSegmentType.Chicane, 1, 220f, 700f),
            CreateSegment("bra-t4", "Descida do Lago", TrackSegmentType.Corner, 4, 1_260f, 1_620f),
            CreateSegment("bra-t6-t8", "Ferradura", TrackSegmentType.CornerComplex, 6, 1_820f, 2_360f),
            CreateSegment("bra-t10-t12", "Turns 10-12", TrackSegmentType.CornerComplex, 10, 2_700f, 3_280f),
            CreateSegment("bra-t13-t15", "Subida dos Boxes", TrackSegmentType.CornerComplex, 13, 3_460f, 4_120f)),
        [31] = CreateEstimatedMap(
            trackId: 31,
            trackName: "Las Vegas",
            lapLengthMeters: 6_201f,
            CreateSegment("veg-t1-t4", "Turns 1-4", TrackSegmentType.CornerComplex, 1, 360f, 1_120f),
            CreateSegment("veg-t5-t7", "Turns 5-7", TrackSegmentType.CornerComplex, 5, 1_420f, 2_040f),
            CreateSegment("veg-t12-t14", "Turns 12-14", TrackSegmentType.CornerComplex, 12, 3_680f, 4_340f),
            CreateSegment("veg-t16-t17", "Turns 16-17", TrackSegmentType.CornerComplex, 16, 5_160f, 5_760f)),
        [32] = CreateEstimatedMap(
            trackId: 32,
            trackName: "Qatar",
            lapLengthMeters: 5_419f,
            CreateSegment("qat-t1", "Turn 1", TrackSegmentType.Corner, 1, 300f, 660f),
            CreateSegment("qat-t4-t6", "Turns 4-6", TrackSegmentType.CornerComplex, 4, 1_300f, 1_920f),
            CreateSegment("qat-t7-t10", "Turns 7-10", TrackSegmentType.CornerComplex, 7, 2_080f, 2_920f),
            CreateSegment("qat-t12-t14", "Turns 12-14", TrackSegmentType.CornerComplex, 12, 3_420f, 4_040f),
            CreateSegment("qat-t15-t16", "Turns 15-16", TrackSegmentType.CornerComplex, 15, 4_560f, 5_180f)),
        [14] = CreateEstimatedMap(
            trackId: 14,
            trackName: "Abu Dhabi",
            lapLengthMeters: 5_281f,
            CreateSegment("abu-t1-t3", "Turns 1-3", TrackSegmentType.CornerComplex, 1, 260f, 880f),
            CreateSegment("abu-t5-t7", "Turns 5-7", TrackSegmentType.CornerComplex, 5, 1_360f, 2_020f),
            CreateSegment("abu-t9", "Turn 9", TrackSegmentType.Corner, 9, 2_920f, 3_320f),
            CreateSegment("abu-t12-t14", "Turns 12-14", TrackSegmentType.CornerComplex, 12, 3_720f, 4_320f),
            CreateSegment("abu-t15-t16", "Turns 15-16", TrackSegmentType.CornerComplex, 15, 4_520f, 4_980f))
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
