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
    private string _emptyMessage = string.Empty;
    private bool _isEmpty;
    private IReadOnlyList<ChartSeriesModel> _series = Array.Empty<ChartSeriesModel>();

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
    public ChartPanelViewModel(
        string title,
        string xAxisLabel,
        string yAxisLabel,
        string emptyMessage,
        bool isEmpty,
        IReadOnlyList<ChartSeriesModel> series)
    {
        _title = title;
        _xAxisLabel = xAxisLabel;
        _yAxisLabel = yAxisLabel;
        _emptyMessage = emptyMessage;
        _isEmpty = isEmpty;
        _series = series ?? Array.Empty<ChartSeriesModel>();
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
    public string EmptyMessage
    {
        get => _emptyMessage;
        private set => SetProperty(ref _emptyMessage, value);
    }

    /// <summary>
    /// Gets a value indicating whether the chart currently has no plottable data.
    /// </summary>
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    /// <summary>
    /// Gets the current chart series.
    /// </summary>
    public IReadOnlyList<ChartSeriesModel> Series
    {
        get => _series;
        private set => SetProperty(ref _series, value);
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
        EmptyMessage = source.EmptyMessage;
        IsEmpty = source.IsEmpty;
        Series = source.Series;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(XAxisLabel));
        OnPropertyChanged(nameof(YAxisLabel));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Series));
    }
}
