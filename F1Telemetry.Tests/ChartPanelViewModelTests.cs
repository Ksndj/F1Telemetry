using System.ComponentModel;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using F1Telemetry.App.Charts;
using F1Telemetry.App.ViewModels;
using F1Telemetry.App.Views.Controls;
using ScottPlot.WPF;
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
                emptyMessage: "完成至少一圈后显示",
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
        Assert.Contains(nameof(ChartPanelViewModel.EmptyStateText), notifications);
        Assert.Contains(nameof(ChartPanelViewModel.HasData), notifications);
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
            emptyMessage: "等待本圈采样",
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
        Assert.Equal(source.EmptyStateText, panel.EmptyStateText);
        Assert.Equal(source.HasData, panel.HasData);
        Assert.Same(source.Series, panel.Series);
    }

    /// <summary>
    /// Verifies that updating a populated panel to an empty snapshot clears stale series.
    /// </summary>
    [Fact]
    public void UpdateFrom_DataPanelToEmptyPanel_ClearsSeriesAndHasData()
    {
        var panel = new ChartPanelViewModel(
            title: "当前圈速度曲线",
            xAxisLabel: "圈内距离 (m)",
            yAxisLabel: "km/h",
            emptyMessage: "等待本圈采样",
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

        panel.UpdateFrom(
            new ChartPanelViewModel(
                title: "当前圈速度曲线",
                xAxisLabel: "圈内距离 (m)",
                yAxisLabel: "km/h",
                emptyMessage: "等待本圈采样",
                isEmpty: true,
                series: Array.Empty<ChartSeriesModel>()));

        Assert.False(panel.HasData);
        Assert.True(panel.IsEmpty);
        Assert.Empty(panel.Series);
        Assert.Equal("等待本圈采样", panel.EmptyStateText);
    }

    /// <summary>
    /// Verifies that chart data state is derived from plottable points instead of series count.
    /// </summary>
    [Theory]
    [MemberData(nameof(NonPlottableSeries))]
    public void HasData_WithNoPlottablePoints_ReturnsFalse(IReadOnlyList<ChartSeriesModel> series)
    {
        var panel = new ChartPanelViewModel(
            title: "当前圈速度曲线",
            xAxisLabel: "圈内距离 (m)",
            yAxisLabel: "km/h",
            emptyMessage: "等待本圈采样",
            isEmpty: false,
            series: series);

        Assert.False(panel.HasData);
        Assert.True(panel.IsEmpty);
    }

    /// <summary>
    /// Verifies that a single finite chart point is enough to render a live chart.
    /// </summary>
    [Fact]
    public void HasData_WithSinglePlottablePoint_ReturnsTrue()
    {
        var panel = new ChartPanelViewModel(
            title: "当前圈速度曲线",
            xAxisLabel: "圈内距离 (m)",
            yAxisLabel: "km/h",
            emptyMessage: "等待本圈采样",
            isEmpty: true,
            series:
            [
                new ChartSeriesModel
                {
                    Name = "速度",
                    StrokeBrush = Brushes.DeepSkyBlue,
                    Points =
                    [
                        new ChartPointModel { X = 120d, Y = 238d }
                    ]
                }
            ]);

        Assert.True(panel.HasData);
        Assert.False(panel.IsEmpty);
    }

    /// <summary>
    /// Verifies chart rendering creates a styled plot surface for historical charts.
    /// </summary>
    [Fact]
    public void TelemetryChartControl_WithData_AppliesDarkThemeAndChineseFont()
    {
        RunOnStaThread(() =>
        {
            var control = new TelemetryChartControl
            {
                DataContext = new ChartPanelViewModel(
                    title: "圈速趋势",
                    xAxisLabel: "圈号",
                    yAxisLabel: "s",
                    emptyMessage: "暂无数据",
                    isEmpty: false,
                    series:
                    [
                        new ChartSeriesModel
                        {
                            Name = "圈速",
                            StrokeBrush = Brushes.DeepSkyBlue,
                            Points =
                            [
                                new ChartPointModel { X = 1d, Y = 90d },
                                new ChartPointModel { X = 2d, Y = 89d }
                            ]
                        }
                    ])
            };

            control.UpdateLayout();

            var plotBorder = Assert.IsType<Border>(control.FindName("PlotBorder"));
            var plotHost = GetPlotHost(control);

            Assert.Equal(Visibility.Visible, plotBorder.Visibility);
            Assert.Equal("Microsoft YaHei UI", plotHost.Plot.Axes.Bottom.TickLabelStyle.FontName);
            Assert.Equal("Microsoft YaHei UI", plotHost.Plot.Axes.Left.TickLabelStyle.FontName);
            Assert.Equal("Microsoft YaHei UI", plotHost.Plot.Legend.FontName);
            Assert.Equal(ScottPlot.Color.FromHex("#0C1830").ARGB, plotHost.Plot.FigureBackground.Color.ARGB);
            Assert.Equal(ScottPlot.Color.FromHex("#10233F").ARGB, plotHost.Plot.DataBackground.Color.ARGB);
        });
    }

    /// <summary>
    /// Returns series shapes that contain no finite plottable points.
    /// </summary>
    public static TheoryData<IReadOnlyList<ChartSeriesModel>> NonPlottableSeries()
    {
        return new TheoryData<IReadOnlyList<ChartSeriesModel>>
        {
            Array.Empty<ChartSeriesModel>(),
            new[]
            {
                new ChartSeriesModel
                {
                    Name = "空序列",
                    StrokeBrush = Brushes.Gray,
                    Points = Array.Empty<ChartPointModel>()
                }
            },
            new[]
            {
                new ChartSeriesModel
                {
                    Name = "无效点",
                    StrokeBrush = Brushes.Gray,
                    Points =
                    [
                        new ChartPointModel { X = double.NaN, Y = 1d },
                        new ChartPointModel { X = 1d, Y = double.PositiveInfinity }
                    ]
                }
            }
        };
    }

    private static WpfPlot GetPlotHost(TelemetryChartControl control)
    {
        var field = typeof(TelemetryChartControl).GetField("_plotHost", BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsType<WpfPlot>(field?.GetValue(control));
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? capturedException = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (capturedException is not null)
        {
            ExceptionDispatchInfo.Capture(capturedException).Throw();
        }
    }
}
