using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.Strategy;
using F1Telemetry.Core.Models;

namespace F1Telemetry.AI.Models;

/// <summary>
/// Defines race-assistant modes used to constrain strategy question handling.
/// </summary>
public enum RaceAssistantMode
{
    /// <summary>No live telemetry is connected.</summary>
    NoTelemetry,

    /// <summary>Waiting for enough live telemetry to answer with race context.</summary>
    WaitingForTelemetry,

    /// <summary>Practice-oriented advice.</summary>
    Practice,

    /// <summary>Qualifying preparation advice before a push lap.</summary>
    QualifyingPrep,

    /// <summary>Qualifying push-lap advice.</summary>
    QualifyingPush,

    /// <summary>Opening-lap race advice.</summary>
    RaceOpening,

    /// <summary>Normal race stint-management advice.</summary>
    RaceStintManagement,

    /// <summary>Advice while approaching the pit window.</summary>
    PitWindowApproaching,

    /// <summary>Advice while inside the pit window.</summary>
    InPitWindow,

    /// <summary>Full safety-car advice.</summary>
    SafetyCar,

    /// <summary>Virtual-safety-car advice.</summary>
    VirtualSafetyCar,

    /// <summary>Red-flag advice.</summary>
    RedFlag,

    /// <summary>Final-laps advice.</summary>
    FinalLaps,

    /// <summary>Post-race review advice.</summary>
    PostRace
}

/// <summary>
/// Defines the recognized driver-question intent.
/// </summary>
public enum VoiceQuestionIntent
{
    /// <summary>Tyre status question.</summary>
    TYRE_STATUS,

    /// <summary>Pit decision question.</summary>
    PIT_DECISION,

    /// <summary>Setup or driving feedback question.</summary>
    SETUP_FEEDBACK,

    /// <summary>Gap, attack, or defense question.</summary>
    GAP_ANALYSIS,

    /// <summary>ERS usage question.</summary>
    ERS_STRATEGY,

    /// <summary>General race-state question.</summary>
    GENERAL_STATUS,

    /// <summary>Corner-level driving question.</summary>
    CORNER_ANALYSIS,

    /// <summary>Damage status question.</summary>
    DAMAGE_STATUS,

    /// <summary>Unknown or unsupported question.</summary>
    UNKNOWN
}

/// <summary>
/// Defines structured assistant advice categories.
/// </summary>
public enum RaceAssistantAdviceType
{
    /// <summary>Pit-window advice.</summary>
    PitWindow,

    /// <summary>Tyre-management advice.</summary>
    TyreManagement,

    /// <summary>Fuel-saving advice.</summary>
    FuelSaving,

    /// <summary>ERS-management advice.</summary>
    ErsManagement,

    /// <summary>Attack advice.</summary>
    Attack,

    /// <summary>Defense advice.</summary>
    Defense,

    /// <summary>Undercut advice.</summary>
    Undercut,

    /// <summary>Overcut advice.</summary>
    Overcut,

    /// <summary>Safety-car advice.</summary>
    SafetyCar,

    /// <summary>Setup-feedback advice.</summary>
    SetupFeedback,

    /// <summary>Corner-specific advice.</summary>
    Corner,

    /// <summary>Damage advice.</summary>
    Damage,

    /// <summary>General status advice.</summary>
    GeneralStatus,

    /// <summary>Post-race review advice.</summary>
    PostRaceReview,

    /// <summary>Unknown advice.</summary>
    Unknown
}

/// <summary>
/// Defines coarse strategy confidence bands.
/// </summary>
public enum StrategyAdviceConfidence
{
    /// <summary>Low confidence.</summary>
    Low,

    /// <summary>Medium confidence.</summary>
    Medium,

    /// <summary>High confidence.</summary>
    High
}

/// <summary>
/// Carries one rule-derived strategy signal.
/// </summary>
public sealed record StrategyRuleSignal
{
    /// <summary>Gets the signal name.</summary>
    public string SignalType { get; init; } = string.Empty;

    /// <summary>Gets the advice category supported by this signal.</summary>
    public RaceAssistantAdviceType AdviceType { get; init; } = RaceAssistantAdviceType.Unknown;

