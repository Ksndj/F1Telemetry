using System.IO;
using F1Telemetry.AI.Models;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;
using F1Telemetry.Storage.Repositories;
using F1Telemetry.Storage.Services;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies SQLite repositories persist and query recent session data.
/// </summary>
public sealed class StorageRepositoryTests
{
    /// <summary>
    /// Verifies that sessions can be created, ended, and queried back.
    /// </summary>
    [Fact]
    public async Task SessionRepository_CreatesAndEndsSession()
    {
        var rootPath = CreateRootPath();
        await using IDatabaseService databaseService = new SqliteDatabaseService(rootPath);
        await databaseService.InitializeAsync();
        ISessionRepository repository = new SessionRepository(databaseService);
        var startedAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z");
        var endedAt = DateTimeOffset.Parse("2026-04-18T10:30:00Z");
        var session = new StoredSession
        {
            Id = "session-a",
            SessionUid = "123456789",
            TrackId = 10,
            SessionType = 12,
            StartedAt = startedAt
        };

        await repository.CreateAsync(session);
        await repository.EndAsync(session.Id, endedAt);

        var recentSessions = await repository.GetRecentAsync(5);
        var storedSession = Assert.Single(recentSessions);
        Assert.Equal(session.Id, storedSession.Id);
        Assert.Equal(session.SessionUid, storedSession.SessionUid);
        Assert.Equal(endedAt, storedSession.EndedAt);
    }

    /// <summary>
    /// Verifies that recent sessions are ordered by start time descending and limited by count.
    /// </summary>
    [Fact]
    public async Task SessionRepository_GetRecentAsync_ReturnsNewestSessionsLimited()
    {
        var rootPath = CreateRootPath();
        await using IDatabaseService databaseService = new SqliteDatabaseService(rootPath);
        await databaseService.InitializeAsync();
        ISessionRepository repository = new SessionRepository(databaseService);

        await repository.CreateAsync(CreateSession("session-old", "uid-old", DateTimeOffset.Parse("2026-04-18T10:00:00Z")));
        await repository.CreateAsync(CreateSession("session-new", "uid-new", DateTimeOffset.Parse("2026-04-18T10:02:00Z")));
        await repository.CreateAsync(CreateSession("session-middle", "uid-middle", DateTimeOffset.Parse("2026-04-18T10:01:00Z")));

        var recentSessions = await repository.GetRecentAsync(2);

        Assert.Equal(new[] { "session-new", "session-middle" }, recentSessions.Select(session => session.Id));
    }

    /// <summary>
    /// Verifies deleting a session removes its associated history rows without affecting other sessions.
    /// </summary>
    [Fact]
    public async Task SessionRepository_DeleteAsync_RemovesAssociatedRowsOnlyForTargetSession()
    {
        var rootPath = CreateRootPath();
        await using IDatabaseService databaseService = new SqliteDatabaseService(rootPath);
        await databaseService.InitializeAsync();
        ISessionRepository sessionRepository = new SessionRepository(databaseService);
        ILapRepository lapRepository = new LapRepository(databaseService);
        IEventRepository eventRepository = new EventRepository(databaseService);
        IAIReportRepository aiReportRepository = new AIReportRepository(databaseService);

        await sessionRepository.CreateAsync(CreateSession("session-delete", "uid-delete", DateTimeOffset.Parse("2026-04-18T10:00:00Z")));
        await sessionRepository.CreateAsync(CreateSession("session-keep", "uid-keep", DateTimeOffset.Parse("2026-04-18T11:00:00Z")));
        await AddHistoryRowsAsync(lapRepository, eventRepository, aiReportRepository, "session-delete", 1);
        await AddHistoryRowsAsync(lapRepository, eventRepository, aiReportRepository, "session-keep", 2);

        var deleted = await sessionRepository.DeleteAsync("session-delete");

        Assert.True(deleted);
        Assert.DoesNotContain(await sessionRepository.GetRecentAsync(10), session => session.Id == "session-delete");
        Assert.Empty(await lapRepository.GetRecentAsync("session-delete", 10));
        Assert.Empty(await eventRepository.GetRecentAsync("session-delete", 10));
        Assert.Empty(await aiReportRepository.GetRecentAsync("session-delete", 10));
        Assert.Single(await lapRepository.GetRecentAsync("session-keep", 10));
        Assert.Single(await eventRepository.GetRecentAsync("session-keep", 10));
        Assert.Single(await aiReportRepository.GetRecentAsync("session-keep", 10));
    }

