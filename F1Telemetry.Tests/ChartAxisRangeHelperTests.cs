using F1Telemetry.App.Charts;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies chart axis ranges stay meaningful for fixed analysis charts.
/// </summary>
public sealed class ChartAxisRangeHelperTests
{
    /// <summary>
    /// Verifies lap-number axes never start below the first legal lap number.
    /// </summary>
    [Fact]
    public void GetLapAxisRange_WithLapNumbers_StartsAtOneOrHigher()
    {
        var range = ChartAxisRangeHelper.GetLapAxisRange([1d, 2d, 3d]);

        Assert.True(range.Minimum >= 1d);
        Assert.Equal(1d, range.Minimum);
        Assert.True(range.Maximum >= 3d);
    }

    /// <summary>
    /// Verifies non-negative metrics keep a zero lower bound after padding.
    /// </summary>
    [Theory]
    [InlineData(new[] { 0d, 1.2d, 1.4d })]
    [InlineData(new[] { 0d, 0.5d, 1.0d })]
    [InlineData(new[] { 0d, 30d, 60d })]
    public void GetNonNegativeRange_WithPositiveMetrics_UsesZeroMinimum(double[] values)
    {
        var range = ChartAxisRangeHelper.GetNonNegativeRange(values);

        Assert.Equal(0d, range.Minimum);
        Assert.True(range.Maximum > values.Max());
    }

    /// <summary>
    /// Verifies all-zero values still produce a visible non-negative axis.
    /// </summary>
    [Fact]
    public void GetNonNegativeRange_WithAllZeroValues_UsesDefaultPositiveMaximum()
    {
        var range = ChartAxisRangeHelper.GetNonNegativeRange([0d, 0d, 0d]);

        Assert.Equal(0d, range.Minimum);
        Assert.Equal(1d, range.Maximum);
    }

    /// <summary>
    /// Verifies relative delta charts can opt into negative Y ranges.
    /// </summary>
    [Fact]
    public void GetPaddedRange_WithDeltaValues_AllowsNegativeMinimum()
    {
        var range = ChartAxisRangeHelper.GetPaddedRange([-1.5d, 0.2d, 1.0d], allowNegative: true);

        Assert.True(range.Minimum < 0d);
        Assert.True(range.Maximum > 1d);
    }

    /// <summary>
    /// Verifies empty data is detected before drawing an empty coordinate plane.
    /// </summary>
    [Fact]
    public void ShouldShowEmptyState_WithNoFiniteValues_ReturnsTrue()
    {
        Assert.True(ChartAxisRangeHelper.ShouldShowEmptyState([]));
        Assert.True(ChartAxisRangeHelper.ShouldShowEmptyState([double.NaN, double.PositiveInfinity]));
    }

    /// <summary>
    /// Verifies single-point data keeps simple ticks and a data-quality message.
    /// </summary>
    [Fact]
    public void EvaluateDataQuality_WithSinglePoint_ReturnsSparseDataNotice()
    {
        var state = ChartAxisRangeHelper.EvaluateDataQuality([42d]);
        var ticks = ChartAxisRangeHelper.BuildSparseAxisLabels([7d], width: 320d);

        Assert.True(state.HasEnoughData);
        Assert.Contains("数据点较少", state.Message, StringComparison.Ordinal);
        Assert.Single(ticks.Values);
        Assert.Equal("7", ticks.Labels[0]);
    }

    /// <summary>
    /// Verifies dense lap labels are reduced to integer tick labels.
    /// </summary>
    [Fact]
    public void BuildSparseAxisLabels_WithManyLaps_UsesIntegerSparseTicks()
    {
        var ticks = ChartAxisRangeHelper.BuildSparseAxisLabels(Enumerable.Range(1, 29).Select(value => (double)value), width: 360d);

        Assert.NotEmpty(ticks.Values);
        Assert.True(ticks.Values.All(value => value >= 1d));
        Assert.True(ticks.Values.Count < 29);
        Assert.All(ticks.Labels, label => Assert.DoesNotContain(".", label, StringComparison.Ordinal));
    }
}
