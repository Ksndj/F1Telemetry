using F1Telemetry.App.Charts;
using F1Telemetry.Storage.Models;
using System.Windows.Media;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies stored-lap chart projections used by the session comparison page.
/// </summary>
public sealed class SessionComparisonChartBuilderTests
{
    /// <summary>
    /// Verifies lap-time, fuel, and ERS panels create one series per session with data.
    /// </summary>
    [Fact]
    public void BuildPanels_WithMultipleSessions_ReturnsPlottableSeries()
    {
        var builder = new StoredLapSessionComparisonChartBuilder();
        var inputs = new[]
        {
            new SessionComparisonChartInput(
                "Race A",
                [
                    CreateLap("session-a", 1, lapTimeInMs: 90_000, fuelUsedLitres: 1.10f, ersUsed: 1_200_000f),
                    CreateLap("session-a", 2, lapTimeInMs: 89_500, fuelUsedLitres: 1.20f, ersUsed: 1_100_000f)
                ]),
            new SessionComparisonChartInput(
                "Race B",
                [
                    CreateLap("session-b", 1, lapTimeInMs: 91_000, fuelUsedLitres: 1.30f, ersUsed: 1_500_000f),
                    CreateLap("session-b", 2, lapTimeInMs: 90_700, fuelUsedLitres: 1.25f, ersUsed: 1_450_000f)
                ])
        };

        var lapPanel = builder.BuildLapTimePanel(inputs);
        var fuelPanel = builder.BuildFuelPanel(inputs);
        var ersPanel = builder.BuildErsPanel(inputs);

        Assert.True(lapPanel.HasData);
        Assert.True(lapPanel.UsesLapNumberXAxis);
        Assert.True(lapPanel.UsesNonNegativeYAxis);
        Assert.Equal(new[] { "Race A", "Race B" }, lapPanel.Series.Select(series => series.Name));
        Assert.Equal(new[] { 1d, 2d }, lapPanel.Series[0].Points.Select(point => point.X));
        Assert.Equal(new[] { 90d, 89.5d }, lapPanel.Series[0].Points.Select(point => point.Y));
        Assert.True(fuelPanel.HasData);
        Assert.True(fuelPanel.UsesLapNumberXAxis);
        Assert.True(fuelPanel.UsesNonNegativeYAxis);
        Assert.Equal(2, fuelPanel.Series.Count);
        Assert.True(ersPanel.HasData);
        Assert.True(ersPanel.UsesLapNumberXAxis);
        Assert.True(ersPanel.UsesNonNegativeYAxis);
        Assert.Equal(new[] { 1.2d, 1.1d }, ersPanel.Series[0].Points.Select(point => point.Y));
    }

    /// <summary>
    /// Verifies sessions without values for one metric are skipped for that metric only.
    /// </summary>
    [Fact]
    public void BuildFuelPanel_WithMissingSessionMetric_SkipsOnlyThatSession()
    {
        var builder = new StoredLapSessionComparisonChartBuilder();
        var inputs = new[]
        {
            new SessionComparisonChartInput(
                "Race A",
                [CreateLap("session-a", 1, lapTimeInMs: 90_000)]),
            new SessionComparisonChartInput(
                "Race B",
                [CreateLap("session-b", 1, lapTimeInMs: 91_000, fuelUsedLitres: 1.3f)])
        };

        var fuelPanel = builder.BuildFuelPanel(inputs);

        Assert.True(fuelPanel.HasData);
        var series = Assert.Single(fuelPanel.Series);
        Assert.Equal("Race B", series.Name);
        Assert.All(series.Points, point => Assert.NotEqual(0d, point.Y));
    }

