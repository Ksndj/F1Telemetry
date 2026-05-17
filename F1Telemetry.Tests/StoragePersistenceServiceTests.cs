using System.Net;
using F1Telemetry.AI.Models;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Core.Models;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;
using F1Telemetry.Storage.Services;
using F1Telemetry.Udp.Packets;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies that persistence faults do not escape the background storage pipeline.
/// </summary>
public sealed class StoragePersistenceServiceTests
{
    /// <summary>
    /// Verifies that a repository exception is converted into a storage log instead of breaking the caller.
    /// </summary>
    [Fact]
    public async Task EnqueueLapSummary_RepositoryThrows_EmitsLogWithoutThrowing()
    {
        var sessionRepository = new RecordingSessionRepository();
        var service = new StoragePersistenceService(
            sessionRepository,
            new ThrowingLapRepository(),
            new RecordingEventRepository(),
            new RecordingAiReportRepository());
        var logReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.LogEmitted += (_, message) =>
        {
            if (message.Contains("Lap", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("圈", StringComparison.Ordinal))
            {
                logReceived.TrySetResult(message);
            }
        };

        service.ObserveParsedPacket(CreateSessionParsedPacket(42UL, trackId: 10, sessionType: 12));
        service.EnqueueLapSummary(
            new LapSummary
            {
                LapNumber = 5,
                FuelUsedLitres = 1.2f,
                StartTyre = "Medium",
                EndTyre = "Medium",
                ClosedAt = DateTimeOffset.Parse("2026-04-18T10:05:00Z")
            });

        await service.CompleteActiveSessionAsync();

        var loggedMessage = await logReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("持久化", loggedMessage, StringComparison.Ordinal);
        Assert.Single(sessionRepository.CreatedSessions);

        await service.DisposeAsync();
    }

    /// <summary>
    /// Verifies that session lifecycle commands remain durable even when the normal write queue overflows.
    /// </summary>
    [Fact]
    public async Task QueueOverflow_PreservesSessionLifecycleCommandsAndDropsBufferedNormalWrites()
    {
        var sessionRepository = new RecordingSessionRepository();
        var lapRepository = new RecordingLapRepository();
        var releaseInitialization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new StoragePersistenceService(
            sessionRepository,
            lapRepository,
            new RecordingEventRepository(),
            new RecordingAiReportRepository(),
            _ => releaseInitialization.Task,
            maxBufferedCommands: 1,
            maxCriticalCommands: 8);
        var emittedLogs = new List<string>();
        service.LogEmitted += (_, message) => emittedLogs.Add(message);

        service.ObserveParsedPacket(CreateSessionParsedPacket(42UL, trackId: 10, sessionType: 12));
        service.EnqueueLapSummary(new LapSummary
        {
            LapNumber = 1,
            FuelUsedLitres = 1.1f,
            StartTyre = "Medium",
            EndTyre = "Medium",
            ClosedAt = DateTimeOffset.Parse("2026-04-18T10:01:00Z")
        });
        service.EnqueueLapSummary(new LapSummary
        {
            LapNumber = 2,
            FuelUsedLitres = 1.2f,
            StartTyre = "Medium",
            EndTyre = "Medium",
            ClosedAt = DateTimeOffset.Parse("2026-04-18T10:02:00Z")
        });
        service.ObserveParsedPacket(CreateSessionParsedPacket(43UL, trackId: 11, sessionType: 12));

        releaseInitialization.TrySetResult();

        await WaitUntilAsync(() => sessionRepository.CreatedSessions.Count == 2);
        await WaitUntilAsync(() => lapRepository.StoredLaps.Count == 1);
        await service.CompleteActiveSessionAsync();
        await service.DisposeAsync();

        Assert.Equal(2, sessionRepository.CreatedSessions.Count);
        Assert.Single(lapRepository.StoredLaps);
        Assert.Contains(emittedLogs, message => message.Contains("队列已满", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies completed-lap samples are persisted in batches for the active session.
    /// </summary>
    [Fact]
    public async Task EnqueueLapSamples_WithActiveSession_PersistsStoredSamples()
    {
        var sessionRepository = new RecordingSessionRepository();
        var sampleRepository = new RecordingLapSampleRepository();
        var service = new StoragePersistenceService(
            sessionRepository,
            new RecordingLapRepository(),
            new RecordingEventRepository(),
            new RecordingAiReportRepository(),
            sampleRepository);

        service.ObserveParsedPacket(CreateSessionParsedPacket(42UL, trackId: 0, sessionType: 15));
        service.EnqueueLapSamples(
            3,
            [
                CreateLapSample(3, 0, 120f, 5_000),
                CreateLapSample(3, 1, 220f, 8_000)
            ]);

        await WaitUntilAsync(() => sampleRepository.StoredSamples.Count == 2);
        await service.DisposeAsync();

        var sessionId = Assert.Single(sessionRepository.CreatedSessions).Id;
        Assert.All(sampleRepository.StoredSamples, sample => Assert.Equal(sessionId, sample.SessionId));
        Assert.Equal(new[] { 0, 1 }, sampleRepository.StoredSamples.Select(sample => sample.SampleIndex));
        Assert.All(sampleRepository.StoredSamples, sample => Assert.Equal(3, sample.LapNumber));
    }

    /// <summary>
    /// Verifies sample writes queued before completion are drained before the session is closed.
    /// </summary>
    [Fact]
    public async Task CompleteActiveSessionAsync_WithPendingLapSamples_DrainsSamplesBeforeClosing()
    {
        var sessionRepository = new RecordingSessionRepository();
        var sampleRepository = new RecordingLapSampleRepository();
        var releaseInitialization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new StoragePersistenceService(
            sessionRepository,
            new RecordingLapRepository(),
            new RecordingEventRepository(),
            new RecordingAiReportRepository(),
            sampleRepository,
            _ => releaseInitialization.Task);

        service.ObserveParsedPacket(CreateSessionParsedPacket(42UL, trackId: 0, sessionType: 15));
        service.EnqueueLapSamples(4, [CreateLapSample(4, 0, 100f, 2_000)]);
        var completeTask = service.CompleteActiveSessionAsync();

        releaseInitialization.TrySetResult();
        await completeTask;
        await service.DisposeAsync();

        var sessionId = Assert.Single(sessionRepository.CreatedSessions).Id;
        var storedSample = Assert.Single(sampleRepository.StoredSamples);
        Assert.Equal(sessionId, storedSample.SessionId);
        Assert.Equal(4, storedSample.LapNumber);
    }

    /// <summary>
    /// Verifies all buffered write types queued before stop are persisted before completion.
    /// </summary>
    [Fact]
    public async Task CompleteActiveSessionAsync_WithPendingBufferedWrites_DrainsAllWriteTypes()
    {
        var sessionRepository = new RecordingSessionRepository();
        var lapRepository = new RecordingLapRepository();
        var eventRepository = new RecordingEventRepository();
        var aiReportRepository = new RecordingAiReportRepository();
        var releaseInitialization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new StoragePersistenceService(
            sessionRepository,
            lapRepository,
            eventRepository,
            aiReportRepository,
            _ => releaseInitialization.Task);

        service.ObserveParsedPacket(CreateSessionParsedPacket(42UL, trackId: 0, sessionType: 15));
        service.EnqueueLapSummary(CreateLapSummary(5));
        service.EnqueueRaceEvent(CreateRaceEvent(5));
        service.EnqueueAiReport(5, new AIAnalysisResult { IsSuccess = true, Summary = "ok" });
        var completeTask = service.CompleteActiveSessionAsync();

        releaseInitialization.TrySetResult();
        await completeTask;
        await service.DisposeAsync();

        var sessionId = Assert.Single(sessionRepository.CreatedSessions).Id;
        Assert.Equal(sessionId, Assert.Single(lapRepository.StoredLaps).SessionId);
        Assert.Equal(sessionId, Assert.Single(eventRepository.StoredEvents).SessionId);
        Assert.Equal(sessionId, Assert.Single(aiReportRepository.StoredReports).SessionId);
    }

    /// <summary>
    /// Verifies a later session packet cannot steal writes that were queued for the previous session.
    /// </summary>
    [Fact]
    public async Task ObserveParsedPacket_WithPendingOldSessionWrites_KeepsBufferedWritesOnOriginalSession()
    {
        var sessionRepository = new RecordingSessionRepository();
        var lapRepository = new RecordingLapRepository();
        var releaseInitialization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new StoragePersistenceService(
            sessionRepository,
            lapRepository,
            new RecordingEventRepository(),
            new RecordingAiReportRepository(),
            _ => releaseInitialization.Task);

        service.ObserveParsedPacket(CreateSessionParsedPacket(42UL, trackId: 0, sessionType: 15));
        service.EnqueueLapSummary(CreateLapSummary(1));
        service.ObserveParsedPacket(CreateSessionParsedPacket(43UL, trackId: 2, sessionType: 15));
        service.EnqueueLapSummary(CreateLapSummary(2));
        var completeTask = service.CompleteActiveSessionAsync();

        releaseInitialization.TrySetResult();
        await completeTask;
        await service.DisposeAsync();

        Assert.Equal(2, sessionRepository.CreatedSessions.Count);
        Assert.Equal(
            [sessionRepository.CreatedSessions[0].Id, sessionRepository.CreatedSessions[1].Id],
            lapRepository.StoredLaps.Select(lap => lap.SessionId).ToArray());
        Assert.Equal([1, 2], lapRepository.StoredLaps.Select(lap => lap.LapSummary.LapNumber).ToArray());
    }

    /// <summary>
    /// Verifies repeated completion and disposal remain idempotent after buffered writes drain.
    /// </summary>
    [Fact]
    public async Task CompleteAndDisposeRepeatedly_WithPendingWrites_DrainsOnceWithoutThrowing()
    {
        var lapRepository = new RecordingLapRepository();
        var service = new StoragePersistenceService(
            new RecordingSessionRepository(),
            lapRepository,
            new RecordingEventRepository(),
            new RecordingAiReportRepository());

        service.ObserveParsedPacket(CreateSessionParsedPacket(42UL, trackId: 0, sessionType: 15));
        service.EnqueueLapSummary(CreateLapSummary(6));

        await service.CompleteActiveSessionAsync();
        await service.CompleteActiveSessionAsync();
        await service.DisposeAsync();
        await service.DisposeAsync();

        Assert.Single(lapRepository.StoredLaps);
    }

    private static ParsedPacket CreateSessionParsedPacket(ulong sessionUid, sbyte trackId, byte sessionType)
    {
        var header = new PacketHeader(
            PacketFormat: 2025,
            GameYear: 25,
            GameMajorVersion: 1,
            GameMinorVersion: 0,
            PacketVersion: 1,
            RawPacketId: (byte)PacketId.Session,
            SessionUid: sessionUid,
            SessionTime: 0.5f,
            FrameIdentifier: 1,
            OverallFrameIdentifier: 1,
            PlayerCarIndex: 0,
            SecondaryPlayerCarIndex: 255);
        var packet = new SessionPacket(
            Weather: 0,
            TrackTemperature: 30,
            AirTemperature: 25,
            TotalLaps: 20,
            TrackLength: 5300,
            SessionType: sessionType,
            TrackId: trackId,
            Formula: 0,
            SessionTimeLeft: 0,
            SessionDuration: 0,
            PitSpeedLimit: 80,
            GamePaused: false,
            IsSpectating: false,
            SpectatorCarIndex: 0,
            SliProNativeSupport: false,
            NumMarshalZones: 0,
            MarshalZones: [],
            SafetyCarStatus: 0,
            NetworkGame: false,
            NumWeatherForecastSamples: 0,
            WeatherForecastSamples: [],
            ForecastAccuracy: 0,
            AiDifficulty: 0,
            SeasonLinkIdentifier: 1,
            WeekendLinkIdentifier: 1,
            SessionLinkIdentifier: 1,
            PitStopWindowIdealLap: 0,
            PitStopWindowLatestLap: 0,
            PitStopRejoinPosition: 0,
            SteeringAssist: false,
            BrakingAssist: 0,
            GearboxAssist: 0,
            PitAssist: false,
            PitReleaseAssist: false,
            ErsAssist: false,
            DrsAssist: false,
            DynamicRacingLine: 0,
            DynamicRacingLineType: 0,
            GameMode: 0,
            RuleSet: 0,
            TimeOfDay: 0,
            SessionLength: 0,
            SpeedUnitsLeadPlayer: 0,
            TemperatureUnitsLeadPlayer: 0,
            SpeedUnitsSecondaryPlayer: 0,
            TemperatureUnitsSecondaryPlayer: 0,
            NumSafetyCarPeriods: 0,
            NumVirtualSafetyCarPeriods: 0,
            NumRedFlagPeriods: 0,
            EqualCarPerformance: false,
            RecoveryMode: 0,
            FlashbackLimit: 0,
            SurfaceType: 0,
            LowFuelMode: false,
            RaceStarts: false,
            TyreTemperature: false,
            PitLaneTyreSim: false,
            CarDamage: 0,
            CarDamageRate: 0,
            Collisions: 0,
            CollisionsOffForFirstLapOnly: false,
            MpUnsafePitRelease: false,
            MpOffForGriefing: false,
            CornerCuttingStringency: 0,
            ParcFermeRules: false,
            PitStopExperience: 0,
            SafetyCar: 0,
            SafetyCarExperience: 0,
            FormationLap: false,
            FormationLapExperience: false,
            RedFlags: 0,
            AffectsLicenceLevelSolo: false,
            AffectsLicenceLevelMp: false,
            NumSessionsInWeekend: 0,
            WeekendStructure: [],
            Sector2LapDistanceStart: 0f,
            Sector3LapDistanceStart: 0f);
        var datagram = new UdpDatagram(
            Payload: [],
            RemoteEndPoint: new IPEndPoint(IPAddress.Loopback, 20777),
            ReceivedAt: DateTimeOffset.Parse("2026-04-18T10:00:00Z"));

        return new ParsedPacket(PacketId.Session, header, packet, datagram);
    }

    private static LapSample CreateLapSample(int lapNumber, uint frameIdentifier, float lapDistance, uint timeMs)
    {
        return new LapSample
        {
            SampledAt = DateTimeOffset.Parse("2026-05-17T10:00:00Z").AddMilliseconds(timeMs),
            FrameIdentifier = frameIdentifier,
            LapNumber = lapNumber,
            LapDistance = lapDistance,
            CurrentLapTimeInMs = timeMs,
            SpeedKph = 180,
            Throttle = 0.7,
            Brake = 0.1,
            Steering = 0.2f,
            IsValid = true
        };
    }

    private static LapSummary CreateLapSummary(int lapNumber)
    {
        return new LapSummary
        {
            LapNumber = lapNumber,
            FuelUsedLitres = 1.2f,
            StartTyre = "Medium",
            EndTyre = "Medium",
            ClosedAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(lapNumber)
        };
    }

    private static RaceEvent CreateRaceEvent(int lapNumber)
    {
        return new RaceEvent
        {
            LapNumber = lapNumber,
            Message = $"Lap {lapNumber} event",
            Timestamp = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(lapNumber),
            DedupKey = $"event-{lapNumber}"
        };
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(5);
        while (!predicate())
        {
            if (DateTime.UtcNow >= timeoutAt)
            {
                throw new TimeoutException("The expected storage state was not reached in time.");
            }

            await Task.Delay(25);
        }
    }

    private sealed class RecordingSessionRepository : ISessionRepository
    {
        public List<StoredSession> CreatedSessions { get; } = [];

        public Task CreateAsync(StoredSession session, CancellationToken cancellationToken = default)
        {
            CreatedSessions.Add(session);
            return Task.CompletedTask;
        }

        public Task EndAsync(string sessionId, DateTimeOffset endedAt, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredSession>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredSession>>(CreatedSessions);
        }

        public Task<bool> DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class ThrowingLapRepository : ILapRepository
    {
        public Task AddAsync(string sessionId, LapSummary lapSummary, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("lap insert failed");
        }

        public Task<IReadOnlyList<StoredLap>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredLap>>(Array.Empty<StoredLap>());
        }
    }

    private sealed class RecordingLapRepository : ILapRepository
    {
        public List<StoredLapWrite> StoredLaps { get; } = [];

        public Task AddAsync(string sessionId, LapSummary lapSummary, CancellationToken cancellationToken = default)
        {
            StoredLaps.Add(new StoredLapWrite(sessionId, lapSummary));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredLap>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredLap>>(Array.Empty<StoredLap>());
        }
    }

    private sealed class RecordingEventRepository : IEventRepository
    {
        public List<StoredEventWrite> StoredEvents { get; } = [];

        public Task AddAsync(string sessionId, RaceEvent raceEvent, CancellationToken cancellationToken = default)
        {
            StoredEvents.Add(new StoredEventWrite(sessionId, raceEvent));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredEvent>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
        }
    }

    private sealed class RecordingAiReportRepository : IAIReportRepository
    {
        public List<StoredAiReportWrite> StoredReports { get; } = [];

        public Task AddAsync(string sessionId, int lapNumber, AIAnalysisResult analysisResult, DateTimeOffset? createdAt = null, CancellationToken cancellationToken = default)
        {
            StoredReports.Add(new StoredAiReportWrite(sessionId, lapNumber, analysisResult));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredAiReport>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredAiReport>>(Array.Empty<StoredAiReport>());
        }
    }

    private sealed record StoredLapWrite(string SessionId, LapSummary LapSummary);

    private sealed record StoredEventWrite(string SessionId, RaceEvent RaceEvent);

    private sealed record StoredAiReportWrite(string SessionId, int LapNumber, AIAnalysisResult AnalysisResult);

    private sealed class RecordingLapSampleRepository : ILapSampleRepository
    {
        public List<StoredLapSample> StoredSamples { get; } = [];

        public Task AddAsync(StoredLapSample sample, CancellationToken cancellationToken = default)
        {
            StoredSamples.Add(sample);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<StoredLapSample> samples, CancellationToken cancellationToken = default)
        {
            StoredSamples.AddRange(samples);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredLapSample>> GetForLapAsync(string sessionId, int lapNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredLapSample>>(
                StoredSamples
                    .Where(sample => sample.SessionId == sessionId && sample.LapNumber == lapNumber)
                    .OrderBy(sample => sample.SampleIndex)
                    .ToArray());
        }
    }
}
