using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Strategy;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies V3 stint and strategy timeline analysis.
/// </summary>
public sealed class StintStrategyAnalyzerTests
{
    /// <summary>
    /// Verifies tyre changes and pit flags split stints while adjusted metrics keep raw data separate.
    /// </summary>
    [Fact]
    public void Analyze_SplitsStintsAndExcludesNeutralizedLapsFromAdjustedMetrics()
    {
        var analyzer = new StintStrategyAnalyzer();

        var result = analyzer.Analyze(
            [
                CreateLap(1, "Medium", 91_000),
                CreateLap(2, "Medium", 92_000),
                CreateLap(3, "Medium", 140_000),
                CreateLap(4, "Soft", 90_000, startedInPit: true),
                CreateLap(5, "Soft", 89_000)
            ],
            [
                new RaceEvent
                {
                    EventType = EventType.SafetyCar,
                    Severity = EventSeverity.Information,
                    LapNumber = 3,
                    Message = "Safety car deployed"
                }
            ]);

        Assert.Equal(2, result.Stints.Count);
        Assert.Equal(new[] { 1, 2, 3 }, result.Stints[0].LapNumbers);
        Assert.Equal(new[] { 1, 2 }, result.Stints[0].AdjustedLapNumbers);
        Assert.True(result.Stints[0].HasSafetyCarInfluence);
        Assert.NotEqual(result.Stints[0].RawAverageLapTimeMs, result.Stints[0].AdjustedAverageLapTimeMs);
        Assert.Contains(result.Timeline, entry => entry.Category == "RaceEvent" && entry.LapNumber == 3);
    }

    /// <summary>
    /// Verifies missing lap inputs surface a data quality warning.
    /// </summary>
    [Fact]
    public void Analyze_WithoutLaps_ReturnsDataQualityWarning()
    {
        var result = new StintStrategyAnalyzer().Analyze(Array.Empty<StrategyLapInput>());

        Assert.Empty(result.Stints);
        Assert.Contains(result.DataQualityWarnings, warning => warning.Contains("No completed lap", StringComparison.Ordinal));
    }

    private static StrategyLapInput CreateLap(
        int lapNumber,
        string tyre,
        uint lapTimeMs,
        bool startedInPit = false)
    {
        return new StrategyLapInput
        {
            LapNumber = lapNumber,
            LapTimeInMs = lapTimeMs,
            IsValid = true,
            StartTyre = tyre,
            EndTyre = tyre,
            FuelUsedLitres = 1.4f,
            ErsUsed = 150_000f,
            StartedInPit = startedInPit
        };
    }
}
