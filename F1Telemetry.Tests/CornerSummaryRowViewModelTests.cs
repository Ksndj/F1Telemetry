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

        Assert.Contains("缺少参考圈", row.WarningDisplayText, StringComparison.Ordinal);
        Assert.Contains("估算赛道图", row.WarningDisplayText, StringComparison.Ordinal);
        Assert.DoesNotContain("MissingRefLap", row.WarningDisplayText, StringComparison.Ordinal);
        Assert.Contains(nameof(DataQualityWarning.MissingReferenceLap), row.WarningText, StringComparison.Ordinal);
        Assert.Equal("#FBBF24", row.WarningBrush);
        Assert.Equal("#FBBF24", row.TimeLossBrush);
        Assert.Equal("#60A5FA", row.ConfidenceBrush);
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
