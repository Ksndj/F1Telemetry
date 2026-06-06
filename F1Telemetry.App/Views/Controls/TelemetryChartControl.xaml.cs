using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
    private const int WmMouseWheel = 0x020A;
    private const int WmMouseHWheel = 0x020E;
    private static readonly ScottPlot.Color PlotBackgroundColor = ScottPlot.Color.FromHex("#0C1830");
    private static readonly ScottPlot.Color DataBackgroundColor = ScottPlot.Color.FromHex("#10233F");
    private static readonly ScottPlot.Color AxisColor = ScottPlot.Color.FromHex("#D7E4F3");
    private static readonly ScottPlot.Color GridColor = ScottPlot.Color.FromHex("#244B72");
    private static readonly ScottPlot.Color LegendBackgroundColor = ScottPlot.Color.FromHex("#132743");

    private ChartPanelViewModel? _chartPanel;
    private readonly WpfPlot _plotHost;
    private readonly MouseWheelEventHandler _hostPreviewMouseWheelHandler;
    private readonly HwndSourceHook _hostWindowMessageHook;
    private readonly PreProcessInputEventHandler _preProcessInputHandler;
    private HwndSource? _hostHwndSource;
    private Window? _hostWindow;

    /// <summary>
    /// Initializes a new telemetry chart control.
    /// </summary>
    public TelemetryChartControl()
    {
        InitializeComponent();
        _hostPreviewMouseWheelHandler = OnHostPreviewMouseWheel;
        _hostWindowMessageHook = OnHostWindowMessage;
        _preProcessInputHandler = OnPreProcessInput;
        _plotHost = new WpfPlot();
        PlotBorder.Child = _plotHost;
        ChartInteractionHelper.DisableFixedChartInteractions(_plotHost);
        ChartInteractionHelper.AttachNoWheelZoomBehavior(this);
        ChartInteractionHelper.AttachNoWheelZoomBehavior(PlotBorder);
        ChartInteractionHelper.AttachNoWheelZoomBehavior(_plotHost);
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachToInputManager();
        AttachToHostWindow();
        AttachToDataContext(DataContext as ChartPanelViewModel);
        RefreshChart();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachFromInputManager();
        DetachFromHostWindow();
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

    private void AttachToHostWindow()
    {
        var hostWindow = Window.GetWindow(this);
        if (ReferenceEquals(_hostWindow, hostWindow))
        {
            return;
        }

        DetachFromHostWindow();
        if (hostWindow is null)
        {
            return;
        }

        ChartInteractionHelper.AttachNoWheelZoomBehavior(hostWindow, _hostPreviewMouseWheelHandler);
        AttachToHostHwndSource(hostWindow);
        _hostWindow = hostWindow;
    }

    private void DetachFromHostWindow()
    {
        if (_hostWindow is null)
        {
            return;
        }

        ChartInteractionHelper.DetachNoWheelZoomBehavior(_hostWindow, _hostPreviewMouseWheelHandler);
        DetachFromHostHwndSource();
        _hostWindow = null;
    }

    private void OnHostPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (IsMouseOver || IsCurrentMousePositionInsideChart() || IsWheelPointerInsideChart(e) || IsWheelSourceInsideChart(e.OriginalSource))
        {
            e.Handled = true;
        }
    }

    private void AttachToInputManager()
    {
        InputManager.Current.PreProcessInput -= _preProcessInputHandler;
        InputManager.Current.PreProcessInput += _preProcessInputHandler;
    }

    private void DetachFromInputManager()
    {
        InputManager.Current.PreProcessInput -= _preProcessInputHandler;
    }

    private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is MouseWheelEventArgs mouseWheelEventArgs
            && IsCurrentMousePositionInsideChart())
        {
            mouseWheelEventArgs.Handled = true;
        }
    }

    private void AttachToHostHwndSource(Window hostWindow)
    {
        var hwndSource = PresentationSource.FromVisual(hostWindow) as HwndSource;
        if (hwndSource is null)
        {
            return;
        }

        hwndSource.RemoveHook(_hostWindowMessageHook);
        hwndSource.AddHook(_hostWindowMessageHook);
        _hostHwndSource = hwndSource;
    }

    private void DetachFromHostHwndSource()
    {
        if (_hostHwndSource is null)
        {
            return;
        }

        _hostHwndSource.RemoveHook(_hostWindowMessageHook);
        _hostHwndSource = null;
    }

    private IntPtr OnHostWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((msg == WmMouseWheel || msg == WmMouseHWheel)
            && IsScreenPointInsideChart(GetMouseMessageScreenPoint(lParam)))
        {
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void OnChartPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChartPanelViewModel.Series)
            or nameof(ChartPanelViewModel.HasData)
            or nameof(ChartPanelViewModel.Title)
            or nameof(ChartPanelViewModel.XAxisLabel)
            or nameof(ChartPanelViewModel.YAxisLabel)
            or nameof(ChartPanelViewModel.EmptyStateText)
            or nameof(ChartPanelViewModel.UsesLapNumberXAxis)
            or nameof(ChartPanelViewModel.UsesNonNegativeYAxis))
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
        ApplyAxisRules(plot, panel, plottableSeries.SelectMany(series => series.Points).ToArray());
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
        plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();
        plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();

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

    private void ApplyAxisRules(Plot plot, ChartPanelViewModel panel, IReadOnlyList<Coordinates> points)
    {
        if (points.Count == 0)
        {
            return;
        }

        var limits = plot.Axes.GetLimits();
        var xMinimum = limits.Left;
        var xMaximum = limits.Right;
        var yMinimum = limits.Bottom;
        var yMaximum = limits.Top;

        if (panel.UsesLapNumberXAxis)
        {
            var xRange = ChartAxisRangeHelper.GetLapAxisRange(points.Select(point => point.X));
            xMinimum = xRange.Minimum;
            xMaximum = xRange.Maximum;

            var xTicks = ChartAxisRangeHelper.BuildSparseAxisLabels(points.Select(point => point.X), GetChartWidth());
            if (xTicks.Values.Count > 0)
            {
                plot.Axes.Bottom.SetTicks(xTicks.Values.ToArray(), xTicks.Labels.ToArray());
            }
        }

        if (panel.UsesNonNegativeYAxis)
        {
            var yRange = ChartAxisRangeHelper.GetNonNegativeRange(points.Select(point => point.Y));
            yMinimum = yRange.Minimum;
            yMaximum = yRange.Maximum;

            var yTicks = ChartAxisRangeHelper.BuildSparseNumericAxisLabels(yRange, points.Count, GetChartWidth());
            if (yTicks.Values.Count > 0)
            {
                plot.Axes.Left.SetTicks(yTicks.Values.ToArray(), yTicks.Labels.ToArray());
            }
        }

        if (points.Count <= 3)
        {
            plot.Grid.MinorLineWidth = 0;
        }

        plot.Axes.SetLimits(xMinimum, xMaximum, yMinimum, yMaximum);
    }

    private double GetChartWidth()
    {
        if (double.IsFinite(_plotHost.ActualWidth) && _plotHost.ActualWidth > 0d)
        {
            return _plotHost.ActualWidth;
        }

        return double.IsFinite(ActualWidth) && ActualWidth > 0d
            ? ActualWidth
            : 420d;
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

    private bool IsWheelSourceInsideChart(object originalSource)
    {
        return originalSource is DependencyObject source
            && (ReferenceEquals(source, this) || IsAncestorOf(source));
    }

    private bool IsWheelPointerInsideChart(MouseWheelEventArgs e)
    {
        if (!IsLoaded || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return false;
        }

        var position = e.GetPosition(this);
        return IsPointInsideChart(position);
    }

    private bool IsScreenPointInsideChart(Point screenPoint)
    {
        if (!IsLoaded || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return false;
        }

        var position = PointFromScreen(screenPoint);
        return IsPointInsideChart(position);
    }

    private bool IsCurrentMousePositionInsideChart()
    {
        if (!IsLoaded || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return false;
        }

        return IsPointInsideChart(Mouse.GetPosition(this));
    }

    private bool IsPointInsideChart(Point position)
    {
        return position.X >= 0
            && position.X <= ActualWidth
            && position.Y >= 0
            && position.Y <= ActualHeight;
    }

    private static Point GetMouseMessageScreenPoint(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        var x = (short)(value & 0xFFFF);
        var y = (short)((value >> 16) & 0xFFFF);
        return new Point(x, y);
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
