using F1Telemetry.App.Charts;
using F1Telemetry.Storage.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies stored-lap chart projections used by the post-race review page.
/// </summary>
public sealed class PostRaceReviewChartBuilderTests
{
    /// <summary>
    /// Verifies stored lap times are converted to seconds by lap number.
    /// </summary>
    [Fact]
    public void BuildLapTimePanel_WithStoredLaps_ReturnsPlottableSeconds()
    {
        var builder = new StoredLapPostRaceChartBuilder();

        var panel = builder.BuildLapTimePanel(
        [
            CreateLap(1, lapTimeInMs: 90_000),
            CreateLap(2, lapTimeInMs: 91_500)
        ]);

        Assert.True(panel.HasData);
        Assert.Equal("圈速趋势", panel.Title);
        Assert.Equal(new[] { 1d, 2d }, panel.Series[0].Points.Select(point => point.X));
        Assert.Equal(new[] { 90d, 91.5d }, panel.Series[0].Points.Select(point => point.Y));
    }

    /// <summary>
    /// Verifies sector trends render only available sector series.
    /// </summary>
    [Fact]
    public void BuildSectorSplitPanel_WithPartialSectors_ReturnsAvailableSeries()
    {
        var builder = new StoredLapPostRaceChartBuilder();

        var panel = builder.BuildSectorSplitPanel(
        [
            CreateLap(1, sector1TimeInMs: 30_000, sector3TimeInMs: 31_000),
            CreateLap(2, sector1TimeInMs: 30_500, sector3TimeInMs: 30_800)
        ]);

        Assert.True(panel.HasData);
        Assert.Equal(new[] { "S1", "S3" }, panel.Series.Select(series => series.Name));
    }

    /// <summary>
    /// Verifies missing fuel and ERS data expose explicit empty states.
    /// </summary>
    [Fact]
    public void BuildFuelAndErsPanels_WithoutStoredValues_ReturnEmptyPanels()
    {
        var builder = new StoredLapPostRaceChartBuilder();
        var laps = new[] { CreateLap(1) };

        var fuelPanel = builder.BuildFuelPanel(laps);
        var ersPanel = builder.BuildErsPanel(laps);

        Assert.False(fuelPanel.HasData);
        Assert.Contains("燃油", fuelPanel.EmptyStateText, StringComparison.Ordinal);
        Assert.False(ersPanel.HasData);
        Assert.Contains("ERS", ersPanel.EmptyStateText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies complete stored sample tyre wear renders as four wheel series.
    /// </summary>
    [Fact]
    public void BuildTyreWearTrendPanel_WithCompleteSamples_ReturnsFourWheelSeries()
    {
        var builder = new StoredLapPostRaceChartBuilder();

        var panel = builder.BuildTyreWearTrendPanel(
        [
            CreateTyreWearPoint(1, rearLeft: 12.1f, rearRight: 12.2f, frontLeft: 11.1f, frontRight: 11.2f),
            CreateTyreWearPoint(2, rearLeft: 13.1f, rearRight: 13.2f, frontLeft: 12.1f, frontRight: 12.2f)
        ]);

        Assert.True(panel.HasData);
        Assert.Equal(new[] { "后左", "后右", "前左", "前右" }, panel.Series.Select(series => series.Name));
        Assert.Equal(new[] { 1d, 2d }, panel.Series[0].Points.Select(point => point.X));
        Assert.Equal(12.1d, panel.Series[0].Points[0].Y, precision: 1);
        Assert.Equal(13.1d, panel.Series[0].Points[1].Y, precision: 1);
    }

    /// <summary>
    /// Verifies missing tyre wear sample data has an explicit empty state.
    /// </summary>
    [Fact]
    public void BuildTyreWearTrendPanel_WithoutSamples_ReturnsEmptyPanel()
    {
        var builder = new StoredLapPostRaceChartBuilder();

        var panel = builder.BuildTyreWearTrendPanel([]);

        Assert.False(panel.HasData);
        Assert.Empty(panel.Series);
        Assert.Contains("暂无完整四轮胎磨样本", panel.EmptyStateText, StringComparison.Ordinal);
    }

    private static StoredLap CreateLap(
        int lapNumber,
        int? lapTimeInMs = null,
        int? sector1TimeInMs = null,
        int? sector2TimeInMs = null,
        int? sector3TimeInMs = null)
    {
        return new StoredLap
        {
            Id = lapNumber,
            SessionId = "session-a",
            LapNumber = lapNumber,
            LapTimeInMs = lapTimeInMs,
            Sector1TimeInMs = sector1TimeInMs,
            Sector2TimeInMs = sector2TimeInMs,
            Sector3TimeInMs = sector3TimeInMs,
            IsValid = true,
            CreatedAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(lapNumber)
        };
    }

    private static StoredLapTyreWearTrendPoint CreateTyreWearPoint(
        int lapNumber,
        float rearLeft,
        float rearRight,
        float frontLeft,
        float frontRight)
    {
        return new StoredLapTyreWearTrendPoint
        {
            LapNumber = lapNumber,
            SampleIndex = lapNumber * 10,
            SampledAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(lapNumber),
            RearLeft = rearLeft,
            RearRight = rearRight,
            FrontLeft = frontLeft,
            FrontRight = frontRight
        };
    }
}
