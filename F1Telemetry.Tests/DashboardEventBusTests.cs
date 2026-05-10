using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Interfaces;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.State;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Core.Eventing;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.TTS;
using F1Telemetry.TTS.Models;
using F1Telemetry.TTS.Services;
using F1Telemetry.Udp.Packets;
using Xunit;

namespace F1Telemetry.Tests;

#pragma warning disable CS0067

/// <summary>
/// Verifies the V2 EventBus bridge in the dashboard keeps existing event handling intact.
/// </summary>
public sealed class DashboardEventBusTests
{
    /// <summary>
    /// Verifies drained race events are published to the EventBus.
    /// </summary>
    [Fact]
    public void DrainDetectedRaceEvents_PublishesRaceEventToEventBus()
    {
        RunOnStaThread(() =>
        {
            var raceEvent = CreateRaceEvent();
            var eventDetectionService = new FakeEventDetectionService(raceEvent);
            var eventBus = new InMemoryEventBus<RaceEvent>();
            RaceEvent? received = null;
            eventBus.Subscribe(value => received = value);
            using var harness = CreateDashboardViewModel(eventDetectionService, eventBus);

            InvokeDrainDetectedRaceEvents(harness.ViewModel);

            Assert.Same(raceEvent, received);
            Assert.Equal(new[] { raceEvent }, harness.StoragePersistenceService.EnqueuedRaceEvents);
        });
    }

    /// <summary>
    /// Verifies EventBus subscriber failures do not break the legacy dashboard event path.
    /// </summary>
    [Fact]
    public void DrainDetectedRaceEvents_WhenEventBusSubscriberThrows_ProtectsLegacyPath()
    {
        RunOnStaThread(() =>
        {
            var raceEvent = CreateRaceEvent();
            var eventDetectionService = new FakeEventDetectionService(raceEvent);
            var eventBus = new InMemoryEventBus<RaceEvent>();
            eventBus.Subscribe(_ => throw new InvalidOperationException("subscriber failed"));
            using var harness = CreateDashboardViewModel(eventDetectionService, eventBus);

            var exception = Record.Exception(() => InvokeDrainDetectedRaceEvents(harness.ViewModel));
            InvokeDrainPendingEventLogs(harness.ViewModel);

            Assert.Null(exception);
            Assert.Equal(new[] { raceEvent }, harness.StoragePersistenceService.EnqueuedRaceEvents);
            Assert.Contains(
                harness.ViewModel.LogEntries,
                entry => entry.Category == "System" &&
                    entry.Message.Contains("EventBus", StringComparison.Ordinal));
        });
    }

    /// <summary>
    /// Verifies drained race events are exposed to AI context through the EventBus insight buffer.
    /// </summary>
    [Fact]
    public void BuildAiAnalysisContext_AfterDrainingRaceEvent_UsesEventBusInsightBuffer()
    {
        RunOnStaThread(() =>
        {
            var raceEvent = CreateRaceEvent();
            var eventDetectionService = new FakeEventDetectionService(raceEvent);
            var eventBus = new InMemoryEventBus<RaceEvent>();
            using var harness = CreateDashboardViewModel(eventDetectionService, eventBus);

            InvokeDrainDetectedRaceEvents(harness.ViewModel);
            var context = InvokeBuildAiAnalysisContext(harness.ViewModel);

            Assert.Equal(new[] { raceEvent.Message }, context.RecentEvents);
        });
    }

    private static DashboardViewModelHarness CreateDashboardViewModel(
        IEventDetectionService eventDetectionService,
        IEventBus<RaceEvent> eventBus)
    {
        var ttsQueue = new TtsQueue(new FakeTtsService(), new TtsOptions());
        var storagePersistenceService = new FakeStoragePersistenceService();
        var viewModel = new DashboardViewModel(
            new FakeUdpListener(),
            new FakePacketDispatcher(),
            new SessionStateStore(new CarStateStore()),
            new LapAnalyzer(),
            eventDetectionService,
            new FakeAiAnalysisService(),
            new FakeAppSettingsStore(),
            new FakeUdpRawLogWriter(),
            new TtsMessageFactory(),
            ttsQueue,
            storagePersistenceService,
            Dispatcher.CurrentDispatcher,
            new WindowsVoiceCatalog(() => new WindowsVoiceCatalogResult(Array.Empty<string>(), string.Empty, "No voices.")),
            raceEventBus: eventBus);

        return new DashboardViewModelHarness(viewModel, storagePersistenceService);
    }

    private static RaceEvent CreateRaceEvent()
    {
        return new RaceEvent
        {
            EventType = EventType.LowFuel,
            Timestamp = DateTimeOffset.UtcNow,
            LapNumber = 8,
            Severity = EventSeverity.Warning,
            Message = "低油警告：预计剩余 0.6 圈。",
            DedupKey = "low-fuel"
        };
    }

    private static void InvokeDrainDetectedRaceEvents(DashboardViewModel viewModel)
    {
        var method = typeof(DashboardViewModel).GetMethod(
            "DrainDetectedRaceEvents",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method!.Invoke(viewModel, null);
    }

    private static void InvokeDrainPendingEventLogs(DashboardViewModel viewModel)
    {
        var method = typeof(DashboardViewModel).GetMethod(
            "DrainPendingEventLogs",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method!.Invoke(viewModel, null);
    }

    private static AIAnalysisContext InvokeBuildAiAnalysisContext(DashboardViewModel viewModel)
    {
        var method = typeof(DashboardViewModel).GetMethod(
            "BuildAiAnalysisContext",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var result = method!.Invoke(
            viewModel,
            new object?[]
            {
                new SessionState(),
                null,
                new LapSummary
                {
                    LapNumber = 8,
                    IsValid = true,
                    ClosedAt = DateTimeOffset.UtcNow
                }
            });

        return Assert.IsType<AIAnalysisContext>(result);
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

    private sealed record DashboardViewModelHarness(
        DashboardViewModel ViewModel,
        FakeStoragePersistenceService StoragePersistenceService) : IDisposable
    {
        public void Dispose()
        {
            ViewModel.Dispose();
        }
    }

    private sealed class FakeEventDetectionService : IEventDetectionService
    {
        private readonly RaceEvent _raceEvent;
        private bool _drained;

        public FakeEventDetectionService(RaceEvent raceEvent)
        {
            _raceEvent = raceEvent;
        }

        public void Observe(SessionState sessionState)
        {
        }

        public IReadOnlyList<RaceEvent> DrainPendingEvents()
        {
            if (_drained)
            {
                return Array.Empty<RaceEvent>();
            }

            _drained = true;
            return new[] { _raceEvent };
        }

        public void Reset()
        {
            _drained = false;
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

        public List<RaceEvent> EnqueuedRaceEvents { get; } = new();

        public void ObserveParsedPacket(ParsedPacket parsedPacket)
        {
        }

        public void EnqueueLapSummary(LapSummary lapSummary)
        {
        }

        public void EnqueueRaceEvent(RaceEvent raceEvent)
        {
            EnqueuedRaceEvents.Add(raceEvent);
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
