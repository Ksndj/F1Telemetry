using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using F1Telemetry.App.Charts;
using F1Telemetry.App.Formatting;
using F1Telemetry.App.Services;
using F1Telemetry.Core.Abstractions;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Drives multi-session comparison state for historical stored sessions.
/// </summary>
public sealed class SessionComparisonViewModel : ViewModelBase
{
    private const int MaxRecentSessions = 50;
    private const int MaxRecentLaps = 200;
    private const int MaxSelectedSessions = 4;

    private readonly ISessionRepository _sessionRepository;
    private readonly ILapRepository _lapRepository;
    private readonly IHistorySessionDeletionConfirmationService _deletionConfirmationService;
    private readonly StoredLapSessionComparisonChartBuilder _chartBuilder;
    private readonly RelayCommand _refreshCommand;
    private readonly RelayCommand<SessionComparisonSessionItemViewModel> _deleteSessionCommand;
    private IReadOnlyList<StoredSession> _recentSessions = Array.Empty<StoredSession>();
    private SessionComparisonTrackFilterViewModel? _selectedTrackFilter;
    private bool _isLoadingSessions;
    private bool _isLoadingComparison;
    private bool _isApplyingSelection;
    private bool _isDeletingSession;
    private int _sessionLoadVersion;
    private int _comparisonLoadVersion;
    private string _statusText = "等待加载历史会话对比。";
    private string _emptyStateText = "请选择至少 2 个历史会话进行对比";

    /// <summary>
    /// Initializes a session comparison view model.
    /// </summary>
    /// <param name="sessionRepository">The stored session repository.</param>
    /// <param name="lapRepository">The stored lap repository.</param>
    public SessionComparisonViewModel(
        ISessionRepository sessionRepository,
        ILapRepository lapRepository,
        IHistorySessionDeletionConfirmationService? deletionConfirmationService = null)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _lapRepository = lapRepository ?? throw new ArgumentNullException(nameof(lapRepository));
        _deletionConfirmationService = deletionConfirmationService ?? new MessageBoxHistorySessionDeletionConfirmationService();
        _chartBuilder = new StoredLapSessionComparisonChartBuilder();
        _refreshCommand = new RelayCommand(() => _ = RefreshAsync(), () => !IsLoadingSessions && !IsLoadingComparison && !IsDeletingSession);
        _deleteSessionCommand = new RelayCommand<SessionComparisonSessionItemViewModel>(
            session => _ = DeleteSessionAsync(session),
            CanDeleteSession);

        TrackFilters = new ObservableCollection<SessionComparisonTrackFilterViewModel>();
        CandidateSessions = new ObservableCollection<SessionComparisonSessionItemViewModel>();
        CandidateSessionPages = new PagedCollectionViewModel<SessionComparisonSessionItemViewModel>();
        SelectedSessions = new ObservableCollection<SessionComparisonSessionItemViewModel>();
        SummaryRows = new ObservableCollection<SessionComparisonMetricRowViewModel>();

