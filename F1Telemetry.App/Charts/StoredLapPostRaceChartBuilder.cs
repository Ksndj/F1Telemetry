using System.Windows.Media;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.App.Charts;

/// <summary>
/// Builds post-race trend panels from persisted lap rows.
/// </summary>
public sealed class StoredLapPostRaceChartBuilder
{
    /// <summary>
    /// Builds the stored-lap time trend panel.
    /// </summary>
    /// <param name="laps">The stored laps to plot.</param>
    public ChartPanelViewModel BuildLapTimePanel(IReadOnlyList<StoredLap> laps)
    {
        var points = BuildPoints(laps, lap => lap.LapTimeInMs);
        if (points.Count == 0)
        {
            return CreateEmptyPanel("圈速趋势", "圈号", "s", "该会话暂无可绘制圈速数据");
        }

        return CreatePanel(
            "圈速趋势",
            "圈号",
            "s",
            "该会话暂无可绘制圈速数据",
            [
                new ChartSeriesModel
                {
                    Name = "圈速",
                    StrokeBrush = Brushes.DeepSkyBlue,
                    Points = points
                }
            ]);
    }

    /// <summary>
    /// Builds the stored-lap sector split trend panel.
    /// </summary>
    /// <param name="laps">The stored laps to plot.</param>
    public ChartPanelViewModel BuildSectorSplitPanel(IReadOnlyList<StoredLap> laps)
    {
        var sector1 = BuildPoints(laps, lap => lap.Sector1TimeInMs);
        var sector2 = BuildPoints(laps, lap => lap.Sector2TimeInMs);
        var sector3 = BuildPoints(laps, lap => lap.Sector3TimeInMs);
        if (sector1.Count == 0 && sector2.Count == 0 && sector3.Count == 0)
        {
            return CreateEmptyPanel("分段趋势", "圈号", "s", "该会话暂无可绘制分段数据");
        }

        var series = new List<ChartSeriesModel>(capacity: 3);
        AddSeries(series, "S1", Brushes.LimeGreen, sector1);
        AddSeries(series, "S2", Brushes.Gold, sector2);
        AddSeries(series, "S3", Brushes.OrangeRed, sector3);

        return CreatePanel("分段趋势", "圈号", "s", "该会话暂无可绘制分段数据", series);
    }

    /// <summary>
    /// Builds the stored-lap fuel trend panel.
    /// </summary>
    /// <param name="laps">The stored laps to plot.</param>
    public ChartPanelViewModel BuildFuelPanel(IReadOnlyList<StoredLap> laps)
    {
        var points = BuildPoints(laps, lap => lap.FuelUsedLitres);
        if (points.Count == 0)
        {
            return CreateEmptyPanel("燃油趋势", "圈号", "L", "该会话暂无可绘制燃油数据");
        }

        return CreatePanel(
            "燃油趋势",
            "圈号",
            "L",
            "该会话暂无可绘制燃油数据",
            [
                new ChartSeriesModel
                {
                    Name = "燃油消耗",
                    StrokeBrush = Brushes.Gold,
                    Points = points
                }
            ]);
    }

    /// <summary>
    /// Builds the stored-lap ERS trend panel.
    /// </summary>
    /// <param name="laps">The stored laps to plot.</param>
    public ChartPanelViewModel BuildErsPanel(IReadOnlyList<StoredLap> laps)
    {
        var points = BuildPoints(laps, lap => lap.ErsUsed, value => value / 1_000_000d);
        if (points.Count == 0)
        {
            return CreateEmptyPanel("ERS 趋势", "圈号", "MJ", "该会话暂无可绘制 ERS 数据");
        }

        return CreatePanel(
            "ERS 趋势",
            "圈号",
            "MJ",
            "该会话暂无可绘制 ERS 数据",
            [
                new ChartSeriesModel
                {
                    Name = "ERS 消耗",
                    StrokeBrush = Brushes.MediumPurple,
                    Points = points
                }
            ]);
    }

    /// <summary>
    /// Builds the explicit unavailable state for stored-lap tyre wear.
    /// </summary>
    public ChartPanelViewModel BuildTyreWearUnavailablePanel()
    {
        return CreateEmptyPanel("四轮胎磨趋势", "圈号", "%", "历史单圈未保存四轮胎磨数据，无法生成胎磨趋势");
    }

    private static void AddSeries(
        ICollection<ChartSeriesModel> series,
        string name,
        Brush strokeBrush,
        IReadOnlyList<ChartPointModel> points)
    {
        if (points.Count == 0)
        {
            return;
        }

        series.Add(
            new ChartSeriesModel
            {
                Name = name,
                StrokeBrush = strokeBrush,
                Points = points
            });
    }

    private static IReadOnlyList<ChartPointModel> BuildPoints(
        IReadOnlyList<StoredLap> laps,
        Func<StoredLap, int?> selector)
    {
        return BuildPoints(laps, selector, value => value / 1_000d);
    }

    private static IReadOnlyList<ChartPointModel> BuildPoints(
        IReadOnlyList<StoredLap> laps,
        Func<StoredLap, float?> selector)
    {
        return BuildPoints(laps, selector, value => value);
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
