using F1Telemetry.Udp.Packets;

namespace F1Telemetry.RawLogAnalyzer;

/// <summary>
/// Contains the stable V1.2-M1 race analysis report skeleton produced from one Race session.
/// </summary>
public sealed record RaceAnalysisReport(
    DateTimeOffset GeneratedAt,
    string InputFile,
    ulong SessionUid,
    RaceSessionSummary SessionSummary,
    PlayerRaceSummary PlayerRaceSummary,
    IReadOnlyList<RaceLapSummary> LapSummaries,
    IReadOnlyList<string> DataQualityWarnings);

/// <summary>
/// Contains basic metadata for the selected Race session.
/// </summary>
public sealed record RaceSessionSummary(
    ulong SessionUid,
    int TrackId,
    int SessionType,
    int TotalLaps,
    int PlayerCarIndex,
    DateTimeOffset? FirstSeenUtc,
    DateTimeOffset? LastSeenUtc,
    long DatagramCount,
    IReadOnlyDictionary<PacketId, long> PacketCounts);

/// <summary>
/// Contains the player's aggregate race result fields available in M1.
/// </summary>
public sealed record PlayerRaceSummary(
    int? GridPosition,
    int? FinalPosition,
    int? CompletedLaps,
    int? Points,
    uint? BestLapTimeInMs,
    int? PenaltiesTimeSeconds,
    int? NumPenalties);

/// <summary>
/// Contains one aggregated lap row without high-frequency telemetry samples.
/// </summary>
public sealed record RaceLapSummary(
    int LapNumber,
    uint? LapTimeInMs,
    int? Position,
    bool? IsValid,
    int? ResultStatus,
    long SampleCount);
