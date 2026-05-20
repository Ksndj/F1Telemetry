using F1Telemetry.Analytics.Corners;
using F1Telemetry.Analytics.Tracks;
using F1Telemetry.App.ViewModels;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies corner summary rows expose readable UI state for the WPF analysis page.
/// </summary>
public sealed class CornerSummaryRowViewModelTests
{
    /// <summary>
    /// Verifies clean corner data is projected into a positive visual state.
    /// </summary>
    [Fact]
    public void FromSummary_WithCleanHighConfidenceData_UsesPositiveStatus()
    {
        var row = CornerSummaryRowViewModel.FromSummary(CreateSummary(
            confidence: ConfidenceLevel.High,
            timeLossInMs: -42,
            warnings: []));

        Assert.Equal("数据完整", row.WarningDisplayText);
        Assert.Equal("#34D399", row.WarningBrush);
        Assert.Equal("#34D399", row.TimeLossBrush);
        Assert.Equal("#34D399", row.ConfidenceBrush);
        Assert.Equal("#34D399", row.RowAccentBrush);
        Assert.Equal("-42 ms", row.TimeLossText);
    }

    /// <summary>
    /// Verifies low-confidence warning data remains visible instead of being rendered as a dash.
    /// </summary>
    [Fact]
    public void FromSummary_WithWarnings_UsesWarningStatusText()
    {
        var row = CornerSummaryRowViewModel.FromSummary(CreateSummary(
            confidence: ConfidenceLevel.Low,
            timeLossInMs: null,
            warnings: [DataQualityWarning.MissingReferenceLap, DataQualityWarning.EstimatedTrackMap]));

        Assert.Contains("缺参考", row.WarningDisplayText, StringComparison.Ordinal);
        Assert.Contains("估算", row.WarningDisplayText, StringComparison.Ordinal);
        Assert.Contains("低置信", row.WarningDisplayText, StringComparison.Ordinal);
        Assert.Contains("缺少参考圈", row.WarningTooltipText, StringComparison.Ordinal);
        Assert.Contains("估算赛道图", row.WarningTooltipText, StringComparison.Ordinal);
        Assert.DoesNotContain("MissingRefLap", row.WarningDisplayText, StringComparison.Ordinal);
        Assert.DoesNotContain("MissingReferenceLap", row.WarningDisplayText, StringComparison.Ordinal);
        Assert.DoesNotContain("EstimatedTrackMap", row.WarningDisplayText, StringComparison.Ordinal);
        Assert.Contains(nameof(DataQualityWarning.MissingReferenceLap), row.WarningText, StringComparison.Ordinal);
        Assert.Equal("#FBBF24", row.WarningBrush);
        Assert.Equal("#FBBF24", row.TimeLossBrush);
        Assert.Equal("#60A5FA", row.ConfidenceBrush);
    }

    /// <summary>
    /// Verifies positive and negative time differences use clear race semantics.
    /// </summary>
    [Fact]
    public void FromSummary_WithPositiveAndNegativeTimeDifference_UsesLossAndGainColors()
    {
        var slower = CornerSummaryRowViewModel.FromSummary(CreateSummary(
            confidence: ConfidenceLevel.Medium,
            timeLossInMs: 120,
            warnings: []));
        var faster = CornerSummaryRowViewModel.FromSummary(CreateSummary(
            confidence: ConfidenceLevel.Medium,
            timeLossInMs: -80,
            warnings: []));

        Assert.Equal("+120 ms", slower.TimeLossText);
        Assert.Equal("#F87171", slower.TimeLossBrush);
        Assert.Equal("-80 ms", faster.TimeLossText);
        Assert.Equal("#34D399", faster.TimeLossBrush);
        Assert.Contains("正数为比参考圈慢", slower.TimeDifferenceTooltipText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the detail metrics show current, reference, and delta values.
    /// </summary>
    [Fact]
    public void FromSummary_WithReferenceMetrics_ShowsCurrentReferenceAndDelta()
    {
        var row = CornerSummaryRowViewModel.FromSummary(
            CreateSummary(
                confidence: ConfidenceLevel.Medium,
                timeLossInMs: 120,
                warnings: []),
            referenceEntrySpeedKph: 205,
            referenceMinimumSpeedKph: 95,
            referenceExitSpeedKph: 172,
            referenceMaxBrake: 0.82);

        Assert.Contains("当前 210 km/h", row.EntrySpeedComparisonText, StringComparison.Ordinal);
        Assert.Contains("参考 205 km/h", row.EntrySpeedComparisonText, StringComparison.Ordinal);
        Assert.Contains("差值 +5 km/h", row.EntrySpeedComparisonText, StringComparison.Ordinal);
        Assert.Contains("当前 95%", row.BrakeComparisonText, StringComparison.Ordinal);
        Assert.Contains("参考 82%", row.BrakeComparisonText, StringComparison.Ordinal);
        Assert.Contains("差值 +13pp", row.BrakeComparisonText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies missing reference metrics are explicit instead of blank.
    /// </summary>
    [Fact]
    public void FromSummary_WithoutReferenceMetrics_ShowsMissingReferenceData()
    {
        var row = CornerSummaryRowViewModel.FromSummary(CreateSummary(
            confidence: ConfidenceLevel.Medium,
            timeLossInMs: null,
            warnings: [DataQualityWarning.MissingReferenceLap]));

        Assert.Contains("缺少参考数据", row.EntrySpeedComparisonText, StringComparison.Ordinal);
        Assert.Contains("缺少参考数据", row.BrakeComparisonText, StringComparison.Ordinal);
    }

    private static CornerSummary CreateSummary(
        ConfidenceLevel confidence,
        int? timeLossInMs,
        IReadOnlyList<DataQualityWarning> warnings)
    {
        return new CornerSummary
        {
            Segment = new TrackSegment
            {
                SegmentId = "t1",
                Name = "Tarzan",
                SegmentType = TrackSegmentType.Corner,
                CornerNumber = 1
            },
            EntrySpeedKph = 210,
            MinSpeedKph = 92,
            ExitSpeedKph = 178,
            MaxBrake = 0.95,
            TimeLossToReferenceInMs = timeLossInMs,
            Confidence = confidence,
            Warnings = warnings
        };
    }
}
