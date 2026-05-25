using System.Globalization;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.Strategy;
using F1Telemetry.App.Formatting;
using F1Telemetry.Core.Formatting;
using F1Telemetry.Core.Models;

namespace F1Telemetry.App.Services;

/// <summary>
/// Builds compressed race-assistant snapshots from the current aggregate state and lap summaries.
/// </summary>
public sealed class RaceAssistantSnapshotBuilder
{
    private const int StaleSnapshotSeconds = 15;
    private const double DefaultPitLossMs = 24_000d;
    private readonly RaceAssistantStateMachine _stateMachine;

    /// <summary>
    /// Initializes a snapshot builder.
    /// </summary>
    /// <param name="stateMachine">The assistant mode resolver.</param>
    public RaceAssistantSnapshotBuilder(RaceAssistantStateMachine? stateMachine = null)
    {
        _stateMachine = stateMachine ?? new RaceAssistantStateMachine();
    }

    /// <summary>
    /// Builds a compressed assistant snapshot.
    /// </summary>
    public RaceAssistantSnapshot Build(
        ulong? sessionUid,
        SessionState sessionState,
        IReadOnlyList<LapSummary> recentLaps,
        IReadOnlyList<string> recentEvents,
        DateTimeOffset now,
        RaceWeekendTyrePlan? tyrePlan = null,
        bool isListening = true)
    {
        ArgumentNullException.ThrowIfNull(sessionState);

        var player = sessionState.PlayerCar;
        var sessionMode = SessionModeFormatter.Resolve(
            sessionState.SessionType,
            sessionState.TotalLaps,
            sessionState.WeekendStructure);
        var trend = BuildTrend(recentLaps);
        var tyreInventorySummary = BuildTyreInventorySummary(sessionState.PlayerTyreInventory, tyrePlan);
        var weatherSummary = BuildWeatherSummary(sessionState);
        var quality = BuildQuality(
            sessionUid,
            sessionState,
            player,
            tyreInventorySummary,
            HasTyreSetsPacket(sessionState.PlayerTyreInventory),
            isListening,
            now);
        var mode = _stateMachine.Resolve(sessionState, isListening, sessionUid, player, quality.IsStale);
        int? currentLap = player?.CurrentLapNumber is null ? null : player.CurrentLapNumber.Value;
        int? totalLaps = sessionState.TotalLaps is null ? null : sessionState.TotalLaps.Value;
        var remainingLaps = currentLap is not null && totalLaps is not null
            ? Math.Max(0, totalLaps.Value - currentLap.Value)
            : (int?)null;
        var playerPosition = player?.Position;
        var carBehind = playerPosition is null
            ? null
            : sessionState.Cars.FirstOrDefault(car => car.Position is not null && car.Position.Value == playerPosition.Value + 1);
        var gapToBehind = NormalizeGapMs(carBehind?.DeltaToCarInFrontInMs);
        var gapToFront = NormalizeGapMs(player?.DeltaToCarInFrontInMs);
        var pitDecision = BuildPitDecision(
            currentLap,
            totalLaps,
            remainingLaps,
            player,
            trend,
            tyreInventorySummary,
            gapToFront,
            gapToBehind,
            sessionState.SafetyCarStatus,
            mode);
        var safetyCarPit = BuildSafetyCarPitOpportunity(
            remainingLaps,
            player,
            tyreInventorySummary,
            gapToBehind);
        var signals = BuildRuleSignals(
            mode,
            player,
            pitDecision.Signal,
            safetyCarPit.Signal,
            gapToBehind).ToArray();

        return new RaceAssistantSnapshot
        {
            SessionUid = sessionUid,
            Mode = mode,
            SessionMode = sessionMode,
            CurrentLap = currentLap,
            TotalLaps = totalLaps,
            Position = player?.Position is null ? null : player.Position.Value,
            CurrentTyre = BuildTyreText(player),
            TyreAgeLaps = player?.TyresAgeLaps is null ? null : player.TyresAgeLaps.Value,
            TyreWearPercent = player?.TyreWear,
            FuelRemainingLaps = player?.FuelRemainingLaps,
            ErsStoreEnergy = player?.ErsStoreEnergy,
            GapToFrontMs = gapToFront,
            GapToBehindMs = gapToBehind,
            Weather = sessionState.Weather,
            TrackWetness = null,
            WeatherSummary = weatherSummary,
            TyreInventorySummary = tyreInventorySummary,
            DamageSummary = DamageSummaryFormatter.Format(player?.Damage),
            RecentLaps = recentLaps.Take(5).ToArray(),
            RecentEvents = recentEvents.TakeLast(8).ToArray(),
            RecentLapTrend = trend,
            Quality = quality,
            PitDecision = pitDecision,
            SafetyCarPitOpportunity = safetyCarPit,
            RuleSignals = signals
        };
    }

