using F1Telemetry.Analytics.Laps;
using F1Telemetry.App.Services;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies persisted session history browser behavior.
/// </summary>
public sealed class HistorySessionBrowserViewModelTests
{
    /// <summary>
    /// Verifies an empty repository produces a stable empty state.
    /// </summary>
    [Fact]
    public async Task RefreshSessionsAsync_WhenEmpty_ShowsEmptyState()
    {
        var viewModel = new HistorySessionBrowserViewModel(
            new FakeSessionRepository(),
            new FakeLapRepository());

        var exception = await Record.ExceptionAsync(() => viewModel.RefreshSessionsAsync());

        Assert.Null(exception);
        Assert.Empty(viewModel.HistorySessions);
        Assert.Empty(viewModel.HistoryLaps);
        Assert.False(viewModel.IsLoadingSessions);
        Assert.Contains("暂无历史会话", viewModel.EmptyStateText, StringComparison.Ordinal);
        Assert.Contains("暂无历史会话", viewModel.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies session loading exposes a loading state while the repository is pending.
    /// </summary>
    [Fact]
    public async Task RefreshSessionsAsync_WhilePending_ShowsLoadingState()
    {
        var pendingSessions = new TaskCompletionSource<IReadOnlyList<StoredSession>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionRepository = new FakeSessionRepository
        {
            GetRecentHandler = (_, _) => pendingSessions.Task
        };
        var viewModel = new HistorySessionBrowserViewModel(
            sessionRepository,
            new FakeLapRepository());

        var refreshTask = viewModel.RefreshSessionsAsync();

        Assert.True(viewModel.IsLoadingSessions);
        Assert.Contains("正在加载历史会话", viewModel.StatusText, StringComparison.Ordinal);

        pendingSessions.SetResult(
        [
            CreateSession("session-a", DateTimeOffset.Parse("2026-04-18T10:00:00Z"))
        ]);

        await refreshTask;

        Assert.False(viewModel.IsLoadingSessions);
        Assert.Single(viewModel.HistorySessions);
    }

    /// <summary>
    /// Verifies selecting a session loads stored laps sorted by lap number ascending.
    /// </summary>
    [Fact]
    public async Task RefreshSessionsAsync_WithLaps_SortsHistoryLapsByLapNumber()
    {
        var session = CreateSession("session-a", DateTimeOffset.Parse("2026-04-18T10:00:00Z"));
        var sessionRepository = new FakeSessionRepository
        {
            Sessions = [session]
        };
        var lapRepository = new FakeLapRepository
        {
            LapsBySession =
            {
                ["session-a"] =
                [
                    CreateLap("session-a", 3, 3),
                    CreateLap("session-a", 1, 1),
                    CreateLap("session-a", 2, 2)
                ]
            }
        };
        var viewModel = new HistorySessionBrowserViewModel(sessionRepository, lapRepository);

        await viewModel.RefreshSessionsAsync();

        Assert.Equal("session-a", viewModel.SelectedSession?.SessionId);
        Assert.Equal(new[] { "Lap 1", "Lap 2", "Lap 3" }, viewModel.HistoryLaps.Select(lap => lap.LapText));
        Assert.Contains("已加载 3 条单圈记录", viewModel.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies history lap rows expose all three sector times and mark each fastest sector independently.
    /// </summary>
    [Fact]
    public async Task RefreshSessionsAsync_WithLaps_MarksFastestSectorTimes()
    {
        var session = CreateSession("session-a", DateTimeOffset.Parse("2026-04-18T10:00:00Z"));
        var sessionRepository = new FakeSessionRepository
        {
            Sessions = [session]
        };
        var lapRepository = new FakeLapRepository
        {
            LapsBySession =
            {
                ["session-a"] =
                [
                    CreateLap("session-a", 1, 1, lapTimeInMs: 90_000, sector1TimeInMs: 29_000, sector2TimeInMs: 31_000, sector3TimeInMs: 30_000),
                    CreateLap("session-a", 2, 2, lapTimeInMs: 88_500, sector1TimeInMs: 30_000, sector2TimeInMs: 30_000, sector3TimeInMs: null),
                    CreateLap("session-a", 3, 3, lapTimeInMs: 90_000, sector1TimeInMs: 31_000, sector2TimeInMs: 29_000, sector3TimeInMs: 30_000)
                ]
            }
        };
        var viewModel = new HistorySessionBrowserViewModel(sessionRepository, lapRepository);

        await viewModel.RefreshSessionsAsync();

        var rows = viewModel.HistoryLaps.ToArray();
        Assert.Equal("29.000s / 31.000s / 30.000s", rows[0].SectorsText);
        Assert.Equal("30.000s / 30.000s / 28.500s", rows[1].SectorsText);
        Assert.Equal("31.000s / 29.000s / 30.000s", rows[2].SectorsText);
        Assert.True(rows[0].IsFastestSector1);
        Assert.True(rows[1].IsFastestSector3);
        Assert.True(rows[2].IsFastestSector2);
        Assert.False(rows[0].IsFastestSector2);
        Assert.False(rows[1].IsFastestSector1);
        Assert.False(rows[2].IsFastestSector3);
    }

    /// <summary>
    /// Verifies session and lap lists expose paged projections.
    /// </summary>
    [Fact]
    public async Task RefreshSessionsAsync_WithManyRows_PaginatesSessionsAndLaps()
    {
        var sessions = Enumerable.Range(1, 5)
            .Select(index => CreateSession($"session-{index}", DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(index)))
            .ToArray();
        var sessionRepository = new FakeSessionRepository
        {
            Sessions = sessions.ToList()
        };
        var lapRepository = new FakeLapRepository();
        lapRepository.LapsBySession["session-1"] = Enumerable.Range(1, 5)
            .Select(index => CreateLap("session-1", index, index))
            .ToArray();
        var viewModel = new HistorySessionBrowserViewModel(sessionRepository, lapRepository);

        await viewModel.RefreshSessionsAsync();
        viewModel.HistorySessionPages.SetPageSize(2);
        viewModel.HistoryLapPages.SetPageSize(2);

        Assert.Equal(new[] { "session-1", "session-2" }, viewModel.HistorySessionPages.Items.Select(item => item.SessionId));
        Assert.Equal(new[] { "Lap 1", "Lap 2" }, viewModel.HistoryLapPages.Items.Select(item => item.LapText));

        viewModel.HistorySessionPages.NextPageCommand.Execute(null);
        viewModel.HistoryLapPages.NextPageCommand.Execute(null);

        Assert.Equal(new[] { "session-3", "session-4" }, viewModel.HistorySessionPages.Items.Select(item => item.SessionId));
        Assert.Equal(new[] { "Lap 3", "Lap 4" }, viewModel.HistoryLapPages.Items.Select(item => item.LapText));
    }

    /// <summary>
    /// Verifies confirmed deletion removes the selected session and loads the next one.
    /// </summary>
    [Fact]
    public async Task DeleteSessionAsync_WhenConfirmed_RemovesSessionAndSelectsNext()
    {
        var sessionRepository = new FakeSessionRepository
        {
            Sessions =
            [
                CreateSession("session-a", DateTimeOffset.Parse("2026-04-18T10:00:00Z")),
                CreateSession("session-b", DateTimeOffset.Parse("2026-04-18T09:00:00Z"))
            ]
        };
        var lapRepository = new FakeLapRepository
        {
            LapsBySession =
            {
                ["session-a"] = [CreateLap("session-a", 1, 1)],
                ["session-b"] = [CreateLap("session-b", 2, 2)]
            }
        };
        var confirmationService = new FakeDeletionConfirmationService { Confirmed = true };
        var viewModel = new HistorySessionBrowserViewModel(sessionRepository, lapRepository, confirmationService);

        await viewModel.RefreshSessionsAsync();
        await viewModel.DeleteSessionAsync(viewModel.SelectedSession);

        Assert.Equal(1, confirmationService.CallCount);
        Assert.Equal(new[] { "session-b" }, sessionRepository.Sessions.Select(session => session.Id));
        Assert.Equal("session-b", viewModel.SelectedSession?.SessionId);
        Assert.Equal(new[] { "Lap 2" }, viewModel.HistoryLaps.Select(lap => lap.LapText));
        Assert.Contains("历史会话已删除", viewModel.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies cancelled deletion leaves stored history intact.
    /// </summary>
    [Fact]
    public async Task DeleteSessionAsync_WhenConfirmationCancelled_DoesNotDelete()
    {
        var sessionRepository = new FakeSessionRepository
        {
            Sessions = [CreateSession("session-a", DateTimeOffset.Parse("2026-04-18T10:00:00Z"))]
        };
        var confirmationService = new FakeDeletionConfirmationService { Confirmed = false };
        var viewModel = new HistorySessionBrowserViewModel(sessionRepository, new FakeLapRepository(), confirmationService);

        await viewModel.RefreshSessionsAsync();
        await viewModel.DeleteSessionAsync(viewModel.SelectedSession);

        Assert.Single(sessionRepository.Sessions);
        Assert.Contains("删除已取消", viewModel.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies session repository failures are captured into status text.
    /// </summary>
    [Fact]
    public async Task RefreshSessionsAsync_WhenSessionRepositoryThrows_DoesNotThrow()
    {
        var sessionRepository = new FakeSessionRepository
        {
            GetRecentHandler = (_, _) => throw new InvalidOperationException("database unavailable")
        };
        var viewModel = new HistorySessionBrowserViewModel(
            sessionRepository,
            new FakeLapRepository());

        var exception = await Record.ExceptionAsync(() => viewModel.RefreshSessionsAsync());

        Assert.Null(exception);
        Assert.Contains("历史会话加载失败", viewModel.StatusText, StringComparison.Ordinal);
        Assert.False(viewModel.IsLoadingSessions);
    }

    /// <summary>
    /// Verifies lap repository failures are captured into status text.
    /// </summary>
    [Fact]
    public async Task RefreshSessionsAsync_WhenLapRepositoryThrows_DoesNotThrow()
    {
        var sessionRepository = new FakeSessionRepository
        {
            Sessions = [CreateSession("session-a", DateTimeOffset.Parse("2026-04-18T10:00:00Z"))]
        };
        var lapRepository = new FakeLapRepository
        {
            GetRecentHandler = (_, _, _) => throw new InvalidOperationException("lap query failed")
        };
        var viewModel = new HistorySessionBrowserViewModel(sessionRepository, lapRepository);

        var exception = await Record.ExceptionAsync(() => viewModel.RefreshSessionsAsync());

        Assert.Null(exception);
        Assert.Contains("单圈记录加载失败", viewModel.StatusText, StringComparison.Ordinal);
        Assert.False(viewModel.IsLoadingLaps);
    }

    private static StoredSession CreateSession(string id, DateTimeOffset startedAt)
    {
        return new StoredSession
        {
            Id = id,
            SessionUid = $"uid-{id}",
            TrackId = 10,
            SessionType = 12,
            StartedAt = startedAt,
            EndedAt = startedAt.AddMinutes(45)
        };
    }

    private static StoredLap CreateLap(
        string sessionId,
        int lapNumber,
        long id,
        int? lapTimeInMs = null,
        int? sector1TimeInMs = 30_000,
        int? sector2TimeInMs = 30_000,
        int? sector3TimeInMs = 30_000)
    {
        return new StoredLap
        {
            Id = id,
            SessionId = sessionId,
            LapNumber = lapNumber,
            LapTimeInMs = lapTimeInMs ?? 90_000 + lapNumber,
            Sector1TimeInMs = sector1TimeInMs,
            Sector2TimeInMs = sector2TimeInMs,
            Sector3TimeInMs = sector3TimeInMs,
            IsValid = true,
            AverageSpeedKph = 215,
            FuelUsedLitres = 1.2f,
            ErsUsed = 150_000f,
            StartTyre = "V17 / A19",
            EndTyre = "V17 / A19",
            CreatedAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(lapNumber)
        };
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

        public Func<string, int, CancellationToken, Task<IReadOnlyList<StoredLap>>>? GetRecentHandler { get; init; }

        public Task AddAsync(string sessionId, LapSummary lapSummary, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredLap>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
        {
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