    /// <summary>
    /// Verifies deleting a missing session reports false.
    /// </summary>
    [Fact]
    public async Task SessionRepository_DeleteAsync_WhenSessionMissing_ReturnsFalse()
    {
        var rootPath = CreateRootPath();
        await using IDatabaseService databaseService = new SqliteDatabaseService(rootPath);
        await databaseService.InitializeAsync();
        ISessionRepository repository = new SessionRepository(databaseService);

        var deleted = await repository.DeleteAsync("missing-session");

        Assert.False(deleted);
    }

    /// <summary>
    /// Verifies that laps are inserted and queried in descending creation order.
    /// </summary>
    [Fact]
    public async Task LapRepository_InsertsAndReturnsRecentLaps()
    {
        var rootPath = CreateRootPath();
        await using IDatabaseService databaseService = new SqliteDatabaseService(rootPath);
        await databaseService.InitializeAsync();
        await SeedSessionAsync(databaseService, "session-lap");
        ILapRepository repository = new LapRepository(databaseService);

        await repository.AddAsync(
            "session-lap",
            new LapSummary
            {
                LapNumber = 4,
                LapTimeInMs = 90123,
                Sector1TimeInMs = 30123,
                Sector2TimeInMs = 30000,
                Sector3TimeInMs = 30000,
                AverageSpeedKph = 215.4,
                FuelUsedLitres = 1.42f,
                ErsUsed = 160_000f,
                IsValid = true,
                StartTyre = "Medium",
                EndTyre = "Medium",
                ClosedAt = DateTimeOffset.Parse("2026-04-18T10:01:00Z")
            });
        await repository.AddAsync(
            "session-lap",
            new LapSummary
            {
                LapNumber = 5,
                LapTimeInMs = 89900,
                Sector1TimeInMs = 30000,
                Sector2TimeInMs = 29900,
                Sector3TimeInMs = 30000,
                AverageSpeedKph = 217.2,
                FuelUsedLitres = 1.35f,
                ErsUsed = 150_000f,
                IsValid = false,
                StartTyre = "Medium",
                EndTyre = "Soft",
                ClosedAt = DateTimeOffset.Parse("2026-04-18T10:02:00Z")
            });

        var recentLaps = await repository.GetRecentAsync("session-lap", 10);

        Assert.Equal(2, recentLaps.Count);
        Assert.Equal(5, recentLaps[0].LapNumber);
        Assert.Equal(1.35f, recentLaps[0].FuelUsedLitres);
        Assert.Equal("Soft", recentLaps[0].EndTyre);
        Assert.Equal(4, recentLaps[1].LapNumber);
    }

    /// <summary>
    /// Verifies that race events are inserted and queried in descending creation order.
    /// </summary>
    [Fact]
    public async Task EventRepository_InsertsAndReturnsRecentEvents()
    {
        var rootPath = CreateRootPath();
        await using IDatabaseService databaseService = new SqliteDatabaseService(rootPath);
        await databaseService.InitializeAsync();
        await SeedSessionAsync(databaseService, "session-event");
        IEventRepository repository = new EventRepository(databaseService);

        await repository.AddAsync(
            "session-event",
            new RaceEvent
            {
                EventType = EventType.FrontCarPitted,
                Severity = EventSeverity.Information,
                LapNumber = 8,
                VehicleIdx = 12,
                DriverName = "Front Runner",
                Message = "前车已进站。",
                DedupKey = "event:front_pit:car12:lap8",
                PayloadJson = "{\"pit\":true}",
                Timestamp = DateTimeOffset.Parse("2026-04-18T10:05:00Z")
            });
        await repository.AddAsync(
            "session-event",
            new RaceEvent
            {
                EventType = EventType.LowFuel,
                Severity = EventSeverity.Warning,
                LapNumber = 9,
                VehicleIdx = 0,
                DriverName = "Player",
                Message = "燃油低于阈值。",
                DedupKey = "event:low_fuel:lap9",
                Timestamp = DateTimeOffset.Parse("2026-04-18T10:06:00Z")
            });

        var recentEvents = await repository.GetRecentAsync("session-event", 10);

        Assert.Equal(2, recentEvents.Count);
        Assert.Equal(EventType.LowFuel, recentEvents[0].EventType);
        Assert.Equal("Front Runner", recentEvents[1].DriverName);
    }

