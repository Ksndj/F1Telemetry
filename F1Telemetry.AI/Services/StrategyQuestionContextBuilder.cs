using F1Telemetry.AI.Models;

namespace F1Telemetry.AI.Services;

/// <summary>
/// Builds complete strategy question contexts from classified questions and snapshots.
/// </summary>
public sealed class StrategyQuestionContextBuilder
{
    /// <summary>
    /// Builds the prompt-ready strategy question context.
    /// </summary>
    public StrategyQuestionContext Build(
        RaceAssistantSnapshot snapshot,
        string question,
        VoiceQuestionIntent intent)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var requiredData = BuildRequiredData(intent, snapshot.Mode);
        var missing = requiredData
            .Where(field => IsMissing(field, snapshot))
            .Concat(snapshot.Quality.MissingData)
            .Concat(intent == VoiceQuestionIntent.PIT_DECISION
                ? SelectPitMissingData(snapshot)
                : Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new StrategyQuestionContext
        {
            SessionUid = snapshot.SessionUid,
            Question = question?.Trim() ?? string.Empty,
            Intent = intent,
            Mode = snapshot.Mode,
            Snapshot = snapshot,
            RequiredData = requiredData,
            MissingData = missing,
            IntentPromptTemplate = BuildIntentTemplate(intent)
        };
    }

    private static IReadOnlyList<string> BuildRequiredData(VoiceQuestionIntent intent, RaceAssistantMode mode)
    {
        var common = new List<string> { "session-uid", "session-mode", "snapshot-quality" };
        common.AddRange(intent switch
        {
            VoiceQuestionIntent.PIT_DECISION => mode is RaceAssistantMode.SafetyCar or RaceAssistantMode.VirtualSafetyCar
                ? ["tyre-age", "tyre-wear", "remaining-laps", "tyre-inventory", "weather", "track-wetness", "pit-entry-state", "pit-exit-traffic", "position"]
                : ["tyre-age", "tyre-wear", "recent-lap-trend", "tyre-inventory", "weather", "track-wetness", "remaining-laps", "gaps", "estimated-pit-loss"],
            VoiceQuestionIntent.TYRE_STATUS => ["current-tyre", "tyre-age", "tyre-wear", "recent-lap-trend"],
            VoiceQuestionIntent.ERS_STRATEGY => ["ers-store-energy", "recent-lap-trend"],
            VoiceQuestionIntent.GAP_ANALYSIS => ["gap-to-front-ms", "gap-to-behind-ms", "position"],
            VoiceQuestionIntent.SETUP_FEEDBACK => ["telemetry-summary", "recent-lap-trend"],
            VoiceQuestionIntent.CORNER_ANALYSIS => ["recent-lap-trend"],
            VoiceQuestionIntent.DAMAGE_STATUS => ["damage-summary"],
            _ => ["current-tyre", "fuel-remaining-laps", "ers-store-energy"]
        });

        return common.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool IsMissing(string field, RaceAssistantSnapshot snapshot)
    {
        return field switch
        {
            "session-uid" => snapshot.SessionUid is null,
            "tyre-age" => snapshot.TyreAgeLaps is null,
            "tyre-wear" => snapshot.TyreWearPercent is null,
            "current-tyre" => string.IsNullOrWhiteSpace(snapshot.CurrentTyre) || snapshot.CurrentTyre == "-",
            "remaining-laps" => snapshot.CurrentLap is null || snapshot.TotalLaps is null,
            "tyre-inventory" => string.IsNullOrWhiteSpace(snapshot.TyreInventorySummary),
            "gap-to-front-ms" => snapshot.GapToFrontMs is null,
            "gap-to-behind-ms" => snapshot.GapToBehindMs is null,
            "gaps" => snapshot.GapToFrontMs is null || snapshot.GapToBehindMs is null,
            "estimated-pit-loss" => snapshot.PitDecision.Inputs.EstimatedPitLossMs is null,
            "ers-store-energy" => snapshot.ErsStoreEnergy is null,
            "fuel-remaining-laps" => snapshot.FuelRemainingLaps is null,
            "damage-summary" => string.IsNullOrWhiteSpace(snapshot.DamageSummary),
            "pit-entry-state" => snapshot.SafetyCarPitOpportunity.Inputs.HasPassedPitEntry is null,
            "pit-exit-traffic" => string.IsNullOrWhiteSpace(snapshot.SafetyCarPitOpportunity.Inputs.PitExitTrafficSummary),
            "position" => snapshot.Position is null,
            "weather" => snapshot.Weather is null,
            "track-wetness" => snapshot.TrackWetness is null,
            _ => false
        };
    }

    private static IEnumerable<string> SelectPitMissingData(RaceAssistantSnapshot snapshot)
    {
        return snapshot.Mode is RaceAssistantMode.SafetyCar or RaceAssistantMode.VirtualSafetyCar
            ? snapshot.SafetyCarPitOpportunity.Inputs.MissingData
            : snapshot.PitDecision.Inputs.MissingData;
    }

    private static string BuildIntentTemplate(VoiceQuestionIntent intent)
    {
        return intent switch
        {
            VoiceQuestionIntent.PIT_DECISION =>
                "PIT_DECISION: 判断是否建议进站，只能输出现在进/等1圈/暂不进等保守动作；必须遵守库存、天气、赛制和规则信号。",
            VoiceQuestionIntent.TYRE_STATUS =>
                "TYRE_STATUS: 只解释当前轮胎状态、胎龄、胎磨和趋势，动作限定为保胎/继续推/准备进站。",
            VoiceQuestionIntent.ERS_STRATEGY =>
                "ERS_STRATEGY: 只解释 ERS 状态，动作限定为省电/直道使用/防守保留。",
            VoiceQuestionIntent.GAP_ANALYSIS =>
                "GAP_ANALYSIS: 只判断攻防压力和 gap，动作限定为追击/防守/省电。",
            VoiceQuestionIntent.SETUP_FEEDBACK =>
                "SETUP_FEEDBACK: 只给驾驶与设置反馈，动作围绕前翼、差速、刹车点或油门节奏。",
            VoiceQuestionIntent.CORNER_ANALYSIS =>
                "CORNER_ANALYSIS: 只给弯角执行建议；没有弯角数据时必须说数据不足。",
            VoiceQuestionIntent.DAMAGE_STATUS =>
                "DAMAGE_STATUS: 只基于损伤摘要判断，不编造未提供的损伤组件。",
            _ =>
                "GENERAL_STATUS: 总结当前整体状态和驾驶重点；比赛中必须简短。"
        };
    }
}
