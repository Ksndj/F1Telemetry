using System.ComponentModel;
using System.Windows.Media;
using F1Telemetry.App.Charts;
using F1Telemetry.App.ViewModels;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies that chart panel view models notify WPF bindings when they are refreshed.
/// </summary>
public sealed class ChartPanelViewModelTests
{
    /// <summary>
    /// Verifies that UpdateFrom raises property notifications for all visible panel fields.
    /// </summary>
    [Fact]
    public void UpdateFrom_RaisesPropertyChangedForAllBindableFields()
    {
        var panel = new ChartPanelViewModel();
        var notifications = new List<string>();
        panel.PropertyChanged += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.PropertyName))
            {
                notifications.Add(args.PropertyName!);
            }
        };

        panel.UpdateFrom(
            new ChartPanelViewModel(
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
                        Points =
                        [
                            new ChartPointModel { X = 5d, Y = 1.8d }
                        ]
                    }
                ]));

        Assert.Contains(nameof(ChartPanelViewModel.Title), notifications);
        Assert.Contains(nameof(ChartPanelViewModel.XAxisLabel), notifications);
        Assert.Contains(nameof(ChartPanelViewModel.YAxisLabel), notifications);
        Assert.Contains(nameof(ChartPanelViewModel.EmptyMessage), notifications);
        Assert.Contains(nameof(ChartPanelViewModel.IsEmpty), notifications);
        Assert.Contains(nameof(ChartPanelViewModel.Series), notifications);
    }

    /// <summary>
    /// Verifies that UpdateFrom copies chart state into the current panel instance.
    /// </summary>
    [Fact]
    public void UpdateFrom_CopiesChartState()
    {
        var panel = new ChartPanelViewModel();
        var source = new ChartPanelViewModel(
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
                    Points =
                    [
                        new ChartPointModel { X = 10d, Y = 200d }
                    ]
                }
            ]);

        panel.UpdateFrom(source);

        Assert.Equal(source.Title, panel.Title);
        Assert.Equal(source.XAxisLabel, panel.XAxisLabel);
        Assert.Equal(source.YAxisLabel, panel.YAxisLabel);
        Assert.Equal(source.EmptyMessage, panel.EmptyMessage);
        Assert.Equal(source.IsEmpty, panel.IsEmpty);
        Assert.Same(source.Series, panel.Series);
    }
}