    /// <summary>
    /// Verifies different session lap counts align on legal lap-number X values.
    /// </summary>
    [Fact]
    public void BuildFuelPanel_WithDifferentSessionLapCounts_KeepsPositiveLapAxis()
    {
        var builder = new StoredLapSessionComparisonChartBuilder();
        var inputs = new[]
        {
            new SessionComparisonChartInput(
                "Race A",
                Enumerable.Range(1, 29)
                    .Select(lap => CreateLap("session-a", lap, fuelUsedLitres: lap / 10f))
                    .ToArray()),
            new SessionComparisonChartInput(
                "Race B",
                Enumerable.Range(1, 5)
                    .Select(lap => CreateLap("session-b", lap, fuelUsedLitres: lap / 20f))
                    .ToArray())
        };

        var panel = builder.BuildFuelPanel(inputs);
        var xRange = ChartAxisRangeHelper.GetLapAxisRange(panel.Series.SelectMany(series => series.Points).Select(point => point.X));

        Assert.True(panel.UsesLapNumberXAxis);
        Assert.True(panel.UsesNonNegativeYAxis);
        Assert.Equal(29, panel.Series[0].Points.Count);
        Assert.Equal(5, panel.Series[1].Points.Count);
        Assert.True(xRange.Minimum >= 1d);
        Assert.All(panel.Series.SelectMany(series => series.Points), point => Assert.True(point.X >= 1d));
    }

    /// <summary>
    /// Verifies missing metric values are skipped instead of converted into fake zero points.
    /// </summary>
    [Fact]
    public void BuildFuelAndErsPanels_WithMissingMetricValues_DoNotCreateZeroPoints()
    {
        var builder = new StoredLapSessionComparisonChartBuilder();
        var inputs = new[]
        {
            new SessionComparisonChartInput(
                "Race A",
                [
                    CreateLap("session-a", 1, fuelUsedLitres: 1.1f),
                    CreateLap("session-a", 2),
                    CreateLap("session-a", 3, ersUsed: 900_000f)
                ]),
            new SessionComparisonChartInput(
                "Race B",
                [
                    CreateLap("session-b", 1),
                    CreateLap("session-b", 2, fuelUsedLitres: 1.4f, ersUsed: 1_100_000f)
                ])
        };

        var fuelPanel = builder.BuildFuelPanel(inputs);
        var ersPanel = builder.BuildErsPanel(inputs);

        Assert.Equal(new[] { 1d }, fuelPanel.Series[0].Points.Select(point => point.X));
        Assert.Equal(new[] { 2d }, fuelPanel.Series[1].Points.Select(point => point.X));
        Assert.Equal(new[] { 3d }, ersPanel.Series[0].Points.Select(point => point.X));
        Assert.Equal(new[] { 2d }, ersPanel.Series[1].Points.Select(point => point.X));
        Assert.All(fuelPanel.Series.SelectMany(series => series.Points), point => Assert.True(point.Y > 0d));
        Assert.All(ersPanel.Series.SelectMany(series => series.Points), point => Assert.True(point.Y > 0d));
    }

    /// <summary>
    /// Verifies empty inputs return explicit empty states.
    /// </summary>
    [Fact]
    public void BuildPanels_WithoutMetricValues_ReturnEmptyPanels()
    {
        var builder = new StoredLapSessionComparisonChartBuilder();
        var inputs = new[]
        {
            new SessionComparisonChartInput("Race A", [CreateLap("session-a", 1)]),
            new SessionComparisonChartInput("Race B", [CreateLap("session-b", 1)])
        };

        var lapPanel = builder.BuildLapTimePanel(inputs);
        var fuelPanel = builder.BuildFuelPanel(inputs);
        var ersPanel = builder.BuildErsPanel(inputs);

        Assert.False(lapPanel.HasData);
        Assert.True(lapPanel.UsesLapNumberXAxis);
        Assert.True(lapPanel.UsesNonNegativeYAxis);
        Assert.Empty(lapPanel.Series);
        Assert.False(fuelPanel.HasData);
        Assert.True(fuelPanel.UsesLapNumberXAxis);
        Assert.True(fuelPanel.UsesNonNegativeYAxis);
        Assert.Empty(fuelPanel.Series);
        Assert.False(ersPanel.HasData);
        Assert.True(ersPanel.UsesLapNumberXAxis);
        Assert.True(ersPanel.UsesNonNegativeYAxis);
        Assert.Empty(ersPanel.Series);
    }

