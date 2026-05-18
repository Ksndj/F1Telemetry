using System.Globalization;
using System.Text.Json;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents one stored AI report row in the post-race review.
/// </summary>
public sealed record PostRaceReviewAiReportRowViewModel
{
    /// <summary>
    /// Gets the formatted lap label.
    /// </summary>
    public string LapText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted report timestamp.
    /// </summary>
    public string TimeText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted report timestamp used by WPF bindings.
    /// </summary>
    public string GeneratedAtText => TimeText;

    /// <summary>
    /// Gets the report status text.
    /// </summary>
    public string StatusText { get; init; } = "-";

    /// <summary>
    /// Gets the compact row title used by WPF bindings.
    /// </summary>
    public string Title => $"{LapText} · {StatusText}";

    /// <summary>
    /// Gets the AI summary text.
    /// </summary>
    public string SummaryText { get; init; } = "-";

    /// <summary>
    /// Gets the tyre advice text.
    /// </summary>
    public string TyreAdviceText { get; init; } = "-";

    /// <summary>
    /// Gets the fuel advice text.
    /// </summary>
    public string FuelAdviceText { get; init; } = "-";

    /// <summary>
    /// Gets the traffic advice text.
    /// </summary>
    public string TrafficAdviceText { get; init; } = "-";

    /// <summary>
    /// Gets the speech text saved with the report.
    /// </summary>
    public string TtsText { get; init; } = "-";

    /// <summary>
    /// Gets the detailed post-race report text shown in the UI.
    /// </summary>
    public string DetailReportText { get; init; } = "-";

    /// <summary>
    /// Creates a review AI report row from a stored report.
    /// </summary>
    /// <param name="report">The stored AI report to project.</param>
    public static PostRaceReviewAiReportRowViewModel FromStoredReport(StoredAiReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new PostRaceReviewAiReportRowViewModel
        {
            LapText = $"Lap {report.LapNumber}",
            TimeText = report.CreatedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            StatusText = report.IsSuccess ? "成功" : $"失败：{NormalizeText(report.ErrorMessage)}",
            SummaryText = NormalizeText(report.Summary),
            TyreAdviceText = NormalizeText(report.TyreAdvice),
            FuelAdviceText = NormalizeText(report.FuelAdvice),
            TrafficAdviceText = NormalizeText(report.TrafficAdvice),
            TtsText = NormalizeText(report.TtsText),
            DetailReportText = "-"
        };
    }

    /// <summary>
    /// Creates a review row from a stored race-engineer detail report.
    /// </summary>
    /// <param name="report">The stored race-engineer report.</param>
    public static PostRaceReviewAiReportRowViewModel FromStoredRaceEngineerReport(StoredRaceEngineerReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new PostRaceReviewAiReportRowViewModel
        {
            LapText = report.LapNumber is null ? "Race" : $"Lap {report.LapNumber}",
            TimeText = report.CreatedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            StatusText = report.IsSuccess ? "详细报告" : $"失败：{NormalizeText(report.ErrorMessage)}",
            SummaryText = NormalizeText(report.Summary),
            TyreAdviceText = ExtractDetail(report.DetailJson, "tyreReview"),
            FuelAdviceText = ExtractDetail(report.DetailJson, "ersFuelReview"),
            TrafficAdviceText = ExtractDetail(report.DetailJson, "opponentReview"),
            TtsText = NormalizeText(report.SpokenText),
            DetailReportText = BuildDetailReportText(report.DetailJson)
        };
    }

    private static string BuildDetailReportText(string? detailJson)
    {
        if (string.IsNullOrWhiteSpace(detailJson))
        {
            return "-";
        }

        try
        {
            using var document = JsonDocument.Parse(detailJson);
            var root = document.RootElement;
            var parts = new List<string>();
            AppendProperty(parts, root, "summary", "比赛结论");
            AppendArray(parts, root, "keyProblems", "主要问题");
            AppendProperty(parts, root, "strategyReview", "策略判断");
            AppendProperty(parts, root, "tyreReview", "轮胎判断");
            AppendProperty(parts, root, "ersFuelReview", "ERS/燃油判断");
            AppendProperty(parts, root, "opponentReview", "前后车判断");
            AppendArray(parts, root, "improvements", "下次改进");
            return parts.Count == 0 ? "-" : string.Join(Environment.NewLine, parts);
        }
        catch (JsonException)
        {
            return NormalizeText(detailJson);
        }
    }

    private static string ExtractDetail(string? detailJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(detailJson))
        {
            return "-";
        }

        try
        {
            using var document = JsonDocument.Parse(detailJson);
            return document.RootElement.TryGetProperty(propertyName, out var property) &&
                   property.ValueKind == JsonValueKind.String
                ? NormalizeText(property.GetString())
                : "-";
        }
        catch (JsonException)
        {
            return "-";
        }
    }

    private static void AppendProperty(List<string> parts, JsonElement root, string propertyName, string title)
    {
        if (root.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(property.GetString()))
        {
            parts.Add($"{title}：{property.GetString()!.Trim()}");
        }
    }

    private static void AppendArray(List<string> parts, JsonElement root, string propertyName, string title)
    {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var values = property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            .Select(item => item.GetString()!.Trim())
            .ToArray();
        if (values.Length > 0)
        {
            parts.Add($"{title}：{string.Join("；", values)}");
        }
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }
}
