using F1Telemetry.App.Charts;
using F1Telemetry.Core.Abstractions;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents the bindable state for a single dashboard chart panel.
/// </summary>
public sealed class ChartPanelViewModel : ViewModelBase
{
    private string _title = string.Empty;
    private string _xAxisLabel = string.Empty;
    private string _yAxisLabel = string.Empty;
    private string _emptyStateText = string.Empty;
    private IReadOnlyList<ChartSeriesModel> _series = Array.Empty<ChartSeriesModel>();
    private bool _usesLapNumberXAxis;
    private bool _usesNonNegativeYAxis;

    /// <summary>
    /// Initializes an empty chart panel view model.
    /// </summary>
    public ChartPanelViewModel()
    {
    }

    /// <summary>
    /// Initializes a chart panel view model with the specified state.
    /// </summary>
    /// <param name="title">The chart title.</param>
    /// <param name="xAxisLabel">The X-axis label.</param>
    /// <param name="yAxisLabel">The Y-axis label.</param>
    /// <param name="emptyMessage">The empty-state message.</param>
    /// <param name="isEmpty">Whether the chart is currently empty.</param>
    /// <param name="series">The plotted series.</param>
    /// <param name="usesLapNumberXAxis">Whether the X axis represents positive integer lap numbers.</param>
    /// <param name="usesNonNegativeYAxis">Whether the Y axis represents a metric that cannot be negative.</param>
    public ChartPanelViewModel(
        string title,
        string xAxisLabel,
        string yAxisLabel,
        string emptyMessage,
        bool isEmpty,
        IReadOnlyList<ChartSeriesModel> series,
        bool usesLapNumberXAxis = false,
        bool usesNonNegativeYAxis = false)
    {
        _title = title;
        _xAxisLabel = xAxisLabel;
        _yAxisLabel = yAxisLabel;
        _emptyStateText = string.IsNullOrWhiteSpace(emptyMessage)
            ? "等待图表数据"
            : emptyMessage;
        _series = series ?? Array.Empty<ChartSeriesModel>();
        _usesLapNumberXAxis = usesLapNumberXAxis;
        _usesNonNegativeYAxis = usesNonNegativeYAxis;
    }

    /// <summary>
    /// Gets the panel title.
    /// </summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>
    /// Gets the X-axis label.
    /// </summary>
    public string XAxisLabel
    {
        get => _xAxisLabel;
        private set => SetProperty(ref _xAxisLabel, value);
    }

    /// <summary>
    /// Gets the Y-axis label.
    /// </summary>
    public string YAxisLabel
    {
        get => _yAxisLabel;
        private set => SetProperty(ref _yAxisLabel, value);
    }

    /// <summary>
    /// Gets the empty-state message.
    /// </summary>
    public string EmptyStateText
    {
        get => _emptyStateText;
        private set => SetProperty(ref _emptyStateText, string.IsNullOrWhiteSpace(value) ? "等待图表数据" : value);
    }

    /// <summary>
    /// Gets the legacy empty-state message alias.
    /// </summary>
    public string EmptyMessage
    {
        get => EmptyStateText;
    }

    /// <summary>
    /// Gets a value indicating whether the chart contains at least one finite plottable point.
    /// </summary>
    public bool HasData => HasPlottablePoints(_series);

    /// <summary>
    /// Gets a value indicating whether the chart currently has no plottable data.
    /// </summary>
    public bool IsEmpty => !HasData;

    /// <summary>
    /// Gets the current chart series.
    /// </summary>
    public IReadOnlyList<ChartSeriesModel> Series
    {
        get => _series;
        private set => SetProperty(ref _series, value);
    }

    /// <summary>
    /// Gets a value indicating whether the X axis should be rendered as positive integer lap numbers.
    /// </summary>
    public bool UsesLapNumberXAxis
    {
        get => _usesLapNumberXAxis;
        private set => SetProperty(ref _usesLapNumberXAxis, value);
    }

    /// <summary>
    /// Gets a value indicating whether the Y axis should be clamped to non-negative values.
    /// </summary>
    public bool UsesNonNegativeYAxis
    {
        get => _usesNonNegativeYAxis;
        private set => SetProperty(ref _usesNonNegativeYAxis, value);
    }

    /// <summary>
    /// Updates the current panel from another chart panel snapshot.
    /// </summary>
    /// <param name="source">The source panel state.</param>
    public void UpdateFrom(ChartPanelViewModel source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Title = source.Title;
        XAxisLabel = source.XAxisLabel;
        YAxisLabel = source.YAxisLabel;
        EmptyStateText = source.EmptyStateText;
        Series = source.Series;
        UsesLapNumberXAxis = source.UsesLapNumberXAxis;
        UsesNonNegativeYAxis = source.UsesNonNegativeYAxis;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(XAxisLabel));
        OnPropertyChanged(nameof(YAxisLabel));
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Series));
        OnPropertyChanged(nameof(UsesLapNumberXAxis));
        OnPropertyChanged(nameof(UsesNonNegativeYAxis));
    }

    private static bool HasPlottablePoints(IReadOnlyList<ChartSeriesModel> series)
    {
        return series.Any(chartSeries => chartSeries.Points.Any(point => double.IsFinite(point.X) && double.IsFinite(point.Y)));
    }
}
