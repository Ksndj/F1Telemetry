using F1Telemetry.AI.Models;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the historical post-race review view model.
/// </summary>
public sealed class PostRaceReviewViewModelTests
{
    /// <summary>
    /// Verifies an empty history browser produces a stable empty state.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WhenNoHistorySessions_ShowsEmptyState()
    {
        var viewModel = CreateViewModel();

        var exception = await Record.ExceptionAsync(() => viewModel.RefreshAsync());

        Assert.Null(exception);
        Assert.False(viewModel.IsLoading);
        Assert.Empty(viewModel.SummaryMetricRows);
        Assert.Contains("暂无历史会话", viewModel.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies selecting a history session loads all review repositories.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WithSelectedSession_LoadsLapsEventsAndReports()
    {
        var sessionRepository = new FakeSessionRepository
        {
            Sessions = [CreateSession("session-a")]
        };
        var lapRepository = new FakeLapRepository
        {
            LapsBySession =
            {
                ["session-a"] = [CreateLap("session-a", 1)]
            }
        };
        var eventRepository = new FakeEventRepository
        {
            EventsBySession =
            {
                ["session-a"] = [CreateEvent("session-a", 1, 1)]
            }
        };
        var aiReportRepository = new FakeAiReportRepository
        {
            ReportsBySession =
            {
                ["session-a"] = [CreateReport("session-a", 1, 1)]
            }
        };
        var viewModel = CreateViewModel(sessionRepository, lapRepository, eventRepository, aiReportRepository);

        await viewModel.RefreshAsync();

        Assert.Equal("session-a", viewModel.SelectedSession?.SessionId);
        Assert.NotEmpty(viewModel.SummaryMetricRows);
        Assert.Single(viewModel.EventTimelineRows);
        Assert.Single(viewModel.AiReportRows);
        Assert.True(viewModel.LapTimeTrendPanel.HasData);
        Assert.Contains("1 圈、1 事件、1 份 AI 报告", viewModel.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies repository return order is normalized for review display.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WithUnorderedRepositoryRows_SortsReviewRows()
    {
        var sessionRepository = new FakeSessionRepository
        {
            Sessions = [CreateSession("session-a")]
        };
        var lapRepository = new FakeLapRepository
        {
            LapsBySession =
            {
                ["session-a"] =
                [
                    CreateLap("session-a", 3),
                    CreateLap("session-a", 1),
                    CreateLap("session-a", 2)
                ]
            }
        };
        var eventRepository = new FakeEventRepository
        {
            EventsBySession =
            {
                ["session-a"] =
                [
                    CreateEvent("session-a", null, 1),
                    CreateEvent("session-a", 2, 2),
                    CreateEvent("session-a", 1, 3)
                ]
            }
        };
        var aiReportRepository = new FakeAiReportRepository
        {
            ReportsBySession =
            {
                ["session-a"] =
                [
                    CreateReport("session-a", 3, 3),
                    CreateReport("session-a", 1, 1),
                    CreateReport("session-a", 2, 2)
                ]
            }
        };
        var viewModel = CreateViewModel(sessionRepository, lapRepository, eventRepository, aiReportRepository);

        await viewModel.RefreshAsync();

        Assert.Equal(new[] { 1d, 2d, 3d }, viewModel.LapTimeTrendPanel.Series[0].Points.Select(point => point.X));
        Assert.Equal(new[] { "Lap 1", "Lap 2", "未知圈" }, viewModel.EventTimelineRows.Select(row => row.LapText));
        Assert.Equal(new[] { "Lap 1", "Lap 2", "Lap 3" }, viewModel.AiReportRows.Select(row => row.LapText));
    }

    /// <summary>
    /// Verifies new race-advice event types have Chinese timeline display text.
    /// </summary>
    [Fact]
    public void EventRow_FromM6AdviceEvent_UsesChineseEventTypeText()
    {
        var row = PostRaceReviewEventRowViewModel.FromStoredEvent(new StoredEvent
        {
            Id = 42,
            SessionId = "session-a",
            EventType = EventType.FrontOldTyreRisk,
            Severity = EventSeverity.Warning,
            LapNumber = 18,
            VehicleIdx = 2,
            DriverName = "Front Runner",
            Message = "前车旧胎风险，适合持续施压",
            CreatedAt = new DateTimeOffset(2026, 5, 10, 10, 0, 0, TimeSpan.Zero)
        });

        Assert.Equal("前车旧胎风险", row.EventTypeText);
        Assert.Equal("前车旧胎风险", row.Category);
    }

    /// <summary>
    /// Verifies repository failures do not escape to UI callers.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WhenRepositoryThrows_DoesNotThrowAndShowsFailure()
    {
        var sessionRepository = new FakeSessionRepository
        {
            Sessions = [CreateSession("session-a")]
        };
        var lapRepository = new FakeLapRepository
        {
            GetRecentHandler = (_, _, _) => throw new InvalidOperationException("lap database failed")
        };
        var viewModel = CreateViewModel(sessionRepository, lapRepository);

        var exception = await Record.ExceptionAsync(() => viewModel.RefreshAsync());

        Assert.Null(exception);
        Assert.False(viewModel.IsLoading);
        Assert.Contains("赛后复盘加载失败", viewModel.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies historical tyre wear is explicitly unavailable instead of fabricated.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WithoutStoredWheelWear_ShowsUnavailableTyreWearState()
    {
        var sessionRepository = new FakeSessionRepository
        {
            Sessions = [CreateSession("session-a")]
        };
        var lapRepository = new FakeLapRepository
        {
            LapsBySession =
            {
                ["session-a"] = [CreateLap("session-a", 1)]
            }
        };
        var viewModel = CreateViewModel(sessionRepository, lapRepository);

        await viewModel.RefreshAsync();

        Assert.False(viewModel.TyreWearTrendPanel.HasData);
        Assert.Contains("未保存四轮胎磨数据", viewModel.TyreWearStatusText, StringComparison.Ordinal);
    }

    private static PostRaceReviewViewModel CreateViewModel(
        FakeSessionRepository? sessionRepository = null,
        FakeLapRepository? lapRepository = null,
        FakeEventRepository? eventRepository = null,
        FakeAiReportRepository? aiReportRepository = null)
    {
        sessionRepository ??= new FakeSessionRepository();
        lapRepository ??= new FakeLapRepository();
        var historyBrowser = new HistorySessionBrowserViewModel(sessionRepository, lapRepository);
        return new PostRaceReviewViewModel(
            historyBrowser,
            lapRepository,
            eventRepository ?? new FakeEventRepository(),
            aiReportRepository ?? new FakeAiReportRepository());
    }

    private static StoredSession CreateSession(string id)
    {
        return new StoredSession
        {
            Id = id,
            SessionUid = $"uid-{id}",
            TrackId = 10,
            SessionType = 15,
            StartedAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z"),
            EndedAt = DateTimeOffset.Parse("2026-04-18T10:45:00Z")
        };
    }

    private static StoredLap CreateLap(string sessionId, int lapNumber)
    {
        return new StoredLap
        {
            Id = lapNumber,
            SessionId = sessionId,
            LapNumber = lapNumber,
            LapTimeInMs = 90_000 + lapNumber,
            Sector1TimeInMs = 30_000,
            Sector2TimeInMs = 30_000,
            Sector3TimeInMs = 30_000,
            IsValid = true,
            AverageSpeedKph = 215,
            FuelUsedLitres = 1.2f,
            ErsUsed = 150_000f,
            StartTyre = "V17 / A19",
            EndTyre = "V17 / A19",
            CreatedAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(lapNumber)
        };
    }

    private static StoredEvent CreateEvent(string sessionId, int? lapNumber, long id)
    {
        return new StoredEvent
        {
            Id = id,
            SessionId = sessionId,
            EventType = EventType.LowFuel,
            Severity = EventSeverity.Warning,
            LapNumber = lapNumber,
            VehicleIdx = 0,
            DriverName = "Player",
            Message = lapNumber is null ? "无圈号事件" : $"Lap {lapNumber} event",
            CreatedAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(id)
        };
    }

    private static StoredAiReport CreateReport(string sessionId, int lapNumber, long id)
    {
        return new StoredAiReport
        {
            Id = id,
            SessionId = sessionId,
            LapNumber = lapNumber,
            Summary = $"Lap {lapNumber} summary",
            TyreAdvice = "stay out",
            FuelAdvice = "target",
            TrafficAdvice = "clear",
            TtsText = "pace stable",
            IsSuccess = true,
            ErrorMessage = "-",
            CreatedAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(id)
        };
    }

    private sealed class FakeSessionRepository : ISessionRepository
    {
        public List<StoredSession> Sessions { get; init; } = [];

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
            return Task.FromResult<IReadOnlyList<StoredSession>>(Sessions.Take(count).ToArray());
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

    private sealed class FakeEventRepository : IEventRepository
    {
        public Dictionary<string, IReadOnlyList<StoredEvent>> EventsBySession { get; } = new(StringComparer.Ordinal);

        public Task AddAsync(string sessionId, RaceEvent raceEvent, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredEvent>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                EventsBySession.TryGetValue(sessionId, out var events)
                    ? events.Take(count).ToArray()
                    : (IReadOnlyList<StoredEvent>)Array.Empty<StoredEvent>());
        }
    }

    private sealed class FakeAiReportRepository : IAIReportRepository
    {
        public Dictionary<string, IReadOnlyList<StoredAiReport>> ReportsBySession { get; } = new(StringComparer.Ordinal);

        public Task AddAsync(
            string sessionId,
            int lapNumber,
            AIAnalysisResult analysisResult,
            DateTimeOffset? createdAt = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredAiReport>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                ReportsBySession.TryGetValue(sessionId, out var reports)
                    ? reports.Take(count).ToArray()
                    : (IReadOnlyList<StoredAiReport>)Array.Empty<StoredAiReport>());
        }
    }
}
