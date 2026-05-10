using System.Collections.ObjectModel;
using System.Windows.Input;
using F1Telemetry.Core.Abstractions;
using F1Telemetry.Storage.Interfaces;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Drives the history session browser state and stored lap projections.
/// </summary>
public sealed class HistorySessionBrowserViewModel : ViewModelBase
{
    private const int MaxRecentSessions = 50;
    private const int MaxRecentLaps = 200;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILapRepository _lapRepository;
    private readonly RelayCommand _refreshSessionsCommand;
    private HistorySessionItemViewModel? _selectedSession;
    private bool _isLoadingSessions;
    private bool _isLoadingLaps;
    private string _statusText = "等待加载历史会话。";
    private string _emptyStateText = "暂无历史会话";
    private int _sessionLoadVersion;
    private int _lapLoadVersion;

    /// <summary>
    /// Initializes a history session browser view model.
    /// </summary>
    /// <param name="sessionRepository">The session repository.</param>
    /// <param name="lapRepository">The lap repository.</param>
    public HistorySessionBrowserViewModel(
        ISessionRepository sessionRepository,
        ILapRepository lapRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _lapRepository = lapRepository ?? throw new ArgumentNullException(nameof(lapRepository));
        _refreshSessionsCommand = new RelayCommand(() => _ = RefreshSessionsAsync());

        HistorySessions = new ObservableCollection<HistorySessionItemViewModel>();
        HistoryLaps = new ObservableCollection<LapSummaryItemViewModel>();
        HistorySessions.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasHistorySessions));
        HistoryLaps.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasHistoryLaps));
    }

    /// <summary>
    /// Gets the recent stored sessions.
    /// </summary>
    public ObservableCollection<HistorySessionItemViewModel> HistorySessions { get; }

    /// <summary>
    /// Gets the stored laps for the selected session.
    /// </summary>
    public ObservableCollection<LapSummaryItemViewModel> HistoryLaps { get; }

    /// <summary>
    /// Gets or sets the selected stored session.
    /// </summary>
    public HistorySessionItemViewModel? SelectedSession
    {
        get => _selectedSession;
        set => _ = SelectSessionAsync(value, CancellationToken.None);
    }

    /// <summary>
    /// Gets a value indicating whether sessions are loading.
    /// </summary>
    public bool IsLoadingSessions
    {
        get => _isLoadingSessions;
        private set => SetProperty(ref _isLoadingSessions, value);
    }

    /// <summary>
    /// Gets a value indicating whether laps are loading.
    /// </summary>
    public bool IsLoadingLaps
    {
        get => _isLoadingLaps;
        private set => SetProperty(ref _isLoadingLaps, value);
    }

    /// <summary>
    /// Gets the current history browser status text.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>
    /// Gets the current empty-state text.
    /// </summary>
    public string EmptyStateText
    {
        get => _emptyStateText;
        private set => SetProperty(ref _emptyStateText, value);
    }

    /// <summary>
    /// Gets a value indicating whether any history sessions are available.
    /// </summary>
    public bool HasHistorySessions => HistorySessions.Count > 0;

    /// <summary>
    /// Gets a value indicating whether any history laps are available.
    /// </summary>
    public bool HasHistoryLaps => HistoryLaps.Count > 0;

    /// <summary>
    /// Gets the command that refreshes stored sessions.
    /// </summary>
    public ICommand RefreshSessionsCommand => _refreshSessionsCommand;

    /// <summary>
    /// Refreshes the recent stored session list.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task RefreshSessionsAsync(CancellationToken cancellationToken = default)
    {
        var loadVersion = Interlocked.Increment(ref _sessionLoadVersion);
        Interlocked.Increment(ref _lapLoadVersion);
        ClearSelectedSession();
        HistorySessions.Clear();
        HistoryLaps.Clear();
        IsLoadingLaps = false;
        IsLoadingSessions = true;
        StatusText = "正在加载历史会话...";
        EmptyStateText = "正在加载历史会话...";

        try
        {
            var sessions = await _sessionRepository.GetRecentAsync(MaxRecentSessions, cancellationToken);
            if (!IsCurrentSessionLoad(loadVersion))
            {
                return;
            }

            HistorySessions.Clear();
            foreach (var session in sessions)
            {
                HistorySessions.Add(new HistorySessionItemViewModel(session));
            }

            if (HistorySessions.Count == 0)
            {
                EmptyStateText = "暂无历史会话";
                StatusText = "暂无历史会话";
                return;
            }

            EmptyStateText = string.Empty;
            StatusText = $"已加载 {HistorySessions.Count} 个历史会话。";
            IsLoadingSessions = false;
            await SelectSessionAsync(HistorySessions[0], cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (IsCurrentSessionLoad(loadVersion))
            {
                EmptyStateText = "暂无历史会话";
                StatusText = "历史会话加载已取消";
            }
        }
        catch (Exception ex)
        {
            if (IsCurrentSessionLoad(loadVersion))
            {
                ClearSelectedSession();
                HistorySessions.Clear();
                HistoryLaps.Clear();
                EmptyStateText = "暂无历史会话";
                StatusText = $"历史会话加载失败：{ex.Message}";
            }
        }
        finally
        {
            if (IsCurrentSessionLoad(loadVersion))
            {
                IsLoadingSessions = false;
            }
        }
    }

    /// <summary>
    /// Loads stored laps for the selected session.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task LoadSelectedSessionLapsAsync(CancellationToken cancellationToken = default)
    {
        var selectedSession = SelectedSession;
        if (selectedSession is null)
        {
            Interlocked.Increment(ref _lapLoadVersion);
            HistoryLaps.Clear();
            IsLoadingLaps = false;
            EmptyStateText = HasHistorySessions ? "请选择历史会话" : "暂无历史会话";
            StatusText = HasHistorySessions ? "请选择历史会话。" : "暂无历史会话";
            return;
        }

        var loadVersion = Interlocked.Increment(ref _lapLoadVersion);
        HistoryLaps.Clear();
        IsLoadingLaps = true;
        StatusText = "正在加载单圈记录...";
        EmptyStateText = string.Empty;

        try
        {
            var laps = await _lapRepository.GetRecentAsync(selectedSession.SessionId, MaxRecentLaps, cancellationToken);
            if (!IsCurrentLapLoad(loadVersion, selectedSession))
            {
                return;
            }

            foreach (var lap in laps.OrderBy(lap => lap.LapNumber).ThenBy(lap => lap.CreatedAt).ThenBy(lap => lap.Id))
            {
                HistoryLaps.Add(LapSummaryItemViewModel.FromStoredLap(lap));
            }

            if (HistoryLaps.Count == 0)
            {
                EmptyStateText = "该会话暂无单圈记录";
                StatusText = "该会话暂无单圈记录";
                return;
            }

            EmptyStateText = string.Empty;
            StatusText = $"已加载 {HistoryLaps.Count} 条单圈记录。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (IsCurrentLapLoad(loadVersion, selectedSession))
            {
                HistoryLaps.Clear();
                EmptyStateText = "该会话暂无单圈记录";
                StatusText = "单圈记录加载已取消";
            }
        }
        catch (Exception ex)
        {
            if (IsCurrentLapLoad(loadVersion, selectedSession))
            {
                HistoryLaps.Clear();
                EmptyStateText = "该会话暂无单圈记录";
                StatusText = $"单圈记录加载失败：{ex.Message}";
            }
        }
        finally
        {
            if (IsCurrentLapLoad(loadVersion, selectedSession))
            {
                IsLoadingLaps = false;
            }
        }
    }

    private Task SelectSessionAsync(HistorySessionItemViewModel? selectedSession, CancellationToken cancellationToken)
    {
        if (!SetProperty(ref _selectedSession, selectedSession, nameof(SelectedSession)))
        {
            return Task.CompletedTask;
        }

        if (selectedSession is null)
        {
            Interlocked.Increment(ref _lapLoadVersion);
            HistoryLaps.Clear();
            IsLoadingLaps = false;
            EmptyStateText = HasHistorySessions ? "请选择历史会话" : "暂无历史会话";
            StatusText = HasHistorySessions ? "请选择历史会话。" : "暂无历史会话";
            return Task.CompletedTask;
        }

        return LoadSelectedSessionLapsAsync(cancellationToken);
    }

    private bool IsCurrentSessionLoad(int loadVersion)
    {
        return loadVersion == _sessionLoadVersion;
    }

    private bool IsCurrentLapLoad(int loadVersion, HistorySessionItemViewModel selectedSession)
    {
        return loadVersion == _lapLoadVersion && ReferenceEquals(SelectedSession, selectedSession);
    }

    private void ClearSelectedSession()
    {
        if (_selectedSession is null)
        {
            return;
        }

        _selectedSession = null;
        OnPropertyChanged(nameof(SelectedSession));
    }
}
