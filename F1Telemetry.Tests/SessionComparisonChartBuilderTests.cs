using F1Telemetry.App.Charts;
using F1Telemetry.Storage.Models;
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
        Assert.Equal(new[] { "Race A", "Race B" }, lapPanel.Series.Select(series => series.Name));
        Assert.Equal(new[] { 1d, 2d }, lapPanel.Series[0].Points.Select(point => point.X));
        Assert.Equal(new[] { 90d, 89.5d }, lapPanel.Series[0].Points.Select(point => point.Y));
        Assert.True(fuelPanel.HasData);
        Assert.Equal(2, fuelPanel.Series.Count);
        Assert.True(ersPanel.HasData);
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
        Assert.Empty(lapPanel.Series);
        Assert.False(fuelPanel.HasData);
        Assert.Empty(fuelPanel.Series);
        Assert.False(ersPanel.HasData);
        Assert.Empty(ersPanel.Series);
    }

    /// <summary>
    /// Verifies tyre wear comparison is explicitly unavailable.
    /// </summary>
    [Fact]
    public void BuildTyreWearUnavailablePanel_DoesNotFakeStoredTyreWear()
    {
        var builder = new StoredLapSessionComparisonChartBuilder();

        var panel = builder.BuildTyreWearUnavailablePanel();

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
}
