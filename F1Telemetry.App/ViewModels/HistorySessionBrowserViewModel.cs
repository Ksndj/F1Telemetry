using System.Collections.ObjectModel;
using System.Windows.Input;
using F1Telemetry.App.Services;
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
    private readonly IHistorySessionDeletionConfirmationService _deletionConfirmationService;
    private readonly RelayCommand _refreshSessionsCommand;
    private readonly RelayCommand<HistorySessionItemViewModel> _deleteSessionCommand;
    private HistorySessionItemViewModel? _selectedSession;
    private bool _isLoadingSessions;
    private bool _isLoadingLaps;
    private bool _isDeletingSession;
    private string _statusText = "等待加载历史会话。";
    private string _emptyStateText = "暂无历史会话";
    private int _sessionLoadVersion;
    private int _lapLoadVersion;

    /// <summary>
    /// Initializes a history session browser view model.
    /// </summary>
    /// <param name="sessionRepository">The session repository.</param>
    /// <param name="lapRepository">The lap repository.</param>
    /// <param name="deletionConfirmationService">The optional deletion confirmation service.</param>
    public HistorySessionBrowserViewModel(
        ISessionRepository sessionRepository,
        ILapRepository lapRepository,
        IHistorySessionDeletionConfirmationService? deletionConfirmationService = null)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _lapRepository = lapRepository ?? throw new ArgumentNullException(nameof(lapRepository));
        _deletionConfirmationService = deletionConfirmationService ?? new MessageBoxHistorySessionDeletionConfirmationService();
        _refreshSessionsCommand = new RelayCommand(() => _ = RefreshSessionsAsync(), () => !IsDeletingSession);
        _deleteSessionCommand = new RelayCommand<HistorySessionItemViewModel>(
            session => _ = DeleteSessionAsync(session),
            CanDeleteSession);

        HistorySessions = new ObservableCollection<HistorySessionItemViewModel>();
        HistoryLaps = new ObservableCollection<LapSummaryItemViewModel>();
        HistorySessionPages = new PagedCollectionViewModel<HistorySessionItemViewModel>();
        HistoryLapPages = new PagedCollectionViewModel<LapSummaryItemViewModel>();
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
    /// Gets the paged stored sessions used by history views.
    /// </summary>
    public PagedCollectionViewModel<HistorySessionItemViewModel> HistorySessionPages { get; }

    /// <summary>
    /// Gets the paged stored laps used by history views.
    /// </summary>
    public PagedCollectionViewModel<LapSummaryItemViewModel> HistoryLapPages { get; }

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
        private set
        {
            if (SetProperty(ref _isLoadingSessions, value))
            {
                RaiseCommandStatesChanged();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether laps are loading.
    /// </summary>
    public bool IsLoadingLaps
    {
        get => _isLoadingLaps;
        private set
        {
            if (SetProperty(ref _isLoadingLaps, value))
            {
                RaiseCommandStatesChanged();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a history session is being deleted.
    /// </summary>
    public bool IsDeletingSession
    {
        get => _isDeletingSession;
        private set
        {
            if (SetProperty(ref _isDeletingSession, value))
            {
                RaiseCommandStatesChanged();
            }
        }
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
    /// Gets the command that deletes a stored history session.
    /// </summary>
    public ICommand DeleteSessionCommand => _deleteSessionCommand;

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
        RefreshSessionPages(resetPage: true);
        RefreshLapPages(resetPage: true);
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

            RefreshSessionPages(resetPage: true);

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
                RefreshSessionPages(resetPage: true);
                RefreshLapPages(resetPage: true);
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
            RefreshLapPages(resetPage: true);
            IsLoadingLaps = false;
            EmptyStateText = HasHistorySessions ? "请选择历史会话" : "暂无历史会话";
            StatusText = HasHistorySessions ? "请选择历史会话。" : "暂无历史会话";
            return;
        }

        var loadVersion = Interlocked.Increment(ref _lapLoadVersion);
        HistoryLaps.Clear();
        RefreshLapPages(resetPage: true);
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

            RefreshLapPages(resetPage: true);

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
                RefreshLapPages(resetPage: true);
                EmptyStateText = "该会话暂无单圈记录";
                StatusText = "单圈记录加载已取消";
            }
        }
        catch (Exception ex)
        {
            if (IsCurrentLapLoad(loadVersion, selectedSession))
            {
                HistoryLaps.Clear();
                RefreshLapPages(resetPage: true);
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

    /// <summary>
    /// Deletes the specified stored history session after confirmation.
    /// </summary>
    /// <param name="session">The session to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task DeleteSessionAsync(
        HistorySessionItemViewModel? session,
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
            var sessionIndex = HistorySessions.IndexOf(session);
            var deleted = await _sessionRepository.DeleteAsync(session.SessionId, cancellationToken);
            if (!deleted)
            {
                StatusText = "未找到要删除的历史会话。";
                return;
            }

            HistorySessions.Remove(session);
            RefreshSessionPages(resetPage: false);

            if (SelectedSession is not null && string.Equals(SelectedSession.SessionId, session.SessionId, StringComparison.Ordinal))
            {
                await SelectSessionAsync(ResolveNextSelection(sessionIndex), cancellationToken);
            }

            if (HistorySessions.Count == 0)
            {
                HistoryLaps.Clear();
                RefreshLapPages(resetPage: true);
                EmptyStateText = "暂无历史会话";
                StatusText = "历史会话已删除，当前暂无历史会话。";
                return;
            }

            EmptyStateText = string.Empty;
            StatusText = $"历史会话已删除。剩余 {HistorySessions.Count} 个历史会话。";
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
            RefreshLapPages(resetPage: true);
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

    private bool CanDeleteSession(HistorySessionItemViewModel? session)
    {
        return session is not null && session.CanDelete && !IsLoadingSessions && !IsLoadingLaps && !IsDeletingSession;
    }

    private HistorySessionItemViewModel? ResolveNextSelection(int deletedIndex)
    {
        if (HistorySessions.Count == 0)
        {
            return null;
        }

        return HistorySessions[Math.Clamp(deletedIndex, 0, HistorySessions.Count - 1)];
    }

    private void RefreshSessionPages(bool resetPage)
    {
        HistorySessionPages.SetItems(HistorySessions, resetPage);
    }

    private void RefreshLapPages(bool resetPage)
    {
        HistoryLapPages.SetItems(HistoryLaps, resetPage);
    }

    private void RaiseCommandStatesChanged()
    {
        _refreshSessionsCommand.RaiseCanExecuteChanged();
        _deleteSessionCommand.RaiseCanExecuteChanged();
    }
}
