using F1Telemetry.Analytics.Strategy;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies V3 undercut and overcut risk analysis.
/// </summary>
public sealed class StrategyRiskAnalyzerTests
{
    /// <summary>
    /// Verifies missing strategy evidence yields observation-only insufficient-data advice.
    /// </summary>
    [Fact]
    public void Analyze_WhenPitLossAndGapMissing_ReturnsInsufficientData()
    {
        var advice = new StrategyRiskAnalyzer().Analyze(new StrategyRiskInput { CurrentLapNumber = 12 });

        Assert.Equal(StrategyAdviceType.InsufficientData, advice.AdviceType);
        Assert.Equal(StrategyRiskLevel.Unknown, advice.RiskLevel);
        Assert.Contains("estimated-pit-loss-ms", advice.MissingData);
        Assert.Contains("gap-to-front-ms", advice.MissingData);
        Assert.Contains(advice.DataQualityWarnings, warning => warning.Contains("require pit loss", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(advice.DataQualityWarnings, warning => warning.Contains("gap", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(advice.DataQualityWarnings, warning => warning.Contains("tyre", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies a close front gap and strong fresh-tyre estimate produce conditional undercut advice.
    /// </summary>
    [Fact]
    public void Analyze_WithCloseFrontGap_ReturnsConditionalUndercutAdvice()
    {
        var advice = new StrategyRiskAnalyzer().Analyze(new StrategyRiskInput
        {
            CurrentLapNumber = 18,
            GapToCarAheadMs = 2_100,
            GapToCarBehindMs = 6_000,
            EstimatedPitLossMs = 24_000,
            CurrentTyre = "Medium",
            CurrentTyreAgeLaps = 12,
            FreshTyrePaceGainPerLapMs = 800,
            PitExitTrafficRisk = false
        });

        Assert.Equal(StrategyAdviceType.Undercut, advice.AdviceType);
        Assert.Empty(advice.MissingData);
        Assert.InRange(advice.Confidence, 0.6, 0.85);
        Assert.Contains("conditional", string.Join(" ", advice.Summary, string.Join(" ", advice.InferredSuggestions)), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies strategy advice remains non-absolute even when an overcut looks viable.
    /// </summary>
    [Fact]
    public void Analyze_WithControlledTyreAge_ReturnsOvercutWithoutAbsoluteCommand()
    {
        var advice = new StrategyRiskAnalyzer().Analyze(new StrategyRiskInput
        {
            GapToCarAheadMs = 5_000,
            GapToCarBehindMs = 7_000,
            EstimatedPitLossMs = 23_000,
            CurrentTyre = "Hard",
            CurrentTyreAgeLaps = 4,
            FreshTyrePaceGainPerLapMs = 300
        });

        var text = advice.Summary + " " + string.Join(" ", advice.InferredSuggestions);
        Assert.Equal(StrategyAdviceType.Overcut, advice.AdviceType);
        Assert.DoesNotContain("must", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("一定", text, StringComparison.Ordinal);
    }
}
