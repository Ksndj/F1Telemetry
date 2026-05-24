using System.Windows.Media;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.App.Charts;

/// <summary>
/// Builds comparison chart panels from persisted laps across multiple sessions.
/// </summary>
public sealed class StoredLapSessionComparisonChartBuilder
{
    private static readonly Brush[] SeriesBrushes =
    [
        Brushes.DeepSkyBlue,
        Brushes.Gold,
        Brushes.LimeGreen,
        Brushes.OrangeRed
    ];

    /// <summary>
    /// Builds the lap-time comparison panel.
    /// </summary>
    /// <param name="sessions">The session lap inputs to plot.</param>
    public ChartPanelViewModel BuildLapTimePanel(IReadOnlyList<SessionComparisonChartInput> sessions)
    {
        return BuildPanel(
            "圈速对比",
            "圈号",
            "s",
            "请选择至少 2 个历史会话进行圈速对比",
            sessions,
            lap => lap.LapTimeInMs,
            value => value / 1_000d);
    }

    /// <summary>
    /// Builds the fuel comparison panel.
    /// </summary>
    /// <param name="sessions">The session lap inputs to plot.</param>
    public ChartPanelViewModel BuildFuelPanel(IReadOnlyList<SessionComparisonChartInput> sessions)
    {
        return BuildPanel(
            "燃油对比",
            "圈号",
            "L",
            "所选会话暂无可绘制燃油数据",
            sessions,
            lap => lap.FuelUsedLitres,
            value => value);
    }

    /// <summary>
    /// Builds the ERS comparison panel.
    /// </summary>
    /// <param name="sessions">The session lap inputs to plot.</param>
    public ChartPanelViewModel BuildErsPanel(IReadOnlyList<SessionComparisonChartInput> sessions)
    {
        return BuildPanel(
            "ERS 对比",
            "圈号",
            "MJ",
            "所选会话暂无可绘制 ERS 数据",
            sessions,
            lap => lap.ErsUsed,
            value => value / 1_000_000d);
    }

    /// <summary>
    /// Builds the stored four-wheel average tyre-wear comparison panel.
    /// </summary>
    /// <param name="sessions">The session tyre-wear inputs to plot.</param>
    public ChartPanelViewModel BuildTyreWearPanel(IReadOnlyList<SessionComparisonTyreWearChartInput> sessions)
    {
        var series = new List<ChartSeriesModel>();
        for (var index = 0; index < sessions.Count; index++)
        {
            var points = BuildAverageTyreWearPoints(sessions[index].Trend);
            if (points.Count == 0)
            {
                continue;
            }

            series.Add(
                new ChartSeriesModel
                {
                    Name = sessions[index].SessionLabel,
                    StrokeBrush = SeriesBrushes[index % SeriesBrushes.Length],
                    Points = points
                });
        }

        return series.Count == 0
            ? CreateTyreWearEmptyPanel()
            : CreatePanel("四轮平均胎磨对比", "圈号", "%", "所选会话暂无完整四轮胎磨数据", series);
    }

    private static ChartPanelViewModel BuildPanel<T>(
        string title,
        string xAxisLabel,
        string yAxisLabel,
        string emptyStateText,
        IReadOnlyList<SessionComparisonChartInput> sessions,
        Func<StoredLap, T?> selector,
        Func<T, double> valueConverter)
        where T : struct
    {
        var series = new List<ChartSeriesModel>();
        for (var index = 0; index < sessions.Count; index++)
        {
            var points = BuildPoints(sessions[index].Laps, selector, valueConverter);
            if (points.Count == 0)
            {
                continue;
            }

            series.Add(
                new ChartSeriesModel
                {
                    Name = sessions[index].SessionLabel,
                    StrokeBrush = SeriesBrushes[index % SeriesBrushes.Length],
                    Points = points
                });
        }

        return series.Count == 0
            ? CreateEmptyPanel(title, xAxisLabel, yAxisLabel, emptyStateText)
            : CreatePanel(title, xAxisLabel, yAxisLabel, emptyStateText, series);
    }

    private static IReadOnlyList<ChartPointModel> BuildPoints<T>(
        IReadOnlyList<StoredLap> laps,
        Func<StoredLap, T?> selector,
        Func<T, double> valueConverter)
        where T : struct
    {
        return laps
            .Select(
                lap =>
                {
                    var value = selector(lap);
                    return value is null
                        ? null
                        : new ChartPointModel
                        {
                            X = lap.LapNumber,
                            Y = valueConverter(value.Value)
                        };
                })
            .OfType<ChartPointModel>()
            .Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y))
            .ToArray();
    }

    private static IReadOnlyList<ChartPointModel> BuildAverageTyreWearPoints(
        IReadOnlyList<StoredLapTyreWearTrendPoint> trend)
    {
        return trend
            .OrderBy(point => point.LapNumber)
            .ThenBy(point => point.SampleIndex)
            .Select(point => new ChartPointModel
            {
                X = point.LapNumber,
                Y = (point.RearLeft + point.RearRight + point.FrontLeft + point.FrontRight) / 4d
            })
            .Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y))
            .ToArray();
    }

    private static ChartPanelViewModel CreateTyreWearEmptyPanel()
    {
        return CreateEmptyPanel("四轮平均胎磨对比", "圈号", "%", "所选会话暂无完整四轮胎磨数据，无法生成胎磨对比");
    }

    private static ChartPanelViewModel CreatePanel(
        string title,
        string xAxisLabel,
        string yAxisLabel,
        string emptyStateText,
        IReadOnlyList<ChartSeriesModel> series)
    {
        return new ChartPanelViewModel(
            title: title,
            xAxisLabel: xAxisLabel,
            yAxisLabel: yAxisLabel,
            emptyMessage: emptyStateText,
            isEmpty: false,
            series: series);
    }

    private static ChartPanelViewModel CreateEmptyPanel(
        string title,
        string xAxisLabel,
        string yAxisLabel,
        string emptyStateText)
    {
        return new ChartPanelViewModel(
            title: title,
            xAxisLabel: xAxisLabel,
            yAxisLabel: yAxisLabel,
            emptyMessage: emptyStateText,
            isEmpty: true,
            series: Array.Empty<ChartSeriesModel>());
    }
}

/// <summary>
/// Represents one selected session and its ordered laps for comparison charts.
/// </summary>
/// <param name="SessionLabel">The chart legend label.</param>
/// <param name="Laps">The ordered stored laps for the session.</param>
public sealed record SessionComparisonChartInput(
    string SessionLabel,
    IReadOnlyList<StoredLap> Laps);

/// <summary>
/// Represents one selected session and its ordered tyre-wear trend points for comparison charts.
/// </summary>
/// <param name="SessionLabel">The chart legend label.</param>
/// <param name="Trend">The ordered four-wheel tyre-wear trend points for the session.</param>
public sealed record SessionComparisonTyreWearChartInput(
    string SessionLabel,
    IReadOnlyList<StoredLapTyreWearTrendPoint> Trend);
