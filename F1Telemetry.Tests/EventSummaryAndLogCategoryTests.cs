using System.IO;
using System.Xml.Linq;
using F1Telemetry.App.Logging;
using F1Telemetry.App.ViewModels;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies event-summary compression and log category presentation rules.
/// </summary>
public sealed class EventSummaryAndLogCategoryTests
{
    /// <summary>
    /// Verifies that overview summaries are capped to a small number of key entries.
    /// </summary>
    [Fact]
    public void BuildSummaries_WithManyKeyEvents_LimitsOverviewCount()
    {
        var logs = Enumerable.Range(1, 6)
            .Select(index => CreateLog("RaceEvent", $"低油警告 {index}"))
            .ToArray();

        var summaries = OverviewEventSummaryFormatter.BuildSummaries(logs);

        Assert.Equal(4, summaries.Count);
    }

    /// <summary>
    /// Verifies that long overview messages are shortened without mutating the full log entry.
    /// </summary>
    [Fact]
    public void BuildSummaries_WithLongEvent_TruncatesOverviewOnly()
    {
        const string fullMessage = "安全车部署，前方事故导致全场限速，请保持 delta 并注意前车突然减速，这是一条很长的完整日志。";
        var log = CreateLog("RaceEvent", fullMessage);

        var summary = Assert.Single(OverviewEventSummaryFormatter.BuildSummaries([log]));

        Assert.True(summary.Message.Length <= 40);
        Assert.EndsWith("...", summary.Message, StringComparison.Ordinal);
        Assert.Equal(fullMessage, log.Message);
    }

    /// <summary>
    /// Verifies that high-priority race events outrank low-value system noise.
    /// </summary>
    [Fact]
    public void BuildSummaries_PrefersHighPriorityRaceEvents()
    {
        var logs = new[]
        {
            CreateLog("System", "监听端口已更新。"),
            CreateLog("UDP", "收到遥测包。"),
            CreateLog("Storage", "已写入一条圈速记录。"),
            CreateLog("RaceEvent", "低油警告：预计剩余 0.6 圈。")
        };

        var summaries = OverviewEventSummaryFormatter.BuildSummaries(logs, maxCount: 2);

        Assert.Contains(summaries, summary => summary.Message.Contains("低油", StringComparison.Ordinal));
        Assert.DoesNotContain(summaries, summary => summary.Category == "System");
    }

