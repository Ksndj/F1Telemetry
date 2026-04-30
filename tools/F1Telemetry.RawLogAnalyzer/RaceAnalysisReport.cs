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
    IReadOnlyList<TyreUsageSummary> TyreUsageSummaries,
    FuelTrendSummary FuelTrendSummary,
    ErsTrendSummary ErsTrendSummary,
    GapTrendSummary GapTrendSummary,
    IReadOnlyList<RaceEventTimelineEntry> RaceEventTimeline,
    AiRaceSummary AiRaceSummary,
    string AiInputPreview,
    IReadOnlyList<string> RaceAdviceQuestions,
    IReadOnlyList<string> DataQualityForAi,
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
/// Describes coarse risk levels for race trend summaries.
/// </summary>
public enum RaceTrendRisk
{
    Unknown,
    Low,
    Medium,
    High
}

/// <summary>
/// Describes confidence levels used only by M4 gap-derived summaries.
/// </summary>
public enum GapAnalysisConfidence
{
    Unknown,
    Low,
    Medium,
    High
}

/// <summary>
/// Describes the type of compact gap window represented in the report.
/// </summary>
public enum GapWindowType
{
    Attack,
    Defense
}

/// <summary>
/// Describes the compact traffic-impact category for one lap.
/// </summary>
public enum TrafficImpactType
{
    FrontTraffic,
    RearPressure,
    Sandwich
}

/// <summary>
/// Describes key race timeline event categories emitted by the offline analyzer.
/// </summary>
public enum RaceEventTimelineType
{
    Start,
    PitStop,
    TyreChange,
    YellowFlag,
    SafetyCar,
    VirtualSafetyCar,
    RedFlag,
    Overtake,
    PositionLost,
    InvalidLap,
    LowFuel,
    HighTyreWear,
    LowErs,
    Penalty,
    Retirement,
    RaceWinner,
    FinalClassification
}

/// <summary>
/// Describes the severity assigned to one timeline event.
/// </summary>
public enum RaceEventTimelineSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// Describes the source that produced one timeline event.
/// </summary>
public enum RaceEventTimelineSource
{
    UdpEvent,
    SessionStatus,
    DerivedSummary
}

/// <summary>
/// Contains a compact aggregate race summary intended for offline AI review input.
/// </summary>
public sealed record AiRaceSummary(
    string TrackName,
    int TrackId,
    int SessionType,
    bool IsRaceSession,
    int TotalLaps,
    int? CompletedLaps,
    int? GridPosition,
    int? FinalPosition,
    int? PositionGain,
    uint? BestLapTimeInMs,
    double? AverageLapTimeInMs,
    int PitStopCount,
    int SafetyCarCount,
    int VirtualSafetyCarCount,
    int RedFlagCount,
    IReadOnlyList<string> Stints,
    IReadOnlyList<string> Trends,
    IReadOnlyList<string> KeyEvents,
    IReadOnlyList<string> DataQualityLimitations,
    bool IsTruncated);

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

/// <summary>
/// Contains one aggregate tyre usage row for an already identified stint.
/// </summary>
public sealed record TyreUsageSummary(
    int StintIndex,
    int StartLap,
    int EndLap,
    int LapCount,
    int? ActualTyreCompound,
    int? VisualTyreCompound,
    int? StartTyreAge,
    int? EndTyreAge,
    float? StartWearPercent,
    float? EndWearPercent,
    float? MaxWearPercent,
    float? WearDeltaPercent,
    float? AverageWearPerLapPercent,
    int ObservedLapCount,
    RaceTrendRisk Risk,
    RaceAnalysisConfidence Confidence,
    string Notes);

/// <summary>
/// Contains aggregate player fuel trends using kilogram field names only.
/// </summary>
public sealed record FuelTrendSummary(
    float? StartFuelKg,
    float? EndFuelKg,
    float? MinFuelKg,
    float? MaxFuelKg,
    float? FuelUsedKg,
    float? AverageFuelPerLapKg,
    float? StartFuelRemainingLaps,
    float? EndFuelRemainingLaps,
    float? MinFuelRemainingLaps,
    int ObservedLapCount,
    RaceTrendRisk Risk,
    RaceAnalysisConfidence Confidence,
    string Notes);

/// <summary>
/// Contains aggregate player ERS trends converted from joules to megajoules.
/// </summary>
public sealed record ErsTrendSummary(
    float? StartStoreEnergyMJ,
    float? EndStoreEnergyMJ,
    float? MinStoreEnergyMJ,
    float? MaxStoreEnergyMJ,
    float? NetStoreEnergyDeltaMJ,
    float? AverageHarvestedPerLapMJ,
    float? AverageDeployedPerLapMJ,
    int? LastDeployMode,
    int LowErsLapCount,
    int HighUsageLaps,
    int RecoveryLaps,
    int ObservedLapCount,
    RaceTrendRisk Risk,
    RaceAnalysisConfidence Confidence,
    string Notes);

/// <summary>
/// Contains aggregate race gap trends using millisecond fields internally.
/// </summary>
public sealed record GapTrendSummary(
    int ObservedLapCount,
    int AttackWindowLapCount,
    int DefenseWindowLapCount,
    int TrafficImpactLapCount,
    uint? MinGapFrontMs,
    double? AverageGapFrontMs,
    uint? MinGapBehindMs,
    double? AverageGapBehindMs,
    IReadOnlyList<GapWindowSummary> AttackWindows,
    IReadOnlyList<GapWindowSummary> DefenseWindows,
    IReadOnlyList<TrafficImpactLapSummary> TrafficImpactLaps,
    GapAnalysisConfidence Confidence,
    string Notes);

/// <summary>
/// Contains one contiguous attack or defense candidate window without raw lap arrays.
/// </summary>
public sealed record GapWindowSummary(
    GapWindowType WindowType,
    int StartLap,
    int EndLap,
    int LapCount,
    uint? MinGapFrontMs,
    double? AverageGapFrontMs,
    uint? MinGapBehindMs,
    double? AverageGapBehindMs,
    int? StartPosition,
    int? EndPosition,
    GapAnalysisConfidence Confidence,
    string Notes);

/// <summary>
/// Contains one compact traffic-impact lap with millisecond gap fields.
/// </summary>
public sealed record TrafficImpactLapSummary(
    int LapNumber,
    int? Position,
    uint? GapFrontMs,
    uint? GapBehindMs,
    TrafficImpactType ImpactType,
    GapAnalysisConfidence Confidence,
    string Notes);

/// <summary>
/// Contains one key race timeline event without raw UDP payload data.
/// </summary>
public sealed record RaceEventTimelineEntry(
    int Lap,
    DateTimeOffset? TimestampUtc,
    RaceEventTimelineType EventType,
    RaceEventTimelineSeverity Severity,
    RaceEventTimelineSource Source,
    string Message,
    int? RelatedVehicleIndex,
    RaceAnalysisConfidence Confidence);
