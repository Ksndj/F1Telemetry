using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.Strategy;
using F1Telemetry.App.Services;
using F1Telemetry.Core.Models;
using F1Telemetry.TTS;
using F1Telemetry.TTS.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the first race assistant strategy question rules and data gates.
/// </summary>
public sealed class RaceAssistantTests
{
    /// <summary>
    /// Verifies pit decision keywords are recognized.
    /// </summary>
    [Fact]
    public void RaceAssistant_Classifier_NowPitQuestionIsPitDecision()
    {
        Assert.Equal(VoiceQuestionIntent.PIT_DECISION, new VoiceQuestionIntentClassifier().Classify("现在进站吗"));
    }

    /// <summary>
    /// Verifies tyre status keywords are recognized.
    /// </summary>
    [Fact]
    public void RaceAssistant_Classifier_TyreLifeQuestionIsTyreStatus()
    {
        Assert.Equal(VoiceQuestionIntent.TYRE_STATUS, new VoiceQuestionIntentClassifier().Classify("轮胎还能撑几圈"));
    }

    /// <summary>
    /// Verifies ERS strategy keywords are recognized.
    /// </summary>
    [Fact]
    public void RaceAssistant_Classifier_ErsQuestionIsErsStrategy()
    {
        Assert.Equal(VoiceQuestionIntent.ERS_STRATEGY, new VoiceQuestionIntentClassifier().Classify("ERS怎么用"));
    }

    /// <summary>
    /// Verifies gap analysis keywords are recognized.
    /// </summary>
    [Fact]
    public void RaceAssistant_Classifier_RearGapQuestionIsGapAnalysis()
    {
        Assert.Equal(VoiceQuestionIntent.GAP_ANALYSIS, new VoiceQuestionIntentClassifier().Classify("后车能守住吗"));
    }

    /// <summary>
    /// Verifies high tyre wear produces a pit or tyre-management signal.
    /// </summary>
    [Fact]
    public void RaceAssistant_RulePrecheck_HighTyreWearProducesPitSignal()
    {
        var snapshot = BuildSnapshot(tyreWear: 74f);

        Assert.Contains(
            snapshot.RuleSignals,
            signal => (signal.AdviceType == RaceAssistantAdviceType.PitWindow ||
                       signal.AdviceType == RaceAssistantAdviceType.TyreManagement) &&
                      signal.Confidence == StrategyAdviceConfidence.High);
    }

