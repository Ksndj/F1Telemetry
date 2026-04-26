using System.ComponentModel;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.State;
using F1Telemetry.App;
using F1Telemetry.App.ViewModels;
using F1Telemetry.App.Windowing;
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
/// Verifies that closing the WPF shell releases background resources before the process exits.
/// </summary>
public sealed class AppShutdownTests
{
    /// <summary>
    /// Verifies that WPF exits when the main window closes.
    /// </summary>
    [Fact]
    public void AppXaml_UsesMainWindowCloseShutdownMode()
    {
        var document = XDocument.Load(FindRepositoryFile("F1Telemetry.App", "App.xaml"));
        Assert.NotNull(document.Root);
        var root = document.Root!;

        Assert.Equal("OnMainWindowClose", root.Attribute("ShutdownMode")?.Value);
    }

    /// <summary>
    /// Verifies that the first close request waits for shutdown and the second close is allowed.
    /// </summary>
    [Fact]
    public void MainWindow_CloseCancelsUntilShutdownCompletes()
    {
        RunOnStaThread(() =>
        {
            var shutdownCoordinator = new FakeShutdownCoordinator();
            var window = new MainWindow
            {
                DataContext = shutdownCoordinator
            };
            var closingCancelStates = new List<bool>();
            var closedBeforeShutdown = false;
            var closed = false;

            window.Closing += (_, e) => closingCancelStates.Add(e.Cancel);
            window.Closed += (_, _) =>
            {
                closedBeforeShutdown = !shutdownCoordinator.ShutdownCompleted;
                closed = true;
            };

            try
            {
                window.Show();
                window.Close();

                PumpDispatcherUntil(() => shutdownCoordinator.ShutdownCallCount == 1, TimeSpan.FromSeconds(5));
                Assert.Equal(new[] { true }, closingCancelStates);
                Assert.True(window.IsVisible);

                window.Close();
                Assert.Equal(new[] { true, true }, closingCancelStates);
                Assert.Equal(1, shutdownCoordinator.ShutdownCallCount);

                shutdownCoordinator.CompleteShutdown();
                PumpDispatcherUntil(() => closed, TimeSpan.FromSeconds(5));

                Assert.Equal(new[] { true, true, false }, closingCancelStates);
                Assert.False(closedBeforeShutdown);
                Assert.Equal(1, shutdownCoordinator.ShutdownCallCount);
            }
            finally
            {
                if (window.IsVisible)
                {
                    shutdownCoordinator.CompleteShutdown();
                    window.Close();
                }
            }
        });
    }

    /// <summary>
    /// Verifies that the view-model shutdown path can be called repeatedly without repeating disposal.
    /// </summary>
    [Fact]
    public void DashboardViewModel_ShutdownAsyncAndDispose_AreIdempotent()
    {
        RunOnStaThread(() =>
        {
            var storage = new FakeStoragePersistenceService();
            storage.CompleteDispose();
            var harness = CreateDashboardViewModel(storagePersistenceService: storage);

            harness.ViewModel.ShutdownAsync().GetAwaiter().GetResult();
            harness.ViewModel.ShutdownAsync().GetAwaiter().GetResult();
            harness.ViewModel.Dispose();

            Assert.Equal(1, storage.DisposeCount);
            Assert.Equal(1, harness.UdpListener.DisposeCount);
            Assert.Equal(1, harness.TtsService.DisposeCount);
        });
    }

    /// <summary>
    /// Verifies that shutdown cancels in-flight lifecycle work such as settings/AI operations.
    /// </summary>
    [Fact]
    public void DashboardViewModel_ShutdownAsync_CancelsLifecycleWork()
    {
        RunOnStaThread(() =>
        {
            var settingsStore = new BlockingSettingsStore();
            var storage = new FakeStoragePersistenceService();
            storage.CompleteDispose();
            var harness = CreateDashboardViewModel(appSettingsStore: settingsStore, storagePersistenceService: storage);

            Assert.True(settingsStore.LoadStarted.Wait(TimeSpan.FromSeconds(2)));

            var shutdownTask = harness.ViewModel.ShutdownAsync();

            Assert.True(settingsStore.LoadCanceled.Wait(TimeSpan.FromSeconds(2)));
            shutdownTask.GetAwaiter().GetResult();
        });
    }

    private static DashboardViewModelHarness CreateDashboardViewModel(
        IAppSettingsStore? appSettingsStore = null,
        FakeStoragePersistenceService? storagePersistenceService = null)
    {
        var udpListener = new FakeUdpListener();
        var ttsService = new FakeTtsService();
        var ttsQueue = new TtsQueue(ttsService, new TtsOptions());
        var storage = storagePersistenceService ?? new FakeStoragePersistenceService();
        var viewModel = new DashboardViewModel(
            udpListener,
            new FakePacketDispatcher(),
            new SessionStateStore(new CarStateStore()),
            new LapAnalyzer(),
            new EventDetectionService(),
            new FakeAiAnalysisService(),
            appSettingsStore ?? new FakeAppSettingsStore(),
            new TtsMessageFactory(),
            ttsQueue,
            storage,
            Dispatcher.CurrentDispatcher,
            new WindowsVoiceCatalog(() => new WindowsVoiceCatalogResult(Array.Empty<string>(), string.Empty, "No voices.")));

        return new DashboardViewModelHarness(viewModel, udpListener, storage, ttsService);
    }

    private static string FindRepositoryFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(pathParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(pathParts)}");
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

            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
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
        FakeUdpListener UdpListener,
        FakeStoragePersistenceService StoragePersistenceService,
        FakeTtsService TtsService);

    private sealed class FakeShutdownCoordinator : IApplicationShutdownCoordinator
    {
        private readonly TaskCompletionSource _shutdownCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ShutdownCallCount { get; private set; }

        public bool ShutdownCompleted { get; private set; }

        public void CompleteShutdown()
        {
            _shutdownCompletion.TrySetResult();
        }

        public async Task ShutdownAsync()
        {
            ShutdownCallCount++;
            await _shutdownCompletion.Task;
            ShutdownCompleted = true;
        }
    }

    private sealed class FakeUdpListener : IUdpListener
    {
        public event EventHandler<UdpDatagram>? DatagramReceived;

        public event EventHandler<Exception>? ReceiveFaulted;

        public bool IsListening { get; private set; }

        public int? ListeningPort { get; private set; }

        public int DisposeCount { get; private set; }

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
            DisposeCount++;
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

    private sealed class FakeStoragePersistenceService : IStoragePersistenceService
    {
        private readonly TaskCompletionSource _disposeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public event EventHandler<string>? LogEmitted;

        public int DisposeCount { get; private set; }

        public bool DisposeCompleted { get; private set; }

        public void CompleteDispose()
        {
            _disposeCompletion.TrySetResult();
        }

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

        public async ValueTask DisposeAsync()
        {
            DisposeCount++;
            await _disposeCompletion.Task;
            DisposeCompleted = true;
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
    }

    private sealed class BlockingSettingsStore : IAppSettingsStore
    {
        public ManualResetEventSlim LoadStarted { get; } = new();

        public ManualResetEventSlim LoadCanceled { get; } = new();

        public async Task<AppSettingsDocument> LoadAsync(CancellationToken cancellationToken = default)
        {
            LoadStarted.Set();
            await using var registration = cancellationToken.Register(() => LoadCanceled.Set());
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new AppSettingsDocument();
        }

        public Task SaveAiSettingsAsync(AISettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveTtsSettingsAsync(TtsOptions options, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTtsService : ITtsService, IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Configure(string? voiceName, int volume, int rate)
        {
        }

        public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}

#pragma warning restore CS0067