    /// <summary>
    /// Verifies that AI reports are inserted and queried back.
    /// </summary>
    [Fact]
    public async Task AiReportRepository_InsertsAndReturnsRecentReports()
    {
        var rootPath = CreateRootPath();
        await using IDatabaseService databaseService = new SqliteDatabaseService(rootPath);
        await databaseService.InitializeAsync();
        await SeedSessionAsync(databaseService, "session-ai");
        IAIReportRepository repository = new AIReportRepository(databaseService);

        await repository.AddAsync(
            "session-ai",
            10,
            new AIAnalysisResult
            {
                IsSuccess = true,
                Summary = "pace stable",
                TyreAdvice = "stay out",
                FuelAdvice = "target +0.2",
                TrafficAdvice = "watch undercut",
                TtsText = "pace is stable",
                ErrorMessage = "-"
            },
            DateTimeOffset.Parse("2026-04-18T10:10:00Z"));
        await repository.AddAsync(
            "session-ai",
            11,
            new AIAnalysisResult
            {
                IsSuccess = false,
                Summary = "-",
                TyreAdvice = "-",
                FuelAdvice = "-",
                TrafficAdvice = "-",
                TtsText = "-",
                ErrorMessage = "API timeout"
            },
            DateTimeOffset.Parse("2026-04-18T10:11:00Z"));

        var recentReports = await repository.GetRecentAsync("session-ai", 10);

        Assert.Equal(2, recentReports.Count);
        Assert.Equal(11, recentReports[0].LapNumber);
        Assert.False(recentReports[0].IsSuccess);
        Assert.Equal("pace stable", recentReports[1].Summary);
    }

    /// <summary>
    /// Verifies that settings values can be upserted and read back.
    /// </summary>
    [Fact]
    public async Task SettingsRepository_UpsertsAndReadsValues()
    {
        var rootPath = CreateRootPath();
        await using IDatabaseService databaseService = new SqliteDatabaseService(rootPath);
        await databaseService.InitializeAsync();
        ISettingsRepository repository = new SettingsRepository(databaseService);

        await repository.UpsertAsync("ai.model", "deepseek-chat");
        await repository.UpsertAsync("tts.enabled", "true");
        await repository.UpsertAsync("tts.enabled", "false");

        var aiModel = await repository.GetAsync("ai.model");
        var ttsEnabled = await repository.GetAsync("tts.enabled");

        Assert.Equal("deepseek-chat", aiModel);
        Assert.Equal("false", ttsEnabled);
    }

    private static string CreateRootPath()
    {
        return Path.Combine(Path.GetTempPath(), "F1TelemetryTests", Guid.NewGuid().ToString("N"));
    }

    private static async Task SeedSessionAsync(IDatabaseService databaseService, string sessionId)
    {
        ISessionRepository repository = new SessionRepository(databaseService);
        await repository.CreateAsync(CreateSession(sessionId, $"uid-{sessionId}", DateTimeOffset.Parse("2026-04-18T10:00:00Z")));
    }

    private static async Task AddHistoryRowsAsync(
        ILapRepository lapRepository,
        IEventRepository eventRepository,
        IAIReportRepository aiReportRepository,
        string sessionId,
        int lapNumber)
    {
        await lapRepository.AddAsync(
            sessionId,
            new LapSummary
            {
                LapNumber = lapNumber,
                LapTimeInMs = (uint)(90_000 + lapNumber),
                Sector1TimeInMs = 30_000,
                Sector2TimeInMs = 30_000,
                Sector3TimeInMs = 30_000,
                AverageSpeedKph = 216,
                FuelUsedLitres = 1.4f,
                ErsUsed = 150_000f,
                IsValid = true,
                StartTyre = "Medium",
                EndTyre = "Medium",
                ClosedAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(lapNumber)
            });
        await eventRepository.AddAsync(
            sessionId,
            new RaceEvent
            {
                EventType = EventType.LowFuel,
                Severity = EventSeverity.Warning,
                LapNumber = lapNumber,
                VehicleIdx = 0,
                DriverName = "Player",
                Message = $"Lap {lapNumber} fuel warning",
                DedupKey = $"fuel:{sessionId}:{lapNumber}",
                Timestamp = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(lapNumber)
            });
        await aiReportRepository.AddAsync(
            sessionId,
            lapNumber,
            new AIAnalysisResult
            {
                IsSuccess = true,
                Summary = $"Lap {lapNumber} summary",
                TyreAdvice = "stay out",
                FuelAdvice = "target",
                TrafficAdvice = "clear",
                TtsText = "pace stable",
                ErrorMessage = "-"
            },
            DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(lapNumber));
    }

    private static StoredSession CreateSession(string id, string sessionUid, DateTimeOffset startedAt)
    {
        return new StoredSession
        {
            Id = id,
            SessionUid = sessionUid,
            TrackId = 10,
            SessionType = 12,
            StartedAt = startedAt
        };
    }
}