        LapTimeComparisonPanel = _chartBuilder.BuildLapTimePanel(Array.Empty<SessionComparisonChartInput>());
        FuelComparisonPanel = _chartBuilder.BuildFuelPanel(Array.Empty<SessionComparisonChartInput>());
        ErsComparisonPanel = _chartBuilder.BuildErsPanel(Array.Empty<SessionComparisonChartInput>());
        TyreWearComparisonPanel = _chartBuilder.BuildTyreWearUnavailablePanel();
    }

    /// <summary>
    /// Gets the command that refreshes candidate sessions and comparison data.
    /// </summary>
    public ICommand RefreshCommand => _refreshCommand;

    /// <summary>
    /// Gets the command that deletes a stored comparison candidate session.
    /// </summary>
    public ICommand DeleteSessionCommand => _deleteSessionCommand;

    /// <summary>
    /// Gets or sets the active track filter.
    /// </summary>
    public SessionComparisonTrackFilterViewModel? SelectedTrackFilter
    {
        get => _selectedTrackFilter;
        set
        {
            if (!SetProperty(ref _selectedTrackFilter, value))
            {
                return;
            }

            ApplyTrackFilter(value, autoSelectDefaultSessions: true, refreshComparison: true);
        }
    }

    /// <summary>
    /// Gets a value indicating whether stored sessions are loading.
    /// </summary>
    public bool IsLoadingSessions
    {
        get => _isLoadingSessions;
        private set
        {
            if (SetProperty(ref _isLoadingSessions, value))
            {
                _refreshCommand.RaiseCanExecuteChanged();
                _deleteSessionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether comparison laps are loading.
    /// </summary>
    public bool IsLoadingComparison
    {
        get => _isLoadingComparison;
        private set
        {
            if (SetProperty(ref _isLoadingComparison, value))
            {
                _refreshCommand.RaiseCanExecuteChanged();
                _deleteSessionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a history session deletion is running.
    /// </summary>
    public bool IsDeletingSession
    {
        get => _isDeletingSession;
        private set
        {
            if (SetProperty(ref _isDeletingSession, value))
            {
                _refreshCommand.RaiseCanExecuteChanged();
                _deleteSessionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the current comparison status text.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>
    /// Gets the current comparison empty-state text.
    /// </summary>
    public string EmptyStateText
    {
        get => _emptyStateText;
        private set => SetProperty(ref _emptyStateText, value);
    }

    /// <summary>
    /// Gets the available track filters grouped from recent sessions.
    /// </summary>
    public ObservableCollection<SessionComparisonTrackFilterViewModel> TrackFilters { get; }

    /// <summary>
    /// Gets the candidate sessions for the active track filter.
    /// </summary>
    public ObservableCollection<SessionComparisonSessionItemViewModel> CandidateSessions { get; }

    /// <summary>
    /// Gets the paged candidate sessions for the active track filter.
    /// </summary>
    public PagedCollectionViewModel<SessionComparisonSessionItemViewModel> CandidateSessionPages { get; }

    /// <summary>
    /// Gets the selected sessions used by the comparison.
    /// </summary>
    public ObservableCollection<SessionComparisonSessionItemViewModel> SelectedSessions { get; }

    /// <summary>
    /// Gets the summary rows for selected sessions.
    /// </summary>
    public ObservableCollection<SessionComparisonMetricRowViewModel> SummaryRows { get; }

    /// <summary>
    /// Gets the lap-time comparison chart panel.
    /// </summary>
    public ChartPanelViewModel LapTimeComparisonPanel { get; }

    /// <summary>
    /// Gets the fuel comparison chart panel.
    /// </summary>
    public ChartPanelViewModel FuelComparisonPanel { get; }

    /// <summary>
    /// Gets the ERS comparison chart panel.
    /// </summary>
    public ChartPanelViewModel ErsComparisonPanel { get; }

    /// <summary>
    /// Gets the fixed unavailable tyre-wear comparison chart panel.
    /// </summary>
    public ChartPanelViewModel TyreWearComparisonPanel { get; }

    /// <summary>
    /// Refreshes recent sessions and reloads the selected comparison.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var loadVersion = Interlocked.Increment(ref _sessionLoadVersion);
        Interlocked.Increment(ref _comparisonLoadVersion);
        var shouldRefreshComparison = false;

        IsLoadingSessions = true;
        IsLoadingComparison = false;
        StatusText = "正在加载历史会话...";
        EmptyStateText = "正在加载历史会话...";
        ClearCandidateSessions();
        ClearComparisonData();
        TrackFilters.Clear();
        _recentSessions = Array.Empty<StoredSession>();
        SetSelectedTrackFilter(null);

        try
        {
            var sessions = await _sessionRepository.GetRecentAsync(MaxRecentSessions, cancellationToken);
            if (!IsCurrentSessionLoad(loadVersion))
            {
                return;
            }

            _recentSessions = sessions
                .OrderByDescending(session => session.StartedAt)
                .ThenByDescending(session => session.Id, StringComparer.Ordinal)
                .ToArray();

            BuildTrackFilters(_recentSessions);
            if (_recentSessions.Count == 0)
            {
                StatusText = "暂无历史会话，无法生成多会话对比。";
                EmptyStateText = "暂无历史会话，无法生成多会话对比";
                return;
            }

            var defaultFilter = SelectDefaultTrackFilter(_recentSessions);
            SetSelectedTrackFilter(defaultFilter);
            ApplyTrackFilter(defaultFilter, autoSelectDefaultSessions: true, refreshComparison: false);
            shouldRefreshComparison = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (IsCurrentSessionLoad(loadVersion))
            {
                StatusText = "历史会话对比加载已取消";
                EmptyStateText = "历史会话对比加载已取消";
            }
        }
        catch (Exception ex)
        {
            if (IsCurrentSessionLoad(loadVersion))
            {
                ClearCandidateSessions();
                ClearComparisonData();
                TrackFilters.Clear();
                SetSelectedTrackFilter(null);
                StatusText = $"多会话对比加载失败：{ex.Message}";
                EmptyStateText = "多会话对比加载失败，请稍后重试";
            }
        }
        finally
        {
            if (IsCurrentSessionLoad(loadVersion))
            {
                IsLoadingSessions = false;
            }
        }

        if (shouldRefreshComparison && IsCurrentSessionLoad(loadVersion))
        {
            await RefreshComparisonAsync(cancellationToken);
        }
    }

    private async Task RefreshComparisonAsync(CancellationToken cancellationToken = default)
    {
        var selectedSessions = SelectedSessions.ToArray();
        var loadVersion = Interlocked.Increment(ref _comparisonLoadVersion);

        if (selectedSessions.Length < 2)
        {
            ClearComparisonData();
            IsLoadingComparison = false;
            StatusText = "请选择至少 2 个历史会话进行对比。";
            EmptyStateText = "请选择至少 2 个历史会话进行对比";
            return;
        }

        IsLoadingComparison = true;
        StatusText = "正在加载多会话对比...";
        EmptyStateText = string.Empty;
        ClearComparisonData();

        try
        {
            var loadTasks = selectedSessions
                .Select(item => LoadSessionLapsAsync(item, cancellationToken))
                .ToArray();

            var comparisonData = await Task.WhenAll(loadTasks);
            if (!IsCurrentComparisonLoad(loadVersion))
            {
                return;
            }

            ApplyComparisonData(comparisonData);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (IsCurrentComparisonLoad(loadVersion))
            {
                ClearComparisonData();
                StatusText = "多会话对比加载已取消";
                EmptyStateText = "多会话对比加载已取消";
            }
        }
        catch (Exception ex)
        {
            if (IsCurrentComparisonLoad(loadVersion))
            {
                ClearComparisonData();
                StatusText = $"多会话对比加载失败：{ex.Message}";
                EmptyStateText = "多会话对比加载失败，请稍后重试";
            }
        }
        finally
        {
            if (IsCurrentComparisonLoad(loadVersion))
            {
                IsLoadingComparison = false;
            }
        }
    }

    private void ApplyComparisonData(IReadOnlyList<SessionComparisonLoadedSession> sessions)
    {
        foreach (var row in sessions.Select(BuildMetricRow))
        {
            SummaryRows.Add(row);
        }

        var chartInputs = sessions
            .Select(session => new SessionComparisonChartInput(session.Item.ComparisonLabel, session.Laps))
            .ToArray();

        LapTimeComparisonPanel.UpdateFrom(_chartBuilder.BuildLapTimePanel(chartInputs));
        FuelComparisonPanel.UpdateFrom(_chartBuilder.BuildFuelPanel(chartInputs));
        ErsComparisonPanel.UpdateFrom(_chartBuilder.BuildErsPanel(chartInputs));
        TyreWearComparisonPanel.UpdateFrom(_chartBuilder.BuildTyreWearUnavailablePanel());

        var lapCount = sessions.Sum(session => session.Laps.Count);
        if (lapCount == 0)
        {
            StatusText = "所选会话暂无可对比单圈数据。";
            EmptyStateText = "所选会话暂无可对比单圈数据";
            return;
        }

        StatusText = string.Format(
            CultureInfo.InvariantCulture,
            "已加载多会话对比：{0} 个会话、{1} 条单圈记录。",
            sessions.Count,
            lapCount);
        EmptyStateText = string.Empty;
    }

    private void ApplyTrackFilter(
        SessionComparisonTrackFilterViewModel? filter,
        bool autoSelectDefaultSessions,
        bool refreshComparison)
    {
        Interlocked.Increment(ref _comparisonLoadVersion);
        ClearCandidateSessions();
        ClearComparisonData();

        if (filter is null)
        {
            CandidateSessionPages.SetItems(CandidateSessions, resetPage: true);
            StatusText = "请选择赛道筛选历史会话。";
            EmptyStateText = "请选择赛道筛选历史会话";
            return;
        }

        var sessions = _recentSessions
            .Where(session => Nullable.Equals(session.TrackId, filter.TrackId))
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id, StringComparer.Ordinal)
            .ToArray();

        _isApplyingSelection = true;
        try
        {
            foreach (var session in sessions)
            {
                var item = new SessionComparisonSessionItemViewModel(session);
                item.SelectionChanged += OnCandidateSessionSelectionChanged;
                CandidateSessions.Add(item);
            }

            if (autoSelectDefaultSessions)
            {
                foreach (var item in CandidateSessions.Take(2))
                {
                    item.IsSelected = true;
                }
            }

            UpdateSelectedSessions();
            CandidateSessionPages.SetItems(CandidateSessions, resetPage: true);
        }
        finally
        {
            _isApplyingSelection = false;
        }

        if (SelectedSessions.Count < 2)
        {
            StatusText = "请选择至少 2 个历史会话进行对比。";
            EmptyStateText = "请选择至少 2 个历史会话进行对比";
            return;
        }

        if (refreshComparison)
        {
            _ = RefreshComparisonAsync();
        }
    }

    private void BuildTrackFilters(IReadOnlyList<StoredSession> sessions)
    {
        foreach (var group in sessions
                     .GroupBy(session => session.TrackId)
                     .OrderByDescending(group => group.Max(session => session.StartedAt)))
        {
            TrackFilters.Add(
                new SessionComparisonTrackFilterViewModel(
                    group.Key,
                    FormatTrack(group.Key),
                    group.Count()));
        }
    }

    private SessionComparisonTrackFilterViewModel? SelectDefaultTrackFilter(IReadOnlyList<StoredSession> sessions)
    {
        var defaultGroup = sessions
            .GroupBy(session => session.TrackId)
            .OrderByDescending(group => group.Max(session => session.StartedAt))
            .FirstOrDefault(group => group.Count() >= 2);
        var defaultTrackId = defaultGroup is null && sessions.Count > 0
            ? sessions[0].TrackId
            : defaultGroup?.Key;

        if (defaultGroup is not null)
        {
            return TrackFilters.FirstOrDefault(filter => Nullable.Equals(filter.TrackId, defaultGroup.Key));
        }

        return TrackFilters.FirstOrDefault(filter => Nullable.Equals(filter.TrackId, defaultTrackId));
    }

    private void OnCandidateSessionSelectionChanged(
        SessionComparisonSessionItemViewModel item,
        bool isSelected)
    {
        if (_isApplyingSelection)
        {
            return;
        }

        if (isSelected && CandidateSessions.Count(candidate => candidate.IsSelected) > MaxSelectedSessions)
        {
            item.SetIsSelectedSilently(false);
            UpdateSelectedSessions();
            StatusText = "最多选择 4 个历史会话进行对比。";
            EmptyStateText = string.Empty;
            return;
        }

        UpdateSelectedSessions();
        _ = RefreshComparisonAsync();
    }

    /// <summary>
    /// Deletes the specified historical comparison session after confirmation.
    /// </summary>
    /// <param name="session">The session to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task DeleteSessionAsync(
        SessionComparisonSessionItemViewModel? session,
        CancellationToken cancellationToken = default)
    {
        if (!CanDeleteSession(session))
        {
            StatusText = session is null
                ? "请选择要删除的历史会话。"
                : "进行中的历史会话不可删除。";
            return;
        }

        IsDeletingSession = true;
        try
        {
            var confirmed = await _deletionConfirmationService.ConfirmDeleteAsync(
                new HistorySessionDeletionConfirmationRequest(session!.SummaryText, session.SessionUid),
                cancellationToken);
            if (!confirmed)
            {
                StatusText = "历史会话删除已取消。";
                return;
            }

            StatusText = "正在删除历史会话...";
            var deleted = await _sessionRepository.DeleteAsync(session.SessionId, cancellationToken);
            if (!deleted)
            {
                StatusText = "未找到要删除的历史会话。";
                return;
            }

            StatusText = "历史会话已删除，正在刷新多会话对比。";
            await RefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "历史会话删除已取消。";
        }
        catch (Exception ex)
        {
            StatusText = $"历史会话删除失败：{ex.Message}";
        }
        finally
        {
            IsDeletingSession = false;
        }
    }

    private async Task<SessionComparisonLoadedSession> LoadSessionLapsAsync(
        SessionComparisonSessionItemViewModel item,
        CancellationToken cancellationToken)
    {
        var laps = await _lapRepository.GetRecentAsync(item.SessionId, MaxRecentLaps, cancellationToken);
        return new SessionComparisonLoadedSession(item, OrderLaps(laps));
    }

    private static IReadOnlyList<StoredLap> OrderLaps(IReadOnlyList<StoredLap> laps)
    {
        return laps
            .OrderBy(lap => lap.LapNumber)
            .ThenBy(lap => lap.CreatedAt)
            .ThenBy(lap => lap.Id)
            .ToArray();
    }

    private static SessionComparisonMetricRowViewModel BuildMetricRow(SessionComparisonLoadedSession session)
    {
        var validLapTimes = session.Laps
            .Where(lap => lap.IsValid && lap.LapTimeInMs is not null && lap.LapTimeInMs.Value >= 0)
            .ToArray();
        var bestLap = validLapTimes.MinBy(lap => lap.LapTimeInMs);
        double? averageLapMs = validLapTimes.Length == 0
            ? null
            : validLapTimes.Average(lap => lap.LapTimeInMs!.Value);
        var fuelValues = session.Laps
            .Select(lap => lap.FuelUsedLitres)
            .Where(value => value is not null && float.IsFinite(value.Value))
            .Select(value => value!.Value)
            .ToArray();
        var ersValues = session.Laps
            .Select(lap => lap.ErsUsed)
            .Where(value => value is not null && float.IsFinite(value.Value))
            .Select(value => value!.Value / 1_000_000f)
            .ToArray();

        return new SessionComparisonMetricRowViewModel
        {
            SessionLabel = session.Item.ComparisonLabel,
            BestLapText = bestLap is null ? "-" : FormatLapTime(bestLap.LapTimeInMs),
            AverageLapText = averageLapMs is null ? "-" : FormatLapTime(averageLapMs.Value),
            ValidLapCountText = validLapTimes.Length == 0
                ? "0 圈"
                : $"{validLapTimes.Length} 圈",
            AverageFuelText = fuelValues.Length == 0
                ? "-"
                : $"{fuelValues.Average():0.00} L",
            AverageErsText = ersValues.Length == 0
                ? "-"
                : $"{ersValues.Average():0.00} MJ"
        };
    }

    private void ClearCandidateSessions()
    {
        foreach (var item in CandidateSessions)
        {
            item.SelectionChanged -= OnCandidateSessionSelectionChanged;
        }

        CandidateSessions.Clear();
        CandidateSessionPages.SetItems(CandidateSessions, resetPage: true);
        SelectedSessions.Clear();
    }

    private void ClearComparisonData()
    {
        SummaryRows.Clear();
        LapTimeComparisonPanel.UpdateFrom(_chartBuilder.BuildLapTimePanel(Array.Empty<SessionComparisonChartInput>()));
        FuelComparisonPanel.UpdateFrom(_chartBuilder.BuildFuelPanel(Array.Empty<SessionComparisonChartInput>()));
        ErsComparisonPanel.UpdateFrom(_chartBuilder.BuildErsPanel(Array.Empty<SessionComparisonChartInput>()));
        TyreWearComparisonPanel.UpdateFrom(_chartBuilder.BuildTyreWearUnavailablePanel());
    }

    private void UpdateSelectedSessions()
    {
        SelectedSessions.Clear();
        foreach (var item in CandidateSessions.Where(candidate => candidate.IsSelected))
        {
            SelectedSessions.Add(item);
        }
    }

    private bool CanDeleteSession(SessionComparisonSessionItemViewModel? session)
    {
        return session is not null && session.CanDelete && !IsLoadingSessions && !IsLoadingComparison && !IsDeletingSession;
    }

    private void SetSelectedTrackFilter(SessionComparisonTrackFilterViewModel? filter)
    {
        SetProperty(ref _selectedTrackFilter, filter, nameof(SelectedTrackFilter));
    }

    private bool IsCurrentSessionLoad(int loadVersion)
    {
        return loadVersion == _sessionLoadVersion;
    }

    private bool IsCurrentComparisonLoad(int loadVersion)
    {
        return loadVersion == _comparisonLoadVersion;
    }

    private static string FormatTrack(int? trackId)
    {
        if (trackId is null)
        {
            return TrackNameFormatter.Format(null);
        }

        if (trackId.Value < sbyte.MinValue || trackId.Value > sbyte.MaxValue)
        {
            return $"未知赛道（ID {trackId.Value}）";
        }

        return TrackNameFormatter.Format((sbyte)trackId.Value);
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

    private sealed record SessionComparisonLoadedSession(
        SessionComparisonSessionItemViewModel Item,
        IReadOnlyList<StoredLap> Laps);
}
