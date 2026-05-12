using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using F1Telemetry.App.Charts;
using F1Telemetry.App.ViewModels;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WPF;

namespace F1Telemetry.App.Views.Controls;

/// <summary>
/// Hosts a ScottPlot chart and refreshes it when chart panel view-model state changes.
/// </summary>
public partial class TelemetryChartControl : UserControl
{
    private const string ChartFontName = "Microsoft YaHei UI";
    private static readonly ScottPlot.Color PlotBackgroundColor = ScottPlot.Color.FromHex("#0C1830");
    private static readonly ScottPlot.Color DataBackgroundColor = ScottPlot.Color.FromHex("#10233F");
    private static readonly ScottPlot.Color AxisColor = ScottPlot.Color.FromHex("#D7E4F3");
    private static readonly ScottPlot.Color GridColor = ScottPlot.Color.FromHex("#244B72");
    private static readonly ScottPlot.Color LegendBackgroundColor = ScottPlot.Color.FromHex("#132743");

    private ChartPanelViewModel? _chartPanel;
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
            or nameof(ChartPanelViewModel.HasData)
            or nameof(ChartPanelViewModel.Title)
            or nameof(ChartPanelViewModel.XAxisLabel)
            or nameof(ChartPanelViewModel.YAxisLabel)
            or nameof(ChartPanelViewModel.EmptyStateText))
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
            _plotHost.Plot.Clear();
            _plotHost.Refresh();
            return;
        }

        var plottableSeries = panel.Series
            .Select(series => new
            {
                Series = series,
                Points = ToCoordinates(series.Points).ToList()
            })
            .Where(series => series.Points.Count > 0)
            .ToArray();

        EmptyStateTextBlock.Text = panel.EmptyStateText;
        var hasData = panel.HasData && plottableSeries.Length > 0;
        EmptyStateBorder.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
        PlotBorder.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;

        var plot = _plotHost.Plot;
        plot.Clear();
        ApplyPlotStyle(plot);

        if (!hasData)
        {
            _plotHost.Refresh();
            return;
        }

        plot.Axes.Bottom.Label.Text = panel.XAxisLabel;
        plot.Axes.Left.Label.Text = panel.YAxisLabel;
        plot.Axes.Title.Label.Text = panel.Title;

        foreach (var series in plottableSeries)
        {
            CreateScatter(series.Series, series.Points);
        }

        plot.ShowLegend();
        plot.Axes.AutoScale();
        _plotHost.Refresh();
    }

    private static void ApplyPlotStyle(Plot plot)
    {
        plot.FigureBackground.Color = PlotBackgroundColor;
        plot.DataBackground.Color = DataBackgroundColor;
        plot.Font.Set(ChartFontName);

        plot.Axes.Color(AxisColor);
        plot.Axes.Bottom.Label.FontName = ChartFontName;
        plot.Axes.Left.Label.FontName = ChartFontName;
        plot.Axes.Bottom.TickLabelStyle.FontName = ChartFontName;
        plot.Axes.Left.TickLabelStyle.FontName = ChartFontName;

        plot.Grid.MajorLineColor = GridColor;
        plot.Grid.MinorLineColor = GridColor.WithAlpha(40);
        plot.Grid.MajorLineWidth = 1;
        plot.Grid.MinorLineWidth = 1;

        plot.Legend.FontName = ChartFontName;
        plot.Legend.FontColor = AxisColor;
        plot.Legend.BackgroundColor = LegendBackgroundColor;
        plot.Legend.OutlineColor = GridColor;
        plot.Legend.OutlineWidth = 1;
    }

    private Scatter CreateScatter(ChartSeriesModel series, List<Coordinates> points)
    {
        var scatter = _plotHost.Plot.Add.Scatter(points, ToScottPlotColor(series.StrokeBrush));
        scatter.LegendText = series.Name;
        scatter.LineWidth = 2f;
        scatter.MarkerSize = points.Count == 1 ? 6f : 0f;
        return scatter;
    }

    private static Coordinates[] ToCoordinates(IReadOnlyList<ChartPointModel> points)
    {
        return points
            .Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y))
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