    /// <summary>Gets the supported summary.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Gets the recommended action supported by this signal.</summary>
    public string RecommendedAction { get; init; } = string.Empty;

    /// <summary>Gets the confidence band.</summary>
    public StrategyAdviceConfidence Confidence { get; init; } = StrategyAdviceConfidence.Low;

    /// <summary>Gets the risk level.</summary>
    public StrategyRiskLevel RiskLevel { get; init; } = StrategyRiskLevel.Unknown;

    /// <summary>Gets the required data fields for the signal.</summary>
    public IReadOnlyList<string> RequiredData { get; init; } = Array.Empty<string>();

    /// <summary>Gets missing required data fields.</summary>
    public IReadOnlyList<string> MissingData { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Carries pit-decision evidence for normal green-flag strategy.
/// </summary>
public sealed record PitDecisionInputs
{
    /// <summary>Gets the current lap number.</summary>
    public int? CurrentLap { get; init; }

    /// <summary>Gets total race laps.</summary>
    public int? TotalLaps { get; init; }

    /// <summary>Gets remaining race laps.</summary>
    public int? RemainingLaps { get; init; }

    /// <summary>Gets the current tyre age.</summary>
    public int? TyreAgeLaps { get; init; }

    /// <summary>Gets average current tyre wear percentage.</summary>
    public float? TyreWearPercent { get; init; }

    /// <summary>Gets recent lap trend evidence.</summary>
    public RecentLapTrendSummary RecentTrend { get; init; } = new();

    /// <summary>Gets the available tyre inventory summary.</summary>
    public string TyreInventorySummary { get; init; } = string.Empty;

    /// <summary>Gets the gap to the car ahead in milliseconds.</summary>
    public double? GapToFrontMs { get; init; }

    /// <summary>Gets the gap to the car behind in milliseconds.</summary>
    public double? GapToBehindMs { get; init; }

    /// <summary>Gets the estimated pit-loss time in milliseconds.</summary>
    public double? EstimatedPitLossMs { get; init; }

    /// <summary>Gets the safety-car status when known.</summary>
    public byte? SafetyCarStatus { get; init; }

    /// <summary>Gets missing required fields.</summary>
    public IReadOnlyList<string> MissingData { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Carries normal pit-decision rule output.
/// </summary>
public sealed record PitDecisionSignal
{
    /// <summary>Gets the pit-decision inputs.</summary>
    public PitDecisionInputs Inputs { get; init; } = new();

    /// <summary>Gets the strategy signal.</summary>
    public StrategyRuleSignal Signal { get; init; } = new();
}

/// <summary>
/// Carries safety-car pit-opportunity evidence.
/// </summary>
public sealed record SafetyCarPitOpportunityInputs
{
    /// <summary>Gets whether the car has already passed pit entry.</summary>
    public bool? HasPassedPitEntry { get; init; }

    /// <summary>Gets current tyre age.</summary>
    public int? TyreAgeLaps { get; init; }

    /// <summary>Gets average current tyre wear percentage.</summary>
    public float? TyreWearPercent { get; init; }

    /// <summary>Gets remaining race laps.</summary>
    public int? RemainingLaps { get; init; }

    /// <summary>Gets available tyre inventory summary.</summary>
    public string TyreInventorySummary { get; init; } = string.Empty;

    /// <summary>Gets estimated pit-exit traffic summary.</summary>
    public string PitExitTrafficSummary { get; init; } = string.Empty;

    /// <summary>Gets current race position.</summary>
    public int? CurrentPosition { get; init; }

    /// <summary>Gets missing required fields.</summary>
    public IReadOnlyList<string> MissingData { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Carries safety-car pit-opportunity rule output.
/// </summary>
public sealed record SafetyCarPitOpportunitySignal
{
    /// <summary>Gets the safety-car pit inputs.</summary>
    public SafetyCarPitOpportunityInputs Inputs { get; init; } = new();

    /// <summary>Gets the strategy signal.</summary>
    public StrategyRuleSignal Signal { get; init; } = new();
}

/// <summary>
/// Summarizes recent completed-lap trends without exposing raw samples.
/// </summary>
public sealed record RecentLapTrendSummary
{
    /// <summary>Gets the number of laps included.</summary>
    public int LapCount { get; init; }

    /// <summary>Gets the lap-time trend text.</summary>
    public string LapTimeTrend { get; init; } = "n/a";

    /// <summary>Gets the tyre-wear trend text.</summary>
    public string TyreWearTrend { get; init; } = "n/a";

    /// <summary>Gets the fuel trend text.</summary>
    public string FuelTrend { get; init; } = "n/a";

    /// <summary>Gets the ERS trend text.</summary>
    public string ErsTrend { get; init; } = "n/a";

    /// <summary>Gets the average tyre-wear delta per lap.</summary>
    public double? AverageTyreWearDeltaPerLap { get; init; }

    /// <summary>Gets the average fuel used per lap.</summary>
    public double? AverageFuelUsedLitres { get; init; }

    /// <summary>Gets the average ERS used per lap.</summary>
    public double? AverageErsUsed { get; init; }

    /// <summary>Gets missing trend fields.</summary>
    public IReadOnlyList<string> MissingData { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Describes whether the assistant snapshot is complete enough for confident advice.
/// </summary>
public sealed record SnapshotQuality
{
    /// <summary>Gets a value indicating whether the snapshot is stale.</summary>
    public bool IsStale { get; init; }

    /// <summary>Gets snapshot age in seconds when known.</summary>
    public int? AgeSeconds { get; init; }

    /// <summary>Gets missing key fields.</summary>
    public IReadOnlyList<string> MissingData { get; init; } = Array.Empty<string>();

    /// <summary>Gets the maximum recommended confidence after quality gates.</summary>
    public StrategyAdviceConfidence MaxRecommendedConfidence { get; init; } = StrategyAdviceConfidence.High;

    /// <summary>Gets compact quality text.</summary>
    public string Summary { get; init; } = string.Empty;
}

/// <summary>
/// Carries a compressed race-assistant snapshot.
/// </summary>
public sealed record RaceAssistantSnapshot
{
    /// <summary>Gets the active session UID.</summary>
    public ulong? SessionUid { get; init; }

    /// <summary>Gets the resolved assistant mode.</summary>
    public RaceAssistantMode Mode { get; init; } = RaceAssistantMode.NoTelemetry;

    /// <summary>Gets the high-level session mode.</summary>
    public SessionMode SessionMode { get; init; } = SessionMode.Unknown;

    /// <summary>Gets the current lap number.</summary>
    public int? CurrentLap { get; init; }

    /// <summary>Gets total laps.</summary>
    public int? TotalLaps { get; init; }

    /// <summary>Gets current player position.</summary>
    public int? Position { get; init; }

    /// <summary>Gets current tyre label.</summary>
    public string CurrentTyre { get; init; } = "-";

    /// <summary>Gets current tyre age.</summary>
    public int? TyreAgeLaps { get; init; }

    /// <summary>Gets current average tyre wear percentage.</summary>
    public float? TyreWearPercent { get; init; }

    /// <summary>Gets remaining fuel in laps.</summary>
    public float? FuelRemainingLaps { get; init; }

    /// <summary>Gets current ERS store energy in joules.</summary>
    public float? ErsStoreEnergy { get; init; }

    /// <summary>Gets gap to the car ahead in milliseconds.</summary>
    public double? GapToFrontMs { get; init; }

    /// <summary>Gets gap to the car behind in milliseconds.</summary>
    public double? GapToBehindMs { get; init; }

    /// <summary>Gets current weather identifier.</summary>
    public byte? Weather { get; init; }

    /// <summary>Gets track wetness when available.</summary>
    public float? TrackWetness { get; init; }

    /// <summary>Gets compact weather summary.</summary>
    public string WeatherSummary { get; init; } = string.Empty;

    /// <summary>Gets compact tyre inventory summary.</summary>
    public string TyreInventorySummary { get; init; } = string.Empty;

    /// <summary>Gets compact damage summary.</summary>
    public string DamageSummary { get; init; } = string.Empty;

    /// <summary>Gets recent completed laps, newest first.</summary>
    public IReadOnlyList<LapSummary> RecentLaps { get; init; } = Array.Empty<LapSummary>();

    /// <summary>Gets recent race-event messages.</summary>
    public IReadOnlyList<string> RecentEvents { get; init; } = Array.Empty<string>();

    /// <summary>Gets recent-lap trend summary.</summary>
    public RecentLapTrendSummary RecentLapTrend { get; init; } = new();

    /// <summary>Gets snapshot quality.</summary>
    public SnapshotQuality Quality { get; init; } = new();

    /// <summary>Gets normal pit-decision signal.</summary>
    public PitDecisionSignal PitDecision { get; init; } = new();

    /// <summary>Gets safety-car pit-opportunity signal.</summary>
    public SafetyCarPitOpportunitySignal SafetyCarPitOpportunity { get; init; } = new();

    /// <summary>Gets all rule precheck signals.</summary>
    public IReadOnlyList<StrategyRuleSignal> RuleSignals { get; init; } = Array.Empty<StrategyRuleSignal>();
}

/// <summary>
/// Carries the complete strategy-question context sent to the AI prompt.
/// </summary>
public sealed record StrategyQuestionContext
{
    /// <summary>Gets the active session UID.</summary>
    public ulong? SessionUid { get; init; }

    /// <summary>Gets the user question.</summary>
    public string Question { get; init; } = string.Empty;

    /// <summary>Gets the recognized question intent.</summary>
    public VoiceQuestionIntent Intent { get; init; } = VoiceQuestionIntent.UNKNOWN;

    /// <summary>Gets the assistant mode.</summary>
    public RaceAssistantMode Mode { get; init; }

    /// <summary>Gets the localized recognized-intent label.</summary>
    public string IntentDisplayName { get; init; } = string.Empty;

    /// <summary>Gets the localized assistant-mode label.</summary>
    public string ModeDisplayName { get; init; } = string.Empty;

    /// <summary>Gets the localized expected advice-type label for this question.</summary>
    public string AdviceTypeDisplayName { get; init; } = string.Empty;

    /// <summary>Gets the source snapshot.</summary>
    public RaceAssistantSnapshot Snapshot { get; init; } = new();

    /// <summary>Gets required data fields.</summary>
    public IReadOnlyList<string> RequiredData { get; init; } = Array.Empty<string>();

    /// <summary>Gets missing data fields.</summary>
    public IReadOnlyList<string> MissingData { get; init; } = Array.Empty<string>();

    /// <summary>Gets prompt guidance for the intent.</summary>
    public string IntentPromptTemplate { get; init; } = string.Empty;
}

/// <summary>
/// Carries structured assistant advice for UI and TTS.
/// </summary>
public sealed record StrategyAdviceResult
{
    /// <summary>Gets the advice type.</summary>
    public RaceAssistantAdviceType AdviceType { get; init; } = RaceAssistantAdviceType.Unknown;

    /// <summary>Gets the short advice summary.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Gets the user-facing reason.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Gets the recommended action.</summary>
    public string RecommendedAction { get; init; } = string.Empty;

    /// <summary>Gets confidence.</summary>
    public StrategyAdviceConfidence Confidence { get; init; } = StrategyAdviceConfidence.Low;

    /// <summary>Gets risk level.</summary>
    public StrategyRiskLevel RiskLevel { get; init; } = StrategyRiskLevel.Unknown;

    /// <summary>Gets required data fields.</summary>
    public IReadOnlyList<string> RequiredData { get; init; } = Array.Empty<string>();

    /// <summary>Gets missing data fields.</summary>
    public IReadOnlyList<string> MissingData { get; init; } = Array.Empty<string>();

    /// <summary>Gets short text used for TTS only.</summary>
    public string Tts { get; init; } = string.Empty;

    /// <summary>Gets parser or rule warnings.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>Gets a value indicating whether the result came from rule fallback.</summary>
    public bool IsFallback { get; init; }
}

/// <summary>
/// Describes the result of parsing an AI strategy advice payload.
/// </summary>
public sealed record StrategyAdviceParseResult
{
    /// <summary>Gets a value indicating whether parsing succeeded.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Gets the parsed advice.</summary>
    public StrategyAdviceResult Advice { get; init; } = new();

    /// <summary>Gets the user-facing error message.</summary>
    public string ErrorMessage { get; init; } = string.Empty;
}
