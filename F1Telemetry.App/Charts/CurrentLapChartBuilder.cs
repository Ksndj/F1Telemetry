using System.Windows.Media;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.App.ViewModels;

namespace F1Telemetry.App.Charts;

/// <summary>
/// Builds chart-ready current-lap panels from player lap samples.
/// </summary>
public sealed class CurrentLapChartBuilder
{
    private readonly int _maxPointsPerSeries;

    /// <summary>
    /// Initializes a new current-lap chart builder.
    /// </summary>
    /// <param name="maxPointsPerSeries">The maximum number of points per rendered series.</param>
    public CurrentLapChartBuilder(int maxPointsPerSeries = 240)
    {
        _maxPointsPerSeries = Math.Max(8, maxPointsPerSeries);
    }

    /// <summary>
    /// Builds the current-lap speed chart panel.
    /// </summary>
    /// <param name="samples">The current in-flight lap samples.</param>
    public ChartPanelViewModel BuildSpeedPanel(IReadOnlyList<LapSample> samples)
    {
        var speedPoints = DownSampleWithPeakPreservation(
            samples
                .Where(sample => sample.LapDistance is not null && sample.SpeedKph is not null)
                .Select(sample => new ChartPointModel
                {
                    X = sample.LapDistance!.Value,
                    Y = sample.SpeedKph!.Value
                })
                .ToArray(),
            _maxPointsPerSeries);

        if (speedPoints.Count == 0)
        {
            return CreateEmptyPanel("当前圈速度曲线", "圈内距离 (m)", "km/h");
        }

        return new ChartPanelViewModel(
            title: "当前圈速度曲线",
            xAxisLabel: "圈内距离 (m)",
            yAxisLabel: "km/h",
            emptyMessage: "等待当前圈采样",
            isEmpty: false,
            series:
            [
                new ChartSeriesModel
                {
                    Name = "速度",
                    StrokeBrush = Brushes.DeepSkyBlue,
                    Points = speedPoints
                }
            ]);
    }

    /// <summary>
    /// Builds the current-lap throttle and brake chart panel.
    /// </summary>
    /// <param name="samples">The current in-flight lap samples.</param>
    public ChartPanelViewModel BuildThrottleBrakePanel(IReadOnlyList<LapSample> samples)
    {
        var throttlePoints = DownSampleWithPeakPreservation(
            samples
                .Where(sample => sample.LapDistance is not null && sample.Throttle is not null)
                .Select(sample => new ChartPointModel
                {
                    X = sample.LapDistance!.Value,
                    Y = sample.Throttle!.Value * 100d
                })
                .ToArray(),
            _maxPointsPerSeries);

        var brakePoints = DownSampleWithPeakPreservation(
            samples
                .Where(sample => sample.LapDistance is not null && sample.Brake is not null)
                .Select(sample => new ChartPointModel
                {
                    X = sample.LapDistance!.Value,
                    Y = sample.Brake!.Value * 100d
                })
                .ToArray(),
            _maxPointsPerSeries);

        if (throttlePoints.Count == 0 && brakePoints.Count == 0)
        {
            return CreateEmptyPanel("当前圈油门 / 刹车曲线", "圈内距离 (m)", "%");
        }

        return new ChartPanelViewModel(
            title: "当前圈油门 / 刹车曲线",
            xAxisLabel: "圈内距离 (m)",
            yAxisLabel: "%",
            emptyMessage: "等待当前圈采样",
            isEmpty: false,
            series:
            [
                new ChartSeriesModel
                {
                    Name = "油门",
                    StrokeBrush = Brushes.LimeGreen,
                    Points = throttlePoints
                },
                new ChartSeriesModel
                {
                    Name = "刹车",
                    StrokeBrush = Brushes.OrangeRed,
                    Points = brakePoints
                }
            ]);
    }

    private static ChartPanelViewModel CreateEmptyPanel(string title, string xAxisLabel, string yAxisLabel)
    {
        return new ChartPanelViewModel(
            title: title,
            xAxisLabel: xAxisLabel,
            yAxisLabel: yAxisLabel,
            emptyMessage: "等待当前圈采样",
            isEmpty: true,
            series: Array.Empty<ChartSeriesModel>());
    }

    private static IReadOnlyList<ChartPointModel> DownSampleWithPeakPreservation(IReadOnlyList<ChartPointModel> points, int maxPoints)
    {
        if (points.Count <= maxPoints)
        {
            return points;
        }

        if (maxPoints <= 2)
        {
            return new[] { points[0], points[^1] };
        }

        var firstPoint = points[0];
        var lastPoint = points[^1];
        var interiorPointCount = points.Count - 2;
        var maxInteriorPoints = maxPoints - 2;
        var bucketCount = Math.Max(1, maxInteriorPoints / 2);
        var bucketSize = (int)Math.Ceiling(interiorPointCount / (double)bucketCount);
        var result = new List<ChartPointModel>(maxPoints) { firstPoint };

        for (var bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
        {
            var bucketStart = 1 + (bucketIndex * bucketSize);
            if (bucketStart >= points.Count - 1)
            {
                break;
            }

            var bucketEndExclusive = Math.Min(points.Count - 1, bucketStart + bucketSize);
            if (bucketStart >= bucketEndExclusive)
            {
                continue;
            }

            var minIndex = bucketStart;
            var maxIndex = bucketStart;
            for (var index = bucketStart + 1; index < bucketEndExclusive; index++)
            {
                if (points[index].Y < points[minIndex].Y)
                {
                    minIndex = index;
                }

                if (points[index].Y > points[maxIndex].Y)
                {
                    maxIndex = index;
                }
            }

            if (minIndex == maxIndex)
            {
                result.Add(points[minIndex]);
                continue;
            }

            if (minIndex < maxIndex)
            {
                result.Add(points[minIndex]);
                result.Add(points[maxIndex]);
            }
            else
            {
                result.Add(points[maxIndex]);
                result.Add(points[minIndex]);
            }
        }

        result.Add(lastPoint);
        return result;
    }
}