    /// <summary>
    /// Verifies that raw UDP event codes do not enter the overview summary.
    /// </summary>
    [Fact]
    public void BuildSummaries_DropsRawUdpEventCodes()
    {
        var logs = new[]
        {
            CreateLog("UDP", "收到赛道事件：BUTN"),
            CreateLog("UDP", "收到赛道事件：SPTP"),
            CreateLog("UDP", "收到赛道事件：SEND"),
            CreateLog("RaceEvent", "进站提醒：前车已进站。")
        };

        var summaries = OverviewEventSummaryFormatter.BuildSummaries(logs);

        Assert.Single(summaries);
        Assert.DoesNotContain(summaries, summary => summary.Message.Contains("BUTN", StringComparison.Ordinal));
        Assert.DoesNotContain(summaries, summary => summary.Message.Contains("SPTP", StringComparison.Ordinal));
        Assert.DoesNotContain(summaries, summary => summary.Message.Contains("SEND", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that noisy raw UDP event codes remain loggable as UDP entries.
    /// </summary>
    [Theory]
    [InlineData("BUTN", "UDP")]
    [InlineData("SPTP", "UDP")]
    [InlineData("SEND", "UDP")]
    public void FormatRawEventCode_WithNoisyCodes_KeepsLogsAsUdp(string eventCode, string expectedCategory)
    {
        var display = RawEventCodeLogFormatter.Format(eventCode);

        Assert.Equal(expectedCategory, display.Category);
        Assert.Contains(eventCode, display.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that key raw event codes are shown as readable race events.
    /// </summary>
    [Theory]
    [InlineData("OVTK", "超车事件")]
    [InlineData("FTLP", "最快圈")]
    [InlineData("COLL", "碰撞")]
    public void FormatRawEventCode_WithKnownRaceCodes_ReturnsReadableRaceEvent(string eventCode, string expectedMessage)
    {
        var display = RawEventCodeLogFormatter.Format(eventCode);

        Assert.Equal("RaceEvent", display.Category);
        Assert.Equal(expectedMessage, display.Message);
    }

    /// <summary>
    /// Verifies that session-state and unknown raw events stay out of Overview.
    /// </summary>
    [Fact]
    public void BuildSummaries_WithSessionAndUnknownRawCodes_DropsThemFromOverview()
    {
        var sessionState = RawEventCodeLogFormatter.Format("SSTA");
        var unknown = RawEventCodeLogFormatter.Format("ABCD");
        var logs = new[]
        {
            CreateLog(sessionState.Category, sessionState.Message),
            CreateLog(unknown.Category, unknown.Message),
            CreateLog("RaceEvent", "低油警告：预计剩余 0.6 圈。")
        };

        var summaries = OverviewEventSummaryFormatter.BuildSummaries(logs);

        Assert.Single(summaries);
        Assert.Equal("低油警告：预计剩余 0.6 圈。", summaries[0].Message);
        Assert.Equal("System", sessionState.Category);
        Assert.Equal("Session 状态变化", sessionState.Message);
        Assert.Equal("UDP", unknown.Category);
    }

    /// <summary>
    /// Verifies that frequent overtake events do not outrank critical overview warnings.
    /// </summary>
    [Fact]
    public void BuildSummaries_WithManyOvertakes_KeepsOvertakesBelowCriticalEvents()
    {
        var overtake = RawEventCodeLogFormatter.Format("OVTK");
        var logs = new[]
        {
            CreateLog(overtake.Category, overtake.Message),
            CreateLog(overtake.Category, overtake.Message),
            CreateLog(overtake.Category, overtake.Message),
            CreateLog("RaceEvent", "高胎磨警告：右后磨损过高。"),
            CreateLog("RaceEvent", "低油警告：预计剩余 0.6 圈。"),
            CreateLog("RaceEvent", "圈无效：本圈成绩不会计入。"),
            CreateLog("RaceEvent", "进站提醒：前车已进站。"),
            CreateLog("RaceEvent", "黄旗：前方事故。")
        };

        var summaries = OverviewEventSummaryFormatter.BuildSummaries(logs, maxCount: 5);

        Assert.Equal(5, summaries.Count);
        Assert.DoesNotContain(summaries, summary => summary.Message.Contains("超车", StringComparison.Ordinal));
        Assert.Contains(summaries, summary => summary.Message.Contains("黄旗", StringComparison.Ordinal));
        Assert.Contains(summaries, summary => summary.Message.Contains("进站", StringComparison.Ordinal));
        Assert.Contains(summaries, summary => summary.Message.Contains("圈无效", StringComparison.Ordinal));
        Assert.Contains(summaries, summary => summary.Message.Contains("低油", StringComparison.Ordinal));
        Assert.Contains(summaries, summary => summary.Message.Contains("高胎磨", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that fastest lap and collision stay above low-priority overtake events.
    /// </summary>
    [Fact]
    public void BuildSummaries_WithFastestLapCollisionAndOvertake_PrioritizesReadableKeyEvents()
    {
        var overtake = RawEventCodeLogFormatter.Format("OVTK");
        var fastestLap = RawEventCodeLogFormatter.Format("FTLP");
        var collision = RawEventCodeLogFormatter.Format("COLL");
        var logs = new[]
        {
            CreateLog(overtake.Category, overtake.Message),
            CreateLog(fastestLap.Category, fastestLap.Message),
            CreateLog(collision.Category, collision.Message)
        };

        var summaries = OverviewEventSummaryFormatter.BuildSummaries(logs, maxCount: 2);

        Assert.Equal(2, summaries.Count);
        Assert.DoesNotContain(summaries, summary => summary.Message.Contains("超车", StringComparison.Ordinal));
        Assert.Contains(summaries, summary => summary.Message.Contains("最快圈", StringComparison.Ordinal));
        Assert.Contains(summaries, summary => summary.Message.Contains("碰撞", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that known log categories are normalized to the public category set.
    /// </summary>
    [Theory]
    [InlineData("系统", "System")]
    [InlineData("异常", "System")]
    [InlineData("协议", "UDP")]
    [InlineData("事件", "RaceEvent")]
    [InlineData("告警", "RaceEvent")]
    [InlineData("存储", "Storage")]
    [InlineData("AI", "AI")]
    [InlineData("TTS", "TTS")]
    public void Normalize_ReturnsStandardCategoryText(string input, string expected)
    {
        Assert.Equal(expected, LogCategoryFormatter.Normalize(input, "message"));
    }

    /// <summary>
    /// Verifies that Overview uses the compressed summary collection.
    /// </summary>
    [Fact]
    public void OverviewView_BindsEventSummaryToCompressedCollection()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "OverviewView.xaml"));
        var text = document.ToString(SaveOptions.DisableFormatting);

        Assert.Contains("ItemsSource=\"{Binding OverviewEventSummaries}\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding EventLogs}\"", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that LogsView keeps the full unified log stream.
    /// </summary>
    [Fact]
    public void LogsView_BindsToFullLogEntries()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "LogsView.xaml"));
        var text = document.ToString(SaveOptions.DisableFormatting);

        Assert.Contains("ItemsSource=\"{Binding LogEntries}\"", text, StringComparison.Ordinal);
    }

    private static LogEntryViewModel CreateLog(string category, string message)
    {
        return new LogEntryViewModel
        {
            Timestamp = "12:34:56",
            Category = category,
            Message = message
        };
    }

    private static string FindRepositoryFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(pathParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(pathParts)}");
    }
}
