using F1Telemetry.Analytics.Laps;
using F1Telemetry.App.Charts;
using F1Telemetry.Udp.Packets;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies that lap-history trend builders adapt summaries into chart-friendly series.
/// </summary>
public sealed class TrendChartBuilderTests
{
    /// <summary>
    /// Verifies that the fuel trend uses litres semantics and values.
    /// </summary>
    [Fact]
    public void BuildFuelTrendPanel_UsesFuelUsedLitres()
    {
        var builder = new TrendChartBuilder();

        var panel = builder.BuildFuelTrendPanel(
        [
            new LapSummary { LapNumber = 5, FuelUsedLitres = 1.85f },
            new LapSummary { LapNumber = 6, FuelUsedLitres = 1.93f }
        ]);

        Assert.False(panel.IsEmpty);
        Assert.Equal("多圈燃油趋势", panel.Title);
        Assert.Equal("L", panel.YAxisLabel);
        Assert.DoesNotContain("kg", panel.Title + panel.YAxisLabel, StringComparison.OrdinalIgnoreCase);
        Assert.InRange(panel.Series[0].Points[0].Y, 1.849d, 1.851d);
        Assert.Equal(6d, panel.Series[0].Points[1].X);
    }

    /// <summary>
    /// Verifies that missing wheel-delta data is skipped instead of breaking the chart.
    /// </summary>
    [Fact]
    public void BuildTyreWearTrendPanel_WithNullWheelDelta_SkipsIncompleteLap()
    {
        var builder = new TrendChartBuilder();

        var panel = builder.BuildTyreWearTrendPanel(
        [
            new LapSummary { LapNumber = 5, TyreWearDeltaPerWheel = null },
            new LapSummary
            {
                LapNumber = 6,
                TyreWearDeltaPerWheel = new WheelSet<float>(1.1f, 1.3f, 1.4f, 1.2f)
            }
        ]);

        Assert.False(panel.IsEmpty);
        Assert.Equal(4, panel.Series.Count);
        Assert.All(panel.Series, series => Assert.Single(series.Points));
        Assert.All(panel.Series, series => Assert.Equal(6d, series.Points[0].X));
    }

    /// <summary>
    /// Verifies that an all-null tyre wear history returns an explicit empty state.
    /// </summary>
    [Fact]
    public void BuildTyreWearTrendPanel_WithOnlyNullWheelDeltas_ReturnsEmptyPanel()
    {
        var builder = new TrendChartBuilder();

        var panel = builder.BuildTyreWearTrendPanel(
        [
            new LapSummary { LapNumber = 8, TyreWearDeltaPerWheel = null }
        ]);

        Assert.True(panel.IsEmpty);
        Assert.Equal("暂无历史圈数据", panel.EmptyMessage);
        Assert.Empty(panel.Series);
    }
}