    private static SnapshotQuality BuildQuality(
        ulong? sessionUid,
        SessionState sessionState,
        CarSnapshot? player,
        string tyreInventorySummary,
        bool hasTyreSetsPacket,
        bool isListening,
        DateTimeOffset now)
    {
        var missing = new List<string>();
        if (!isListening)
        {
            missing.Add("fresh-snapshot");
        }

        if (sessionUid is null)
        {
            missing.Add("session-uid");
        }

        if (player is null)
        {
            missing.Add("player-car");
        }

        if (player?.TyreWear is null)
        {
            missing.Add("tyre-wear");
        }

        if (player?.FuelRemainingLaps is null)
        {
            missing.Add("fuel-remaining-laps");
        }

        if (player?.ErsStoreEnergy is null)
        {
            missing.Add("ers-store-energy");
        }

        if (player?.DeltaToCarInFrontInMs is null)
        {
            missing.Add("gap-to-front-ms");
        }

        if (string.IsNullOrWhiteSpace(tyreInventorySummary))
        {
            missing.Add("tyre-inventory");
        }

        if (!hasTyreSetsPacket)
        {
            missing.Add("tyre-sets-packet");
        }

        if (sessionState.Weather is null)
        {
            missing.Add("weather");
        }

        missing.Add("track-wetness");

        var ageSeconds = sessionState.UpdatedAt == default
            ? (int?)null
            : (int)Math.Max(0, (now - sessionState.UpdatedAt).TotalSeconds);
        var stale = !isListening || ageSeconds is null or > StaleSnapshotSeconds;
        if (stale)
        {
            missing.Add("fresh-snapshot");
        }

        var maxConfidence = stale || missing.Count > 0
            ? StrategyAdviceConfidence.Low
            : StrategyAdviceConfidence.High;
        return new SnapshotQuality
        {
            IsStale = stale,
            AgeSeconds = ageSeconds,
            MissingData = missing.Distinct(StringComparer.Ordinal).ToArray(),
            MaxRecommendedConfidence = maxConfidence,
            Summary = stale
                ? $"快照可能过旧，age={ageSeconds?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}s，需降置信度。"
                : "快照时效正常。"
        };
    }

