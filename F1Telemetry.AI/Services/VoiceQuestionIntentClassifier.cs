using F1Telemetry.AI.Models;

namespace F1Telemetry.AI.Services;

/// <summary>
/// Classifies driver race-assistant questions with conservative keyword matching.
/// </summary>
public sealed class VoiceQuestionIntentClassifier
{
    private static readonly (VoiceQuestionIntent Intent, string[] Keywords)[] KeywordGroups =
    [
        (VoiceQuestionIntent.TYRE_STATUS, ["轮胎怎么样", "胎还能撑几圈", "轮胎还能撑几圈", "胎耗怎么样", "要不要保胎"]),
        (VoiceQuestionIntent.PIT_DECISION, ["现在进站吗", "要不要进站", "安全车要进吗", "什么时候进站", "还能等几圈", "该不该进站", "进站吗"]),
        (VoiceQuestionIntent.SETUP_FEEDBACK, ["推头", "出弯不稳", "打滑", "轮胎过热", "刹不住"]),
        (VoiceQuestionIntent.GAP_ANALYSIS, ["前车追得上吗", "后车能守住吗", "差距怎么样", "能不能进攻", "要不要防守", "前车", "后车"]),
        (VoiceQuestionIntent.ERS_STRATEGY, ["ERS 怎么用", "ERS怎么用", "要不要省电", "还剩多少电", "什么时候用电", "省电"]),
        (VoiceQuestionIntent.GENERAL_STATUS, ["现在情况", "给我总结", "当前策略", "现在怎么跑"]),
        (VoiceQuestionIntent.CORNER_ANALYSIS, ["哪个弯亏最多", "这个弯怎么跑", "弯角损失", "刹车点"]),
        (VoiceQuestionIntent.DAMAGE_STATUS, ["车损怎么样", "要不要修车", "前翼坏了吗", "刹车坏了吗", "车损"])
    ];

    /// <summary>
    /// Classifies the supplied question into a race-assistant intent.
    /// </summary>
    /// <param name="question">The user question.</param>
    public VoiceQuestionIntent Classify(string? question)
    {
        var normalized = Normalize(question);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return VoiceQuestionIntent.UNKNOWN;
        }

        foreach (var (intent, keywords) in KeywordGroups)
        {
            if (keywords.Any(keyword => normalized.Contains(Normalize(keyword), StringComparison.OrdinalIgnoreCase)))
            {
                return intent;
            }
        }

        return VoiceQuestionIntent.UNKNOWN;
    }

    private static string Normalize(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
    }
}