    /// <summary>
    /// Verifies stored four-wheel tyre wear renders average series using official compound colors.
    /// </summary>
    [Fact]
    public void BuildTyreWearPanel_WithStoredWheelWear_UsesCompoundColoredAverageSeries()
    {
        var builder = new StoredLapSessionComparisonChartBuilder();
        var inputs = new[]
        {
            new SessionComparisonTyreWearChartInput(
                "Race A",
                [
                    CreateTyreWearPoint(1, rearLeft: 10f, rearRight: 12f, frontLeft: 14f, frontRight: 16f, visualTyreCompound: 16),
                    CreateTyreWearPoint(2, rearLeft: 20f, rearRight: 22f, frontLeft: 24f, frontRight: 26f, visualTyreCompound: 17)
                ]),
            new SessionComparisonTyreWearChartInput(
                "Race B",
                [
                    CreateTyreWearPoint(1, rearLeft: 8f, rearRight: 10f, frontLeft: 12f, frontRight: 14f, visualTyreCompound: 8)
                ])
        };

        var panel = builder.BuildTyreWearPanel(inputs);

        Assert.True(panel.HasData);
        Assert.Equal("四轮平均胎磨对比", panel.Title);
        Assert.True(panel.UsesLapNumberXAxis);
        Assert.True(panel.UsesNonNegativeYAxis);
        Assert.Equal(new[] { "Race A · 红胎", "Race A · 黄胎", "Race B · 全雨胎" }, panel.Series.Select(series => series.Name));
        Assert.Equal(13d, panel.Series[0].Points[0].Y);
        Assert.Equal(23d, panel.Series[1].Points[0].Y);
        Assert.Equal(Color.FromRgb(0xE1, 0x06, 0x00), GetSolidColor(panel.Series[0].StrokeBrush));
        Assert.Equal(Color.FromRgb(0xFF, 0xD1, 0x2E), GetSolidColor(panel.Series[1].StrokeBrush));
        Assert.Equal(Color.FromRgb(0x00, 0x9F, 0xE3), GetSolidColor(panel.Series[2].StrokeBrush));
    }

    /// <summary>
    /// Verifies tyre wear comparison keeps an explicit empty state when no stored samples exist.
    /// </summary>
    [Fact]
    public void BuildTyreWearPanel_WithoutStoredWheelWear_ReturnsEmptyState()
    {
        var builder = new StoredLapSessionComparisonChartBuilder();

        var panel = builder.BuildTyreWearPanel([]);

        Assert.False(panel.HasData);
        Assert.Empty(panel.Series);
        Assert.Contains("无法生成胎磨对比", panel.EmptyStateText, StringComparison.Ordinal);
    }

    private static StoredLap CreateLap(
        string sessionId,
        int lapNumber,
        int? lapTimeInMs = null,
        float? fuelUsedLitres = null,
        float? ersUsed = null)
    {
        return new StoredLap
        {
            Id = lapNumber,
            SessionId = sessionId,
            LapNumber = lapNumber,
            LapTimeInMs = lapTimeInMs,
            IsValid = true,
            FuelUsedLitres = fuelUsedLitres,
            ErsUsed = ersUsed,
            CreatedAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(lapNumber)
        };
    }

    private static StoredLapTyreWearTrendPoint CreateTyreWearPoint(
        int lapNumber,
        float rearLeft,
        float rearRight,
        float frontLeft,
        float frontRight,
        int? visualTyreCompound = null,
        int? actualTyreCompound = null)
    {
        return new StoredLapTyreWearTrendPoint
        {
            LapNumber = lapNumber,
            SampleIndex = lapNumber,
            SampledAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(lapNumber),
            RearLeft = rearLeft,
            RearRight = rearRight,
            FrontLeft = frontLeft,
            FrontRight = frontRight,
            VisualTyreCompound = visualTyreCompound,
            ActualTyreCompound = actualTyreCompound
        };
    }

    private static Color GetSolidColor(Brush brush)
    {
        return Assert.IsType<SolidColorBrush>(brush).Color;
    }
}
