using System.Globalization;

namespace F1Telemetry.App.Charts;

/// <summary>
/// Represents a numeric axis range after chart-specific safety rules are applied.
/// </summary>
/// <param name="Minimum">The minimum visible axis value.</param>
/// <param name="Maximum">The maximum visible axis value.</param>
public readonly record struct ChartAxisRange(double Minimum, double Maximum);

/// <summary>
/// Represents manually selected axis tick values and labels.
/// </summary>
/// <param name="Values">The numeric tick positions.</param>
/// <param name="Labels">The labels shown for each tick position.</param>
public sealed record ChartAxisTicks(IReadOnlyList<double> Values, IReadOnlyList<string> Labels);

/// <summary>
/// Describes whether a chart has enough finite data to render.
/// </summary>
/// <param name="EmptyReason">A stable reason code for the current data quality state.</param>
/// <param name="HasEnoughData">Whether the chart can render meaningful data.</param>
/// <param name="Message">The user-facing data quality message.</param>
public sealed record ChartEmptyState(string EmptyReason, bool HasEnoughData, string Message);

/// <summary>
/// Builds safe axis ranges and sparse labels for fixed telemetry analysis charts.
/// </summary>
public static class ChartAxisRangeHelper
{
    private const double DefaultNonNegativeMaximum = 1d;
    private const double CompactLapSlotWidth = 90d;
    private const double CompactLapBaseWidth = 220d;
    private const double CompactLapMinimumWidth = 360d;
    private const double CompactLapMaximumWidth = 780d;
    private const int CompactLapMaximumSpan = 9;

    /// <summary>
    /// Gets a padded axis range while allowing negative values only for relative charts.
    /// </summary>
    /// <param name="values">The values to include in the range.</param>
    /// <param name="allowNegative">Whether negative axis values are valid for the metric.</param>
    public static ChartAxisRange GetPaddedRange(IEnumerable<double> values, bool allowNegative)
    {
        if (!allowNegative)
        {
            return GetNonNegativeRange(values);
        }

        var finiteValues = GetFiniteValues(values);
        if (finiteValues.Length == 0)
        {
            return new ChartAxisRange(-1d, 1d);
        }

        var minimum = finiteValues.Min();
        var maximum = finiteValues.Max();
        if (NearlyEqual(minimum, maximum))
        {
            var margin = NearlyEqual(maximum, 0d) ? 1d : Math.Abs(maximum) * 0.05d;
            return new ChartAxisRange(minimum - margin, maximum + margin);
        }

        var padding = (maximum - minimum) * 0.05d;
        return new ChartAxisRange(minimum - padding, maximum + padding);
    }

    /// <summary>
    /// Gets a safe range for metrics that cannot be negative.
    /// </summary>
    /// <param name="values">The metric values to include in the range.</param>
    public static ChartAxisRange GetNonNegativeRange(IEnumerable<double> values)
    {
        var finiteValues = GetFiniteValues(values);
        if (finiteValues.Length == 0)
        {
            return new ChartAxisRange(0d, DefaultNonNegativeMaximum);
        }

        var maximum = finiteValues.Max();
        if (maximum <= 0d)
        {
            return new ChartAxisRange(0d, DefaultNonNegativeMaximum);
        }

        return new ChartAxisRange(0d, maximum * 1.05d);
    }

    /// <summary>
    /// Gets a legal lap-number X-axis range.
    /// </summary>
    /// <param name="lapNumbers">The lap numbers plotted on the X axis.</param>
    public static ChartAxisRange GetLapAxisRange(IEnumerable<double> lapNumbers)
    {
        var finiteLaps = GetFiniteValues(lapNumbers)
            .Select(value => Math.Max(1d, value))
            .ToArray();
        if (finiteLaps.Length == 0)
        {
            return new ChartAxisRange(1d, 2d);
        }

        var minimum = Math.Max(1d, Math.Floor(finiteLaps.Min()));
        var maximum = Math.Max(minimum, Math.Ceiling(finiteLaps.Max()));
        if (NearlyEqual(minimum, maximum))
        {
            maximum = minimum + 1d;
        }

        return new ChartAxisRange(minimum, maximum);
    }

    /// <summary>
    /// Gets a compact plot width for sparse lap-number charts on wide cards.
    /// </summary>
    /// <param name="lapNumbers">The lap numbers plotted on the X axis.</param>
    /// <param name="availableWidth">The available plot area width in device-independent pixels.</param>
    public static double GetCompactLapPlotWidth(IEnumerable<double> lapNumbers, double availableWidth)
    {
        if (!double.IsFinite(availableWidth) || availableWidth <= 0d)
        {
            return double.PositiveInfinity;
        }

        var finiteLaps = GetFiniteValues(lapNumbers)
            .Select(value => Math.Max(1, (int)Math.Round(value, MidpointRounding.AwayFromZero)))
            .Distinct()
            .Order()
            .ToArray();
        if (finiteLaps.Length == 0)
        {
            return availableWidth;
        }

        var lapSpan = Math.Max(1, finiteLaps[^1] - finiteLaps[0] + 1);
        if (lapSpan > CompactLapMaximumSpan)
        {
            return availableWidth;
        }

        var compactWidth = Math.Clamp(
            CompactLapBaseWidth + (lapSpan * CompactLapSlotWidth),
            CompactLapMinimumWidth,
            CompactLapMaximumWidth);

        return Math.Min(availableWidth, compactWidth);
    }

