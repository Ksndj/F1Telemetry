using F1Telemetry.AI.Models;
using F1Telemetry.Analytics.Strategy;

namespace F1Telemetry.AI.Services;

/// <summary>
/// Produces conservative race-assistant answers when AI cannot return usable advice.
/// </summary>
public sealed class RuleBasedFallbackAdviceService
{
    /// <summary>
    /// Creates a conservative answer from the supplied context and rule signals.
    /// </summary>
    public StrategyAdviceResult BuildFallback(
        StrategyQuestionContext context,
        string failureReason)
    {
        ArgumentNullException.ThrowIfNull(context);

        var signal = SelectSignal(context);
        if (signal is not null && signal.Confidence != StrategyAdviceConfidence.Low)
        {
            return FromSignal(signal, context, failureReason, isFallback: true);
        }

        var missing = context.MissingData.Distinct(StringComparer.Ordinal).ToArray();
        var summary = missing.Length > 0
            ? "数据不足，先按保守节奏执行。"
            : BuildGenericSummary(context.Intent, context.Mode);
        var action = BuildGenericAction(context.Intent, context.Mode, missing.Length > 0);

        return new StrategyAdviceResult
        {
            AdviceType = MapAdviceType(context.Intent, context.Mode),
            Summary = summary,
            Reason = string.IsNullOrWhiteSpace(failureReason)
                ? "AI 暂不可用，已使用规则兜底。"
                : $"AI 暂不可用：{failureReason}。已使用规则兜底。",
            RecommendedAction = action,
            Confidence = missing.Length > 0 ? StrategyAdviceConfidence.Low : StrategyAdviceConfidence.Medium,
            RiskLevel = StrategyRiskLevel.Unknown,
            RequiredData = context.RequiredData,
            MissingData = missing,
            Tts = StrategyAdviceJsonParser.CompressTts(action),
            Warnings = string.IsNullOrWhiteSpace(failureReason) ? Array.Empty<string>() : new[] { failureReason },
            IsFallback = true
        };
    }

    internal static StrategyAdviceResult FromSignal(
        StrategyRuleSignal signal,
        StrategyQuestionContext context,
        string warning,
        bool isFallback)
    {
        var warnings = string.IsNullOrWhiteSpace(warning) ? Array.Empty<string>() : new[] { warning };
        return new StrategyAdviceResult
        {
            AdviceType = signal.AdviceType,
            Summary = signal.Summary,
            Reason = string.IsNullOrWhiteSpace(warning)
                ? "规则预检给出高置信信号。"
                : $"规则预检优先：{warning}",
            RecommendedAction = signal.RecommendedAction,
            Confidence = ApplyQualityLimit(signal.Confidence, context.Snapshot.Quality.MaxRecommendedConfidence),
            RiskLevel = signal.RiskLevel,
            RequiredData = signal.RequiredData,
            MissingData = signal.MissingData.Concat(context.MissingData).Distinct(StringComparer.Ordinal).ToArray(),
            Tts = StrategyAdviceJsonParser.CompressTts(signal.RecommendedAction),
            Warnings = warnings,
            IsFallback = isFallback
        };
    }

    private static StrategyRuleSignal? SelectSignal(StrategyQuestionContext context)
    {
        var signals = context.Intent == VoiceQuestionIntent.PIT_DECISION &&
                      context.Mode is RaceAssistantMode.SafetyCar or RaceAssistantMode.VirtualSafetyCar
            ? new[] { context.Snapshot.SafetyCarPitOpportunity.Signal }
            : context.Snapshot.RuleSignals;

        return signals
            .Where(signal => !string.IsNullOrWhiteSpace(signal.Summary))
            .OrderByDescending(signal => signal.Confidence)
            .FirstOrDefault();
    }

    private static StrategyAdviceConfidence ApplyQualityLimit(
        StrategyAdviceConfidence confidence,
        StrategyAdviceConfidence maxConfidence)
    {
        return confidence > maxConfidence ? maxConfidence : confidence;
    }

    private static RaceAssistantAdviceType MapAdviceType(VoiceQuestionIntent intent)
    {
        return MapAdviceType(intent, RaceAssistantMode.RaceStintManagement);
    }

    private static RaceAssistantAdviceType MapAdviceType(VoiceQuestionIntent intent, RaceAssistantMode mode)
    {
        if (intent == VoiceQuestionIntent.PIT_DECISION &&
            mode is RaceAssistantMode.Practice or RaceAssistantMode.NoTelemetry or RaceAssistantMode.WaitingForTelemetry)
        {
            return RaceAssistantAdviceType.GeneralStatus;
        }

        return intent switch
        {
            VoiceQuestionIntent.PIT_DECISION => RaceAssistantAdviceType.PitWindow,
            VoiceQuestionIntent.TYRE_STATUS => RaceAssistantAdviceType.TyreManagement,
            VoiceQuestionIntent.ERS_STRATEGY => RaceAssistantAdviceType.ErsManagement,
            VoiceQuestionIntent.GAP_ANALYSIS => RaceAssistantAdviceType.Defense,
            VoiceQuestionIntent.SETUP_FEEDBACK => RaceAssistantAdviceType.SetupFeedback,
            VoiceQuestionIntent.CORNER_ANALYSIS => RaceAssistantAdviceType.Corner,
            VoiceQuestionIntent.DAMAGE_STATUS => RaceAssistantAdviceType.Damage,
            _ => RaceAssistantAdviceType.GeneralStatus
        };
    }

    private static string BuildGenericSummary(VoiceQuestionIntent intent, RaceAssistantMode mode)
    {
        if (intent == VoiceQuestionIntent.PIT_DECISION &&
            mode is RaceAssistantMode.Practice or RaceAssistantMode.NoTelemetry or RaceAssistantMode.WaitingForTelemetry)
        {
            return "暂不做正赛进站判断。";
        }

        return intent switch
        {
            VoiceQuestionIntent.PIT_DECISION => "进站证据不够明确。",
            VoiceQuestionIntent.TYRE_STATUS => "轮胎状态需继续观察。",
            VoiceQuestionIntent.ERS_STRATEGY => "ERS 需要保守管理。",
            VoiceQuestionIntent.GAP_ANALYSIS => "前后车压力需继续观察。",
            _ => "当前状态按保守节奏处理。"
        };
    }

    private static string BuildGenericAction(VoiceQuestionIntent intent, RaceAssistantMode mode, bool hasMissingData)
    {
        if (hasMissingData)
        {
            return intent == VoiceQuestionIntent.PIT_DECISION &&
                   mode is RaceAssistantMode.Practice or RaceAssistantMode.NoTelemetry or RaceAssistantMode.WaitingForTelemetry
                ? "当前数据不足，暂不做进站判断。"
                : "数据不足，先保守观察。";
        }

        if (intent == VoiceQuestionIntent.PIT_DECISION &&
            mode is RaceAssistantMode.Practice or RaceAssistantMode.NoTelemetry or RaceAssistantMode.WaitingForTelemetry)
        {
            return mode == RaceAssistantMode.Practice
                ? "练习赛先多跑一圈收集胎耗。"
                : "当前未接入实时数据，仅能通用建议。";
        }

        return intent switch
        {
            VoiceQuestionIntent.PIT_DECISION => "暂不进，再观察一圈。",
            VoiceQuestionIntent.TYRE_STATUS => "先保胎，观察一圈。",
            VoiceQuestionIntent.ERS_STRATEGY => "ERS偏低，直道先省电。",
            VoiceQuestionIntent.GAP_ANALYSIS => "保持节奏，优先防守。",
            _ => "节奏稳定，先保守执行。"
        };
    }
}
