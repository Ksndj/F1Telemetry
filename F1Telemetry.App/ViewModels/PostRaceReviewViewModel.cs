using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using F1Telemetry.App.Charts;
using F1Telemetry.App.Reports;
using F1Telemetry.App.Services;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Strategy;
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
    private readonly PostRaceReviewReportBuilder _reportBuilder;
    private readonly IPostRaceReviewReportExportService _reportExportService;
    private readonly RelayCommand _refreshCommand;
    private readonly RelayCommand _exportMarkdownCommand;
    private readonly RelayCommand _exportJsonCommand;
    private bool _isLoading;
    private bool _isExportingReport;
    private bool _isRefreshingHistoryBrowser;
    private int _loadVersion;
    private string _statusText = "请选择历史会话进行赛后复盘。";
    private string _emptyStateText = "请选择历史会话进行赛后复盘";
    private string _reportExportStatusText = "尚未导出复盘报告。";
    private HistorySessionItemViewModel? _lastLoadedSession;
    private IReadOnlyList<StoredLap> _lastLoadedLaps = Array.Empty<StoredLap>();
    private IReadOnlyList<StoredEvent> _lastLoadedEvents = Array.Empty<StoredEvent>();
    private IReadOnlyList<StoredAiReport> _lastLoadedAiReports = Array.Empty<StoredAiReport>();
    private IReadOnlyList<PostRaceReviewMetricRowViewModel> _lastLoadedSummaryMetrics = Array.Empty<PostRaceReviewMetricRowViewModel>();
    private IReadOnlyList<PostRaceReviewStintRowViewModel> _lastLoadedStints = Array.Empty<PostRaceReviewStintRowViewModel>();

    /// <summary>
    /// Initializes a new post-race review view model.
    /// </summary>
    /// <param name="historyBrowser">The shared history session browser.</param>
    /// <param name="lapRepository">The stored lap repository.</param>
    /// <param name="eventRepository">The stored event repository.</param>
    /// <param name="aiReportRepository">The stored AI report repository.</param>
    /// <param name="reportExportService">The optional report export service.</param>
    public PostRaceReviewViewModel(
        HistorySessionBrowserViewModel historyBrowser,
        ILapRepository lapRepository,
        IEventRepository eventRepository,
        IAIReportRepository aiReportRepository,
        IPostRaceReviewReportExportService? reportExportService = null)
    {
        HistoryBrowser = historyBrowser ?? throw new ArgumentNullException(nameof(historyBrowser));
        _lapRepository = lapRepository ?? throw new ArgumentNullException(nameof(lapRepository));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _aiReportRepository = aiReportRepository ?? throw new ArgumentNullException(nameof(aiReportRepository));
        _chartBuilder = new StoredLapPostRaceChartBuilder();
        _reportBuilder = new PostRaceReviewReportBuilder();
        _reportExportService = reportExportService ?? new SaveFilePostRaceReviewReportExportService();
        _refreshCommand = new RelayCommand(() => _ = RefreshAsync(), () => !IsLoading && !IsExportingReport);
        _exportMarkdownCommand = new RelayCommand(
            () => _ = ExportReportAsync(PostRaceReviewReportFormat.Markdown),
            () => !IsLoading && !IsExportingReport);
        _exportJsonCommand = new RelayCommand(
            () => _ = ExportReportAsync(PostRaceReviewReportFormat.Json),
            () => !IsLoading && !IsExportingReport);

        SummaryMetricRows = new ObservableCollection<PostRaceReviewMetricRowViewModel>();
        EventTimelineRows = new ObservableCollection<PostRaceReviewEventRowViewModel>();
        AiReportRows = new ObservableCollection<PostRaceReviewAiReportRowViewModel>();
        TyreStintSummaryRows = new ObservableCollection<PostRaceReviewStintRowViewModel>();
        EventTimelinePages = new PagedCollectionViewModel<PostRaceReviewEventRowViewModel>();
        AiReportPages = new PagedCollectionViewModel<PostRaceReviewAiReportRowViewModel>();

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
    /// Gets the command that exports the selected review as Markdown.
    /// </summary>
    public ICommand ExportMarkdownCommand => _exportMarkdownCommand;

    /// <summary>
    /// Gets the command that exports the selected review as JSON.
    /// </summary>
    public ICommand ExportJsonCommand => _exportJsonCommand;

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
                RaiseCommandStatesChanged();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a report export is in progress.
    /// </summary>
    public bool IsExportingReport
    {
        get => _isExportingReport;
        private set
        {
            if (SetProperty(ref _isExportingReport, value))
            {
                RaiseCommandStatesChanged();
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
    /// Gets the latest report export status text.
    /// </summary>
    public string ReportExportStatusText
    {
        get => _reportExportStatusText;
        private set => SetProperty(ref _reportExportStatusText, value);
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
    /// Gets the paged stored event timeline rows used by WPF bindings.
    /// </summary>
    public PagedCollectionViewModel<PostRaceReviewEventRowViewModel> EventTimelinePages { get; }

    /// <summary>
    /// Gets the stored AI report rows.
    /// </summary>
    public ObservableCollection<PostRaceReviewAiReportRowViewModel> AiReportRows { get; }

    /// <summary>
    /// Gets the stored AI report rows used by WPF bindings.
    /// </summary>
    public ObservableCollection<PostRaceReviewAiReportRowViewModel> AiReports => AiReportRows;

    /// <summary>
    /// Gets the paged stored AI report rows used by WPF bindings.
    /// </summary>
    public PagedCollectionViewModel<PostRaceReviewAiReportRowViewModel> AiReportPages { get; }

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
    /// Exports the selected post-race review in the requested format.
    /// </summary>
    /// <param name="format">The report format.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task ExportReportAsync(
        PostRaceReviewReportFormat format,
        CancellationToken cancellationToken = default)
    {
        if (IsExportingReport)
        {
            return;
        }

        IsExportingReport = true;
        ReportExportStatusText = "正在导出历史会话复盘报告...";

        try
        {
            var reportData = await EnsureReportDataAsync(cancellationToken);
            if (reportData is null)
            {
                return;
            }

            var content = format == PostRaceReviewReportFormat.Markdown
                ? _reportBuilder.BuildMarkdown(reportData)
                : _reportBuilder.BuildJson(reportData);
            var request = new PostRaceReviewReportExportRequest(
                format,
                BuildSuggestedFileName(reportData.Session, format),
                content);

            var result = await _reportExportService.ExportAsync(request, cancellationToken);
            ReportExportStatusText = result.Exported
                ? $"报告已导出：{result.FilePath ?? request.SuggestedFileName}"
                : "报告导出已取消。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ReportExportStatusText = "报告导出已取消。";
        }
        catch (Exception ex)
        {
            ReportExportStatusText = $"报告导出失败：{ex.Message}";
        }
        finally
        {
            IsExportingReport = false;
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
        var summaryRows = BuildSummaryMetricRows(selectedSession, laps, events, aiReports);
        var stintRows = PostRaceReviewStintRowViewModel.BuildFromLaps(laps);
        var strategyTimeline = BuildStrategyTimeline(laps, events);

        foreach (var row in summaryRows)
        {
            SummaryMetricRows.Add(row);
        }

        foreach (var row in events.Select(PostRaceReviewEventRowViewModel.FromStoredEvent))
        {
            EventTimelineRows.Add(row);
        }

        foreach (var row in strategyTimeline.Select(PostRaceReviewEventRowViewModel.FromStrategyTimeline))
        {
            EventTimelineRows.Add(row);
        }

        foreach (var row in aiReports.Select(PostRaceReviewAiReportRowViewModel.FromStoredReport))
        {
            AiReportRows.Add(row);
        }

        EventTimelinePages.SetItems(EventTimelineRows, resetPage: true);
        AiReportPages.SetItems(AiReportRows, resetPage: true);

        foreach (var row in stintRows)
        {
            TyreStintSummaryRows.Add(row);
        }

        _lastLoadedSession = selectedSession;
        _lastLoadedLaps = laps.ToArray();
        _lastLoadedEvents = events.ToArray();
        _lastLoadedAiReports = aiReports.ToArray();
        _lastLoadedSummaryMetrics = summaryRows.ToArray();
        _lastLoadedStints = stintRows.ToArray();

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
            events.Count + strategyTimeline.Count,
            aiReports.Count);
    }

    private static IReadOnlyList<StrategyTimelineEntry> BuildStrategyTimeline(
        IReadOnlyList<StoredLap> laps,
        IReadOnlyList<StoredEvent> events)
    {
        var analyzer = new StintStrategyAnalyzer();
        var lapInputs = laps
            .Select(lap => new StrategyLapInput
            {
                LapNumber = lap.LapNumber,
                LapTimeInMs = lap.LapTimeInMs is null or < 0 ? null : (uint)lap.LapTimeInMs.Value,
                IsValid = lap.IsValid,
                FuelUsedLitres = lap.FuelUsedLitres,
                ErsUsed = lap.ErsUsed,
                StartTyre = lap.StartTyre,
                EndTyre = lap.EndTyre
            })
            .ToArray();
        var raceEvents = events
            .Select(storedEvent => new RaceEvent
            {
                EventType = storedEvent.EventType,
                Severity = storedEvent.Severity,
                LapNumber = storedEvent.LapNumber,
                VehicleIdx = storedEvent.VehicleIdx,
                DriverName = storedEvent.DriverName,
                Message = storedEvent.Message,
                Timestamp = storedEvent.CreatedAt
            })
            .ToArray();

        return analyzer.Analyze(lapInputs, raceEvents).Timeline;
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
        EventTimelinePages.SetItems(EventTimelineRows, resetPage: true);
        AiReportPages.SetItems(AiReportRows, resetPage: true);
        _lastLoadedSession = null;
        _lastLoadedLaps = Array.Empty<StoredLap>();
        _lastLoadedEvents = Array.Empty<StoredEvent>();
        _lastLoadedAiReports = Array.Empty<StoredAiReport>();
        _lastLoadedSummaryMetrics = Array.Empty<PostRaceReviewMetricRowViewModel>();
        _lastLoadedStints = Array.Empty<PostRaceReviewStintRowViewModel>();
        LapTimeTrendPanel.UpdateFrom(_chartBuilder.BuildLapTimePanel(Array.Empty<StoredLap>()));
        SectorSplitTrendPanel.UpdateFrom(_chartBuilder.BuildSectorSplitPanel(Array.Empty<StoredLap>()));
        FuelTrendPanel.UpdateFrom(_chartBuilder.BuildFuelPanel(Array.Empty<StoredLap>()));
        ErsTrendPanel.UpdateFrom(_chartBuilder.BuildErsPanel(Array.Empty<StoredLap>()));
        TyreWearTrendPanel.UpdateFrom(_chartBuilder.BuildTyreWearUnavailablePanel());
    }

    private async Task<PostRaceReviewReportData?> EnsureReportDataAsync(CancellationToken cancellationToken)
    {
        var selectedSession = SelectedSession;
        if (selectedSession is null)
        {
            await RefreshAsync(cancellationToken);
            selectedSession = SelectedSession;
        }

        if (selectedSession is null)
        {
            ReportExportStatusText = "请选择历史会话后再导出报告。";
            return null;
        }

        if (!HasLoadedSnapshotFor(selectedSession))
        {
            await RefreshAsync(cancellationToken);
            selectedSession = SelectedSession;
        }

        if (selectedSession is null || !HasLoadedSnapshotFor(selectedSession))
        {
            ReportExportStatusText = "报告导出失败：历史会话复盘数据尚未加载。";
            return null;
        }

        return new PostRaceReviewReportData(
            selectedSession,
            _lastLoadedLaps,
            _lastLoadedEvents,
            _lastLoadedAiReports,
            _lastLoadedSummaryMetrics,
            _lastLoadedStints,
            DateTimeOffset.UtcNow,
            VersionInfo.CurrentVersion);
    }

    private bool HasLoadedSnapshotFor(HistorySessionItemViewModel selectedSession)
    {
        return _lastLoadedSession is not null
            && string.Equals(_lastLoadedSession.SessionId, selectedSession.SessionId, StringComparison.Ordinal);
    }

    private void RaiseCommandStatesChanged()
    {
        _refreshCommand.RaiseCanExecuteChanged();
        _exportMarkdownCommand.RaiseCanExecuteChanged();
        _exportJsonCommand.RaiseCanExecuteChanged();
    }

    private static string BuildSuggestedFileName(
        HistorySessionItemViewModel session,
        PostRaceReviewReportFormat format)
    {
        var extension = format == PostRaceReviewReportFormat.Markdown ? "md" : "json";
        var sessionIdentifier = session.SessionUid == "-" ? session.SessionId : session.SessionUid;
        var normalizedSession = SanitizeFileNamePart(sessionIdentifier);
        return $"F1Telemetry-{normalizedSession}-post-race-review.{extension}";
    }

    private static string SanitizeFileNamePart(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(ch => invalidChars.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch)
            .ToArray())
            .Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "session" : sanitized;
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
