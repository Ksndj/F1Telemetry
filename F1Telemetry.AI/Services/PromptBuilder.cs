using System.Text;
using F1Telemetry.AI.Models;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Core.Formatting;

namespace F1Telemetry.AI.Services;

/// <summary>
/// Builds prompt messages for the fixed DeepSeek JSON analysis contract.
/// </summary>
public sealed class PromptBuilder
{
    /// <summary>
    /// Builds the system and user messages for an analysis request.
    /// </summary>
    public AIPromptMessages BuildMessages(AIAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.StrategyQuestionContext is not null)
        {
            return BuildRaceAssistantMessages(context.StrategyQuestionContext);
        }

        var systemMessage = """
You are an F1 race engineer creating a compact post-race F1 25 summary.
Return only valid JSON and no extra text.
The JSON must contain exactly these keys: summary, keyProblems, strategyReview, tyreReview, ersFuelReview, opponentReview, improvements, tts.
Use Chinese. The report fields must be detailed enough for a real post-race review, but do not include raw UDP payloads.
keyProblems must contain at least 2 concrete items when evidence exists.
improvements must contain at least 3 concrete next-race suggestions.
tts must contain one short broadcast-ready conclusion, target 35 Chinese characters or fewer, 禁止长段分析.
Only summarize the completed or user-confirmed race. Do not repeat raw event logs.
F1 25 不能进站加油或补油；燃油不足时只能建议省油、抬滑、短换或控制节奏，不要建议进站加油或补油。
换胎建议必须服从 tyre inventory constraints；禁止推荐不存在、不可用、超出磨损上限或赛前未输入的轮胎。
策略播报需综合圈速、对手圈速、天气预报、进站窗口、排名位置、对手进站、胎磨、push/省油和历史会话摘要。
ERS must be written in MJ, never as a large integer joule value.
""";

