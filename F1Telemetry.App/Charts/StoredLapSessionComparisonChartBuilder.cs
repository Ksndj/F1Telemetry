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
    /// Builds the fixed unavailable state for stored-lap tyre wear comparison.
    /// </summary>
    public ChartPanelViewModel BuildTyreWearUnavailablePanel()
    {
        return CreateEmptyPanel(
            "四轮胎磨对比",
            "圈号",
            "%",
            "历史单圈未保存四轮胎磨数据，无法生成胎磨对比");
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
