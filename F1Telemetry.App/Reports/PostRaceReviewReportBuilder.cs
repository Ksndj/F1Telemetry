using System.Globalization;
using System.Text;
using System.Text.Json;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.App.Reports;

/// <summary>
/// Builds exportable post-race review reports from loaded historical data.
/// </summary>
public sealed class PostRaceReviewReportBuilder
{
    private const string SchemaVersion = "v2-m7-post-race-review";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Builds a Markdown post-race review report.
    /// </summary>
    /// <param name="data">The loaded review data.</param>
    /// <returns>The Markdown report text.</returns>
    public string BuildMarkdown(PostRaceReviewReportData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var builder = new StringBuilder();
        builder.AppendLine("# F1Telemetry 历史会话复盘报告");
        builder.AppendLine();
        builder.AppendLine($"生成时间：{FormatTimestamp(data.GeneratedAt)}");
        builder.AppendLine($"应用版本：{NormalizeText(data.ApplicationVersion)}");
        builder.AppendLine("数据边界：本报告基于历史 SQLite 摘要数据生成，不包含 API Key、设置文件或原始 UDP / JSONL payload。");
        builder.AppendLine();

        builder.AppendLine("## 会话摘要");
        builder.AppendLine();
        builder.AppendLine($"- 会话：{NormalizeText(data.Session.SummaryText)}");
        builder.AppendLine($"- SessionUid：{NormalizeText(data.Session.SessionUid)}");
        builder.AppendLine($"- 赛道：{NormalizeText(data.Session.TrackText)}");
        builder.AppendLine($"- 赛制：{NormalizeText(data.Session.SessionTypeText)}");
        builder.AppendLine($"- 开始时间：{NormalizeText(data.Session.StartedAtText)}");
        builder.AppendLine($"- 结束时间：{NormalizeText(data.Session.EndedAtText)}");
        builder.AppendLine($"- 时长：{NormalizeText(data.Session.DurationText)}");
        builder.AppendLine();

        AppendMetricTable(builder, data.SummaryMetrics);
        AppendLapTable(builder, data.Laps);
        AppendStintTable(builder, data.Stints);
        AppendEventTable(builder, data.Events);
        AppendAiReportTable(builder, data.AiReports);

        builder.AppendLine("## 数据限制");
        builder.AppendLine();
        builder.AppendLine("- 历史单圈未保存四轮胎磨数据，无法生成四轮胎磨趋势。");
        builder.AppendLine("- stint 摘要仅基于已保存的 StartTyre / EndTyre 标签轻量推断。");

        return builder.ToString();
    }

    /// <summary>
    /// Builds a JSON post-race review report.
    /// </summary>
    /// <param name="data">The loaded review data.</param>
    /// <returns>The JSON report text.</returns>
    public string BuildJson(PostRaceReviewReportData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var report = new JsonReport(
            SchemaVersion,
            data.GeneratedAt,
            NormalizeText(data.ApplicationVersion),
            new JsonSession(
                data.Session.SessionId,
                data.Session.SessionUid,
                data.Session.TrackText,
                data.Session.SessionTypeText,
                data.Session.StartedAtText,
                data.Session.EndedAtText,
                data.Session.DurationText,
                data.Session.SummaryText),
            data.SummaryMetrics.Select(row => new JsonMetric(row.Label, row.Value, row.Detail)).ToArray(),
            data.Laps.Select(ToJsonLap).ToArray(),
            data.Stints.Select(row => new JsonStint(row.StintText, row.LapRangeText, row.TyreText, row.EvidenceText)).ToArray(),
            data.Events.Select(ToJsonEvent).ToArray(),
            data.AiReports.Select(ToJsonAiReport).ToArray(),
            "历史单圈未保存四轮胎磨数据，无法生成四轮胎磨趋势。");

        return JsonSerializer.Serialize(report, JsonOptions);
    }