    private static PitDecisionSignal BuildPitDecision(
        int? currentLap,
        int? totalLaps,
        int? remainingLaps,
        CarSnapshot? player,
        RecentLapTrendSummary trend,
        string tyreInventorySummary,
        double? gapToFrontMs,
        double? gapToBehindMs,
        byte? safetyCarStatus,
        RaceAssistantMode mode)
    {
        var missing = new List<string>();
        if (player?.TyresAgeLaps is null) missing.Add("tyre-age");
        if (player?.TyreWear is null) missing.Add("tyre-wear");
        if (remainingLaps is null) missing.Add("remaining-laps");
        if (string.IsNullOrWhiteSpace(tyreInventorySummary)) missing.Add("tyre-inventory");
        if (gapToFrontMs is null) missing.Add("gap-to-front-ms");
        if (gapToBehindMs is null) missing.Add("gap-to-behind-ms");

        var inputs = new PitDecisionInputs
        {
            CurrentLap = currentLap,
            TotalLaps = totalLaps,
            RemainingLaps = remainingLaps,
            TyreAgeLaps = player?.TyresAgeLaps is null ? null : player.TyresAgeLaps.Value,
            TyreWearPercent = player?.TyreWear,
            RecentTrend = trend,
            TyreInventorySummary = tyreInventorySummary,
            GapToFrontMs = gapToFrontMs,
            GapToBehindMs = gapToBehindMs,
            EstimatedPitLossMs = DefaultPitLossMs,
            SafetyCarStatus = safetyCarStatus,
            MissingData = missing
        };

        var wear = player?.TyreWear;
        if (mode == RaceAssistantMode.Practice)
        {
            return new PitDecisionSignal
            {
                Inputs = inputs,
                Signal = new StrategyRuleSignal
                {
                    SignalType = "practice-pit-question",
                    AdviceType = RaceAssistantAdviceType.GeneralStatus,
                    Summary = missing.Count > 0 ? "当前数据不足，暂不做进站判断。" : "练习赛进站问题按长距离采集处理。",
                    RecommendedAction = missing.Count > 0 ? "当前数据不足，暂不做进站判断。" : "练习赛先多跑一圈收集胎耗。",
                    Confidence = StrategyAdviceConfidence.Low,
                    RiskLevel = StrategyRiskLevel.Unknown,
                    RequiredData = ["current-tyre", "tyre-age", "tyre-wear", "recent-lap-trend"],
                    MissingData = missing
                }
            };
        }

        var signal = wear switch
        {
            >= 72f => new StrategyRuleSignal
            {
                SignalType = "pit-tyre-critical",
                AdviceType = RaceAssistantAdviceType.PitWindow,
                Summary = "胎磨已进入高风险区间。",
                RecommendedAction = "建议考虑进站或立即保胎。",
                Confidence = StrategyAdviceConfidence.High,
                RiskLevel = StrategyRiskLevel.High,
                RequiredData = ["tyre-wear", "tyre-inventory", "remaining-laps"],
                MissingData = missing
            },
            >= 55f => new StrategyRuleSignal
            {
                SignalType = "pit-tyre-window",
                AdviceType = RaceAssistantAdviceType.TyreManagement,
                Summary = "胎磨接近策略窗口。",
                RecommendedAction = "再观察一圈，准备进站。",
                Confidence = StrategyAdviceConfidence.Medium,
                RiskLevel = StrategyRiskLevel.Medium,
                RequiredData = ["tyre-wear", "tyre-inventory"],
                MissingData = missing
            },
            <= 35f when wear is not null => new StrategyRuleSignal
            {
                SignalType = "pit-tyre-low-wear",
                AdviceType = RaceAssistantAdviceType.PitWindow,
                Summary = "胎磨仍低，进站证据不足。",
                RecommendedAction = "暂不进，继续观察胎速。",
                Confidence = StrategyAdviceConfidence.High,
                RiskLevel = StrategyRiskLevel.Low,
                RequiredData = ["tyre-wear"],
                MissingData = missing
            },
            _ => new StrategyRuleSignal
            {
                SignalType = "pit-insufficient-data",
                AdviceType = RaceAssistantAdviceType.PitWindow,
                Summary = missing.Count > 0 ? "进站判断数据不足。" : "进站判断不明确。",
                RecommendedAction = "暂不进，再观察一圈。",
                Confidence = missing.Count > 0 ? StrategyAdviceConfidence.Low : StrategyAdviceConfidence.Medium,
                RiskLevel = StrategyRiskLevel.Unknown,
                RequiredData = ["tyre-age", "tyre-wear", "remaining-laps", "tyre-inventory", "gaps", "estimated-pit-loss"],
                MissingData = missing
            }
        };

        return new PitDecisionSignal
        {
            Inputs = inputs,
            Signal = signal
        };
    }

    private static SafetyCarPitOpportunitySignal BuildSafetyCarPitOpportunity(
        int? remainingLaps,
        CarSnapshot? player,
        string tyreInventorySummary,
        double? gapToBehindMs)
    {
        var missing = new List<string> { "pit-entry-state" };
        if (player?.TyresAgeLaps is null) missing.Add("tyre-age");
        if (player?.TyreWear is null) missing.Add("tyre-wear");
        if (remainingLaps is null) missing.Add("remaining-laps");
        if (string.IsNullOrWhiteSpace(tyreInventorySummary)) missing.Add("tyre-inventory");
        if (gapToBehindMs is null) missing.Add("pit-exit-traffic");
        if (player?.Position is null) missing.Add("position");

        var traffic = gapToBehindMs is null
            ? string.Empty
            : $"后车间隔 {gapToBehindMs.Value / 1000d:0.0}s，出站交通需确认";
        var canConsider = player?.TyreWear is >= 50f && remainingLaps is > 3 && !string.IsNullOrWhiteSpace(tyreInventorySummary);
        return new SafetyCarPitOpportunitySignal
        {
            Inputs = new SafetyCarPitOpportunityInputs
            {
                HasPassedPitEntry = null,
                TyreAgeLaps = player?.TyresAgeLaps is null ? null : player.TyresAgeLaps.Value,
                TyreWearPercent = player?.TyreWear,
                RemainingLaps = remainingLaps,
                TyreInventorySummary = tyreInventorySummary,
                PitExitTrafficSummary = traffic,
                CurrentPosition = player?.Position is null ? null : player.Position.Value,
                MissingData = missing
            },
            Signal = new StrategyRuleSignal
            {
                SignalType = "safety-car-pit-opportunity",
                AdviceType = RaceAssistantAdviceType.SafetyCar,
                Summary = canConsider ? "安全车/VSC 下存在进站窗口，但入口与交通数据不完整。" : "安全车/VSC 进站证据不足。",
                RecommendedAction = canConsider ? "可考虑进站，但先确认入口。" : "先保持 delta，继续观察。",
                Confidence = missing.Count > 0 ? StrategyAdviceConfidence.Low : StrategyAdviceConfidence.Medium,
                RiskLevel = canConsider ? StrategyRiskLevel.Medium : StrategyRiskLevel.Unknown,
                RequiredData = ["pit-entry-state", "tyre-age", "tyre-wear", "remaining-laps", "tyre-inventory", "pit-exit-traffic", "position"],
                MissingData = missing
            }
        };
    }

