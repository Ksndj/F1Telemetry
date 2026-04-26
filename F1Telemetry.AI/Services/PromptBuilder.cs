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

        var systemMessage = """
You are an F1 race engineer.
Return only valid JSON and no extra text.
The JSON must contain exactly these keys: summary, tyreAdvice, fuelAdvice, trafficAdvice, ttsText.
Each value must be a concise string.
""";

        var userMessage = BuildUserMessage(context);
        return new AIPromptMessages
        {
            SystemMessage = systemMessage,
            UserMessage = userMessage
        };
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
        builder.AppendLine($"Current ERS store energy: {FormatNullable(context.CurrentErsStoreEnergy)}");
        builder.AppendLine($"Current tyre: {context.CurrentTyre ?? "-"}");
        builder.AppendLine($"Current tyre age laps: {FormatNullable(context.CurrentTyreAgeLaps)}");
        builder.AppendLine($"Gap to front in ms: {FormatNullable(context.GapToFrontInMs)}");
        builder.AppendLine($"Gap to behind in ms: {FormatNullable(context.GapToBehindInMs)}");

        if (recentEvents.Count > 0)
        {
            builder.AppendLine("Recent events:");
            foreach (var eventSummary in recentEvents)
            {
                builder.Append("  - ");
                builder.AppendLine(eventSummary);
            }
        }

        builder.Append("Use only these summaries and state values. Do not mention raw telemetry or low-level details.");
        return builder.ToString();
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
