using System.Net;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.State;
using F1Telemetry.App.Services;
using F1Telemetry.App.ViewModels;
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
/// Verifies dashboard UDP port settings are loaded, validated, saved, and shut down safely.
/// </summary>
public sealed class DashboardUdpPortSettingsTests
{
    /// <summary>
    /// Verifies the dashboard restores the persisted UDP listen port during settings load.
    /// </summary>
    [Fact]
    public void LoadSettingsAsync_WhenUdpPortConfigured_RestoresPortText()
    {
        RunOnStaThread(() =>
        {
            var settingsStore = new FakeAppSettingsStore(20778);
            var harness = CreateDashboardViewModel(settingsStore);

            try
            {
                PumpDispatcherUntil(() => settingsStore.LoadCallCount > 0 && harness.ViewModel.PortText == "20778", TimeSpan.FromSeconds(2));

                Assert.Equal("20778", harness.ViewModel.PortText);
                Assert.Empty(settingsStore.SavedUdpPorts);
            }
            finally
            {
                harness.ViewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies invalid port edits are not persisted and cannot start UDP listening.
    /// </summary>
    [Fact]
    public void PortText_WhenInvalid_DoesNotSaveAndStartDoesNotListen()
    {
        RunOnStaThread(() =>
        {
            var settingsStore = new FakeAppSettingsStore(20777);
            var harness = CreateDashboardViewModel(settingsStore);

            try
            {
                PumpDispatcherUntil(() => settingsStore.LoadCallCount > 0, TimeSpan.FromSeconds(2));
                harness.ViewModel.PortText = "70000";

                harness.ViewModel.StartListeningCommand.Execute(null);
                PumpDispatcherFor(TimeSpan.FromMilliseconds(250));

                Assert.Empty(settingsStore.SavedUdpPorts);
                Assert.Equal(0, harness.UdpListener.StartCount);
                Assert.False(harness.UdpListener.IsListening);
            }
            finally
            {
                harness.ViewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies rapid valid edits are debounced so only the final distinct port is saved.
    /// </summary>
    [Fact]
    public void PortText_WhenValidChangesDebounced_SavesOnlyFinalDistinctPort()
    {
        RunOnStaThread(() =>
        {
            var settingsStore = new FakeAppSettingsStore(20777);
            var harness = CreateDashboardViewModel(settingsStore);

            try
            {
                PumpDispatcherUntil(() => settingsStore.LoadCallCount > 0, TimeSpan.FromSeconds(2));

                harness.ViewModel.PortText = "2";
                harness.ViewModel.PortText = "20";
                harness.ViewModel.PortText = "207";
                harness.ViewModel.PortText = "2077";
                harness.ViewModel.PortText = "20778";

                PumpDispatcherUntil(() => settingsStore.SavedUdpPorts.Count == 1, TimeSpan.FromSeconds(3));
                Assert.Equal(20778, settingsStore.SavedUdpPorts.Single().ListenPort);

                harness.ViewModel.PortText = "20778";
                PumpDispatcherFor(TimeSpan.FromMilliseconds(1000));

                Assert.Single(settingsStore.SavedUdpPorts);
            }
            finally
            {
                harness.ViewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies starting listening saves and uses the current valid UDP port.
    /// </summary>
    [Fact]
    public void StartListening_UsesAndSavesCurrentValidPort()
    {
        RunOnStaThread(() =>
        {
            var settingsStore = new FakeAppSettingsStore(20777);
            var harness = CreateDashboardViewModel(settingsStore);

            try
            {
                PumpDispatcherUntil(() => settingsStore.LoadCallCount > 0, TimeSpan.FromSeconds(2));
                harness.ViewModel.PortText = "20779";

                harness.ViewModel.StartListeningCommand.Execute(null);

                PumpDispatcherUntil(() => harness.UdpListener.StartCount == 1, TimeSpan.FromSeconds(3));
                Assert.Equal(20779, harness.UdpListener.ListeningPort);
                Assert.Contains(settingsStore.SavedUdpPorts, settings => settings.ListenPort == 20779);
            }
            finally
            {
                harness.ViewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies shutdown stops the debounce timer and persists a pending valid port once.
    /// </summary>
    [Fact]
    public void ShutdownAsync_WhenValidPortPending_StopsDebounceAndSavesOnce()
    {
        RunOnStaThread(() =>
        {
            var settingsStore = new FakeAppSettingsStore(20777);
            var harness = CreateDashboardViewModel(settingsStore);

            PumpDispatcherUntil(() => settingsStore.LoadCallCount > 0, TimeSpan.FromSeconds(2));
            harness.ViewModel.PortText = "20778";

            harness.ViewModel.ShutdownAsync().GetAwaiter().GetResult();
            PumpDispatcherFor(TimeSpan.FromMilliseconds(1000));

            Assert.Single(settingsStore.SavedUdpPorts);
            Assert.Equal(20778, settingsStore.SavedUdpPorts.Single().ListenPort);
        });
    }

    /// <summary>
    /// Verifies no debounce callback writes settings after shutdown has completed.
    /// </summary>
    [Fact]
    public void ShutdownAsync_AfterDebounceStopped_DoesNotSaveAgain()
    {
        RunOnStaThread(() =>
        {
            var settingsStore = new FakeAppSettingsStore(20777);
            var harness = CreateDashboardViewModel(settingsStore);

            PumpDispatcherUntil(() => settingsStore.LoadCallCount > 0, TimeSpan.FromSeconds(2));
            harness.ViewModel.PortText = "20778";
            PumpDispatcherUntil(() => settingsStore.SavedUdpPorts.Count == 1, TimeSpan.FromSeconds(3));

            harness.ViewModel.ShutdownAsync().GetAwaiter().GetResult();
            PumpDispatcherFor(TimeSpan.FromMilliseconds(1000));

            Assert.Single(settingsStore.SavedUdpPorts);
        });
    }

    /// <summary>
    /// Verifies the raw log directory command reports opener failures without leaking async exceptions.
    /// </summary>
    [Fact]
    public void OpenUdpRawLogDirectoryCommand_WhenDirectoryServiceThrows_ShowsError()
    {
        RunOnStaThread(() =>
        {
            var settingsStore = new FakeAppSettingsStore(20777);
            var rawLogWriter = new FakeUdpRawLogWriter(new UdpRawLogStatus
            {
                DirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
            });
            var rawLogDirectoryService = new FakeUdpRawLogDirectoryService
            {
                OpenException = new InvalidOperationException("explorer unavailable")
            };
            var harness = CreateDashboardViewModel(settingsStore, rawLogWriter, rawLogDirectoryService);

            try
            {
                PumpDispatcherUntil(() => settingsStore.LoadCallCount > 0, TimeSpan.FromSeconds(2));
                rawLogWriter.Status = rawLogWriter.Status with
                {
                    DirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
                };

                harness.ViewModel.OpenUdpRawLogDirectoryCommand.Execute(null);

                PumpDispatcherUntil(
                    () => harness.ViewModel.UdpRawLogLastErrorText.Contains("打开日志目录失败", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(3));

                Assert.Contains("explorer unavailable", harness.ViewModel.UdpRawLogLastErrorText, StringComparison.Ordinal);
            }
            finally
            {
                harness.ViewModel.Dispose();
            }
        });
    }

    private static DashboardViewModelHarness CreateDashboardViewModel(
        FakeAppSettingsStore appSettingsStore,
        FakeUdpRawLogWriter? udpRawLogWriter = null,
        IUdpRawLogDirectoryService? udpRawLogDirectoryService = null)
    {
        var udpListener = new FakeUdpListener();
        var ttsQueue = new TtsQueue(new FakeTtsService(), new TtsOptions());
        var viewModel = new DashboardViewModel(
            udpListener,
            new FakePacketDispatcher(),
            new SessionStateStore(new CarStateStore()),
            new LapAnalyzer(),
            new EventDetectionService(),
            new FakeAiAnalysisService(),
            appSettingsStore,
            udpRawLogWriter ?? new FakeUdpRawLogWriter(),
            new TtsMessageFactory(),
            ttsQueue,
            new FakeStoragePersistenceService(),
            Dispatcher.CurrentDispatcher,
            new WindowsVoiceCatalog(() => new WindowsVoiceCatalogResult(Array.Empty<string>(), string.Empty, "No voices.")),
            udpRawLogDirectoryService);

        return new DashboardViewModelHarness(viewModel, udpListener);
    }

    private static void PumpDispatcherUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                Assert.True(condition(), "Condition was not met before the dispatcher pump timed out.");
            }

            PumpDispatcherFrame();
        }
    }

    private static void PumpDispatcherFor(TimeSpan duration)
    {
        var deadline = DateTimeOffset.UtcNow + duration;
        while (DateTimeOffset.UtcNow < deadline)
        {
            PumpDispatcherFrame();
        }
    }

    private static void PumpDispatcherFrame()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? capturedException = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
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
        FakeUdpListener UdpListener);

    private sealed class FakeUdpListener : IUdpListener
    {
        public event EventHandler<UdpDatagram>? DatagramReceived;

        public event EventHandler<Exception>? ReceiveFaulted;

        public bool IsListening { get; private set; }

        public int? ListeningPort { get; private set; }

        public int StartCount { get; private set; }

        public Task StartAsync(int port, CancellationToken cancellationToken = default)
        {
            StartCount++;
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

    private sealed class FakeAppSettingsStore : IAppSettingsStore
    {
        private readonly AppSettingsDocument _settingsDocument;

        public FakeAppSettingsStore(int udpListenPort)
        {
            _settingsDocument = new AppSettingsDocument
            {
                Udp = new UdpSettings { ListenPort = udpListenPort }
            };
        }

        public int LoadCallCount { get; private set; }

        public List<UdpSettings> SavedUdpPorts { get; } = new();

        public Task<AppSettingsDocument> LoadAsync(CancellationToken cancellationToken = default)
        {
            LoadCallCount++;
            return Task.FromResult(_settingsDocument);
        }

        public Task SaveAiSettingsAsync(AISettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveTtsSettingsAsync(TtsOptions options, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveRaceWeekendTyrePlanAsync(RaceWeekendTyrePlan plan, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveUdpRawLogOptionsAsync(UdpRawLogOptions options, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveUdpSettingsAsync(UdpSettings settings, CancellationToken cancellationToken = default)
        {
            SavedUdpPorts.Add(settings);
            return Task.CompletedTask;
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

    private sealed class FakeUdpRawLogWriter : IUdpRawLogWriter
    {
        public FakeUdpRawLogWriter(UdpRawLogStatus? status = null)
        {
            Status = status ?? new UdpRawLogStatus();
        }

        public UdpRawLogStatus Status { get; set; } = new();

        public void UpdateOptions(UdpRawLogOptions options)
        {
            Status = Status with
            {
                Enabled = options.Enabled,
                DirectoryPath = options.DirectoryPath
            };
        }

        public void TryEnqueue(UdpDatagram datagram)
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeUdpRawLogDirectoryService : IUdpRawLogDirectoryService
    {
        public Exception? OpenException { get; init; }

        public UdpRawLogFileInfo GetLatestFileInfo(UdpRawLogStatus status)
        {
            return new UdpRawLogFileInfo("无", "无", "无", string.Empty);
        }

        public UdpRawLogDirectoryOpenResult OpenDirectory(string directoryPath)
        {
            if (OpenException is not null)
            {
                throw OpenException;
            }

            return new UdpRawLogDirectoryOpenResult(true, string.Empty);
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
