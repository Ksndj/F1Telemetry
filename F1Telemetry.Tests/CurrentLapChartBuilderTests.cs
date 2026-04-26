using F1Telemetry.Analytics.Laps;
using F1Telemetry.App.Charts;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies that current-lap chart builders adapt analytics samples into chart-ready panels.
/// </summary>
public sealed class CurrentLapChartBuilderTests
{
    /// <summary>
    /// Verifies that the speed chart maps lap distance to speed points.
    /// </summary>
    [Fact]
    public void BuildSpeedPanel_WithCurrentLapSamples_UsesLapDistanceAndSpeed()
    {
        var builder = new CurrentLapChartBuilder();

        var panel = builder.BuildSpeedPanel(
        [
            new LapSample { LapDistance = 10f, SpeedKph = 180d },
            new LapSample { LapDistance = 20f, SpeedKph = 205d }
        ]);

        Assert.True(panel.HasData);
        Assert.False(panel.IsEmpty);
        Assert.Equal("当前圈速度曲线", panel.Title);
        Assert.Equal("圈内距离 (m)", panel.XAxisLabel);
        Assert.Equal("km/h", panel.YAxisLabel);
        Assert.Single(panel.Series);
        Assert.Equal(2, panel.Series[0].Points.Count);
        Assert.Equal(10d, panel.Series[0].Points[0].X);
        Assert.Equal(205d, panel.Series[0].Points[1].Y);
    }

    /// <summary>
    /// Verifies that the throttle and brake chart returns both series when samples exist.
    /// </summary>
    [Fact]
    public void BuildThrottleBrakePanel_WithSamples_ReturnsBothSeries()
    {
        var builder = new CurrentLapChartBuilder();

        var panel = builder.BuildThrottleBrakePanel(
        [
            new LapSample { LapDistance = 10f, Throttle = 0.75d, Brake = 0.10d },
            new LapSample { LapDistance = 20f, Throttle = 0.90d, Brake = 0.00d }
        ]);

        Assert.True(panel.HasData);
        Assert.False(panel.IsEmpty);
        Assert.Equal(2, panel.Series.Count);
        Assert.Contains(panel.Series, series => series.Name == "油门");
        Assert.Contains(panel.Series, series => series.Name == "刹车");
        Assert.All(panel.Series, series => Assert.Equal("%", panel.YAxisLabel));
    }

    /// <summary>
    /// Verifies that empty current-lap data yields an explicit empty-state panel.
    /// </summary>
    [Fact]
    public void BuildThrottleBrakePanel_WithoutSamples_ReturnsEmptyPanel()
    {
        var builder = new CurrentLapChartBuilder();

        var panel = builder.BuildThrottleBrakePanel(Array.Empty<LapSample>());

        Assert.False(panel.HasData);
        Assert.True(panel.IsEmpty);
        Assert.Equal("等待输入数据", panel.EmptyStateText);
        Assert.Empty(panel.Series);
    }

    /// <summary>
    /// Verifies that empty current-lap speed data yields the speed-specific empty state.
    /// </summary>
    [Fact]
    public void BuildSpeedPanel_WithoutSamples_ReturnsSpeedEmptyState()
    {
        var builder = new CurrentLapChartBuilder();

        var panel = builder.BuildSpeedPanel(Array.Empty<LapSample>());

        Assert.False(panel.HasData);
        Assert.True(panel.IsEmpty);
        Assert.Equal("等待本圈采样", panel.EmptyStateText);
        Assert.Empty(panel.Series);
    }

    /// <summary>
    /// Verifies that down-sampling preserves the first point, last point, and key peaks.
    /// </summary>
    [Fact]
    public void BuildSpeedPanel_DownSamplePreservesFirstLastAndSpike()
    {
        var builder = new CurrentLapChartBuilder(maxPointsPerSeries: 8);
        var samples = Enumerable.Range(0, 40)
            .Select(index => new LapSample
            {
                LapDistance = index * 25f,
                SpeedKph = index == 17 ? 312d : index == 28 ? 85d : 180d + (index % 3)
            })
            .ToArray();

        var panel = builder.BuildSpeedPanel(samples);
        var points = panel.Series[0].Points;

        Assert.Equal(0d, points[0].X);
        Assert.Equal(samples[^1].LapDistance, (float)points[^1].X);
        Assert.Contains(points, point => Math.Abs(point.Y - 312d) < 0.001d);
        Assert.Contains(points, point => Math.Abs(point.Y - 85d) < 0.001d);
    }
}
