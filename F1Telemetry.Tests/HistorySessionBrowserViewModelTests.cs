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
    /// Verifies the raw sprint session type displays as sprint race.
    /// </summary>
    [Fact]
    public async Task RefreshSessionsAsync_WithSprintRawType_DisplaysSprintRace()
    {
        var sprintSession = CreateSession("session-sprint", DateTimeOffset.Parse("2026-05-17T16:59:00Z")) with
        {
            SessionType = 16,
            TotalLaps = 10,
            NumSessionsInWeekend = 7,
            WeekendStructure = [1, 10, 16, 5, 6, 7, 15]
        };
        var viewModel = new HistorySessionBrowserViewModel(
            new FakeSessionRepository { Sessions = [sprintSession] },
            new FakeLapRepository());

        await viewModel.RefreshSessionsAsync();

        Assert.Equal("冲刺赛", viewModel.SelectedSession?.SessionTypeText);
        Assert.Contains("冲刺赛", viewModel.SelectedSession?.SummaryText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies Miami 50% race history rows keep raw Race sessions as grand prix races.
    /// </summary>
    [Fact]
    public async Task RefreshSessionsAsync_WithMiamiHalfRace_DisplaysRace()
    {
        var raceSession = CreateSession("session-miami-race", DateTimeOffset.Parse("2026-05-17T16:59:00Z")) with
        {
            TrackId = 30,
            SessionType = 15,
            TotalLaps = 29,
            NumSessionsInWeekend = 7,
            WeekendStructure = [1, 10, 15, 5, 6, 7, 17]
        };
        var viewModel = new HistorySessionBrowserViewModel(
            new FakeSessionRepository { Sessions = [raceSession] },
            new FakeLapRepository());

        await viewModel.RefreshSessionsAsync();

        Assert.Equal("正赛", viewModel.SelectedSession?.SessionTypeText);
        Assert.Contains("迈阿密 · 正赛", viewModel.SelectedSession?.SummaryText, StringComparison.Ordinal);
        Assert.DoesNotContain("冲刺赛", viewModel.SelectedSession?.SummaryText, StringComparison.Ordinal);
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
    /// Verifies qualifying sessions with persisted laps populate the history lap list.
    /// </summary>
    [Fact]
    public async Task RefreshSessionsAsync_WithQualifyingLaps_ShowsHistoryLapRows()
    {
        var session = CreateSession("session-q", DateTimeOffset.Parse("2026-05-17T10:15:00Z")) with
        {
            SessionType = 5
        };
        var sessionRepository = new FakeSessionRepository
        {
            Sessions = [session]
        };
        var lapRepository = new FakeLapRepository
        {
            LapsBySession =
            {
                ["session-q"] =
                [
                    CreateLap("session-q", 1, 1),
                    CreateLap("session-q", 2, 2)
                ]
            }
        };
        var viewModel = new HistorySessionBrowserViewModel(sessionRepository, lapRepository);

        await viewModel.RefreshSessionsAsync();

        Assert.Equal("排位赛", viewModel.SelectedSession?.SessionTypeText);
        Assert.Equal(new[] { "Lap 1", "Lap 2" }, viewModel.HistoryLaps.Select(lap => lap.LapText));
        Assert.Contains("已加载 2 条单圈记录", viewModel.StatusText, StringComparison.Ordinal);
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
    /// Verifies sparse stored laps are enriched from one batch of session samples for display only.
    /// </summary>
    [Fact]
    public async Task RefreshSessionsAsync_WithSparseStoredMetrics_EnrichesRowsFromBatchSamples()
    {
        const string sessionId = "session-screenshot";
        var sessionRepository = new FakeSessionRepository
        {
            Sessions = [CreateSession(sessionId, DateTimeOffset.Parse("2026-05-17T10:00:00Z"))]
        };
        var lapRepository = new FakeLapRepository();
        lapRepository.LapsBySession[sessionId] = Enumerable.Range(1, 5)
            .Select(index => CreateSparseLap(sessionId, index, index))
            .ToArray();
        lapRepository.LapsBySession[sessionId] = lapRepository.LapsBySession[sessionId]
            .Select(lap => lap.LapNumber == 4
                ? lap with
                {
                    AverageSpeedKph = 209,
                    FuelUsedLitres = 1.66f,
                    ErsUsed = 0f,
                    StartTyre = "V17 / A20",
                    EndTyre = "V17 / A20"
                }
                : lap)
            .ToArray();
        var sampleRepository = new FakeLapSampleRepository();
        sampleRepository.SamplesBySession[sessionId] = Enumerable.Range(1, 5)
            .SelectMany(index => CreateLapSamples(sessionId, index))
            .ToArray();
        var viewModel = new HistorySessionBrowserViewModel(
            sessionRepository,
            lapRepository,
            lapSampleRepository: sampleRepository);

        await viewModel.RefreshSessionsAsync();

        Assert.Equal(1, sampleRepository.GetForSessionCallCount);
        Assert.Equal(0, lapRepository.AddCallCount);
        Assert.Equal(5, viewModel.HistoryLaps.Count);
        Assert.All(viewModel.HistoryLaps, row =>
        {
            Assert.NotEqual("-", row.AverageSpeedText);
            Assert.NotEqual("-", row.FuelUsedLitresText);
            Assert.NotEqual("-", row.ErsUsedText);
            Assert.NotEqual("-", row.TyreWearDeltaText);
            Assert.NotEqual("-", row.TyreWindowText);
            Assert.NotEqual("-", row.PitWindowText);
        });
        Assert.Equal("0.00 MJ", viewModel.HistoryLaps.Single(row => row.LapNumber == 4).ErsUsedText);
    }

    /// <summary>
    /// Verifies insufficient or unstable samples stay hidden instead of manufacturing history values.
    /// </summary>
    [Fact]
    public async Task RefreshSessionsAsync_WithInsufficientSamples_KeepsDerivedFieldsMissing()
    {
        const string sessionId = "session-sparse-samples";
        var sessionRepository = new FakeSessionRepository
        {
            Sessions = [CreateSession(sessionId, DateTimeOffset.Parse("2026-05-17T10:00:00Z"))]
        };
        var lapRepository = new FakeLapRepository
        {
            LapsBySession =
            {
                [sessionId] = [CreateSparseLap(sessionId, 1, 1)]
            }
        };
        var sampleRepository = new FakeLapSampleRepository
        {
            SamplesBySession =
            {
                [sessionId] = [CreateSample(sessionId, lapNumber: 1, sampleIndex: 0, speedKph: 200, fuel: 20f, ers: 1_000_000f, tyreWear: 10f)]
            }
        };
        var viewModel = new HistorySessionBrowserViewModel(
            sessionRepository,
            lapRepository,
            lapSampleRepository: sampleRepository);

        await viewModel.RefreshSessionsAsync();

        var row = Assert.Single(viewModel.HistoryLaps);
        Assert.Equal("-", row.AverageSpeedText);
        Assert.Equal("-", row.FuelUsedLitresText);
        Assert.Equal("-", row.ErsUsedText);
        Assert.Equal("-", row.TyreWearDeltaText);
        Assert.Equal("-", row.TyreWindowText);
        Assert.Equal("-", row.PitWindowText);
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

    private static StoredLap CreateSparseLap(string sessionId, int lapNumber, long id)
    {
        return CreateLap(sessionId, lapNumber, id) with
        {
            AverageSpeedKph = null,
            FuelUsedLitres = null,
            ErsUsed = null,
            StartTyre = "-",
            EndTyre = "-"
        };
    }

    private static IReadOnlyList<StoredLapSample> CreateLapSamples(string sessionId, int lapNumber)
    {
        var startFuel = 22f - lapNumber;
        var startErs = lapNumber == 4 ? 1_000_000f : 1_700_000f;
        return
        [
            CreateSample(sessionId, lapNumber, 0, speedKph: 196 + lapNumber, fuel: startFuel, ers: startErs, tyreWear: 8f + lapNumber, visualTyreCompound: 17, actualTyreCompound: 20),
            CreateSample(sessionId, lapNumber, 1, speedKph: 202 + lapNumber, fuel: startFuel - 0.42f, ers: lapNumber == 4 ? startErs : startErs - 450_000f, tyreWear: 9.2f + lapNumber, visualTyreCompound: 17, actualTyreCompound: 20)
        ];
    }

    private static StoredLapSample CreateSample(
        string sessionId,
        int lapNumber,
        int sampleIndex,
        double speedKph,
        float fuel,
        float ers,
        float tyreWear,
        int pitStatus = 0,
        int visualTyreCompound = 17,
        int actualTyreCompound = 20)
    {
        return new StoredLapSample
        {
            SessionId = sessionId,
            LapNumber = lapNumber,
            SampleIndex = sampleIndex,
            SampledAt = DateTimeOffset.Parse("2026-05-17T10:00:00Z").AddSeconds((lapNumber * 100) + sampleIndex),
            FrameIdentifier = (lapNumber * 1000) + sampleIndex,
            SpeedKph = speedKph,
            FuelRemainingLitres = fuel,
            ErsStoreEnergy = ers,
            TyreWear = tyreWear,
            PitStatus = pitStatus,
            IsValid = true,
            VisualTyreCompound = visualTyreCompound,
            ActualTyreCompound = actualTyreCompound,
            CreatedAt = DateTimeOffset.Parse("2026-05-17T10:00:00Z").AddSeconds((lapNumber * 100) + sampleIndex)
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

        public int AddCallCount { get; private set; }

        public Task AddAsync(string sessionId, LapSummary lapSummary, CancellationToken cancellationToken = default)
        {
            AddCallCount++;
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

    private sealed class FakeLapSampleRepository : ILapSampleRepository
    {
        public Dictionary<string, IReadOnlyList<StoredLapSample>> SamplesBySession { get; init; } = new(StringComparer.Ordinal);

        public int GetForSessionCallCount { get; private set; }

        public Task AddAsync(StoredLapSample sample, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<StoredLapSample> samples, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredLapSample>> GetForLapAsync(
            string sessionId,
            int lapNumber,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredLapSample>>(
                SamplesBySession.TryGetValue(sessionId, out var samples)
                    ? samples.Where(sample => sample.LapNumber == lapNumber).ToArray()
                    : Array.Empty<StoredLapSample>());
        }

        public Task<IReadOnlyList<StoredLapSample>> GetForSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            GetForSessionCallCount++;
            return Task.FromResult<IReadOnlyList<StoredLapSample>>(
                SamplesBySession.TryGetValue(sessionId, out var samples)
                    ? samples.ToArray()
                    : Array.Empty<StoredLapSample>());
        }

        public Task<IReadOnlyList<StoredLapTyreWearTrendPoint>> GetTyreWearTrendAsync(
            string sessionId,
            int count,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredLapTyreWearTrendPoint>>(Array.Empty<StoredLapTyreWearTrendPoint>());
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
