using F1Telemetry.Analytics.Laps;
using F1Telemetry.App.Services;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the historical multi-session comparison view model.
/// </summary>
public sealed class SessionComparisonViewModelTests
{
    /// <summary>
    /// Verifies an empty repository produces a stable empty state.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WhenNoHistorySessions_ShowsEmptyState()
    {
        var viewModel = CreateViewModel();

        var exception = await Record.ExceptionAsync(() => viewModel.RefreshAsync());

        Assert.Null(exception);
        Assert.False(viewModel.IsLoadingSessions);
        Assert.Empty(viewModel.TrackFilters);
        Assert.Contains("暂无历史会话", viewModel.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies loading state is exposed while sessions are loading.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WhileLoadingSessions_SetsAndClearsLoadingState()
    {
        var pendingSessions = new TaskCompletionSource<IReadOnlyList<StoredSession>>();
        var sessionRepository = new FakeSessionRepository
        {
            GetRecentHandler = (_, _) => pendingSessions.Task
        };
        var viewModel = CreateViewModel(sessionRepository);

        var refreshTask = viewModel.RefreshAsync();
        await WaitUntilAsync(() => viewModel.IsLoadingSessions);

        Assert.True(viewModel.IsLoadingSessions);
        Assert.Contains("正在加载历史会话", viewModel.StatusText, StringComparison.Ordinal);

        pendingSessions.SetResult(Array.Empty<StoredSession>());
        await refreshTask;

        Assert.False(viewModel.IsLoadingSessions);
    }

    /// <summary>
    /// Verifies the default track filter prefers the newest track with at least two sessions.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_DefaultsToNewestTrackWithAtLeastTwoSessions()
    {
        var sessionRepository = new FakeSessionRepository
        {
            Sessions =
            [
                CreateSession("single-latest", 2, DateTimeOffset.Parse("2026-04-20T10:00:00Z")),
                CreateSession("pair-newer", 10, DateTimeOffset.Parse("2026-04-19T10:00:00Z")),
                CreateSession("pair-older", 10, DateTimeOffset.Parse("2026-04-18T10:00:00Z"))
            ]
        };
        var lapRepository = new FakeLapRepository();
        lapRepository.LapsBySession["pair-newer"] = [CreateLap("pair-newer", 1, lapTimeInMs: 90_000)];
        lapRepository.LapsBySession["pair-older"] = [CreateLap("pair-older", 1, lapTimeInMs: 91_000)];
        var viewModel = CreateViewModel(sessionRepository, lapRepository);

        await viewModel.RefreshAsync();

        Assert.NotNull(viewModel.SelectedTrackFilter);
        Assert.Equal(10, viewModel.SelectedTrackFilter.TrackId);
        Assert.Equal(new[] { "pair-newer", "pair-older" }, viewModel.SelectedSessions.Select(session => session.SessionId));
        Assert.True(viewModel.LapTimeComparisonPanel.HasData);
    }

    /// <summary>
    /// Verifies selecting two sessions loads comparison laps for both sessions.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WithTwoSelectedSessions_LoadsLapsAndSummaryRows()
    {
        var sessionRepository = new FakeSessionRepository
        {
            Sessions =
            [
                CreateSession("session-a", 10, DateTimeOffset.Parse("2026-04-19T10:00:00Z")),
                CreateSession("session-b", 10, DateTimeOffset.Parse("2026-04-18T10:00:00Z"))
            ]
        };
        var lapRepository = new FakeLapRepository();
        lapRepository.LapsBySession["session-a"] = [CreateLap("session-a", 1, lapTimeInMs: 90_000, fuelUsedLitres: 1.1f, ersUsed: 1_200_000f)];
        lapRepository.LapsBySession["session-b"] = [CreateLap("session-b", 1, lapTimeInMs: 91_000, fuelUsedLitres: 1.2f, ersUsed: 1_100_000f)];
        var viewModel = CreateViewModel(sessionRepository, lapRepository);

        await viewModel.RefreshAsync();

        Assert.Equal(2, viewModel.SelectedSessions.Count);
        Assert.Equal(new[] { "session-a", "session-b" }, lapRepository.QueriedSessions);
        Assert.Equal(2, viewModel.SummaryRows.Count);
        Assert.True(viewModel.LapTimeComparisonPanel.HasData);
        Assert.True(viewModel.FuelComparisonPanel.HasData);
        Assert.True(viewModel.ErsComparisonPanel.HasData);
    }

    /// <summary>
    /// Verifies the candidate session list exposes a paged projection.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WithManyCandidateSessions_PaginatesCandidates()
    {
        var sessionRepository = new FakeSessionRepository
        {
            Sessions = Enumerable.Range(1, 5)
                .Select(index => CreateSession($"session-{index}", 10, DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(index)))
                .ToList()
        };
        var viewModel = CreateViewModel(sessionRepository);

        await viewModel.RefreshAsync();
        viewModel.CandidateSessionPages.SetPageSize(2);

        Assert.Equal(new[] { "session-5", "session-4" }, viewModel.CandidateSessionPages.Items.Select(item => item.SessionId));

        viewModel.CandidateSessionPages.NextPageCommand.Execute(null);

        Assert.Equal(new[] { "session-3", "session-2" }, viewModel.CandidateSessionPages.Items.Select(item => item.SessionId));
    }

    /// <summary>
    /// Verifies unordered repository laps are sorted by lap number before plotting.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WithUnorderedLaps_SortsByLapNumberForCharts()
    {
        var sessionRepository = new FakeSessionRepository
        {
            Sessions =
            [
                CreateSession("session-a", 10, DateTimeOffset.Parse("2026-04-19T10:00:00Z")),
                CreateSession("session-b", 10, DateTimeOffset.Parse("2026-04-18T10:00:00Z"))
            ]
        };
        var lapRepository = new FakeLapRepository();
        lapRepository.LapsBySession["session-a"] =
        [
            CreateLap("session-a", 3, id: 3, lapTimeInMs: 93_000),
            CreateLap("session-a", 1, id: 1, lapTimeInMs: 91_000),
            CreateLap("session-a", 2, id: 2, lapTimeInMs: 92_000)
        ];
        lapRepository.LapsBySession["session-b"] = [CreateLap("session-b", 1, lapTimeInMs: 94_000)];
        var viewModel = CreateViewModel(sessionRepository, lapRepository);

        await viewModel.RefreshAsync();

        Assert.Equal(new[] { 1d, 2d, 3d }, viewModel.LapTimeComparisonPanel.Series[0].Points.Select(point => point.X));
        Assert.Equal(new[] { 91d, 92d, 93d }, viewModel.LapTimeComparisonPanel.Series[0].Points.Select(point => point.Y));
    }

    /// <summary>
    /// Verifies selecting more than four sessions is rejected.
    /// </summary>
    [Fact]
    public async Task CandidateSession_WhenSelectingFifthSession_RevertsSelectionAndShowsLimit()
    {
        var sessionRepository = new FakeSessionRepository
        {
            Sessions =
            [
                CreateSession("session-5", 10, DateTimeOffset.Parse("2026-04-20T10:00:00Z")),
                CreateSession("session-4", 10, DateTimeOffset.Parse("2026-04-19T10:00:00Z")),
                CreateSession("session-3", 10, DateTimeOffset.Parse("2026-04-18T10:00:00Z")),
                CreateSession("session-2", 10, DateTimeOffset.Parse("2026-04-17T10:00:00Z")),
                CreateSession("session-1", 10, DateTimeOffset.Parse("2026-04-16T10:00:00Z"))
            ]
        };
        var lapRepository = new FakeLapRepository();
        var viewModel = CreateViewModel(sessionRepository, lapRepository);

        await viewModel.RefreshAsync();
        viewModel.CandidateSessions[2].IsSelected = true;
        viewModel.CandidateSessions[3].IsSelected = true;
        viewModel.CandidateSessions[4].IsSelected = true;

        Assert.False(viewModel.CandidateSessions[4].IsSelected);
        Assert.Equal(4, viewModel.SelectedSessions.Count);
        Assert.Contains("最多选择 4 个历史会话", viewModel.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies repository failures do not escape to UI callers.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WhenRepositoryThrows_DoesNotThrowAndShowsFailure()
    {
        var sessionRepository = new FakeSessionRepository
        {
            GetRecentHandler = (_, _) => throw new InvalidOperationException("database failed")
        };
        var viewModel = CreateViewModel(sessionRepository);

        var exception = await Record.ExceptionAsync(() => viewModel.RefreshAsync());

        Assert.Null(exception);
        Assert.False(viewModel.IsLoadingSessions);
        Assert.Contains("多会话对比加载失败", viewModel.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies tyre wear comparison remains explicitly unavailable.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WithoutStoredWheelWear_ShowsUnavailableTyreWearState()
    {
        var sessionRepository = new FakeSessionRepository
        {
            Sessions =
            [
                CreateSession("session-a", 10, DateTimeOffset.Parse("2026-04-19T10:00:00Z")),
                CreateSession("session-b", 10, DateTimeOffset.Parse("2026-04-18T10:00:00Z"))
            ]
        };
        var lapRepository = new FakeLapRepository();
        lapRepository.LapsBySession["session-a"] = [CreateLap("session-a", 1, lapTimeInMs: 90_000)];
        lapRepository.LapsBySession["session-b"] = [CreateLap("session-b", 1, lapTimeInMs: 91_000)];
        var viewModel = CreateViewModel(sessionRepository, lapRepository);

        await viewModel.RefreshAsync();

        Assert.False(viewModel.TyreWearComparisonPanel.HasData);
        Assert.Empty(viewModel.TyreWearComparisonPanel.Series);
        Assert.Contains("无法生成胎磨对比", viewModel.TyreWearComparisonPanel.EmptyStateText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies deleting a comparison session refreshes filters, candidates, selected sessions, and panels.
    /// </summary>
    [Fact]
    public async Task DeleteSessionAsync_WhenConfirmed_RefreshesComparisonState()
    {
        var sessionRepository = new FakeSessionRepository
        {
            Sessions =
            [
                CreateSession("session-a", 10, DateTimeOffset.Parse("2026-04-20T10:00:00Z")),
                CreateSession("session-b", 10, DateTimeOffset.Parse("2026-04-19T10:00:00Z")),
                CreateSession("session-c", 10, DateTimeOffset.Parse("2026-04-18T10:00:00Z"))
            ]
        };
        var lapRepository = new FakeLapRepository();
        lapRepository.LapsBySession["session-b"] = [CreateLap("session-b", 1, lapTimeInMs: 90_000)];
        lapRepository.LapsBySession["session-c"] = [CreateLap("session-c", 1, lapTimeInMs: 91_000)];
        var confirmationService = new FakeDeletionConfirmationService { Confirmed = true };
        var viewModel = CreateViewModel(sessionRepository, lapRepository, confirmationService);

        await viewModel.RefreshAsync();
        await viewModel.DeleteSessionAsync(viewModel.CandidateSessions[0]);

        Assert.Equal(1, confirmationService.CallCount);
        Assert.DoesNotContain(sessionRepository.Sessions, session => session.Id == "session-a");
        Assert.Equal(new[] { "session-b", "session-c" }, viewModel.CandidateSessions.Select(session => session.SessionId));
        Assert.Equal(new[] { "session-b", "session-c" }, viewModel.SelectedSessions.Select(session => session.SessionId));
        Assert.True(viewModel.LapTimeComparisonPanel.HasData);
    }

    private static SessionComparisonViewModel CreateViewModel(
        FakeSessionRepository? sessionRepository = null,
        FakeLapRepository? lapRepository = null,
        IHistorySessionDeletionConfirmationService? deletionConfirmationService = null)
    {
        return new SessionComparisonViewModel(
            sessionRepository ?? new FakeSessionRepository(),
            lapRepository ?? new FakeLapRepository(),
            deletionConfirmationService);
    }

    private static StoredSession CreateSession(string id, int? trackId, DateTimeOffset startedAt)
    {
        return new StoredSession
        {
            Id = id,
            SessionUid = $"uid-{id}",
            TrackId = trackId,
            SessionType = 15,
            StartedAt = startedAt,
            EndedAt = startedAt.AddMinutes(45)
        };
    }

    private static StoredLap CreateLap(
        string sessionId,
        int lapNumber,
        long? id = null,
        int? lapTimeInMs = null,
        float? fuelUsedLitres = null,
        float? ersUsed = null)
    {
        return new StoredLap
        {
            Id = id ?? lapNumber,
            SessionId = sessionId,
            LapNumber = lapNumber,
            LapTimeInMs = lapTimeInMs,
            IsValid = true,
            FuelUsedLitres = fuelUsedLitres,
            ErsUsed = ersUsed,
            CreatedAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(lapNumber)
        };
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(5);
        while (!predicate())
        {
            if (DateTime.UtcNow >= timeoutAt)
            {
                throw new TimeoutException("The expected session comparison state was not reached in time.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class FakeSessionRepository : ISessionRepository
    {
        public List<StoredSession> Sessions { get; init; } = [];

        public Func<int, CancellationToken, Task<IReadOnlyList<StoredSession>>>? GetRecentHandler { get; init; }

        public Task CreateAsync(StoredSession session, CancellationToken cancellationToken = default)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task EndAsync(string sessionId, DateTimeOffset endedAt, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredSession>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        {
            if (GetRecentHandler is not null)
            {
                return GetRecentHandler(count, cancellationToken);
            }

            return Task.FromResult<IReadOnlyList<StoredSession>>(Sessions.Take(count).ToArray());
        }

        public Task<bool> DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            var session = Sessions.FirstOrDefault(item => string.Equals(item.Id, sessionId, StringComparison.Ordinal));
            if (session is null)
            {
                return Task.FromResult(false);
            }

            Sessions.Remove(session);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeLapRepository : ILapRepository
    {
        public Dictionary<string, IReadOnlyList<StoredLap>> LapsBySession { get; } = new(StringComparer.Ordinal);

        public List<string> QueriedSessions { get; } = [];

        public Func<string, int, CancellationToken, Task<IReadOnlyList<StoredLap>>>? GetRecentHandler { get; init; }

        public Task AddAsync(string sessionId, LapSummary lapSummary, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredLap>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
        {
            QueriedSessions.Add(sessionId);
            if (GetRecentHandler is not null)
            {
                return GetRecentHandler(sessionId, count, cancellationToken);
            }

            return Task.FromResult(
                LapsBySession.TryGetValue(sessionId, out var laps)
                    ? laps.Take(count).ToArray()
                    : (IReadOnlyList<StoredLap>)Array.Empty<StoredLap>());
        }
    }

    private sealed class FakeDeletionConfirmationService : IHistorySessionDeletionConfirmationService
    {
        public bool Confirmed { get; init; }

        public int CallCount { get; private set; }

        public Task<bool> ConfirmDeleteAsync(
            HistorySessionDeletionConfirmationRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Confirmed);
        }
    }
}
