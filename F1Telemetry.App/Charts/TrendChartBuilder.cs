using System.Windows.Media;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.App.ViewModels;

namespace F1Telemetry.App.Charts;

/// <summary>
/// Builds chart-ready trend panels from completed lap summaries.
/// </summary>
public sealed class TrendChartBuilder
{
    /// <summary>
    /// Builds the multi-lap fuel usage trend panel.
    /// </summary>
    /// <param name="laps">The completed lap summaries to plot.</param>
    public ChartPanelViewModel BuildFuelTrendPanel(IReadOnlyList<LapSummary> laps)
    {
        var orderedLaps = laps
            .Where(lap => lap.FuelUsedLitres is not null)
            .OrderBy(lap => lap.LapNumber)
            .ToArray();

        if (orderedLaps.Length == 0)
        {
            return CreateEmptyPanel("多圈燃油趋势", "圈号", "L");
        }

        return new ChartPanelViewModel(
            title: "多圈燃油趋势",
            xAxisLabel: "圈号",
            yAxisLabel: "L",
            emptyMessage: "暂无历史圈数据",
            isEmpty: false,
            series:
            [
                new ChartSeriesModel
                {
                    Name = "燃油",
                    StrokeBrush = Brushes.Gold,
                    Points = orderedLaps
                        .Select(lap => new ChartPointModel
                        {
                            X = lap.LapNumber,
                            Y = lap.FuelUsedLitres!.Value
                        })
                        .ToArray()
                }
            ]);
    }

    /// <summary>
    /// Builds the multi-lap four-wheel tyre wear trend panel.
    /// </summary>
    /// <param name="laps">The completed lap summaries to plot.</param>
    public ChartPanelViewModel BuildTyreWearTrendPanel(IReadOnlyList<LapSummary> laps)
    {
        var orderedLaps = laps
            .Where(lap => lap.TyreWearDeltaPerWheel is not null)
            .OrderBy(lap => lap.LapNumber)
            .ToArray();

        if (orderedLaps.Length == 0)
        {
            return CreateEmptyPanel("多圈四轮磨损趋势", "圈号", "%");
        }

        return new ChartPanelViewModel(
            title: "多圈四轮磨损趋势",
            xAxisLabel: "圈号",
            yAxisLabel: "%",
            emptyMessage: "暂无历史圈数据",
            isEmpty: false,
            series:
            [
                BuildTyreWearSeries("后左", Brushes.Orange, orderedLaps, wheelSet => wheelSet.RearLeft),
                BuildTyreWearSeries("后右", Brushes.HotPink, orderedLaps, wheelSet => wheelSet.RearRight),
                BuildTyreWearSeries("前左", Brushes.DeepSkyBlue, orderedLaps, wheelSet => wheelSet.FrontLeft),
                BuildTyreWearSeries("前右", Brushes.LimeGreen, orderedLaps, wheelSet => wheelSet.FrontRight)
            ]);
    }

    private static ChartSeriesModel BuildTyreWearSeries(
        string name,
        Brush strokeBrush,
        IReadOnlyList<LapSummary> laps,
        Func<F1Telemetry.Udp.Packets.WheelSet<float>, float> selector)
    {
        return new ChartSeriesModel
        {
            Name = name,
            StrokeBrush = strokeBrush,
            Points = laps
                .Select(lap => new ChartPointModel
                {
                    X = lap.LapNumber,
                    Y = selector(lap.TyreWearDeltaPerWheel!)
                })
                .ToArray()
        };
    }

    private static ChartPanelViewModel CreateEmptyPanel(string title, string xAxisLabel, string yAxisLabel)
    {
        return new ChartPanelViewModel(
            title: title,
            xAxisLabel: xAxisLabel,
            yAxisLabel: yAxisLabel,
            emptyMessage: "暂无历史圈数据",
            isEmpty: true,
            series: Array.Empty<ChartSeriesModel>());
    }
}
