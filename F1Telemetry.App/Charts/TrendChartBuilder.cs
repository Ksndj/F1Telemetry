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
            return CreateEmptyPanel("多圈燃油趋势", "圈号", "L", "完成至少一圈后显示");
        }

        return new ChartPanelViewModel(
            title: "多圈燃油趋势",
            xAxisLabel: "圈号",
            yAxisLabel: "L",
            emptyMessage: "完成至少一圈后显示",
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
            return CreateEmptyPanel("多圈四轮磨损趋势", "圈号", "%", "等待轮胎磨损数据");
        }

        var series = new List<ChartSeriesModel>(capacity: 4);
        AddTyreWearSeries(series, "后左", orderedLaps, wheelSet => wheelSet.RearLeft);
        AddTyreWearSeries(series, "后右", orderedLaps, wheelSet => wheelSet.RearRight);
        AddTyreWearSeries(series, "前左", orderedLaps, wheelSet => wheelSet.FrontLeft);
        AddTyreWearSeries(series, "前右", orderedLaps, wheelSet => wheelSet.FrontRight);

        return new ChartPanelViewModel(
            title: "多圈四轮磨损趋势",
            xAxisLabel: "圈号",
            yAxisLabel: "%",
            emptyMessage: "等待轮胎磨损数据",
            isEmpty: false,
            series: series);
    }

    private static void AddTyreWearSeries(
        ICollection<ChartSeriesModel> series,
        string wheelName,
        IReadOnlyList<LapSummary> laps,
        Func<F1Telemetry.Udp.Packets.WheelSet<float>, float> selector)
    {
        foreach (var run in TyreCompoundSeriesBuilder.BuildContiguousRuns(
                     laps,
                     lap => lap.LapNumber,
                     lap => TyreCompoundChartPalette.FromRawCompoundText(SelectLapTyreText(lap)),
                     lap => selector(lap.TyreWearDeltaPerWheel!),
                     style => $"{style.Label} {wheelName}"))
        {
            series.Add(run);
        }
    }

    private static string SelectLapTyreText(LapSummary lap)
    {
        return string.IsNullOrWhiteSpace(lap.EndTyre) || lap.EndTyre == "-"
            ? lap.StartTyre
            : lap.EndTyre;
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
