using F1Telemetry.AI.Models;
using F1Telemetry.Analytics.Strategy;

namespace F1Telemetry.AI.Services;

/// <summary>
/// Ensures high-confidence rule signals cannot be overridden by AI advice.
/// </summary>
public sealed class StrategyRuleConflictResolver
{
    /// <summary>
    /// Resolves conflicts between parsed AI advice and high-confidence rules.
    /// </summary>
    public StrategyAdviceResult Resolve(
        StrategyAdviceResult aiAdvice,
        StrategyQuestionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Intent == VoiceQuestionIntent.PIT_DECISION &&
            context.Mode is RaceAssistantMode.Practice or RaceAssistantMode.NoTelemetry or RaceAssistantMode.WaitingForTelemetry &&
            aiAdvice.AdviceType is RaceAssistantAdviceType.PitWindow or RaceAssistantAdviceType.Undercut or RaceAssistantAdviceType.Overcut)
        {
            return aiAdvice with
            {
                AdviceType = RaceAssistantAdviceType.GeneralStatus,
                Summary = context.Mode == RaceAssistantMode.Practice
                    ? "练习赛不做正赛进站窗口判断。"
                    : "当前未接入实时遥测，仅能给通用建议。",
                RecommendedAction = context.Mode == RaceAssistantMode.Practice
                    ? "练习赛先多跑一圈收集胎耗。"
                    : "当前数据不足，暂不做进站判断。",
                Confidence = StrategyAdviceConfidence.Low,
                RiskLevel = StrategyRiskLevel.Unknown,
                MissingData = aiAdvice.MissingData.Concat(context.MissingData).Distinct(StringComparer.Ordinal).ToArray(),
                Tts = context.Mode == RaceAssistantMode.Practice
                    ? "练习赛先多跑一圈收集胎耗。"
                    : "当前未接入实时数据，先保守。",
                Warnings = aiAdvice.Warnings
                    .Append("Practice/无遥测模式禁止输出正赛进站窗口，已降级。")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            };
        }

        foreach (var signal in context.Snapshot.RuleSignals.Where(signal => signal.Confidence == StrategyAdviceConfidence.High))
        {
            if (Conflicts(signal, aiAdvice))
            {
                return RuleBasedFallbackAdviceService.FromSignal(
                    signal,
                    context,
                    "AI 输出和高置信规则冲突，已优先采用规则结果。",
                    isFallback: false) with
                {
                    Confidence = StrategyAdviceConfidence.Low,
                    Warnings = aiAdvice.Warnings
                        .Append("AI 输出和高置信规则冲突，已降级。")
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
                };
            }
        }

        return aiAdvice with
        {
            Confidence = aiAdvice.Confidence > context.Snapshot.Quality.MaxRecommendedConfidence
                ? context.Snapshot.Quality.MaxRecommendedConfidence
                : aiAdvice.Confidence,
            MissingData = aiAdvice.MissingData
                .Concat(context.MissingData)
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static bool Conflicts(StrategyRuleSignal signal, StrategyAdviceResult aiAdvice)
    {
        var ruleAction = Normalize(signal.RecommendedAction);
        var aiAction = Normalize(aiAdvice.RecommendedAction + " " + aiAdvice.Summary + " " + aiAdvice.Tts);

        if (ContainsAny(ruleAction, "暂不进", "不进", "等待", "观察") &&
            ContainsAny(aiAction, "现在进", "立即进", "进站") &&
            !ContainsAny(aiAction, "不进", "暂不进"))
        {
            return true;
        }

        if (ContainsAny(ruleAction, "省电", "保留") &&
            ContainsAny(aiAction, "用电", "释放", "push", "推进") &&
            !ContainsAny(aiAction, "不用电", "不释放", "不push", "不推进"))
        {
            return true;
        }

        if (ContainsAny(ruleAction, "防守") && ContainsAny(aiAction, "进攻", "追击") &&
            !ContainsAny(aiAction, "不进攻", "不追击"))
        {
            return true;
        }

        if (ContainsAny(ruleAction, "保胎", "保守") && ContainsAny(aiAction, "继续推", "全力推") &&
            !ContainsAny(aiAction, "不继续推", "不全力推"))
        {
            return true;
        }

        return false;
    }

    private static string Normalize(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }
}
