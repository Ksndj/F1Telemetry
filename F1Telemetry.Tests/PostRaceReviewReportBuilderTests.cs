using System.Text.Json;
using F1Telemetry.Analytics.Events;
using F1Telemetry.App.Reports;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Storage.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies historical post-race review report generation.
/// </summary>
public sealed class PostRaceReviewReportBuilderTests
{
    /// <summary>
    /// Verifies Markdown reports include the loaded review sections.
    /// </summary>
    [Fact]
    public void BuildMarkdown_WithReviewData_IncludesReadableSections()
    {
        var builder = new PostRaceReviewReportBuilder();
        var data = CreateReportData();

        var markdown = builder.BuildMarkdown(data);

        Assert.Contains("# F1Telemetry 历史会话复盘报告", markdown, StringComparison.Ordinal);
        Assert.Contains("## 会话摘要", markdown, StringComparison.Ordinal);
        Assert.Contains("## 摘要指标", markdown, StringComparison.Ordinal);
        Assert.Contains("## 单圈摘要", markdown, StringComparison.Ordinal);
        Assert.Contains("## Stint 摘要", markdown, StringComparison.Ordinal);
        Assert.Contains("## 事件时间线", markdown, StringComparison.Ordinal);
        Assert.Contains("## AI 每圈点评", markdown, StringComparison.Ordinal);
        Assert.Contains("历史单圈未保存四轮胎磨数据", markdown, StringComparison.Ordinal);
        Assert.Contains("Lap 1 summary", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies JSON reports use stable camelCase fields and preserve ordered rows.
    /// </summary>
    [Fact]
    public void BuildJson_WithReviewData_UsesCamelCaseAndPreservesOrder()
    {
        var builder = new PostRaceReviewReportBuilder();
        var data = CreateReportData(
            laps:
            [
                CreateLap(1),
                CreateLap(2)
            ],
            events:
            [
                CreateEvent(1, 1),
                CreateEvent(2, 2)
            ],
            reports:
            [
                CreateReport(1, 1),
                CreateReport(2, 2)
            ]);

        using var json = JsonDocument.Parse(builder.BuildJson(data));
        var root = json.RootElement;

        Assert.True(root.TryGetProperty("schemaVersion", out _));
        Assert.True(root.TryGetProperty("generatedAt", out _));
        Assert.Equal("2.0.0-beta4", root.GetProperty("applicationVersion").GetString());
        Assert.Equal("uid-session-a", root.GetProperty("session").GetProperty("sessionUid").GetString());
        Assert.Equal(1, root.GetProperty("laps")[0].GetProperty("lapNumber").GetInt32());
        Assert.Equal(2, root.GetProperty("laps")[1].GetProperty("lapNumber").GetInt32());
        Assert.Equal(1, root.GetProperty("events")[0].GetProperty("lapNumber").GetInt32());
        Assert.Equal(2, root.GetProperty("aiReports")[1].GetProperty("lapNumber").GetInt32());
    }

    /// <summary>
    /// Verifies empty review data still creates usable reports.
    /// </summary>
    [Fact]
    public void BuildReports_WithEmptyData_DescribeMissingData()
    {
        var builder = new PostRaceReviewReportBuilder();
        var data = CreateReportData(
            laps: [],
            events: [],
            reports: [],
            summaryMetrics: [],
            stints: []);

        var markdown = builder.BuildMarkdown(data);
        using var json = JsonDocument.Parse(builder.BuildJson(data));

        Assert.Contains("暂无单圈记录", markdown, StringComparison.Ordinal);
        Assert.Contains("暂无事件记录", markdown, StringComparison.Ordinal);
        Assert.Contains("暂无 AI 报告", markdown, StringComparison.Ordinal);
        Assert.Equal(0, json.RootElement.GetProperty("laps").GetArrayLength());
        Assert.Equal(0, json.RootElement.GetProperty("events").GetArrayLength());
        Assert.Equal(0, json.RootElement.GetProperty("aiReports").GetArrayLength());
    }

    /// <summary>
    /// Verifies exports omit sensitive or raw payload fields.
    /// </summary>
    [Fact]
    public void BuildReports_OmitPayloadJsonAndSensitiveSettings()
    {
        var builder = new PostRaceReviewReportBuilder();
        var data = CreateReportData(
            events:
            [
                CreateEvent(1, 1) with
                {
                    PayloadJson = """{"apiKey":"secret","payloadBase64":"abc","rawFile":"race.jsonl"}"""
                }
            ]);

        var markdown = builder.BuildMarkdown(data);
        var json = builder.BuildJson(data);
        var combined = markdown + json;

        Assert.DoesNotContain("PayloadJson", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("apiKey", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("payloadBase64", combined, StringComparison.Ordinal);
        Assert.DoesNotContain(".jsonl", combined, StringComparison.OrdinalIgnoreCase);
    }

    private static PostRaceReviewReportData CreateReportData(
        IReadOnlyList<StoredLap>? laps = null,
        IReadOnlyList<StoredEvent>? events = null,
        IReadOnlyList<StoredAiReport>? reports = null,
        IReadOnlyList<PostRaceReviewMetricRowViewModel>? summaryMetrics = null,
        IReadOnlyList<PostRaceReviewStintRowViewModel>? stints = null)
    {
        var session = new HistorySessionItemViewModel(new StoredSession
        {
            Id = "session-a",
            SessionUid = "uid-session-a",
            TrackId = 10,
            SessionType = 15,
            StartedAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z"),
            EndedAt = DateTimeOffset.Parse("2026-04-18T10:45:00Z")
        });
        laps ??= [CreateLap(1)];
        events ??= [CreateEvent(1, 1)];
        reports ??= [CreateReport(1, 1)];
        summaryMetrics ??=
        [
            new PostRaceReviewMetricRowViewModel
            {
                Label = "最佳圈",
                Value = "1:30.001",
                Detail = "Lap 1"
            }
        ];
        stints ??=
        [
            new PostRaceReviewStintRowViewModel
            {
                StintText = "Stint 1",
                LapRangeText = "Lap 1-2",
                TyreText = "Medium",
                EvidenceText = "基于 StartTyre/EndTyre 推断，信息有限"
            }
        ];

        return new PostRaceReviewReportData(
            session,
            laps,
            events,
            reports,
            summaryMetrics,
            stints,
            DateTimeOffset.Parse("2026-05-10T12:00:00Z"),
            "2.0.0-beta4");
    }

    private static StoredLap CreateLap(int lapNumber)
    {
        return new StoredLap
        {
            Id = lapNumber,
            SessionId = "session-a",
            LapNumber = lapNumber,
            LapTimeInMs = 90_000 + lapNumber,
            Sector1TimeInMs = 30_000,
            Sector2TimeInMs = 30_000,
            Sector3TimeInMs = 30_000,
            IsValid = true,
            AverageSpeedKph = 215,
            FuelUsedLitres = 1.2f,
            ErsUsed = 150_000f,
            StartTyre = "V17 / A19",
            EndTyre = "V17 / A19",
            CreatedAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(lapNumber)
        };
    }

    private static StoredEvent CreateEvent(int? lapNumber, long id)
    {
        return new StoredEvent
        {
            Id = id,
            SessionId = "session-a",
            EventType = EventType.LowFuel,
            Severity = EventSeverity.Warning,
            LapNumber = lapNumber,
            VehicleIdx = 0,
            DriverName = "Player",
            Message = lapNumber is null ? "无圈号事件" : $"Lap {lapNumber} event",
            CreatedAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(id)
        };
    }

    private static StoredAiReport CreateReport(int lapNumber, long id)
    {
        return new StoredAiReport
        {
            Id = id,
            SessionId = "session-a",
            LapNumber = lapNumber,
            Summary = $"Lap {lapNumber} summary",
            TyreAdvice = "保持当前轮胎窗口",
            FuelAdvice = "燃油目标正常",
            TrafficAdvice = "前方干净",
            TtsText = "节奏稳定",
            IsSuccess = true,
            ErrorMessage = "-",
            CreatedAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(id)
        };
    }
}
