using System.IO;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Udp.Packets;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the conversion from live chart-style telemetry into AI broadcast inputs and surfaces.
/// </summary>
public sealed class AiBroadcastConversionTests
{
    /// <summary>
    /// Verifies chart-style samples are summarized as short AI prompt context instead of requiring a live chart view.
    /// </summary>
    [Fact]
    public void TelemetryAnalysisSummaryBuilder_ConvertsChartDataIntoBroadcastSummary()
    {
        var builder = new TelemetryAnalysisSummaryBuilder();

        var summary = builder.Build(
            [
                new LapSample { LapDistance = 100f, SpeedKph = 248d, Throttle = 0.68d, Brake = 0.05d },
                new LapSample { LapDistance = 320f, SpeedKph = 310d, Throttle = 1.0d, Brake = 0.72d }
            ],
            [
                new LapSummary
                {
                    LapNumber = 11,
                    FuelUsedLitres = 1.18f,
                    TyreWearDeltaPerWheel = new WheelSet<float>(0.5f, 0.6f, 0.4f, 0.5f)
                },
                new LapSummary
                {
                    LapNumber = 12,
                    FuelUsedLitres = 1.35f,
                    TyreWearDeltaPerWheel = new WheelSet<float>(0.7f, 0.8f, 0.5f, 0.6f)
                }
            ]);

        Assert.Contains("当前圈采样 2 个", summary, StringComparison.Ordinal);
        Assert.Contains("最高速度 310 km/h", summary, StringComparison.Ordinal);
        Assert.Contains("最大油门 100%", summary, StringComparison.Ordinal);
        Assert.Contains("最大刹车 72%", summary, StringComparison.Ordinal);
        Assert.Contains("近 2 圈燃油 1.18-1.35 L", summary, StringComparison.Ordinal);
        Assert.Contains("最近胎磨增量 后左 0.7%", summary, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the former charts navigation now points to AI broadcast.
    /// </summary>
    [Fact]
    public void ShellNavigation_ChartsSlotIsRenamedToAiBroadcast()
    {
        var chartsSlot = ShellNavigationItemViewModel.CreateDefaultItems().Single(item => item.Key == "charts");

        Assert.Equal("分析播报", chartsSlot.Name);
    }

    /// <summary>
    /// Verifies the former charts view no longer renders live chart controls.
    /// </summary>
    [Fact]
    public void ChartsView_UsesAiBroadcastContentInsteadOfLiveChartControls()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("F1Telemetry.App", "Views", "ChartsView.xaml"));

        Assert.Contains("AI 分析播报", xaml, StringComparison.Ordinal);
        Assert.Contains("AiAnalysisLogs", xaml, StringComparison.Ordinal);
        Assert.Contains("AiTtsLogs", xaml, StringComparison.Ordinal);
        Assert.Contains("PostRaceAiCompletionModes", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("TelemetryChartControl", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("实时图表", xaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the AI broadcast page is structured as a post-race analysis workspace.
    /// </summary>
    [Fact]
    public void ChartsView_DefinesAiPostRaceAnalysisWorkspace()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("F1Telemetry.App", "Views", "ChartsView.xaml"));
        var sharedStylesXaml = File.ReadAllText(FindRepositoryFile("F1Telemetry.App", "Styles", "SharedStyles.xaml"));

        Assert.Contains("x:Name=\"ChartsScrollViewer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", xaml, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AiAnalysisTitleCard\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AiAnalysisStatusCardsPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource ResponsiveStatusCardsPanelStyle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"{x:Type controls:ResponsiveStatusCardsPanel}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("WideColumns=\"4\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MediumColumns=\"2\"", xaml, StringComparison.Ordinal);
        Assert.Contains("NarrowColumns=\"1\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetType=\"{x:Type UniformGrid}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Setter Property=\"Columns\" Value=\"4\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AiAnalysisOperationCard\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AiAnalysisReportDetailCard\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AiAnalysisLogsCard\"", xaml, StringComparison.Ordinal);

        Assert.Contains("AI 状态", xaml, StringComparison.Ordinal);
        Assert.Contains("赛后总结状态", xaml, StringComparison.Ordinal);
        Assert.Contains("TTS 状态", xaml, StringComparison.Ordinal);
        Assert.Contains("数据状态", xaml, StringComparison.Ordinal);
        Assert.Contains("比赛结论", xaml, StringComparison.Ordinal);
        Assert.Contains("主要问题", xaml, StringComparison.Ordinal);
        Assert.Contains("策略回顾", xaml, StringComparison.Ordinal);
        Assert.Contains("轮胎表现", xaml, StringComparison.Ordinal);
        Assert.Contains("ERS / 燃油", xaml, StringComparison.Ordinal);
        Assert.Contains("对手 / 攻防", xaml, StringComparison.Ordinal);
        Assert.Contains("下次改进建议", xaml, StringComparison.Ordinal);
        Assert.Contains("暂无 AI 分析报告", xaml, StringComparison.Ordinal);
        Assert.Contains("完赛后或手动点击生成赛后总结", xaml, StringComparison.Ordinal);

        Assert.Contains("AiEnabled", xaml, StringComparison.Ordinal);
        Assert.Contains("AiApiKeyStatusText", xaml, StringComparison.Ordinal);
        Assert.Contains("AiModel", xaml, StringComparison.Ordinal);
        Assert.Contains("PostRaceAiStatusText", xaml, StringComparison.Ordinal);
        Assert.Contains("PostRaceAiCompletionText", xaml, StringComparison.Ordinal);
        Assert.Contains("TtsEnabled", xaml, StringComparison.Ordinal);
        Assert.Contains("TtsVoiceName", xaml, StringComparison.Ordinal);
        Assert.Contains("PostRaceAiDataStatusText", xaml, StringComparison.Ordinal);
        Assert.Contains("PostRaceAiSummaryCommandTooltipText", xaml, StringComparison.Ordinal);
        Assert.Contains("PostRaceAiLastAnalysisText", xaml, StringComparison.Ordinal);
        Assert.Contains("PostRaceAiReportSummaryText", xaml, StringComparison.Ordinal);
        Assert.Contains("PostRaceAiKeyProblemsText", xaml, StringComparison.Ordinal);
        Assert.Contains("PostRaceAiStrategyReviewText", xaml, StringComparison.Ordinal);
        Assert.Contains("PostRaceAiTyreReviewText", xaml, StringComparison.Ordinal);
        Assert.Contains("PostRaceAiErsFuelReviewText", xaml, StringComparison.Ordinal);
        Assert.Contains("PostRaceAiOpponentReviewText", xaml, StringComparison.Ordinal);
        Assert.Contains("PostRaceAiImprovementsText", xaml, StringComparison.Ordinal);
        Assert.Contains("PostRaceAiFailureReason", xaml, StringComparison.Ordinal);
        Assert.Contains("生成失败：{0}", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding GeneratePostRaceAiSummaryCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding RegeneratePostRaceAiSummaryCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding PostRaceAiStatusText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource DarkComboBoxStyle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"DarkComboBoxStyle\"", sharedStylesXaml, StringComparison.Ordinal);
        Assert.Contains("ItemContainerStyle\" Value=\"{StaticResource DarkComboBoxItemStyle}\"", sharedStylesXaml, StringComparison.Ordinal);
        Assert.Contains("PART_Popup", sharedStylesXaml, StringComparison.Ordinal);
        Assert.Contains("SelectionBoxItem", sharedStylesXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedItem.DisplayName", sharedStylesXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Value=\"White\"", sharedStylesXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Foreground=\"White\"", sharedStylesXaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"{Binding SelectedPostRaceAiCompletionMode.Description}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource AnalysisPrimaryButtonStyle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource AnalysisSecondaryButtonStyle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"{Binding PostRaceAiSummaryCommandTooltipText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"AnalysisActionButtonBaseStyle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Width\" Value=\"128\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Height\" Value=\"34\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("Trigger Property=\"IsEnabled\" Value=\"False\"", xaml, StringComparison.Ordinal);
        Assert.Contains("FgDisabledBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("Background\" Value=\"{StaticResource BgActionDisabledBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("BorderBrush\" Value=\"{StaticResource BorderActionDisabledBrush}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Background\" Value=\"#21405F\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Style=\"{StaticResource CompactButtonStyle}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Style=\"{x:Null}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AiAnalysisOperationPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Orientation=\"Horizontal\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("StackPanel Width=\"280\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding AiAnalysisLogs}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding AiTtsLogs}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"Wrap\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"None\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip=", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AiAnalysisReportEmptyIcon\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"48\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"48\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Width=\"1120\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Width=\"1000\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Viewbox", xaml, StringComparison.Ordinal);
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
