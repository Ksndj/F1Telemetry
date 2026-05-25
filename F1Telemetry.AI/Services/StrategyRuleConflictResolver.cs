using F1Telemetry.AI.Models;

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
            ContainsAny(aiAction, "用电", "释放", "push", "推进"))
        {
            return true;
        }

        if (ContainsAny(ruleAction, "防守") && ContainsAny(aiAction, "进攻", "追击"))
        {
            return true;
        }

        if (ContainsAny(ruleAction, "保胎", "保守") && ContainsAny(aiAction, "继续推", "全力推"))
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
