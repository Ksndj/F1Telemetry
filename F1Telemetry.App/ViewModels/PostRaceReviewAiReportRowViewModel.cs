using System.Globalization;
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
            TtsText = NormalizeText(report.TtsText)
        };
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }
}
