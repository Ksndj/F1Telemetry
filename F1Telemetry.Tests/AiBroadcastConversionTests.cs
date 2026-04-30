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
        Assert.Contains("AiTtsLogs", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("TelemetryChartControl", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("实时图表", xaml, StringComparison.Ordinal);
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