        var userMessage = BuildUserMessage(context);
        return new AIPromptMessages
        {
            SystemMessage = systemMessage,
            UserMessage = userMessage
        };
    }

    private static AIPromptMessages BuildRaceAssistantMessages(StrategyQuestionContext context)
    {
        var systemMessage = """
你是 F1 25 比赛工程师，正在回答车手的实时策略问题。
Return only valid JSON and no extra text.
JSON must contain exactly these keys: adviceType, summary, reason, recommendedAction, confidence, riskLevel, requiredData, missingData, tts.
Allowed adviceType values: PitWindow, TyreManagement, FuelSaving, ErsManagement, Attack, Defense, Undercut, Overcut, SafetyCar, SetupFeedback, Corner, Damage, GeneralStatus, PostRaceReview, Unknown.
Allowed confidence values: Low, Medium, High.
Allowed riskLevel values: Unknown, Low, Medium, High.
回答必须基于给定摘要，不编造没有的数据。
比赛中只给短建议；tts 必须是不超过 35 个中文字符的一句话。
所有策略建议必须带 confidence；高风险建议必须带 riskLevel。
不得使用“必须进站”“一定 undercut 成功”等绝对化命令。
高置信规则信号不能被覆盖，只能综合表达。
如果数据质量过旧或缺失，confidence 必须降低并写入 missingData。
换胎建议必须检查 weather、trackWetness、tyreInventory；没有天气或湿度数据时，不允许建议半雨胎或全雨胎。
赛制约束：Practice 只回答练习/轮胎/油耗/设置/长距离趋势；Qualifying 不回答正赛进站/undercut/overcut；Race 可回答进站/攻防/燃油/ERS/策略风险；PostRace 可详细复盘。
TTS 只能来自 tts 字段，不要把长 reason 放进 tts。
""";

        return new AIPromptMessages
        {
            SystemMessage = systemMessage,
            UserMessage = BuildRaceAssistantUserMessage(context)
        };
    }

    private static string BuildRaceAssistantUserMessage(StrategyQuestionContext context)
    {
        var snapshot = context.Snapshot;
        var builder = new StringBuilder();
        builder.AppendLine("Race assistant question context:");
        builder.AppendLine($"SessionUid: {context.SessionUid?.ToString() ?? "n/a"}");
        builder.AppendLine($"Mode: {context.Mode}");
        builder.AppendLine($"Intent: {context.Intent}");
        builder.AppendLine($"Question: {context.Question}");
        builder.AppendLine($"Intent template: {context.IntentPromptTemplate}");
        builder.AppendLine();

        builder.AppendLine("Snapshot:");
        builder.AppendLine($"Session mode: {snapshot.SessionMode}");
        builder.AppendLine($"Lap: {FormatNullable(snapshot.CurrentLap)}/{FormatNullable(snapshot.TotalLaps)}");
        builder.AppendLine($"Position: {FormatNullable(snapshot.Position)}");
        builder.AppendLine($"Current tyre: {snapshot.CurrentTyre}");
        builder.AppendLine($"Tyre age laps: {FormatNullable(snapshot.TyreAgeLaps)}");
        builder.AppendLine($"Tyre wear percent: {FormatNullable(snapshot.TyreWearPercent)}");
        builder.AppendLine($"Fuel remaining laps: {FormatNullable(snapshot.FuelRemainingLaps)}");
        builder.AppendLine($"ERS store energy: {EnergyFormatter.FormatErs(snapshot.ErsStoreEnergy)}");
        builder.AppendLine($"Gap to front ms: {FormatNullable(snapshot.GapToFrontMs)}");
        builder.AppendLine($"Gap to behind ms: {FormatNullable(snapshot.GapToBehindMs)}");
        builder.AppendLine($"Weather: {snapshot.WeatherSummary}");
        builder.AppendLine($"Track wetness: {FormatNullable(snapshot.TrackWetness)}");
        builder.AppendLine($"Tyre inventory: {snapshot.TyreInventorySummary}");
        builder.AppendLine($"Damage: {snapshot.DamageSummary}");
        builder.AppendLine();

        builder.AppendLine("Snapshot quality:");
        builder.AppendLine(snapshot.Quality.Summary);
        builder.AppendLine($"Quality missingData: {string.Join(", ", snapshot.Quality.MissingData)}");
        builder.AppendLine($"Max recommended confidence: {snapshot.Quality.MaxRecommendedConfidence}");
        builder.AppendLine();

        builder.AppendLine("RecentLapTrendSummary:");
        builder.AppendLine($"Lap count: {snapshot.RecentLapTrend.LapCount}");
        builder.AppendLine($"Lap time trend: {snapshot.RecentLapTrend.LapTimeTrend}");
        builder.AppendLine($"Tyre wear trend: {snapshot.RecentLapTrend.TyreWearTrend}");
        builder.AppendLine($"Fuel trend: {snapshot.RecentLapTrend.FuelTrend}");
        builder.AppendLine($"ERS trend: {snapshot.RecentLapTrend.ErsTrend}");
        builder.AppendLine();

        builder.AppendLine("Rule signals:");
        foreach (var signal in snapshot.RuleSignals)
        {
            builder.AppendLine($"- {signal.SignalType}: adviceType={signal.AdviceType}, confidence={signal.Confidence}, risk={signal.RiskLevel}, summary={signal.Summary}, action={signal.RecommendedAction}, missing=[{string.Join(", ", signal.MissingData)}]");
        }

        builder.AppendLine();
        builder.AppendLine("PitDecisionSignal:");
        builder.AppendLine($"- confidence={snapshot.PitDecision.Signal.Confidence}, summary={snapshot.PitDecision.Signal.Summary}, action={snapshot.PitDecision.Signal.RecommendedAction}, missing=[{string.Join(", ", snapshot.PitDecision.Inputs.MissingData)}]");
        builder.AppendLine("SafetyCarPitOpportunitySignal:");
        builder.AppendLine($"- confidence={snapshot.SafetyCarPitOpportunity.Signal.Confidence}, summary={snapshot.SafetyCarPitOpportunity.Signal.Summary}, action={snapshot.SafetyCarPitOpportunity.Signal.RecommendedAction}, missing=[{string.Join(", ", snapshot.SafetyCarPitOpportunity.Inputs.MissingData)}]");
        builder.AppendLine();

        if (snapshot.RecentEvents.Count > 0)
        {
            builder.AppendLine("Recent events:");
            foreach (var message in snapshot.RecentEvents.Take(8))
            {
                builder.AppendLine($"- {message}");
            }
        }

        builder.AppendLine($"RequiredData: {string.Join(", ", context.RequiredData)}");
        builder.AppendLine($"MissingData: {string.Join(", ", context.MissingData)}");
        builder.AppendLine("Do not mention raw UDP. Do not invent missing values.");
        return builder.ToString();
    }

    private static string BuildUserMessage(AIAnalysisContext context)
    {
        var builder = new StringBuilder();
        var recentLaps = context.RecentLaps ?? Array.Empty<LapSummary>();
        var recentEvents = context.RecentEvents ?? Array.Empty<string>();
        var sessionTypeText = string.IsNullOrWhiteSpace(context.SessionTypeText)
            ? SessionModeFormatter.FormatDisplayName(context.SessionMode)
            : context.SessionTypeText.Trim();
        var sessionFocusText = string.IsNullOrWhiteSpace(context.SessionFocusText)
            ? SessionModeFormatter.FormatFocus(context.SessionMode)
            : context.SessionFocusText.Trim();

        builder.AppendLine("Post-race summary context:");
        builder.AppendLine($"Session mode: {context.SessionMode}");
        builder.AppendLine($"赛制：{sessionTypeText}");
        builder.AppendLine($"当前赛制重点：{sessionFocusText}");
        builder.AppendLine($"赛制提示：{SessionModeFormatter.FormatPromptGuidance(context.SessionMode)}");
        builder.AppendLine();

        AppendLapSection(builder, "Latest lap", context.LatestLap);
        AppendLapSection(builder, "Best lap", context.BestLap);

        if (recentLaps.Count > 0)
        {
            builder.AppendLine("Recent laps:");
            foreach (var lap in recentLaps)
            {
                builder.Append("  - ");
                builder.Append(DescribeLap(lap));
                builder.AppendLine();
            }
        }

        builder.AppendLine($"Current fuel remaining laps: {FormatNullable(context.CurrentFuelRemainingLaps)}");
        builder.AppendLine($"Current fuel in tank: {FormatNullable(context.CurrentFuelInTank)}");
        builder.AppendLine($"Current ERS store energy: {EnergyFormatter.FormatErs(context.CurrentErsStoreEnergy)}");
        builder.AppendLine($"Current tyre: {context.CurrentTyre ?? "-"}");
        builder.AppendLine($"Current tyre age laps: {FormatNullable(context.CurrentTyreAgeLaps)}");
        builder.AppendLine($"Gap to front in ms: {FormatNullable(context.GapToFrontInMs)}");
        builder.AppendLine($"Gap to behind in ms: {FormatNullable(context.GapToBehindInMs)}");
        AppendContextLine(builder, "Weather forecast:", context.WeatherForecastSummary);
        AppendContextLine(builder, "Pit window:", context.PitWindowSummary);
        AppendContextLine(builder, "Position strategy:", context.PositionStrategySummary);
        AppendContextLine(builder, "Opponent strategy:", context.OpponentStrategySummary);
        AppendContextLine(builder, "Tyre inventory constraints:", context.TyreInventorySummary);
        AppendContextLine(builder, "Historical sessions:", context.HistoricalSessionSummary);
        if (!string.IsNullOrWhiteSpace(context.TelemetryAnalysisSummary))
        {
            builder.AppendLine($"Driving trend summary: {context.TelemetryAnalysisSummary.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(context.RealtimeEngineerAdviceSummary))
        {
            builder.AppendLine($"Realtime engineer advice: {context.RealtimeEngineerAdviceSummary.Trim()}");
            builder.AppendLine("For this realtime request, tts must be one short Chinese driving action and must not summarize the whole session.");
        }

        if (!string.IsNullOrWhiteSpace(context.DamageSummary))
        {
            builder.AppendLine($"Damage summary: {context.DamageSummary.Trim()}");
        }

        if (recentEvents.Count > 0)
        {
            builder.AppendLine("Recent events:");
            foreach (var eventSummary in recentEvents)
            {
                builder.Append("  - ");
                builder.AppendLine(eventSummary);
            }
        }

        builder.AppendLine("Use only these summaries and state values. Do not mention raw telemetry or low-level details.");
        builder.AppendLine("换胎建议必须先检查 tyre inventory constraints；若没有合适库存，明确建议保胎、push、省油或延后进站。");
        builder.Append("UI report fields must be detailed. tts 只写适合 TTS 播报的一句短结论，目标不超过 35 个中文字符。");
        return builder.ToString();
    }

    private static void AppendContextLine(StringBuilder builder, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.Append(label);
        builder.Append(' ');
        builder.AppendLine(value.Trim());
    }

    private static void AppendLapSection(StringBuilder builder, string label, LapSummary? lap)
    {
        builder.Append(label);
        builder.Append(": ");
        builder.AppendLine(DescribeLap(lap));
    }

    private static string DescribeLap(LapSummary? lap)
    {
        if (lap is null)
        {
            return "n/a";
        }

        var timeText = lap.LapTimeInMs is null ? "n/a" : $"{lap.LapTimeInMs} ms";
        var fuelUsedText = lap.FuelUsedLitres is null ? "n/a" : $"{lap.FuelUsedLitres:0.00} L";
        var validText = lap.IsValid ? "valid" : "invalid";
        return $"Lap {lap.LapNumber}, time {timeText}, fuel used {fuelUsedText}, {validText}";
    }

    private static string FormatNullable<T>(T? value)
        where T : struct
    {
        return value.HasValue ? value.Value.ToString()! : "n/a";
    }
}
