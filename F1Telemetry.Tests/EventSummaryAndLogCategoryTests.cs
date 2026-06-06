using System.IO;
using System.Xml.Linq;
using F1Telemetry.App.Logging;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Core.Formatting;
using F1Telemetry.Core.Models;
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
    /// Verifies severe damage can reach Overview while minor damage does not displace key warnings.
    /// </summary>
    [Fact]
    public void BuildSummaries_WithDamageEvents_PrioritizesSevereDamageOnly()
    {
        var logs = new[]
        {
            CreateLog("RaceEvent", "前翼左侧轻微损伤 5%。"),
            CreateLog("RaceEvent", "前翼左侧严重损伤 65%。"),
            CreateLog("RaceEvent", "高胎磨警告：右后磨损过高。"),
            CreateLog("RaceEvent", "低油警告：预计剩余 0.6 圈。"),
            CreateLog("RaceEvent", "进站提醒：前车已进站。"),
            CreateLog("RaceEvent", "黄旗：前方事故。")
        };

        var summaries = OverviewEventSummaryFormatter.BuildSummaries(logs, maxCount: 4);

        Assert.Equal(4, summaries.Count);
        Assert.Contains(summaries, summary => summary.Message.Contains("严重损伤", StringComparison.Ordinal));
        Assert.DoesNotContain(summaries, summary => summary.Message.Contains("轻微损伤", StringComparison.Ordinal));
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
    /// Verifies that Overview exposes the player damage summary binding.
    /// </summary>
    [Fact]
    public void OverviewView_BindsDamageSummary()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "Views", "OverviewView.xaml"));
        var text = document.ToString(SaveOptions.DisableFormatting);

        Assert.Contains("Text=\"损伤\"", text, StringComparison.Ordinal);
        Assert.Contains("OverviewDamageText", text, StringComparison.Ordinal);
        Assert.Contains("OverviewDamageTooltipText", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the Overview page exposes responsive scrolling, local table scrollbars, and complete empty-state text.
    /// </summary>
    [Fact]
    public void OverviewView_DefinesResponsiveScrollAndEmptyStates()
    {
        var filePath = FindRepositoryFile("F1Telemetry.App", "Views", "OverviewView.xaml");
        var document = XDocument.Load(filePath);
        var text = document.ToString(SaveOptions.DisableFormatting);
        var source = File.ReadAllText(filePath);
        var viewModelSource = File.ReadAllText(FindRepositoryFile("F1Telemetry.App", "ViewModels", "DashboardViewModel.cs"));

        Assert.Contains("x:Name=\"OverviewScrollViewer\"", text, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", text, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", text, StringComparison.Ordinal);
        Assert.Contains("GlassCardStyle", text, StringComparison.Ordinal);
        Assert.Contains("MetricTileStyle", text, StringComparison.Ordinal);
        Assert.Contains("SectionHeaderStyle", text, StringComparison.Ordinal);
        Assert.Contains("EmptyStateStyle", text, StringComparison.Ordinal);
        Assert.Contains("IconBadgeStyle", text, StringComparison.Ordinal);
        Assert.Contains("PillTagStyle", text, StringComparison.Ordinal);
        Assert.Contains("PrimaryButtonStyle", text, StringComparison.Ordinal);
        Assert.Contains("SecondaryButtonStyle", text, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OverviewTopCardsGrid\"", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OverviewMiddleCardsGrid\"", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OverviewBottomCardsGrid\"", source, StringComparison.Ordinal);
        Assert.Equal("Grid", FindElementByName(document, "OverviewTopCardsGrid").Name.LocalName);
        Assert.Equal("Grid", FindElementByName(document, "OverviewMiddleCardsGrid").Name.LocalName);
        Assert.Equal("Grid", FindElementByName(document, "OverviewBottomCardsGrid").Name.LocalName);
        Assert.Equal(new[] { "*", "12", "*", "12", "*" }, GetDirectColumnWidths(FindElementByName(document, "OverviewTopCardsGrid")));
        Assert.Equal(new[] { "1.1*", "12", "1.4*" }, GetDirectColumnWidths(FindElementByName(document, "OverviewMiddleCardsGrid")));
        Assert.Equal(new[] { "*", "12", "*", "12", "*" }, GetDirectColumnWidths(FindElementByName(document, "OverviewBottomCardsGrid")));
        AssertCardGridColumn(document, "OverviewPlayerStatusCard", "0");
        AssertCardGridColumn(document, "OverviewVehicleInputCard", "2");
        AssertCardGridColumn(document, "OverviewTyreSummaryCard", "4");
        AssertCardGridColumn(document, "OverviewOpponentSummaryCard", "0");
        AssertCardGridColumn(document, "OverviewEventSummaryCard", "2");
        AssertCardGridColumn(document, "OverviewSessionFocusCard", "0");
        AssertCardGridColumn(document, "OverviewAiSuggestionCard", "2");
        AssertCardGridColumn(document, "OverviewTtsCard", "4");
        Assert.DoesNotContain("x:Name=\"OverviewResponsiveCardsPanel\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"OverviewSecondaryCardsPanel\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"OverviewSupportCardsPanel\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalAlignment=\"Left\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxWidth=", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OpponentCarsHorizontalScrollViewer\"", text, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OverviewEventsHorizontalScrollViewer\"", text, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", text, StringComparison.Ordinal);
        Assert.Equal("WrapPanel", FindElementByName(document, "TyreMetricTilesPanel").Name.LocalName);
        Assert.Contains("x:Name=\"TyreCurrentCompoundLabel\"", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TyreAgeLabel\"", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TyreWearLabel\"", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TyreSurfaceTemperatureLabel\"", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TyreTemperatureLabel\"", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TyrePressureLabel\"", source, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TyreDamageLabel\"", source, StringComparison.Ordinal);
        Assert.Contains("Property=\"ToolTip\" Value=\"{Binding Text, RelativeSource={RelativeSource Self}}\"", source, StringComparison.Ordinal);
        Assert.Contains("DamageValueContentStyle", source, StringComparison.Ordinal);
        Assert.Contains("等待数据", text, StringComparison.Ordinal);
        Assert.Contains("连接后显示", text, StringComparison.Ordinal);
        Assert.Contains("等待玩家车辆状态", text, StringComparison.Ordinal);
        Assert.Contains("连接后显示实时数据", text, StringComparison.Ordinal);
        Assert.Contains("暂无事件数据", text, StringComparison.Ordinal);
        Assert.Contains("比赛事件将在这里显示", text, StringComparison.Ordinal);
        Assert.Contains("等待数据以生成建议", text, StringComparison.Ordinal);
        Assert.Contains("等待播报内容", text, StringComparison.Ordinal);
        Assert.Contains("等待比赛开始或数据连接", text, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"{Binding OverviewDamageTooltipText}\"", source, StringComparison.Ordinal);
        Assert.Contains("未收到 CarDamage 包", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Tag=\"等待 CarDamage 包\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"等待 CarDamage 包\"", source, StringComparison.Ordinal);
        Assert.Contains("圈速", text, StringComparison.Ordinal);
        Assert.Contains("轮胎", text, StringComparison.Ordinal);
        Assert.Contains("燃油", text, StringComparison.Ordinal);
        Assert.Contains("策略", text, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", text, StringComparison.Ordinal);
        Assert.Contains("ToolTip=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Mode=TwoWay", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Width=\"1120\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Width=\"1000\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<Viewbox", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the damage formatter supplies the Overview empty-state text when CarDamage has not arrived.
    /// </summary>
    [Fact]
    public void DamageSummaryFormatter_WithMissingPacket_ReturnsOverviewEmptyState()
    {
        Assert.Equal("等待数据", DamageSummaryFormatter.Format(null, "等待数据"));
        Assert.Equal("暂无损伤", DamageSummaryFormatter.Format(new DamageSnapshot()));
    }

    /// <summary>
    /// Verifies drivetrain wear is kept behind direct damage in compact AI and Overview summaries.
    /// </summary>
    [Fact]
    public void DamageSummaryFormatter_WithDirectDamageAndDrivetrainWear_DowngradesWearText()
    {
        var summary = DamageSummaryFormatter.Format(new DamageSnapshot
        {
            Components = new Dictionary<DamageComponent, byte>
            {
                [DamageComponent.Engine] = 68,
                [DamageComponent.Gearbox] = 36,
                [DamageComponent.FrontLeftWing] = 53
            }
        });

        Assert.Contains("前翼左侧 53%（严重）", summary, StringComparison.Ordinal);
        Assert.Contains("引擎磨损 68%（严重）", summary, StringComparison.Ordinal);
        Assert.Contains("变速箱磨损 36%（中度）", summary, StringComparison.Ordinal);
        Assert.True(
            summary.IndexOf("前翼左侧", StringComparison.Ordinal) <
            summary.IndexOf("引擎磨损", StringComparison.Ordinal));
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

    private static XElement FindElementByName(XContainer document, string name)
    {
        return document.Descendants()
            .Single(element => (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == name);
    }

    private static string[] GetDirectColumnWidths(XElement grid)
    {
        var columnDefinitions = grid.Elements()
            .Single(element => element.Name.LocalName == "Grid.ColumnDefinitions");

        return columnDefinitions.Elements()
            .Where(element => element.Name.LocalName == "ColumnDefinition")
            .Select(element => element.Attribute("Width")?.Value ?? "*")
            .ToArray();
    }

    private static void AssertCardGridColumn(XContainer document, string name, string expectedColumn)
    {
        var card = FindElementByName(document, name);
        Assert.Equal("Stretch", card.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal(expectedColumn, card.Attribute("Grid.Column")?.Value ?? "0");
    }
}