    private static void AppendMetricTable(StringBuilder builder, IReadOnlyList<PostRaceReviewMetricRowViewModel> metrics)
    {
        builder.AppendLine("## 摘要指标");
        builder.AppendLine();
        if (metrics.Count == 0)
        {
            builder.AppendLine("暂无摘要指标。");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| 指标 | 数值 | 说明 |");
        builder.AppendLine("| --- | --- | --- |");
        foreach (var metric in metrics)
        {
            AppendTableRow(builder, metric.Label, metric.Value, metric.Detail);
        }

        builder.AppendLine();
    }

    private static void AppendLapTable(StringBuilder builder, IReadOnlyList<StoredLap> laps)
    {
        builder.AppendLine("## 单圈摘要");
        builder.AppendLine();
        if (laps.Count == 0)
        {
            builder.AppendLine("暂无单圈记录。");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Lap | 圈速 | S1 | S2 | S3 | 有效 | 燃油 | ERS | 起始胎 | 结束胎 |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var lap in laps)
        {
            AppendTableRow(
                builder,
                lap.LapNumber.ToString(CultureInfo.InvariantCulture),
                FormatLapTime(lap.LapTimeInMs),
                FormatLapTime(lap.Sector1TimeInMs),
                FormatLapTime(lap.Sector2TimeInMs),
                FormatLapTime(lap.Sector3TimeInMs),
                lap.IsValid ? "是" : "否",
                lap.FuelUsedLitres is null ? "-" : $"{lap.FuelUsedLitres.Value:0.00} L",
                lap.ErsUsed is null ? "-" : $"{lap.ErsUsed.Value / 1_000_000f:0.00} MJ",
                NormalizeText(lap.StartTyre),
                NormalizeText(lap.EndTyre));
        }

        builder.AppendLine();
    }

    private static void AppendStintTable(StringBuilder builder, IReadOnlyList<PostRaceReviewStintRowViewModel> stints)
    {
        builder.AppendLine("## Stint 摘要");
        builder.AppendLine();
        if (stints.Count == 0)
        {
            builder.AppendLine("暂无可推断 stint。");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Stint | 圈段 | 轮胎 | 证据 |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var stint in stints)
        {
            AppendTableRow(builder, stint.StintText, stint.LapRangeText, stint.TyreText, stint.EvidenceText);
        }

        builder.AppendLine();
    }

    private static void AppendEventTable(StringBuilder builder, IReadOnlyList<StoredEvent> events)
    {
        builder.AppendLine("## 事件时间线");
        builder.AppendLine();
        if (events.Count == 0)
        {
            builder.AppendLine("暂无事件记录。");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Lap | 时间 | 类型 | 严重度 | 目标 | 消息 |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var storedEvent in events)
        {
            var row = PostRaceReviewEventRowViewModel.FromStoredEvent(storedEvent);
            AppendTableRow(
                builder,
                row.LapText,
                row.TimeText,
                row.EventTypeText,
                row.SeverityText,
                row.TargetText,
                row.Message);
        }

        builder.AppendLine();
    }

    private static void AppendAiReportTable(StringBuilder builder, IReadOnlyList<StoredAiReport> reports)
    {
        builder.AppendLine("## AI 每圈点评");
        builder.AppendLine();
        if (reports.Count == 0)
        {
            builder.AppendLine("暂无 AI 报告。");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Lap | 时间 | 状态 | 摘要 | 轮胎建议 | 燃油建议 | 交通建议 | 播报文本 |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var report in reports)
        {
            var row = PostRaceReviewAiReportRowViewModel.FromStoredReport(report);
            AppendTableRow(
                builder,
                row.LapText,
                row.TimeText,
                row.StatusText,
                row.SummaryText,
                row.TyreAdviceText,
                row.FuelAdviceText,
                row.TrafficAdviceText,
                row.TtsText);
        }

        builder.AppendLine();
    }

    private static JsonLap ToJsonLap(StoredLap lap)
    {
        return new JsonLap(
            lap.LapNumber,
            lap.LapTimeInMs,
            lap.Sector1TimeInMs,
            lap.Sector2TimeInMs,
            lap.Sector3TimeInMs,
            lap.IsValid,
            lap.AverageSpeedKph,
            lap.FuelUsedLitres,
            lap.ErsUsed,
            NormalizeText(lap.StartTyre),
            NormalizeText(lap.EndTyre),
            lap.CreatedAt);
    }

    private static JsonEvent ToJsonEvent(StoredEvent storedEvent)
    {
        var row = PostRaceReviewEventRowViewModel.FromStoredEvent(storedEvent);
        return new JsonEvent(
            storedEvent.Id,
            storedEvent.EventType.ToString(),
            row.EventTypeText,
            storedEvent.Severity.ToString(),
            row.SeverityText,
            storedEvent.LapNumber,
            storedEvent.VehicleIdx,
            storedEvent.DriverName,
            storedEvent.Message,
            storedEvent.CreatedAt);
    }

    private static JsonAiReport ToJsonAiReport(StoredAiReport report)
    {
        return new JsonAiReport(
            report.Id,
            report.LapNumber,
            report.Summary,
            report.TyreAdvice,
            report.FuelAdvice,
            report.TrafficAdvice,
            report.TtsText,
            report.IsSuccess,
            report.ErrorMessage,
            report.CreatedAt);
    }

    private static void AppendTableRow(StringBuilder builder, params string?[] cells)
    {
        builder.Append("| ");
        builder.Append(string.Join(" | ", cells.Select(EscapeMarkdownTableCell)));
        builder.AppendLine(" |");
    }

    private static string EscapeMarkdownTableCell(string? value)
    {
        return NormalizeText(value)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
    }

    private static string FormatLapTime(int? milliseconds)
    {
        if (milliseconds is null || milliseconds.Value < 0)
        {
            return "-";
        }

        var time = TimeSpan.FromMilliseconds(milliseconds.Value);
        return time.TotalMinutes >= 1
            ? $"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds:000}"
            : $"{time.Seconds}.{time.Milliseconds:000}s";
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }

    private sealed record JsonReport(
        string SchemaVersion,
        DateTimeOffset GeneratedAt,
        string ApplicationVersion,
        JsonSession Session,
        IReadOnlyList<JsonMetric> SummaryMetrics,
        IReadOnlyList<JsonLap> Laps,
        IReadOnlyList<JsonStint> Stints,
        IReadOnlyList<JsonEvent> Events,
        IReadOnlyList<JsonAiReport> AiReports,
        string TyreWearLimitation);

    private sealed record JsonSession(
        string SessionId,
        string SessionUid,
        string Track,
        string SessionType,
        string StartedAt,
        string EndedAt,
        string Duration,
        string Summary);

    private sealed record JsonMetric(string Label, string Value, string Detail);

    private sealed record JsonLap(
        int LapNumber,
        int? LapTimeInMs,
        int? Sector1TimeInMs,
        int? Sector2TimeInMs,
        int? Sector3TimeInMs,
        bool IsValid,
        double? AverageSpeedKph,
        float? FuelUsedLitres,
        float? ErsUsed,
        string StartTyre,
        string EndTyre,
        DateTimeOffset CreatedAt);

    private sealed record JsonStint(string Stint, string LapRange, string Tyre, string Evidence);

    private sealed record JsonEvent(
        long Id,
        string EventType,
        string EventTypeText,
        string Severity,
        string SeverityText,
        int? LapNumber,
        int? VehicleIdx,
        string? DriverName,
        string Message,
        DateTimeOffset CreatedAt);

    private sealed record JsonAiReport(
        long Id,
        int LapNumber,
        string Summary,
        string TyreAdvice,
        string FuelAdvice,
        string TrafficAdvice,
        string TtsText,
        bool IsSuccess,
        string ErrorMessage,
        DateTimeOffset CreatedAt);
}

/// <summary>
/// Represents all loaded review data needed to build a post-race report.
/// </summary>
/// <param name="Session">The selected historical session.</param>
/// <param name="Laps">The ordered stored laps.</param>
/// <param name="Events">The ordered stored events.</param>
/// <param name="AiReports">The ordered stored AI reports.</param>
/// <param name="SummaryMetrics">The summary metric rows.</param>
/// <param name="Stints">The inferred stint rows.</param>
/// <param name="GeneratedAt">The report generation timestamp.</param>
/// <param name="ApplicationVersion">The application version.</param>
public sealed record PostRaceReviewReportData(
    HistorySessionItemViewModel Session,
    IReadOnlyList<StoredLap> Laps,
    IReadOnlyList<StoredEvent> Events,
    IReadOnlyList<StoredAiReport> AiReports,
    IReadOnlyList<PostRaceReviewMetricRowViewModel> SummaryMetrics,
    IReadOnlyList<PostRaceReviewStintRowViewModel> Stints,
    DateTimeOffset GeneratedAt,
    string ApplicationVersion);
