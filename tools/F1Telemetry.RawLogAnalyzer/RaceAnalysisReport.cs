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
    IReadOnlyList<StintSummary> StintSummaries,
    IReadOnlyList<PitStopSummary> PitStopSummaries,
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

/// <summary>
/// Describes the confidence level shared by race-derived analysis summaries.
/// </summary>
public enum RaceAnalysisConfidence
{
    High,
    Medium,
    Low
}

/// <summary>
/// Describes which evidence source produced a stint summary.
/// </summary>
public enum StintSummarySource
{
    SessionHistory,
    FinalClassification,
    CompoundChangeInference,
    PitStopInference,
    Unknown
}

/// <summary>
/// Contains one aggregated tyre stint without high-frequency telemetry samples.
/// </summary>
public sealed record StintSummary(
    int StintIndex,
    int StartLap,
    int EndLap,
    int LapCount,
    int? ActualTyreCompound,
    int? VisualTyreCompound,
    int? StartTyreAge,
    int? EndTyreAge,
    StintSummarySource Source,
    RaceAnalysisConfidence Confidence,
    string Notes);

/// <summary>
/// Contains one aggregated pit stop candidate with confidence and no raw packet payload.
/// </summary>
public sealed record PitStopSummary(
    int PitLap,
    uint? EntryLapTimeInMs,
    uint? ExitLapTimeInMs,
    string? CompoundBefore,
    string? CompoundAfter,
    int? TyreAgeBefore,
    int? TyreAgeAfter,
    int? PositionBefore,
    int? PositionAfter,
    int? PositionLost,
    int? EstimatedPitLossInMs,
    RaceAnalysisConfidence Confidence,
    string Notes);
