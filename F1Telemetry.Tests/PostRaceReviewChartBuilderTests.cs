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
    /// Verifies historical tyre wear has an explicit unavailable state.
    /// </summary>
    [Fact]
    public void BuildTyreWearUnavailablePanel_DoesNotFakeStoredTyreWear()
    {
        var builder = new StoredLapPostRaceChartBuilder();

        var panel = builder.BuildTyreWearUnavailablePanel();

        Assert.False(panel.HasData);
        Assert.Empty(panel.Series);
        Assert.Contains("未保存四轮胎磨数据", panel.EmptyStateText, StringComparison.Ordinal);
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
}