    /// <summary>
    /// Gets a readable integer tick step based on point count and chart width.
    /// </summary>
    /// <param name="pointCount">The number of points or laps represented by the chart.</param>
    /// <param name="width">The chart width in device-independent pixels.</param>
    public static int GetTickStep(int pointCount, double width)
    {
        if (pointCount <= 3)
        {
            return 1;
        }

        var targetTickCount = GetTargetTickCount(width);
        var rawStep = (int)Math.Ceiling(pointCount / (double)targetTickCount);
        return RoundToNiceStep(rawStep);
    }

    /// <summary>
    /// Returns true when there are no finite values to plot.
    /// </summary>
    /// <param name="values">The values to inspect.</param>
    public static bool ShouldShowEmptyState(IEnumerable<double> values)
    {
        return GetFiniteValues(values).Length == 0;
    }

    /// <summary>
    /// Evaluates chart data quality for empty and sparse states.
    /// </summary>
    /// <param name="values">The values to inspect.</param>
    public static ChartEmptyState EvaluateDataQuality(IEnumerable<double> values)
    {
        var finiteValues = GetFiniteValues(values);
        return finiteValues.Length switch
        {
            0 => new ChartEmptyState("NoData", false, "暂无图表数据"),
            1 => new ChartEmptyState("SparseData", true, "数据点较少，仅供参考"),
            <= 3 => new ChartEmptyState("LimitedData", true, "数据点较少，仅供参考"),
            _ => new ChartEmptyState("Ready", true, string.Empty)
        };
    }

    /// <summary>
    /// Builds sparse integer labels for lap-number axes.
    /// </summary>
    /// <param name="lapNumbers">The plotted lap numbers.</param>
    /// <param name="width">The chart width in device-independent pixels.</param>
    public static ChartAxisTicks BuildSparseAxisLabels(IEnumerable<double> lapNumbers, double width)
    {
        var laps = GetFiniteValues(lapNumbers)
            .Select(value => Math.Max(1, (int)Math.Round(value, MidpointRounding.AwayFromZero)))
            .Distinct()
            .Order()
            .ToArray();
        if (laps.Length == 0)
        {
            return new ChartAxisTicks(Array.Empty<double>(), Array.Empty<string>());
        }

        var firstLap = laps[0];
        var lastLap = laps[^1];
        var tickStep = GetTickStep(laps.Length, width);
        var tickLaps = laps
            .Where(lap => tickStep <= 1 || lap == firstLap || lap == lastLap || lap % tickStep == 0)
            .ToArray();

        return new ChartAxisTicks(
            tickLaps.Select(lap => (double)lap).ToArray(),
            tickLaps.Select(lap => lap.ToString(CultureInfo.InvariantCulture)).ToArray());
    }

    /// <summary>
    /// Builds a small set of numeric labels for sparse non-negative Y axes.
    /// </summary>
    /// <param name="range">The visible axis range.</param>
    /// <param name="pointCount">The number of points represented by the chart.</param>
    /// <param name="width">The chart width in device-independent pixels.</param>
    public static ChartAxisTicks BuildSparseNumericAxisLabels(ChartAxisRange range, int pointCount, double width)
    {
        var tickCount = pointCount <= 1
            ? 2
            : pointCount <= 3
                ? 3
                : Math.Min(6, GetTargetTickCount(width));
        if (tickCount <= 1 || range.Maximum <= range.Minimum)
        {
            return new ChartAxisTicks(Array.Empty<double>(), Array.Empty<string>());
        }

        var values = Enumerable.Range(0, tickCount)
            .Select(index => range.Minimum + ((range.Maximum - range.Minimum) * index / (tickCount - 1)))
            .ToArray();

        return new ChartAxisTicks(values, values.Select(FormatAxisLabel).ToArray());
    }

    private static double[] GetFiniteValues(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Where(double.IsFinite).ToArray();
    }

    private static int GetTargetTickCount(double width)
    {
        if (width < 300d)
        {
            return 4;
        }

        if (width < 480d)
        {
            return 6;
        }

        if (width < 720d)
        {
            return 8;
        }

        return width < 960d ? 10 : 12;
    }

    private static int RoundToNiceStep(int rawStep)
    {
        if (rawStep <= 1)
        {
            return 1;
        }

        if (rawStep <= 2)
        {
            return 2;
        }

        if (rawStep <= 5)
        {
            return 5;
        }

        if (rawStep <= 10)
        {
            return 10;
        }

        var magnitude = (int)Math.Pow(10d, Math.Floor(Math.Log10(rawStep)));
        var normalized = (int)Math.Ceiling(rawStep / (double)magnitude);
        if (normalized <= 2)
        {
            return 2 * magnitude;
        }

        return normalized <= 5 ? 5 * magnitude : 10 * magnitude;
    }

    private static string FormatAxisLabel(double value)
    {
        if (NearlyEqual(value, 0d))
        {
            return "0";
        }

        if (NearlyEqual(value, Math.Round(value)))
        {
            return Math.Round(value).ToString("0", CultureInfo.InvariantCulture);
        }

        return Math.Abs(value) >= 10d
            ? value.ToString("0.#", CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static bool NearlyEqual(double left, double right)
    {
        return Math.Abs(left - right) < 0.000_001d;
    }
}
