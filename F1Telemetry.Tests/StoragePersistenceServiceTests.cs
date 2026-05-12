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
        public List<LapSummary> StoredLaps { get; } = [];

        public Task AddAsync(string sessionId, LapSummary lapSummary, CancellationToken cancellationToken = default)
        {
            StoredLaps.Add(lapSummary);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredLap>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredLap>>(Array.Empty<StoredLap>());
        }
    }

    private sealed class RecordingEventRepository : IEventRepository
    {
        public Task AddAsync(string sessionId, RaceEvent raceEvent, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredEvent>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
        }
    }

    private sealed class RecordingAiReportRepository : IAIReportRepository
    {
        public Task AddAsync(string sessionId, int lapNumber, AIAnalysisResult analysisResult, DateTimeOffset? createdAt = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredAiReport>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredAiReport>>(Array.Empty<StoredAiReport>());
        }
    }
}
