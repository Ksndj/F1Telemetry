using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using F1Telemetry.App.Charts;
using F1Telemetry.App.ViewModels;
using ScottPlot;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
using ScottPlot.WPF;

namespace F1Telemetry.App.Views.Controls;

/// <summary>
/// Hosts a ScottPlot chart and refreshes it when chart panel view-model state changes.
/// </summary>
public partial class TelemetryChartControl : UserControl
{
    private ChartPanelViewModel? _chartPanel;
    private Scatter[] _scatterSeries = Array.Empty<Scatter>();
    private List<Coordinates>[] _seriesBuffers = Array.Empty<List<Coordinates>>();
    private readonly WpfPlot _plotHost;

    /// <summary>
    /// Initializes a new telemetry chart control.
    /// </summary>
    public TelemetryChartControl()
    {
        InitializeComponent();
        _plotHost = new WpfPlot();
        PlotBorder.Child = _plotHost;
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachToDataContext(DataContext as ChartPanelViewModel);
        RefreshChart();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachFromPanel(e.OldValue as ChartPanelViewModel);
        AttachToDataContext(e.NewValue as ChartPanelViewModel);
        RefreshChart();
    }

    private void AttachToDataContext(ChartPanelViewModel? panel)
    {
        if (ReferenceEquals(_chartPanel, panel))
        {
            return;
        }

        _chartPanel = panel;
        AttachToPanel(panel);
    }

    private void AttachToPanel(ChartPanelViewModel? panel)
    {
        if (panel is null)
        {
            return;
        }

        panel.PropertyChanged += OnChartPanelPropertyChanged;
    }

    private void DetachFromPanel(ChartPanelViewModel? panel)
    {
        if (panel is null)
        {
            return;
        }

        panel.PropertyChanged -= OnChartPanelPropertyChanged;
    }

    private void OnChartPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChartPanelViewModel.Series)
            or nameof(ChartPanelViewModel.IsEmpty)
            or nameof(ChartPanelViewModel.Title)
            or nameof(ChartPanelViewModel.XAxisLabel)
            or nameof(ChartPanelViewModel.YAxisLabel)
            or nameof(ChartPanelViewModel.EmptyMessage))
        {
            RefreshChart();
        }
    }

    private void RefreshChart()
    {
        var panel = _chartPanel ?? DataContext as ChartPanelViewModel;
        if (panel is null)
        {
            EmptyStateTextBlock.Text = "等待图表数据";
            EmptyStateBorder.Visibility = Visibility.Visible;
            PlotBorder.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyStateTextBlock.Text = panel.EmptyMessage;
        EmptyStateBorder.Visibility = panel.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        PlotBorder.Visibility = panel.IsEmpty ? Visibility.Collapsed : Visibility.Visible;

        if (panel.IsEmpty)
        {
            return;
        }

        var plot = _plotHost.Plot;
        plot.Axes.Bottom.Label.Text = panel.XAxisLabel;
        plot.Axes.Left.Label.Text = panel.YAxisLabel;
        plot.Axes.Title.Label.Text = panel.Title;

        if (ShouldRebuildSeries(panel.Series))
        {
            plot.Clear();
            _seriesBuffers = panel.Series
                .Select(series => ToCoordinates(series.Points).ToList())
                .ToArray();
            _scatterSeries = panel.Series
                .Zip(_seriesBuffers, (series, buffer) => CreateScatter(series, buffer))
                .ToArray();
            plot.ShowLegend();
        }
        else
        {
            for (var index = 0; index < _seriesBuffers.Length; index++)
            {
                var points = ToCoordinates(panel.Series[index].Points);
                var buffer = _seriesBuffers[index];
                buffer.Clear();
                buffer.AddRange(points);
            }
        }

        plot.Axes.AutoScale();
        _plotHost.Refresh();
    }

    private bool ShouldRebuildSeries(IReadOnlyList<ChartSeriesModel> series)
    {
        if (_scatterSeries.Length != series.Count)
        {
            return true;
        }

        for (var index = 0; index < series.Count; index++)
        {
            if (!string.Equals(_scatterSeries[index].LegendText, series[index].Name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private Scatter CreateScatter(ChartSeriesModel series, List<Coordinates> points)
    {
        var scatter = _plotHost.Plot.Add.Scatter(points, ToScottPlotColor(series.StrokeBrush));
        scatter.LegendText = series.Name;
        scatter.LineWidth = 2f;
        scatter.MarkerSize = 0f;
        return scatter;
    }

    private static Coordinates[] ToCoordinates(IReadOnlyList<ChartPointModel> points)
    {
        return points
            .Select(point => new Coordinates(point.X, point.Y))
            .ToArray();
    }

    private static ScottPlot.Color ToScottPlotColor(Brush brush)
    {
        if (brush is SolidColorBrush solidColorBrush)
        {
            return ScottPlot.Color.FromARGB(
                (solidColorBrush.Color.A << 24)
                | (solidColorBrush.Color.R << 16)
                | (solidColorBrush.Color.G << 8)
                | solidColorBrush.Color.B);
        }

        return ScottPlot.Color.FromARGB(
            (System.Windows.Media.Colors.White.A << 24)
            | (System.Windows.Media.Colors.White.R << 16)
            | (System.Windows.Media.Colors.White.G << 8)
            | System.Windows.Media.Colors.White.B);
    }
}