    private static IEnumerable<StrategyRuleSignal> BuildRuleSignals(
        RaceAssistantMode mode,
        CarSnapshot? player,
        StrategyRuleSignal pitSignal,
        StrategyRuleSignal safetyCarPitSignal,
        double? gapToBehindMs)
    {
        yield return mode is RaceAssistantMode.SafetyCar or RaceAssistantMode.VirtualSafetyCar
            ? safetyCarPitSignal
            : pitSignal;

        if (player?.ErsStoreEnergy is not null && player.ErsStoreEnergy < 1_200_000f)
        {
            yield return new StrategyRuleSignal
            {
                SignalType = "low-ers",
                AdviceType = RaceAssistantAdviceType.ErsManagement,
                Summary = "ERS 储能偏低。",
                RecommendedAction = "ERS偏低，直道先省电。",
                Confidence = StrategyAdviceConfidence.High,
                RiskLevel = StrategyRiskLevel.Medium,
                RequiredData = ["ers-store-energy"]
            };
        }

        if (player?.FuelRemainingLaps is not null && player.FuelRemainingLaps < 1.0f)
        {
            yield return new StrategyRuleSignal
            {
                SignalType = "low-fuel",
                AdviceType = RaceAssistantAdviceType.FuelSaving,
                Summary = "剩余燃油圈数偏低。",
                RecommendedAction = "燃油偏低，先省油保节奏。",
                Confidence = StrategyAdviceConfidence.High,
                RiskLevel = StrategyRiskLevel.High,
                RequiredData = ["fuel-remaining-laps"]
            };
        }

        if (gapToBehindMs is not null && gapToBehindMs < 1_500d)
        {
            yield return new StrategyRuleSignal
            {
                SignalType = "rear-gap-defense",
                AdviceType = RaceAssistantAdviceType.Defense,
                Summary = "后车间隔很小，防守压力高。",
                RecommendedAction = "后车压力大，优先防守。",
                Confidence = StrategyAdviceConfidence.High,
                RiskLevel = StrategyRiskLevel.High,
                RequiredData = ["gap-to-behind-ms"]
            };
        }
    }

    private static RecentLapTrendSummary BuildTrend(IReadOnlyList<LapSummary> recentLaps)
    {
        var laps = recentLaps.Take(5).OrderBy(lap => lap.LapNumber).ToArray();
        var missing = new List<string>();
        if (laps.Length == 0)
        {
            missing.Add("recent-laps");
            return new RecentLapTrendSummary { MissingData = missing };
        }

        var lapTimes = laps.Where(lap => lap.LapTimeInMs is not null).Select(lap => (double)lap.LapTimeInMs!.Value).ToArray();
        var tyreDeltas = laps.Where(lap => lap.TyreWearDelta is not null).Select(lap => (double)lap.TyreWearDelta!.Value).ToArray();
        var fuel = laps.Where(lap => lap.FuelUsedLitres is not null).Select(lap => (double)lap.FuelUsedLitres!.Value).ToArray();
        var ers = laps.Where(lap => lap.ErsUsed is not null).Select(lap => (double)lap.ErsUsed!.Value).ToArray();
        if (lapTimes.Length < 2) missing.Add("recent-lap-times");
        if (tyreDeltas.Length == 0) missing.Add("recent-tyre-wear-delta");
        if (fuel.Length == 0) missing.Add("recent-fuel-used");
        if (ers.Length == 0) missing.Add("recent-ers-used");

        return new RecentLapTrendSummary
        {
            LapCount = laps.Length,
            LapTimeTrend = FormatTrend(lapTimes, true, "圈速"),
            TyreWearTrend = tyreDeltas.Length == 0 ? "n/a" : $"平均每圈胎磨 +{tyreDeltas.Average():0.0}%",
            FuelTrend = fuel.Length == 0 ? "n/a" : $"平均每圈燃油 {fuel.Average():0.00}L",
            ErsTrend = ers.Length == 0 ? "n/a" : $"平均每圈 ERS 使用 {ers.Average() / 1_000_000d:0.00}MJ",
            AverageTyreWearDeltaPerLap = tyreDeltas.Length == 0 ? null : tyreDeltas.Average(),
            AverageFuelUsedLitres = fuel.Length == 0 ? null : fuel.Average(),
            AverageErsUsed = ers.Length == 0 ? null : ers.Average(),
            MissingData = missing
        };
    }

