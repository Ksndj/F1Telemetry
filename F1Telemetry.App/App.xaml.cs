using System.Net.Http;
using System.Windows;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.Services;
using F1Telemetry.Analytics.State;
using F1Telemetry.Storage.Repositories;
using F1Telemetry.Storage.Services;
using F1Telemetry.TTS.Models;
using F1Telemetry.TTS.Services;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Udp.Parsers;
using F1Telemetry.Udp.Services;

namespace F1Telemetry.App;

/// <summary>
/// Boots the WPF shell and wires the UDP pipeline into the central state store.
/// </summary>
public partial class App : Application
{
    private HttpClient? _aiHttpClient;
    private DashboardViewModel? _shellViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var udpListener = new UdpListener();
        var packetDispatcher = new PacketDispatcher(new PacketHeaderParser());
        var lapAnalyzer = new LapAnalyzer();
        var eventDetectionService = new EventDetectionService();
        var appSettingsStore = new AppSettingsStore();
        _aiHttpClient = new HttpClient();
        var ttsQueue = new TtsQueue(new WindowsTtsService(), new TtsOptions());
        var ttsMessageFactory = new TtsMessageFactory();
        var aiAnalysisService = new DeepSeekAnalysisService(
            new DeepSeekClient(_aiHttpClient),
            new PromptBuilder());
        var databaseService = new SqliteDatabaseService();
        var storagePersistenceService = new StoragePersistenceService(
            new SessionRepository(databaseService),
            new LapRepository(databaseService),
            new EventRepository(databaseService),
            new AIReportRepository(databaseService),
            databaseService.InitializeAsync,
            databaseService);
        var stateAggregator = new StateAggregator(new SessionStateStore(new CarStateStore()), lapAnalyzer, eventDetectionService);
        packetDispatcher.PacketParsed += (_, parsedPacket) =>
        {
            stateAggregator.ApplyPacket(parsedPacket);
            storagePersistenceService.ObserveParsedPacket(parsedPacket);
        };
        _shellViewModel = new DashboardViewModel(
            udpListener,
            packetDispatcher,
            stateAggregator.SessionStateStore,
            lapAnalyzer,
            eventDetectionService,
            aiAnalysisService,
            appSettingsStore,
            ttsMessageFactory,
            ttsQueue,
            storagePersistenceService,
            Dispatcher);
        var mainWindow = new MainWindow
        {
            DataContext = _shellViewModel
        };

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shellViewModel?.Dispose();
        _aiHttpClient?.Dispose();
        base.OnExit(e);
    }
}