    /// <summary>
    /// Verifies low tyre wear does not recommend an immediate stop.
    /// </summary>
    [Fact]
    public void RaceAssistant_RulePrecheck_LowTyreWearDoesNotRecommendPit()
    {
        var snapshot = BuildSnapshot(tyreWear: 28f);
        var pitSignal = Assert.Single(snapshot.RuleSignals, signal => signal.SignalType == "pit-tyre-low-wear");

        Assert.Contains("暂不进", pitSignal.RecommendedAction, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies low ERS produces a saving signal.
    /// </summary>
    [Fact]
    public void RaceAssistant_RulePrecheck_LowErsProducesSavingSignal()
    {
        var snapshot = BuildSnapshot(ersStoreEnergy: 900_000f);

        Assert.Contains(
            snapshot.RuleSignals,
            signal => signal.AdviceType == RaceAssistantAdviceType.ErsManagement &&
                      signal.RecommendedAction.Contains("省电", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies a small rear gap produces a defense signal.
    /// </summary>
    [Fact]
    public void RaceAssistant_RulePrecheck_SmallRearGapProducesDefenseSignal()
    {
        var snapshot = BuildSnapshot(gapToBehindMs: 900);

        Assert.Contains(
            snapshot.RuleSignals,
            signal => signal.AdviceType == RaceAssistantAdviceType.Defense &&
                      signal.Confidence == StrategyAdviceConfidence.High);
    }

    /// <summary>
    /// Verifies missing data is explicitly carried into the context.
    /// </summary>
    [Fact]
    public void RaceAssistant_ContextBuilder_MissingDataIsExplicit()
    {
        var snapshot = new RaceAssistantSnapshot
        {
            Quality = new SnapshotQuality
            {
                MissingData = ["session-uid", "tyre-wear"],
                MaxRecommendedConfidence = StrategyAdviceConfidence.Low
            }
        };

        var context = new StrategyQuestionContextBuilder().Build(snapshot, "现在进站吗", VoiceQuestionIntent.PIT_DECISION);

        Assert.Contains("session-uid", context.MissingData);
        Assert.Contains("tyre-wear", context.MissingData);
    }

    /// <summary>
    /// Verifies TTS is compressed to the race limit.
    /// </summary>
    [Fact]
    public void RaceAssistant_JsonParser_CompressesLongTts()
    {
        var parser = new StrategyAdviceJsonParser();
        var result = parser.Parse(
            """
            {"adviceType":"GeneralStatus","summary":"稳定","reason":"趋势正常","recommendedAction":"保胎省电","confidence":"High","riskLevel":"Low","requiredData":[],"missingData":[],"tts":"这是一段非常非常非常非常非常非常长的比赛中播报文本，需要被压缩"}
            """);

        Assert.True(result.IsSuccess);
        Assert.True(result.Advice.Tts.Length <= 35);
    }

    /// <summary>
    /// Verifies non-JSON AI replies fail strict parsing.
    /// </summary>
    [Fact]
    public void RaceAssistant_JsonParser_NonJsonIsInvalid()
    {
        var result = new StrategyAdviceJsonParser().Parse("建议先进站");

        Assert.False(result.IsSuccess);
        Assert.Equal("AI 返回格式无效", result.ErrorMessage);
    }

    /// <summary>
    /// Verifies missing confidence defaults to low.
    /// </summary>
    [Fact]
    public void RaceAssistant_JsonParser_MissingConfidenceDefaultsLow()
    {
        var result = new StrategyAdviceJsonParser().Parse(
            """
            {"adviceType":"ErsManagement","summary":"ERS偏低","reason":"储能不足","recommendedAction":"省电","riskLevel":"Medium","requiredData":[],"missingData":[],"tts":"ERS偏低，直道先省电。"}
            """);

        Assert.True(result.IsSuccess);
        Assert.Equal(StrategyAdviceConfidence.Low, result.Advice.Confidence);
    }

    /// <summary>
    /// Verifies safety car pit questions use the safety-car opportunity signal.
    /// </summary>
    [Fact]
    public void RaceAssistant_SafetyCarPitDecision_UsesSafetyCarSignalAndMissingPitEntry()
    {
        var snapshot = BuildSnapshot(tyreWear: 62f, safetyCarStatus: 1);
        var context = new StrategyQuestionContextBuilder().Build(snapshot, "安全车要进吗", VoiceQuestionIntent.PIT_DECISION);

        Assert.Equal(RaceAssistantMode.SafetyCar, snapshot.Mode);
        Assert.Contains("pit-entry-state", context.MissingData);
        Assert.Equal("safety-car-pit-opportunity", snapshot.RuleSignals[0].SignalType);
    }

    /// <summary>
    /// Verifies pit questions require weather, wetness, and tyre inventory gates.
    /// </summary>
    [Fact]
    public void RaceAssistant_PitDecision_MarksWeatherWetnessAndInventoryMissing()
    {
        var snapshot = BuildSnapshot(weather: null, includeInventory: false);
        var context = new StrategyQuestionContextBuilder().Build(snapshot, "要不要换胎", VoiceQuestionIntent.PIT_DECISION);

        Assert.Contains("weather", context.MissingData);
        Assert.Contains("track-wetness", context.MissingData);
        Assert.Contains("tyre-inventory", context.MissingData);
    }

    /// <summary>
    /// Verifies high-confidence rules override conflicting AI output.
    /// </summary>
    [Fact]
    public void RaceAssistant_ConflictResolver_HighConfidenceRuleOverridesAi()
    {
        var snapshot = BuildSnapshot(tyreWear: 24f);
        var context = new StrategyQuestionContextBuilder().Build(snapshot, "现在进站吗", VoiceQuestionIntent.PIT_DECISION);
        var aiAdvice = new StrategyAdviceResult
        {
            AdviceType = RaceAssistantAdviceType.PitWindow,
            Summary = "现在进站",
            RecommendedAction = "现在进站",
            Confidence = StrategyAdviceConfidence.High,
            RiskLevel = StrategyRiskLevel.Low,
            Tts = "现在进站。"
        };

        var resolved = new StrategyRuleConflictResolver().Resolve(aiAdvice, context);

        Assert.Equal(StrategyAdviceConfidence.Low, resolved.Confidence);
        Assert.Contains("暂不进", resolved.RecommendedAction, StringComparison.Ordinal);
        Assert.Contains(resolved.Warnings, warning => warning.Contains("冲突", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies P4 voice answers use low-priority engineer advice messages.
    /// </summary>
    [Fact]
    public void RaceAssistant_TtsFactory_PlayerQuestionUsesLowPriority()
    {
        var message = new TtsMessageFactory().CreateForEngineerAdvice(
            "voice-ai:test",
            "暂不进，再等一圈看胎速。",
            new TtsOptions { TtsEnabled = true, CooldownSeconds = 8 });

        Assert.NotNull(message);
        Assert.Equal(TtsPriority.Low, message!.Priority);
    }

    private static RaceAssistantSnapshot BuildSnapshot(
        float tyreWear = 48f,
        float ersStoreEnergy = 2_500_000f,
        ushort gapToBehindMs = 2_400,
        byte? safetyCarStatus = 0,
        byte? weather = 0,
        bool includeInventory = true)
    {
        var now = DateTimeOffset.UtcNow;
        var player = new CarSnapshot
        {
            IsPlayer = true,
            Position = 5,
            CurrentLapNumber = 12,
            DeltaToCarInFrontInMs = 1_800,
            TyresAgeLaps = 8,
            TyreWear = tyreWear,
            FuelRemainingLaps = 4.2f,
            ErsStoreEnergy = ersStoreEnergy,
            UpdatedAt = now
        };
        var behind = new CarSnapshot
        {
            Position = 6,
            DeltaToCarInFrontInMs = gapToBehindMs,
            UpdatedAt = now
        };
        var state = new SessionState
        {
            SessionType = 15,
            Weather = weather,
            TrackTemperature = 32,
            AirTemperature = 24,
            TotalLaps = 29,
            PitStopWindowIdealLap = 12,
            PitStopWindowLatestLap = 15,
            SafetyCarStatus = safetyCarStatus,
            PlayerCar = player,
            Cars = [player, behind],
            PlayerTyreInventory = includeInventory
                ? new TyreInventorySnapshot
                {
                    Sets =
                    [
                        new TyreSetSnapshot
                        {
                            Index = 0,
                            ActualTyreCompound = 18,
                            VisualTyreCompound = 17,
                            Wear = 4,
                            Available = true
                        }
                    ]
                }
                : null,
            UpdatedAt = now
        };

        return new RaceAssistantSnapshotBuilder().Build(
            1234,
            state,
            CreateRecentLaps(),
            Array.Empty<string>(),
            now);
    }

    private static IReadOnlyList<LapSummary> CreateRecentLaps()
    {
        return
        [
            new LapSummary { LapNumber = 8, LapTimeInMs = 92_500, TyreWearDelta = 2.1f, FuelUsedLitres = 1.7f, ErsUsed = 800_000f, IsValid = true },
            new LapSummary { LapNumber = 9, LapTimeInMs = 92_700, TyreWearDelta = 2.3f, FuelUsedLitres = 1.8f, ErsUsed = 840_000f, IsValid = true },
            new LapSummary { LapNumber = 10, LapTimeInMs = 93_000, TyreWearDelta = 2.5f, FuelUsedLitres = 1.8f, ErsUsed = 860_000f, IsValid = true },
            new LapSummary { LapNumber = 11, LapTimeInMs = 93_300, TyreWearDelta = 2.7f, FuelUsedLitres = 1.9f, ErsUsed = 900_000f, IsValid = true },
            new LapSummary { LapNumber = 12, LapTimeInMs = 93_800, TyreWearDelta = 2.9f, FuelUsedLitres = 1.9f, ErsUsed = 940_000f, IsValid = true }
        ];
    }
}
