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