    private static string FormatTrend(IReadOnlyList<double> values, bool lowerIsBetter, string label)
    {
        if (values.Count < 2)
        {
            return "n/a";
        }

        var delta = values[^1] - values[0];
        var direction = Math.Abs(delta) < 250d
            ? "稳定"
            : (delta < 0 == lowerIsBetter ? "变好" : "变差");
        return $"{label}{direction}，变化 {delta / 1000d:+0.0;-0.0;0.0}s";
    }

    private static bool HasTyreSetsPacket(TyreInventorySnapshot? inventory)
    {
        return inventory is not null && inventory.Sets.Count > 0;
    }

    private static string BuildTyreInventorySummary(TyreInventorySnapshot? inventory, RaceWeekendTyrePlan? tyrePlan)
    {
        var parts = new List<string>();
        var normalizedPlan = tyrePlan?.Normalize();
        if (HasManualTyreInventory(normalizedPlan))
        {
            parts.Add($"手动库存：{RaceWeekendTyrePlan.FormatInventoryText(normalizedPlan!)}");
        }

        if (inventory is not null && inventory.Sets.Count > 0)
        {
            var available = inventory.Sets
                .Where(set => set.Available)
                .OrderBy(set => set.Wear)
                .Take(6)
                .Select(set => $"#{set.Index + 1} {set.VisualTyreCompound}/{set.ActualTyreCompound} wear {set.Wear}%")
                .ToArray();
            if (available.Length > 0)
            {
                parts.Add($"游戏库存：{string.Join("；", available)}");
            }
        }

        return parts.Count == 0 ? string.Empty : string.Join("；", parts);
    }

    private static bool HasManualTyreInventory(RaceWeekendTyrePlan? tyrePlan)
    {
        return tyrePlan is not null &&
               (tyrePlan.SoftCount > 0 ||
                tyrePlan.MediumCount > 0 ||
                tyrePlan.HardCount > 0 ||
                tyrePlan.IntermediateCount > 0 ||
                tyrePlan.WetCount > 0);
    }

    private static string BuildWeatherSummary(SessionState sessionState)
    {
        var current = $"当前天气 {FormatWeather(sessionState.Weather)}，赛道温度 {FormatTemperature(sessionState.TrackTemperature)}，气温 {FormatTemperature(sessionState.AirTemperature)}";
        var forecast = sessionState.WeatherForecastSamples
            .Take(3)
            .Select(sample => $"+{sample.TimeOffsetMinutes}min {FormatWeather(sample.Weather)} 雨{sample.RainPercentage}%")
            .ToArray();
        return forecast.Length == 0 ? current : $"{current}；预报：{string.Join("；", forecast)}";
    }

    private static string BuildTyreText(CarSnapshot? player)
    {
        if (player?.VisualTyreCompound is null && player?.ActualTyreCompound is null)
        {
            return "-";
        }

        return TyreCompoundFormatter.Format(player.VisualTyreCompound, player.ActualTyreCompound, player.HasTelemetryAccess);
    }

    private static double? NormalizeGapMs(ushort? value)
    {
        return value is null or 0 or ushort.MaxValue ? null : value.Value;
    }

    private static string FormatWeather(byte? weather)
    {
        return weather switch
        {
            0 => "晴",
            1 => "多云",
            2 => "阴",
            3 => "小雨",
            4 => "大雨",
            5 => "暴雨",
            _ => weather?.ToString(CultureInfo.InvariantCulture) ?? "-"
        };
    }

    private static string FormatTemperature(sbyte? temperature)
    {
        return temperature is null ? "-" : $"{temperature.Value}°C";
    }
}
