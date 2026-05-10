using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using F1Telemetry.App.Charts;
using F1Telemetry.Core.Abstractions;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Drives the stored-session post-race review surface.
/// </summary>
public sealed class PostRaceReviewViewModel : ViewModelBase, IDisposable
{
    private const int MaxStoredLaps = 200;
    private const int MaxStoredEvents = 300;
    private const int MaxStoredAiReports = 200;

    private readonly ILapRepository _lapRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IAIReportRepository _aiReportRepository;
    private readonly StoredLapPostRaceChartBuilder _chartBuilder;
    private readonly RelayCommand _refreshCommand;
    private bool _isLoading;
    private bool _isRefreshingHistoryBrowser;
    private int _loadVersion;
    private string _statusText = "请选择历史会话进行赛后复盘。";
    private string _emptyStateText = "请选择历史会话进行赛后复盘";

    /// <summary>
    /// Initializes a new post-race review view model.
    /// </summary>
    /// <param name="historyBrowser">The shared history session browser.</param>
    /// <param name="lapRepository">The stored lap repository.</param>
    /// <param name="eventRepository">The stored event repository.</param>
    /// <param name="aiReportRepository">The stored AI report repository.</param>
    public PostRaceReviewViewModel(
        HistorySessionBrowserViewModel historyBrowser,
        ILapRepository lapRepository,
        IEventRepository eventRepository,
        IAIReportRepository aiReportRepository)
    {
        HistoryBrowser = historyBrowser ?? throw new ArgumentNullException(nameof(historyBrowser));
        _lapRepository = lapRepository ?? throw new ArgumentNullException(nameof(lapRepository));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _aiReportRepository = aiReportRepository ?? throw new ArgumentNullException(nameof(aiReportRepository));
        _chartBuilder = new StoredLapPostRaceChartBuilder();
        _refreshCommand = new RelayCommand(() => _ = RefreshAsync(), () => !IsLoading);

        SummaryMetricRows = new ObservableCollection<PostRaceReviewMetricRowViewModel>();
        EventTimelineRows = new ObservableCollection<PostRaceReviewEventRowViewModel>();
        AiReportRows = new ObservableCollection<PostRaceReviewAiReportRowViewModel>();
        TyreStintSummaryRows = new ObservableCollection<PostRaceReviewStintRowViewModel>();

        LapTimeTrendPanel = _chartBuilder.BuildLapTimePanel(Array.Empty<StoredLap>());
        SectorSplitTrendPanel = _chartBuilder.BuildSectorSplitPanel(Array.Empty<StoredLap>());
        FuelTrendPanel = _chartBuilder.BuildFuelPanel(Array.Empty<StoredLap>());
        ErsTrendPanel = _chartBuilder.BuildErsPanel(Array.Empty<StoredLap>());
        TyreWearTrendPanel = _chartBuilder.BuildTyreWearUnavailablePanel();

        SummaryMetricRows.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSummaryMetrics));
        EventTimelineRows.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasEventTimelineRows));
        AiReportRows.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasAiReportRows));
        TyreStintSummaryRows.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTyreStintSummaryRows));
        HistoryBrowser.PropertyChanged += OnHistoryBrowserPropertyChanged;
    }

    /// <summary>
    /// Gets the command that refreshes the selected session review.
    /// </summary>
    public ICommand RefreshCommand => _refreshCommand;

    /// <summary>
    /// Gets the shared history browser.
    /// </summary>
    public HistorySessionBrowserViewModel HistoryBrowser { get; }

    /// <summary>
    /// Gets the selected history session projection.
    /// </summary>
    public HistorySessionItemViewModel? SelectedSession => HistoryBrowser.SelectedSession;

    /// <summary>
    /// Gets a value indicating whether review data is loading.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                _refreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the current review status text.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>
    /// Gets the current review empty-state text.
    /// </summary>
    public string EmptyStateText
    {
        get => _emptyStateText;
        private set => SetProperty(ref _emptyStateText, value);
    }

    /// <summary>
    /// Gets the summary metric rows.
    /// </summary>
    public ObservableCollection<PostRaceReviewMetricRowViewModel> SummaryMetricRows { get; }

    /// <summary>
    /// Gets the summary metric rows used by WPF bindings.
    /// </summary>
    public ObservableCollection<PostRaceReviewMetricRowViewModel> SummaryMetrics => SummaryMetricRows;

    /// <summary>
    /// Gets the lap time trend panel.
    /// </summary>
    public ChartPanelViewModel LapTimeTrendPanel { get; }

    /// <summary>
    /// Gets the sector split trend panel.
    /// </summary>
    public ChartPanelViewModel SectorSplitTrendPanel { get; }

    /// <summary>
    /// Gets the sector trend panel used by WPF bindings.
    /// </summary>
    public ChartPanelViewModel SectorTrendPanel => SectorSplitTrendPanel;

    /// <summary>
    /// Gets the fuel trend panel.
    /// </summary>
    public ChartPanelViewModel FuelTrendPanel { get; }

    /// <summary>
    /// Gets the ERS trend panel.
    /// </summary>
    public ChartPanelViewModel ErsTrendPanel { get; }

    /// <summary>
    /// Gets the unavailable tyre wear panel.
    /// </summary>
    public ChartPanelViewModel TyreWearTrendPanel { get; }

    /// <summary>
    /// Gets the unavailable tyre wear status text.
    /// </summary>
    public string TyreWearStatusText => "历史单圈未保存四轮胎磨数据，无法生成胎磨趋势。";

    /// <summary>
    /// Gets the stored event timeline rows.
    /// </summary>
    public ObservableCollection<PostRaceReviewEventRowViewModel> EventTimelineRows { get; }

    /// <summary>
    /// Gets the stored event timeline rows used by WPF bindings.
    /// </summary>
    public ObservableCollection<PostRaceReviewEventRowViewModel> EventTimeline => EventTimelineRows;

    /// <summary>
    /// Gets the stored AI report rows.
    /// </summary>
    public ObservableCollection<PostRaceReviewAiReportRowViewModel> AiReportRows { get; }

    /// <summary>
    /// Gets the stored AI report rows used by WPF bindings.
    /// </summary>
    public ObservableCollection<PostRaceReviewAiReportRowViewModel> AiReports => AiReportRows;

    /// <summary>
    /// Gets the inferred tyre stint rows.
    /// </summary>
    public ObservableCollection<PostRaceReviewStintRowViewModel> TyreStintSummaryRows { get; }

    /// <summary>
    /// Gets the inferred tyre stint rows used by WPF bindings.
    /// </summary>
    public ObservableCollection<PostRaceReviewStintRowViewModel> StintSummaries => TyreStintSummaryRows;

    /// <summary>
    /// Gets a value indicating whether summary metrics are available.
    /// </summary>
    public bool HasSummaryMetrics => SummaryMetricRows.Count > 0;

    /// <summary>
    /// Gets a value indicating whether event timeline rows are available.
    /// </summary>
    public bool HasEventTimelineRows => EventTimelineRows.Count > 0;

    /// <summary>
    /// Gets a value indicating whether AI report rows are available.
    /// </summary>
    public bool HasAiReportRows => AiReportRows.Count > 0;

    /// <summary>
    /// Gets a value indicating whether inferred tyre stint rows are available.
    /// </summary>
    public bool HasTyreStintSummaryRows => TyreStintSummaryRows.Count > 0;

    /// <summary>
    /// Refreshes the post-race review for the currently selected session.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var loadVersion = Interlocked.Increment(ref _loadVersion);
        IsLoading = true;
        StatusText = "正在加载赛后复盘...";
        EmptyStateText = string.Empty;
        ClearReviewData();

        try
        {
            if (HistoryBrowser.HistorySessions.Count == 0)
            {
                _isRefreshingHistoryBrowser = true;
                try
                {
                    await HistoryBrowser.RefreshSessionsAsync(cancellationToken);
                }
                finally
                {
                    _isRefreshingHistoryBrowser = false;
                    OnPropertyChanged(nameof(SelectedSession));
                }
            }

            if (!IsCurrentLoad(loadVersion))
            {
                return;
            }

            var selectedSession = SelectedSession;
            if (selectedSession is null)
            {
                ApplyNoSessionState();
                return;
            }

            var sessionId = selectedSession.SessionId;
            var lapsTask = _lapRepository.GetRecentAsync(sessionId, MaxStoredLaps, cancellationToken);
            var eventsTask = _eventRepository.GetRecentAsync(sessionId, MaxStoredEvents, cancellationToken);
            var aiReportsTask = _aiReportRepository.GetRecentAsync(sessionId, MaxStoredAiReports, cancellationToken);

            await Task.WhenAll(lapsTask, eventsTask, aiReportsTask);

            if (!IsCurrentLoad(loadVersion) || !ReferenceEquals(SelectedSession, selectedSession))
            {
                return;
            }

            var laps = OrderLaps(lapsTask.Result);
            var events = OrderEvents(eventsTask.Result);
            var aiReports = OrderAiReports(aiReportsTask.Result);

            ApplyReviewData(selectedSession, laps, events, aiReports);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (IsCurrentLoad(loadVersion))
            {
                ClearReviewData();
                StatusText = "赛后复盘加载已取消";
                EmptyStateText = "赛后复盘加载已取消";
            }
        }
        catch (Exception ex)
        {
            if (IsCurrentLoad(loadVersion))
            {
                ClearReviewData();
                StatusText = $"赛后复盘加载失败：{ex.Message}";
                EmptyStateText = "赛后复盘加载失败，请稍后重试";
            }
        }
        finally
        {
            if (IsCurrentLoad(loadVersion))
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Releases the history browser subscription.
    /// </summary>
    public void Dispose()
    {
        HistoryBrowser.PropertyChanged -= OnHistoryBrowserPropertyChanged;
    }

    private static IReadOnlyList<StoredLap> OrderLaps(IReadOnlyList<StoredLap> laps)
    {
        return laps
            .OrderBy(lap => lap.LapNumber)
            .ThenBy(lap => lap.CreatedAt)
            .ThenBy(lap => lap.Id)
            .ToArray();
    }

    private static IReadOnlyList<StoredEvent> OrderEvents(IReadOnlyList<StoredEvent> events)
    {
        return events
            .OrderBy(storedEvent => storedEvent.LapNumber.HasValue ? 0 : 1)
            .ThenBy(storedEvent => storedEvent.LapNumber ?? int.MaxValue)
            .ThenBy(storedEvent => storedEvent.CreatedAt)
            .ThenBy(storedEvent => storedEvent.Id)
            .ToArray();
    }

    private static IReadOnlyList<StoredAiReport> OrderAiReports(IReadOnlyList<StoredAiReport> reports)
    {
        return reports
            .OrderBy(report => report.LapNumber)
            .ThenBy(report => report.CreatedAt)
            .ThenBy(report => report.Id)
            .ToArray();
    }

    private void ApplyReviewData(
        HistorySessionItemViewModel selectedSession,
        IReadOnlyList<StoredLap> laps,
        IReadOnlyList<StoredEvent> events,
        IReadOnlyList<StoredAiReport> aiReports)
    {
        foreach (var row in BuildSummaryMetricRows(selectedSession, laps, events, aiReports))
        {
            SummaryMetricRows.Add(row);
        }

        foreach (var row in events.Select(PostRaceReviewEventRowViewModel.FromStoredEvent))
        {
            EventTimelineRows.Add(row);
        }

        foreach (var row in aiReports.Select(PostRaceReviewAiReportRowViewModel.FromStoredReport))
        {
            AiReportRows.Add(row);
        }

        foreach (var row in PostRaceReviewStintRowViewModel.BuildFromLaps(laps))
        {
            TyreStintSummaryRows.Add(row);
        }

        LapTimeTrendPanel.UpdateFrom(_chartBuilder.BuildLapTimePanel(laps));
        SectorSplitTrendPanel.UpdateFrom(_chartBuilder.BuildSectorSplitPanel(laps));
        FuelTrendPanel.UpdateFrom(_chartBuilder.BuildFuelPanel(laps));
        ErsTrendPanel.UpdateFrom(_chartBuilder.BuildErsPanel(laps));
        TyreWearTrendPanel.UpdateFrom(_chartBuilder.BuildTyreWearUnavailablePanel());

        var dataCount = laps.Count + events.Count + aiReports.Count;
        if (dataCount == 0)
        {
            StatusText = "该会话暂无赛后复盘数据。";
            EmptyStateText = "该会话暂无赛后复盘数据";
            return;
        }

        EmptyStateText = string.Empty;
        StatusText = string.Format(
            CultureInfo.InvariantCulture,
            "已加载赛后复盘：{0} 圈、{1} 事件、{2} 份 AI 报告。",
            laps.Count,
            events.Count,
            aiReports.Count);
    }

    private IReadOnlyList<PostRaceReviewMetricRowViewModel> BuildSummaryMetricRows(
        HistorySessionItemViewModel selectedSession,
        IReadOnlyList<StoredLap> laps,
        IReadOnlyList<StoredEvent> events,
        IReadOnlyList<StoredAiReport> aiReports)
    {
        var validLapTimes = laps
            .Where(lap => lap.LapTimeInMs is not null && lap.LapTimeInMs.Value >= 0)
            .ToArray();
        var bestLap = validLapTimes.MinBy(lap => lap.LapTimeInMs);
        double? averageLapMs = validLapTimes.Length == 0
            ? null
            : validLapTimes.Average(lap => lap.LapTimeInMs!.Value);
        var invalidLapCount = laps.Count(lap => !lap.IsValid);
        var fuelTotal = laps.Where(lap => lap.FuelUsedLitres is not null).Sum(lap => lap.FuelUsedLitres!.Value);
        var ersTotal = laps.Where(lap => lap.ErsUsed is not null).Sum(lap => lap.ErsUsed!.Value) / 1_000_000f;

        return
        [
            new PostRaceReviewMetricRowViewModel
            {
                Label = "会话",
                Value = selectedSession.SummaryText,
                Detail = selectedSession.DurationText
            },
            new PostRaceReviewMetricRowViewModel
            {
                Label = "完成圈数",
                Value = laps.Count == 0 ? "-" : laps.Count.ToString(CultureInfo.InvariantCulture),
                Detail = invalidLapCount == 0 ? "无无效圈记录" : $"{invalidLapCount} 个无效圈"
            },
            new PostRaceReviewMetricRowViewModel
            {
                Label = "最佳圈",
                Value = bestLap is null ? "-" : FormatLapTime(bestLap.LapTimeInMs),
                Detail = bestLap is null ? "暂无圈速" : $"Lap {bestLap.LapNumber}"
            },
            new PostRaceReviewMetricRowViewModel
            {
                Label = "平均圈速",
                Value = averageLapMs is null ? "-" : FormatLapTime(averageLapMs.Value),
                Detail = validLapTimes.Length == 0 ? "暂无有效圈速" : $"{validLapTimes.Length} 圈有圈速"
            },
            new PostRaceReviewMetricRowViewModel
            {
                Label = "燃油消耗",
                Value = fuelTotal <= 0 ? "-" : $"{fuelTotal:0.00} L",
                Detail = "基于已保存单圈估算"
            },
            new PostRaceReviewMetricRowViewModel
            {
                Label = "ERS 消耗",
                Value = ersTotal <= 0 ? "-" : $"{ersTotal:0.00} MJ",
                Detail = "基于已保存单圈估算"
            },
            new PostRaceReviewMetricRowViewModel
            {
                Label = "事件 / AI",
                Value = $"{events.Count} / {aiReports.Count}",
                Detail = "事件数 / AI 报告数"
            }
        ];
    }

    private void ApplyNoSessionState()
    {
        ClearReviewData();
        EmptyStateText = HistoryBrowser.HistorySessions.Count == 0
            ? "暂无历史会话，无法生成赛后复盘"
            : "请选择历史会话进行赛后复盘";
        StatusText = HistoryBrowser.HistorySessions.Count == 0
            ? "暂无历史会话，无法生成赛后复盘。"
            : "请选择历史会话进行赛后复盘。";
    }

    private void ClearReviewData()
    {
        SummaryMetricRows.Clear();
        EventTimelineRows.Clear();
        AiReportRows.Clear();
        TyreStintSummaryRows.Clear();
        LapTimeTrendPanel.UpdateFrom(_chartBuilder.BuildLapTimePanel(Array.Empty<StoredLap>()));
        SectorSplitTrendPanel.UpdateFrom(_chartBuilder.BuildSectorSplitPanel(Array.Empty<StoredLap>()));
        FuelTrendPanel.UpdateFrom(_chartBuilder.BuildFuelPanel(Array.Empty<StoredLap>()));
        ErsTrendPanel.UpdateFrom(_chartBuilder.BuildErsPanel(Array.Empty<StoredLap>()));
        TyreWearTrendPanel.UpdateFrom(_chartBuilder.BuildTyreWearUnavailablePanel());
    }

    private void OnHistoryBrowserPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(HistorySessionBrowserViewModel.SelectedSession), StringComparison.Ordinal))
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedSession));
        if (!_isRefreshingHistoryBrowser)
        {
            _ = RefreshAsync();
        }
    }

    private bool IsCurrentLoad(int loadVersion)
    {
        return loadVersion == _loadVersion;
    }

    private static string FormatLapTime(int? milliseconds)
    {
        return milliseconds is null || milliseconds.Value < 0
            ? "-"
            : FormatLapTime(milliseconds.Value);
    }

    private static string FormatLapTime(double milliseconds)
    {
        if (milliseconds < 0 || !double.IsFinite(milliseconds))
        {
            return "-";
        }

        return FormatLapTime((int)Math.Round(milliseconds));
    }

    private static string FormatLapTime(int milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(milliseconds);
        return time.TotalMinutes >= 1
            ? $"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds:000}"
            : $"{time.Seconds}.{time.Milliseconds:000}s";
    }
}
