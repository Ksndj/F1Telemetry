using System.Net;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Interfaces;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.Services;
using F1Telemetry.Analytics.State;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;
using F1Telemetry.TTS;
using F1Telemetry.TTS.Models;
using F1Telemetry.TTS.Services;
using F1Telemetry.Udp.Packets;
using Xunit;

namespace F1Telemetry.Tests;

#pragma warning disable CS0067

/// <summary>
/// Verifies dashboard integration with the historical session comparison page.
/// </summary>
public sealed class DashboardSessionComparisonTests
{
    /// <summary>
    /// Verifies opening session comparison refreshes historical data without starting UDP.
    /// </summary>
    [Fact]
    public void SelectingSessionComparison_RefreshesComparisonWithoutStartingUdp()
    {
        RunOnStaThread(() =>
        {
            var udpListener = new FakeUdpListener();
            var sessionRepository = new RecordingSessionRepository
            {
                Sessions =
                [
                    CreateSession("session-a", DateTimeOffset.Parse("2026-04-19T10:00:00Z")),
                    CreateSession("session-b", DateTimeOffset.Parse("2026-04-18T10:00:00Z"))
                ]
            };
            var lapRepository = new RecordingLapRepository();
            lapRepository.LapsBySession["session-a"] = [CreateLap("session-a", 1, 90_000)];
            lapRepository.LapsBySession["session-b"] = [CreateLap("session-b", 1, 91_000)];
            var sessionComparison = new SessionComparisonViewModel(sessionRepository, lapRepository);
            using var viewModel = CreateDashboardViewModel(udpListener, sessionComparison);
            var comparisonItem = viewModel.ShellNavigationItems.Single(item => item.Key == "session-comparison");

            viewModel.SelectedShellNavigationItem = comparisonItem;

            WaitUntil(() => lapRepository.GetRecentCallCount >= 2);
            Assert.False(udpListener.IsListening);
            Assert.Equal(2, viewModel.SessionComparison.SelectedSessions.Count);
            Assert.True(viewModel.SessionComparison.LapTimeComparisonPanel.HasData);
        });
    }

    private static DashboardViewModel CreateDashboardViewModel(
        FakeUdpListener udpListener,
        SessionComparisonViewModel sessionComparison)
    {
        return new DashboardViewModel(
            udpListener,
            new FakePacketDispatcher(),
            new SessionStateStore(new CarStateStore()),
            new LapAnalyzer(),
            new EventDetectionService(),
            new FakeAiAnalysisService(),
            new FakeAppSettingsStore(),
            new FakeUdpRawLogWriter(),
            new TtsMessageFactory(),
            new TtsQueue(new FakeTtsService(), new TtsOptions()),
            new FakeStoragePersistenceService(),
            Dispatcher.CurrentDispatcher,
            new WindowsVoiceCatalog(() => new WindowsVoiceCatalogResult(Array.Empty<string>(), string.Empty, "No voices.")),
            sessionComparison: sessionComparison);
    }

    private static StoredSession CreateSession(string id, DateTimeOffset startedAt)
    {
        return new StoredSession
        {
            Id = id,
            SessionUid = $"uid-{id}",
            TrackId = 10,
            SessionType = 15,
            StartedAt = startedAt,
            EndedAt = startedAt.AddMinutes(45)
        };
    }

    private static StoredLap CreateLap(string sessionId, int lapNumber, int lapTimeInMs)
    {
        return new StoredLap
        {
            Id = lapNumber,
            SessionId = sessionId,
            LapNumber = lapNumber,
            LapTimeInMs = lapTimeInMs,
            IsValid = true,
            CreatedAt = DateTimeOffset.Parse("2026-04-18T10:00:00Z").AddMinutes(lapNumber)
        };
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? capturedException = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (capturedException is not null)
        {
            ExceptionDispatchInfo.Capture(capturedException).Throw();
        }
    }

    private static void WaitUntil(Func<bool> predicate)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(5);
        while (!predicate())
        {
            if (DateTime.UtcNow >= timeoutAt)
            {
                throw new TimeoutException("The expected dashboard session comparison state was not reached in time.");
            }

            Thread.Sleep(25);
        }
    }

    private sealed class RecordingSessionRepository : ISessionRepository
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

    private sealed class RecordingLapRepository : ILapRepository
    {
        public Dictionary<string, IReadOnlyList<StoredLap>> LapsBySession { get; } = new(StringComparer.Ordinal);

        public int GetRecentCallCount { get; private set; }

        public Task AddAsync(string sessionId, LapSummary lapSummary, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredLap>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
        {
            GetRecentCallCount++;
            return Task.FromResult(
                LapsBySession.TryGetValue(sessionId, out var laps)
                    ? laps.Take(count).ToArray()
                    : (IReadOnlyList<StoredLap>)Array.Empty<StoredLap>());
        }
    }

    private sealed class FakeUdpListener : IUdpListener
    {
        public event EventHandler<UdpDatagram>? DatagramReceived;

        public event EventHandler<Exception>? ReceiveFaulted;

        public bool IsListening { get; private set; }

        public int? ListeningPort { get; private set; }

        public Task StartAsync(int port, CancellationToken cancellationToken = default)
        {
            IsListening = true;
            ListeningPort = port;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            IsListening = false;
            ListeningPort = null;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsListening = false;
            ListeningPort = null;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakePacketDispatcher : IPacketDispatcher<PacketId, PacketHeader>
    {
        public event EventHandler<PacketDispatchResult<PacketId, PacketHeader>>? PacketDispatched;

        public bool TryDispatch(UdpDatagram datagram, out string? error)
        {
            error = null;
            return true;
        }
    }

    private sealed class FakeAiAnalysisService : IAIAnalysisService
    {
        public Task<AIAnalysisResult> AnalyzeAsync(
            AIAnalysisContext context,
            AISettings settings,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AIAnalysisResult());
        }
    }

    private sealed class FakeAppSettingsStore : IAppSettingsStore
    {
        public Task<AppSettingsDocument> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AppSettingsDocument());
        }

        public Task SaveAiSettingsAsync(AISettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveTtsSettingsAsync(TtsOptions options, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveUdpRawLogOptionsAsync(UdpRawLogOptions options, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveUdpSettingsAsync(UdpSettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUdpRawLogWriter : IUdpRawLogWriter
    {
        public UdpRawLogStatus Status { get; } = new();

        public void UpdateOptions(UdpRawLogOptions options)
        {
        }

        public void TryEnqueue(UdpDatagram datagram)
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeStoragePersistenceService : IStoragePersistenceService
    {
        public event EventHandler<string>? LogEmitted;

        public void ObserveParsedPacket(ParsedPacket parsedPacket)
        {
        }

        public void EnqueueLapSummary(LapSummary lapSummary)
        {
        }

        public void EnqueueRaceEvent(RaceEvent raceEvent)
        {
        }

        public void EnqueueAiReport(int lapNumber, AIAnalysisResult analysisResult)
        {
        }

        public Task CompleteActiveSessionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeTtsService : ITtsService, IDisposable
    {
        public void Configure(string? voiceName, int volume, int rate)
        {
        }

        public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}

#pragma warning restore CS0067
