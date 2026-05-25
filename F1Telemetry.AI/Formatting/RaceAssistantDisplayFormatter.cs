using F1Telemetry.AI.Models;
using F1Telemetry.Analytics.Strategy;

namespace F1Telemetry.AI.Formatting;

/// <summary>
/// Formats race-assistant enum values and missing-data keys for user-facing Chinese surfaces.
/// </summary>
public static class RaceAssistantDisplayFormatter
{
    /// <summary>
    /// Formats a recognized question intent.
    /// </summary>
    public static string FormatIntent(VoiceQuestionIntent intent)
    {
        return intent switch
        {
            VoiceQuestionIntent.PIT_DECISION => "进站判断",
            VoiceQuestionIntent.TYRE_STATUS => "轮胎状态",
            VoiceQuestionIntent.ERS_STRATEGY => "ERS策略",
            VoiceQuestionIntent.GAP_ANALYSIS => "攻防差距",
            VoiceQuestionIntent.SETUP_FEEDBACK => "调校反馈",
            VoiceQuestionIntent.GENERAL_STATUS => "当前状态",
            VoiceQuestionIntent.CORNER_ANALYSIS => "弯角分析",
            VoiceQuestionIntent.DAMAGE_STATUS => "车损状态",
            _ => "未知意图"
        };
    }

    /// <summary>
    /// Formats an assistant mode.
    /// </summary>
    public static string FormatMode(RaceAssistantMode mode)
    {
        return mode switch
        {
            RaceAssistantMode.NoTelemetry or RaceAssistantMode.WaitingForTelemetry => "等待遥测",
            RaceAssistantMode.Practice => "练习赛",
            RaceAssistantMode.QualifyingPrep => "排位准备",
            RaceAssistantMode.QualifyingPush => "排位推圈",
            RaceAssistantMode.RaceOpening => "正赛起步阶段",
            RaceAssistantMode.RaceStintManagement => "正赛长段管理",
            RaceAssistantMode.PitWindowApproaching => "接近进站窗口",
            RaceAssistantMode.InPitWindow => "进站窗口内",
            RaceAssistantMode.SafetyCar => "安全车",
            RaceAssistantMode.VirtualSafetyCar => "虚拟安全车",
            RaceAssistantMode.RedFlag => "红旗",
            RaceAssistantMode.FinalLaps => "最后阶段",
            RaceAssistantMode.PostRace => "赛后",
            _ => mode.ToString()
        };
    }

    /// <summary>
    /// Formats an advice type.
    /// </summary>
    public static string FormatAdviceType(RaceAssistantAdviceType adviceType)
    {
        return adviceType switch
        {
            RaceAssistantAdviceType.PitWindow => "进站窗口",
            RaceAssistantAdviceType.TyreManagement => "轮胎管理",
            RaceAssistantAdviceType.FuelSaving => "燃油管理",
            RaceAssistantAdviceType.ErsManagement => "ERS管理",
            RaceAssistantAdviceType.Attack => "进攻",
            RaceAssistantAdviceType.Defense => "防守",
            RaceAssistantAdviceType.Undercut => "提前进站",
            RaceAssistantAdviceType.Overcut => "延后进站",
            RaceAssistantAdviceType.SafetyCar => "安全车",
            RaceAssistantAdviceType.SetupFeedback => "调校反馈",
            RaceAssistantAdviceType.Corner => "弯角分析",
            RaceAssistantAdviceType.Damage => "车损状态",
            RaceAssistantAdviceType.GeneralStatus => "当前状态",
            RaceAssistantAdviceType.PostRaceReview => "赛后复盘",
            _ => "未知建议"
        };
    }

    /// <summary>
    /// Formats a confidence band.
    /// </summary>
    public static string FormatConfidence(StrategyAdviceConfidence confidence)
    {
        return confidence switch
        {
            StrategyAdviceConfidence.High => "高",
            StrategyAdviceConfidence.Medium => "中",
            StrategyAdviceConfidence.Low => "低",
            _ => confidence.ToString()
        };
    }

    /// <summary>
    /// Formats a risk band.
    /// </summary>
    public static string FormatRiskLevel(StrategyRiskLevel riskLevel)
    {
        return riskLevel switch
        {
            StrategyRiskLevel.High => "高",
            StrategyRiskLevel.Medium => "中",
            StrategyRiskLevel.Low => "低",
            StrategyRiskLevel.Unknown => "未知",
            _ => riskLevel.ToString()
        };
    }

    /// <summary>
    /// Formats a missing-data key.
    /// </summary>
    public static string FormatMissingDataKey(string key)
    {
        return key switch
        {
            "session-uid" => "会话ID",
            "session-mode" => "赛制",
            "snapshot-quality" => "数据质量",
            "tyre-age" => "胎龄",
            "tyre-wear" => "胎磨",
            "current-tyre" => "当前轮胎",
            "recent-lap-trend" => "最近圈速趋势",
            "recent-laps" => "最近圈历史",
            "recent-lap-times" => "最近圈速",
            "recent-tyre-wear-delta" => "最近胎磨趋势",
            "recent-fuel-used" => "最近油耗趋势",
            "recent-ers-used" => "最近ERS趋势",
            "tyre-inventory" => "轮胎库存",
            "tyre-sets-packet" => "TyreSets数据包",
            "weather" => "天气",
            "track-wetness" => "赛道湿滑度",
            "remaining-laps" => "剩余圈数",
            "gaps" => "前后车差距",
            "gap-to-front-ms" => "前车差距",
            "gap-to-behind-ms" => "后车差距",
            "player-car" => "玩家车辆",
            "fuel-remaining-laps" => "燃油剩余圈数",
            "ers-store-energy" => "ERS电量",
            "fresh-snapshot" => "最新遥测快照",
            "estimated-pit-loss" => "预计进站损失",
            "pit-entry-state" => "维修区入口状态",
            "pit-exit-traffic" => "出站交通",
            "position" => "当前名次",
            "telemetry-summary" => "遥测摘要",
            "damage-summary" => "车损摘要",
            _ => string.IsNullOrWhiteSpace(key) ? "未知数据" : key
        };
    }

    /// <summary>
    /// Formats a value that may already be an enum name from an older in-memory row.
    /// </summary>
    public static string FormatIntentText(string value)
    {
        return Enum.TryParse<VoiceQuestionIntent>(value, ignoreCase: true, out var intent)
            ? FormatIntent(intent)
            : value;
    }

    /// <summary>
    /// Formats a value that may already be a confidence enum name from an older in-memory row.
    /// </summary>
    public static string FormatConfidenceText(string value)
    {
        return Enum.TryParse<StrategyAdviceConfidence>(value, ignoreCase: true, out var confidence)
            ? FormatConfidence(confidence)
            : value;
    }
}
