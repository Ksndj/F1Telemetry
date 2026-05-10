using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Interfaces;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.App.Charts;
using F1Telemetry.App.Formatting;
using F1Telemetry.App.Logging;
using F1Telemetry.App.Services;
using F1Telemetry.App.Windowing;
using F1Telemetry.Analytics.State;
using F1Telemetry.Core.Abstractions;
using F1Telemetry.Core.Eventing;
using F1Telemetry.Core.Formatting;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;
using F1Telemetry.TTS;
using F1Telemetry.TTS.Models;
using F1Telemetry.TTS.Services;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Drives the real-time dashboard and coordinates UI-safe access to analytics, AI, TTS, and storage outputs.
/// </summary>
public sealed class DashboardViewModel : ViewModelBase, IApplicationShutdownCoordinator, IDisposable
{
    private const int MaxLogEntries = 50;
    private const int MaxPendingEventLogs = 200;
    private const int MaxPendingAiTtsLogs = 200;
    private const int MaxPendingAiAnalysisLogs = 200;
    private const int MaxOverviewEventSummaries = 4;
    private const int MaxOverviewEventSummaryChars = 40;
    private const int MaxPostRaceAiLaps = 15;
    private const double ExpandedSidebarWidth = 220d;
    private const double CollapsedSidebarWidth = 80d;
    private static readonly TimeSpan UdpPortSaveDebounceInterval = TimeSpan.FromMilliseconds(800);
    private readonly IUdpListener _udpListener;
    private readonly IPacketDispatcher<PacketId, PacketHeader> _packetDispatcher;
    private readonly SessionStateStore _sessionStateStore;
    private readonly ILapAnalyzer _lapAnalyzer;
    private readonly IEventDetectionService _eventDetectionService;
    private readonly IAIAnalysisService _aiAnalysisService;
    private readonly IAppSettingsStore _appSettingsStore;
    private readonly IUdpRawLogWriter _udpRawLogWriter;
    private readonly TtsMessageFactory _ttsMessageFactory;
    private readonly TtsQueue _ttsQueue;
    private readonly WindowsVoiceCatalog _windowsVoiceCatalog;
    private readonly IStoragePersistenceService _storagePersistenceService;
    private readonly IEventBus<RaceEvent> _raceEventBus;
    private readonly RaceEventSpeechSubscriber _raceEventSpeechSubscriber;
    private readonly RaceEventInsightBuffer _raceEventInsightBuffer;
    private readonly IUdpRawLogDirectoryService _udpRawLogDirectoryService;
    private readonly Dispatcher _dispatcher;
    private readonly CurrentLapChartBuilder _currentLapChartBuilder;
    private readonly TrendChartBuilder _trendChartBuilder;
    private readonly TelemetryAnalysisSummaryBuilder _telemetryAnalysisSummaryBuilder;
    private readonly DispatcherTimer _uiTimer;
    private readonly DispatcherTimer _udpPortSaveTimer;
    private readonly CancellationTokenSource _lifecycleCts = new();
    private readonly Queue<LogEntryViewModel> _pendingEventLogs = new();
    private readonly Queue<LogEntryViewModel> _pendingAiTtsLogs = new();
    private readonly Queue<LogEntryViewModel> _pendingAiAnalysisLogs = new();
    private readonly object _pendingEventLogsLock = new();
    private readonly object _pendingAiTtsLogsLock = new();
    private readonly object _pendingAiAnalysisLogsLock = new();
    private readonly SemaphoreSlim _settingsGate = new(1, 1);
    private readonly RelayCommand _startListeningCommand;
    private readonly RelayCommand _stopListeningCommand;
    private readonly RelayCommand _downloadLatestVersionCommand;
    private readonly RelayCommand _toggleSidebarCommand;
    private readonly RelayCommand _openUdpRawLogDirectoryCommand;
    private readonly RelayCommand _generatePostRaceAiSummaryCommand;
    private ShellNavigationItemViewModel? _selectedShellNavigationItem;
    private PostRaceAiCompletionModeOptionViewModel? _selectedPostRaceAiCompletionMode;
    private bool _isSidebarExpanded = true;
    private bool _isBusy;
    private bool _isListening;
    private bool _isConnected;
    private int? _listeningPort;
    private long _totalPacketCount;
    private int _packetsPerSecond;
    private string _portText = "20777";
    private string _statusMessage = "准备监听 F1 25 UDP。";
    private string _trackText = "等待 Session 包。";
    private string _sessionTypeText = "未知赛制";
    private string _lapText = "-";
    private string _weatherText = "-";
    private string _playerName = "等待玩家车辆状态。";
    private string _playerCurrentLapText = "-";
    private string _playerLastLapText = "-";
    private string _playerBestLapText = "-";
    private string _playerPositionText = "-";
    private string _playerGapText = "-";
    private string _playerFuelText = "-";
    private string _playerErsText = "-";
    private string _playerTyreText = "-";
    private string _playerTyreAgeText = "-";
    private string _bestLapSummaryText = "-";
    private string _lastLapSummaryText = "-";
    private string _overviewSpeedText = "-";
    private string _overviewGearText = "-";
    private string _overviewThrottleText = "-";
    private string _overviewBrakeText = "-";
    private string _overviewDrsText = "-";
    private string _overviewTyreWearText = "-";
    private string _overviewDamageText = "等待 CarDamage 包";
    private string _overviewKeyOpponentText = "-";
    private string _overviewSessionFocusText = SessionModeFormatter.FormatFocus(SessionMode.Unknown);
    private string _overviewRecentAiSuggestionText = "-";
    private string _overviewRecentTtsStatusText = "-";
    private long _receivedPacketCount;
    private long _lastPacketReceivedUnixMs = -1;
    private long _lastPacketsPerSecondSampleCount;
    private DateTimeOffset _lastPacketsPerSecondSampleAt;
    private string? _lastEventCode;
    private bool _aiEnabled;
    private string _aiBaseUrl = "https://api.deepseek.com";
    private string _aiModel = "deepseek-chat";
    private string _aiApiKey = string.Empty;
    private string _aiSettingsSaveStatusText = "等待保存";
    private int _aiRequestTimeoutSeconds = 10;
    private bool _udpRawLogEnabled;
    private string _udpRawLogDirectoryText = string.Empty;
    private string _udpRawLogLastFilePathText = "无";
    private string _udpRawLogLastFileSizeText = "无";
    private string _udpRawLogLastWriteTimeText = "无";
    private string _udpRawLogStatusText = "Raw Log 未启用";
    private string _udpRawLogLastErrorText = string.Empty;
    private string _udpRawLogDirectoryOpenErrorText = string.Empty;
    private long _udpRawLogWrittenPacketCount;
    private long _udpRawLogDroppedPacketCount;
    private int _udpRawLogQueueCapacity = 4096;
    private int _udpRawLogSettingsSaveVersion;
    private bool _ttsEnabled;
    private string _ttsVoiceName = string.Empty;
    private string _ttsVoiceStatusText = "正在读取 Windows 语音...";
    private string _defaultTtsVoiceName = string.Empty;
    private int _ttsVolume = 100;
    private int _ttsRate;
    private int _ttsCooldownSeconds = 8;
    private bool _isApplyingSettings;
    private int _aiSettingsSaveVersion;
    private int _ttsSettingsSaveVersion;
    private bool _isAiAnalysisRunning;
    private int _lastValidUdpListenPort = UdpSettings.DefaultListenPort;
    private int _lastSavedUdpListenPort = UdpSettings.DefaultListenPort;
    private int _udpSettingsSaveVersion;
    private ulong? _activeSessionUid;
    private string? _lastPostRaceAiSummaryKey;
    private string? _lastStagedPostRaceAiKey;
    private string? _lastPersistedLapKey;
    private int? _lastTrendRefreshLapNumber;
    private string _postRaceAiStatusText = "等待完整正赛结束后生成 AI 总结。";
    private string _postRaceAiCompletionText = "自动判断：等待 UDP 最终分类。";
    private readonly object _shutdownGate = new();
    private Task? _shutdownTask;
    private bool _disposed;
    private bool _hasRequestedHistoryLoad;

    /// <summary>
    /// Initializes a new dashboard view model.
    /// </summary>
    /// <param name="udpListener">The UDP listener service.</param>
    /// <param name="packetDispatcher">The packet dispatcher used for header validation.</param>
    /// <param name="sessionStateStore">The central session state store.</param>
    /// <param name="lapAnalyzer">The lap analyzer that exposes completed player laps.</param>
    /// <param name="eventDetectionService">The event detection service that exposes reusable race events.</param>
    /// <param name="aiAnalysisService">The AI analysis service used after completed laps.</param>
    /// <param name="appSettingsStore">The local application settings store.</param>
    /// <param name="udpRawLogWriter">The optional raw UDP log writer.</param>
    /// <param name="ttsMessageFactory">The mapper that converts event and AI outputs into TTS queue messages.</param>
    /// <param name="ttsQueue">The TTS queue that plays race events and AI guidance.</param>
    /// <param name="storagePersistenceService">The background SQLite persistence coordinator.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="windowsVoiceCatalog">The optional Windows voice catalog used by the settings UI.</param>
    /// <param name="udpRawLogDirectoryService">The optional raw UDP log directory helper used by Settings.</param>
    /// <param name="raceEventBus">The optional V2 event bus used to publish detected race events.</param>
    /// <param name="historyBrowser">The optional persisted history session browser.</param>
    public DashboardViewModel(
        IUdpListener udpListener,
        IPacketDispatcher<PacketId, PacketHeader> packetDispatcher,
        SessionStateStore sessionStateStore,
        ILapAnalyzer lapAnalyzer,
        IEventDetectionService eventDetectionService,
        IAIAnalysisService aiAnalysisService,
        IAppSettingsStore appSettingsStore,
        IUdpRawLogWriter udpRawLogWriter,
        TtsMessageFactory ttsMessageFactory,
        TtsQueue ttsQueue,
        IStoragePersistenceService storagePersistenceService,
        Dispatcher dispatcher,
        WindowsVoiceCatalog? windowsVoiceCatalog = null,
        IUdpRawLogDirectoryService? udpRawLogDirectoryService = null,
        IEventBus<RaceEvent>? raceEventBus = null,
        HistorySessionBrowserViewModel? historyBrowser = null)
    {
        _udpListener = udpListener ?? throw new ArgumentNullException(nameof(udpListener));
        _packetDispatcher = packetDispatcher ?? throw new ArgumentNullException(nameof(packetDispatcher));
        _sessionStateStore = sessionStateStore ?? throw new ArgumentNullException(nameof(sessionStateStore));
        _lapAnalyzer = lapAnalyzer ?? throw new ArgumentNullException(nameof(lapAnalyzer));
        _eventDetectionService = eventDetectionService ?? throw new ArgumentNullException(nameof(eventDetectionService));
        _aiAnalysisService = aiAnalysisService ?? throw new ArgumentNullException(nameof(aiAnalysisService));
        _appSettingsStore = appSettingsStore ?? throw new ArgumentNullException(nameof(appSettingsStore));
        _udpRawLogWriter = udpRawLogWriter ?? throw new ArgumentNullException(nameof(udpRawLogWriter));
        _ttsMessageFactory = ttsMessageFactory ?? throw new ArgumentNullException(nameof(ttsMessageFactory));
        _ttsQueue = ttsQueue ?? throw new ArgumentNullException(nameof(ttsQueue));
        _windowsVoiceCatalog = windowsVoiceCatalog ?? new WindowsVoiceCatalog();
        _storagePersistenceService = storagePersistenceService ?? throw new ArgumentNullException(nameof(storagePersistenceService));
        HistoryBrowser = historyBrowser ?? CreateNoOpHistoryBrowser();
        _raceEventBus = raceEventBus ?? new InMemoryEventBus<RaceEvent>();
        _raceEventSpeechSubscriber = new RaceEventSpeechSubscriber(
            _raceEventBus,
            _ttsMessageFactory,
            _ttsQueue,
            () => SessionModeFormatter.Resolve(_sessionStateStore.CaptureState().SessionType),
            BuildTtsOptions,
            LogRaceEventSubscriberWarning);
        _raceEventInsightBuffer = new RaceEventInsightBuffer(_raceEventBus);
        _udpRawLogDirectoryService = udpRawLogDirectoryService ?? new UdpRawLogDirectoryService();
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _currentLapChartBuilder = new CurrentLapChartBuilder();
        _trendChartBuilder = new TrendChartBuilder();
        _telemetryAnalysisSummaryBuilder = new TelemetryAnalysisSummaryBuilder();
        _lastPacketsPerSecondSampleAt = DateTimeOffset.UtcNow;

        ShellNavigationItems = new ObservableCollection<ShellNavigationItemViewModel>(
            ShellNavigationItemViewModel.CreateDefaultItems());
        _selectedShellNavigationItem = ShellNavigationItems[0];
        OpponentCars = new ObservableCollection<CarStateItemViewModel>();
        RecentLapSummaries = new ObservableCollection<LapSummaryItemViewModel>();
        EventLogs = new ObservableCollection<LogEntryViewModel>();
        AiTtsLogs = new ObservableCollection<LogEntryViewModel>();
        AiAnalysisLogs = new ObservableCollection<LogEntryViewModel>();
        LogEntries = new ObservableCollection<LogEntryViewModel>();
        OverviewEventSummaries = new ObservableCollection<LogEntryViewModel>();
        AvailableVoices = new ObservableCollection<string>();
        PostRaceAiCompletionModes = new ObservableCollection<PostRaceAiCompletionModeOptionViewModel>(
        [
            new()
            {
                Mode = PostRaceAiCompletionMode.Auto,
                DisplayName = "自动判断",
                Description = "收到正赛最终分类后自动生成总结。"
            },
            new()
            {
                Mode = PostRaceAiCompletionMode.Hold,
                DisplayName = "暂存不总结",
                Description = "中途存档或未跑完时保留状态，不上传 AI。"
            },
            new()
            {
                Mode = PostRaceAiCompletionMode.ForceComplete,
                DisplayName = "标记已完成",
                Description = "手动确认已跑完并生成赛后总结。"
            }
        ]);
        _selectedPostRaceAiCompletionMode = PostRaceAiCompletionModes[0];
        SpeedChartPanel = new ChartPanelViewModel();
        InputsChartPanel = new ChartPanelViewModel();
        FuelTrendChartPanel = new ChartPanelViewModel();
        TyreWearTrendChartPanel = new ChartPanelViewModel();
        ChartPlaceholders = new ObservableCollection<DashboardPlaceholderViewModel>
        {
            new() { Title = "速度曲线", Description = "后续接入实时速度与速度陷阱走势。" },
            new() { Title = "输入曲线", Description = "后续接入油门、刹车、转向时间序列。" },
            new() { Title = "轮胎窗口", Description = "后续接入温度、磨损与轮胎工作区间。" },
            new() { Title = "能量管理", Description = "后续接入 ERS、燃油与部署策略图表。" }
        };
        ResetChartPanels();

        var initialLogEntry = CreateLogEntry("System", "AI / TTS 日志已准备就绪。");
        AiTtsLogs.Add(initialLogEntry);
        AiAnalysisLogs.Add(CreateLogEntry("AI", "赛后 AI 总结等待完整正赛结束。"));
        LogEntries.Add(CreateLogEntry("System", "AI / TTS 日志已准备就绪。"));
        LoadAvailableVoices();
        RefreshUdpRawLogStatus();

        _startListeningCommand = new RelayCommand(() => _ = StartListeningAsync(), CanStartListening);
        _stopListeningCommand = new RelayCommand(() => _ = StopListeningAsync(), CanStopListening);
        _downloadLatestVersionCommand = new RelayCommand(OpenGitHubReleases);
        _toggleSidebarCommand = new RelayCommand(ToggleSidebar);
        _openUdpRawLogDirectoryCommand = new RelayCommand(OpenUdpRawLogDirectory);
        _generatePostRaceAiSummaryCommand = new RelayCommand(
            () =>
            {
                var sessionState = _sessionStateStore.CaptureState();
                _ = TriggerPostRaceAiAnalysisIfReadyAsync(sessionState, sessionState.PlayerCar, force: true);
            },
            () => CanGeneratePostRaceAiSummary);

        _udpListener.DatagramReceived += OnDatagramReceived;
        _udpListener.ReceiveFaulted += OnReceiveFaulted;
        _packetDispatcher.PacketDispatched += OnPacketDispatched;
        _storagePersistenceService.LogEmitted += OnStorageLogEmitted;

        _uiTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _uiTimer.Tick += OnUiTimerTick;
        _uiTimer.Start();

        _udpPortSaveTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = UdpPortSaveDebounceInterval
        };
        _udpPortSaveTimer.Tick += OnUdpPortSaveTimerTick;

        _ = LoadSettingsAsync();
    }

    /// <summary>
    /// Gets the window title.
    /// </summary>
    public string Title => AppTitleText;

    /// <summary>
    /// Gets the application title text with the dynamic assembly version.
    /// </summary>
    public string AppTitleText => $"F1 Telemetry {VersionInfo.CurrentVersion}";

    /// <summary>
    /// Gets the window subtitle.
    /// </summary>
    public string Subtitle => $"{VersionInfo.DisplayVersion} · 实时遥测助手";

    /// <summary>
    /// Gets the application version text displayed in the shell.
    /// </summary>
    public string ApplicationVersionText => VersionInfo.DisplayVersion;

    /// <summary>
    /// Gets the fixed shell navigation items.
    /// </summary>
    public ObservableCollection<ShellNavigationItemViewModel> ShellNavigationItems { get; }

    /// <summary>
    /// Gets the persisted history session browser.
    /// </summary>
    public HistorySessionBrowserViewModel HistoryBrowser { get; }

    /// <summary>
    /// Gets a value indicating whether the sidebar is expanded.
    /// </summary>
    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        private set
        {
            if (SetProperty(ref _isSidebarExpanded, value))
            {
                OnPropertyChanged(nameof(SidebarColumnWidth));
            }
        }
    }

    /// <summary>
    /// Gets the shell sidebar column width.
    /// </summary>
    public GridLength SidebarColumnWidth => new(IsSidebarExpanded ? ExpandedSidebarWidth : CollapsedSidebarWidth);

    /// <summary>
    /// Gets or sets the currently selected shell navigation item.
    /// </summary>
    public ShellNavigationItemViewModel? SelectedShellNavigationItem
    {
        get => _selectedShellNavigationItem;
        set
        {
            if (SetProperty(ref _selectedShellNavigationItem, value))
            {
                OnPropertyChanged(nameof(IsOverviewSelected));
                OnPropertyChanged(nameof(IsChartsSelected));
                OnPropertyChanged(nameof(IsLapHistorySelected));
                OnPropertyChanged(nameof(IsOpponentsSelected));
                OnPropertyChanged(nameof(IsLogsSelected));
                OnPropertyChanged(nameof(IsAiTtsSelected));
                OnPropertyChanged(nameof(IsSettingsSelected));
                OnPropertyChanged(nameof(IsPlaceholderNavigationSelected));
                OnPropertyChanged(nameof(IsLegacyDashboardSelected));
                OnPropertyChanged(nameof(SelectedShellNavigationTitle));
                RequestHistoryBrowserRefreshIfNeeded();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the overview page is selected.
    /// </summary>
    public bool IsOverviewSelected => IsSelectedShellNavigationKey("overview");

    /// <summary>
    /// Gets a value indicating whether the charts page is selected.
    /// </summary>
    public bool IsChartsSelected => IsSelectedShellNavigationKey("charts");

    /// <summary>
    /// Gets a value indicating whether the lap history page is selected.
    /// </summary>
    public bool IsLapHistorySelected => IsSelectedShellNavigationKey("lap-history") || IsSelectedShellNavigationKey("laps");

    /// <summary>
    /// Gets a value indicating whether the opponents page is selected.
    /// </summary>
    public bool IsOpponentsSelected => IsSelectedShellNavigationKey("opponents");

    /// <summary>
    /// Gets a value indicating whether the event logs page is selected.
    /// </summary>
    public bool IsLogsSelected => IsSelectedShellNavigationKey("event-logs") || IsSelectedShellNavigationKey("logs");

    /// <summary>
    /// Gets a value indicating whether the AI and TTS page is selected.
    /// </summary>
    public bool IsAiTtsSelected => IsSelectedShellNavigationKey("ai-tts");

    /// <summary>
    /// Gets a value indicating whether the settings page is selected.
    /// </summary>
    public bool IsSettingsSelected => IsSelectedShellNavigationKey("settings");

    /// <summary>
    /// Gets a value indicating whether a future shell page placeholder should be shown.
    /// </summary>
    public bool IsPlaceholderNavigationSelected =>
        !IsOverviewSelected &&
        !IsChartsSelected &&
        !IsLapHistorySelected &&
        !IsOpponentsSelected &&
        !IsLogsSelected &&
        !IsAiTtsSelected &&
        !IsSettingsSelected &&
        !IsLegacyDashboardSelected;

    /// <summary>
    /// Gets a value indicating whether the temporary legacy dashboard fallback is selected.
    /// </summary>
    public bool IsLegacyDashboardSelected => IsSelectedShellNavigationKey("legacy-dashboard");

    /// <summary>
    /// Gets the selected shell navigation title.
    /// </summary>
    public string SelectedShellNavigationTitle => SelectedShellNavigationItem?.Name ?? "实时概览";

    private bool IsSelectedShellNavigationKey(string key)
    {
        return string.Equals(SelectedShellNavigationItem?.Key, key, StringComparison.Ordinal);
    }

    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
    }

    /// <summary>
    /// Gets or sets a value indicating whether AI analysis is enabled.
    /// </summary>
    public bool AiEnabled
    {
        get => _aiEnabled;
        set
        {
            if (SetProperty(ref _aiEnabled, value))
            {
                _lastPostRaceAiSummaryKey = null;
                OnPropertyChanged(nameof(AiApiKeyStatusText));
                OnPropertyChanged(nameof(CanGeneratePostRaceAiSummary));
                _generatePostRaceAiSummaryCommand?.RaiseCanExecuteChanged();
                QueuePersistAiSettings();
            }
        }
    }

    /// <summary>
    /// Gets or sets the configured AI base URL.
    /// </summary>
    public string AiBaseUrl
    {
        get => _aiBaseUrl;
        set
        {
            if (SetProperty(ref _aiBaseUrl, value))
            {
                QueuePersistAiSettings();
            }
        }
    }

    /// <summary>
    /// Gets or sets the configured AI model.
    /// </summary>
    public string AiModel
    {
        get => _aiModel;
        set
        {
            if (SetProperty(ref _aiModel, value))
            {
                QueuePersistAiSettings();
            }
        }
    }

    /// <summary>
    /// Gets or sets the configured AI API key.
    /// </summary>
    public string AiApiKey
    {
        get => _aiApiKey;
        set
        {
            if (SetProperty(ref _aiApiKey, value))
            {
                _lastPostRaceAiSummaryKey = null;
                OnPropertyChanged(nameof(AiApiKeyStatusText));
                OnPropertyChanged(nameof(CanGeneratePostRaceAiSummary));
                _generatePostRaceAiSummaryCommand?.RaiseCanExecuteChanged();
                QueuePersistAiSettings();
            }
        }
    }

    /// <summary>
    /// Gets a safe API key status label for the UI.
    /// </summary>
    public string AiApiKeyStatusText => string.IsNullOrWhiteSpace(AiApiKey) ? "未配置" : "已配置";

    /// <summary>
    /// Gets the current AI settings save status.
    /// </summary>
    public string AiSettingsSaveStatusText
    {
        get => _aiSettingsSaveStatusText;
        private set => SetProperty(ref _aiSettingsSaveStatusText, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether raw UDP JSONL logging is enabled.
    /// </summary>
    public bool UdpRawLogEnabled
    {
        get => _udpRawLogEnabled;
        set
        {
            if (SetProperty(ref _udpRawLogEnabled, value))
            {
                ApplyUdpRawLogOptions();
                QueuePersistUdpRawLogOptions();
            }
        }
    }

    /// <summary>
    /// Gets the raw UDP log directory shown in Settings.
    /// </summary>
    public string UdpRawLogDirectoryText
    {
        get => _udpRawLogDirectoryText;
        private set => SetProperty(ref _udpRawLogDirectoryText, value);
    }

    /// <summary>
    /// Gets the current or most recent raw UDP log file path.
    /// </summary>
    public string UdpRawLogLastFilePathText
    {
        get => _udpRawLogLastFilePathText;
        private set => SetProperty(ref _udpRawLogLastFilePathText, value);
    }

    /// <summary>
    /// Gets the current or most recent raw UDP log file size.
    /// </summary>
    public string UdpRawLogLastFileSizeText
    {
        get => _udpRawLogLastFileSizeText;
        private set => SetProperty(ref _udpRawLogLastFileSizeText, value);
    }

    /// <summary>
    /// Gets the current or most recent raw UDP log file write time.
    /// </summary>
    public string UdpRawLogLastWriteTimeText
    {
        get => _udpRawLogLastWriteTimeText;
        private set => SetProperty(ref _udpRawLogLastWriteTimeText, value);
    }

    /// <summary>
    /// Gets the raw UDP log state summary.
    /// </summary>
    public string UdpRawLogStatusText
    {
        get => _udpRawLogStatusText;
        private set => SetProperty(ref _udpRawLogStatusText, value);
    }

    /// <summary>
    /// Gets the raw UDP packet count written in this app session.
    /// </summary>
    public long UdpRawLogWrittenPacketCount
    {
        get => _udpRawLogWrittenPacketCount;
        private set => SetProperty(ref _udpRawLogWrittenPacketCount, value);
    }

    /// <summary>
    /// Gets the raw UDP packet count dropped in this app session.
    /// </summary>
    public long UdpRawLogDroppedPacketCount
    {
        get => _udpRawLogDroppedPacketCount;
        private set => SetProperty(ref _udpRawLogDroppedPacketCount, value);
    }

    /// <summary>
    /// Gets the latest raw UDP log write error.
    /// </summary>
    public string UdpRawLogLastErrorText
    {
        get => _udpRawLogLastErrorText;
        private set => SetProperty(ref _udpRawLogLastErrorText, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether TTS playback is enabled.
    /// </summary>
    public bool TtsEnabled
    {
        get => _ttsEnabled;
        set
        {
            if (SetProperty(ref _ttsEnabled, value))
            {
                _ttsQueue.UpdateOptions(BuildTtsOptions());
                QueuePersistTtsSettings();
            }
        }
    }

    /// <summary>
    /// Gets the Windows speech voices available for TTS selection.
    /// </summary>
    public ObservableCollection<string> AvailableVoices { get; }

    /// <summary>
    /// Gets a value indicating whether Windows voices were discovered.
    /// </summary>
    public bool HasAvailableVoices => AvailableVoices.Count > 0;

    /// <summary>
    /// Gets the current Windows voice discovery status.
    /// </summary>
    public string TtsVoiceStatusText
    {
        get => _ttsVoiceStatusText;
        private set => SetProperty(ref _ttsVoiceStatusText, value);
    }

    /// <summary>
    /// Gets or sets the configured Windows voice name.
    /// </summary>
    public string TtsVoiceName
    {
        get => _ttsVoiceName;
        set
        {
            if (SetProperty(ref _ttsVoiceName, value))
            {
                _ttsQueue.UpdateOptions(BuildTtsOptions());
                QueuePersistTtsSettings();
            }
        }
    }

    /// <summary>
    /// Gets or sets the TTS playback volume.
    /// </summary>
    public int TtsVolume
    {
        get => _ttsVolume;
        set
        {
            var normalizedValue = Math.Clamp(value, 0, 100);
            if (SetProperty(ref _ttsVolume, normalizedValue))
            {
                _ttsQueue.UpdateOptions(BuildTtsOptions());
                QueuePersistTtsSettings();
            }
        }
    }

    /// <summary>
    /// Gets or sets the TTS playback rate.
    /// </summary>
    public int TtsRate
    {
        get => _ttsRate;
        set
        {
            var normalizedValue = Math.Clamp(value, -10, 10);
            if (SetProperty(ref _ttsRate, normalizedValue))
            {
                _ttsQueue.UpdateOptions(BuildTtsOptions());
                QueuePersistTtsSettings();
            }
        }
    }

    /// <summary>
    /// Gets or sets the AI API key for UI binding.
    /// </summary>
    public string ApiKey
    {
        get => AiApiKey;
        set => AiApiKey = value;
    }

    /// <summary>
    /// Gets or sets the AI base URL for UI binding.
    /// </summary>
    public string BaseUrl
    {
        get => AiBaseUrl;
        set => AiBaseUrl = value;
    }

    /// <summary>
    /// Gets or sets the AI model name for UI binding.
    /// </summary>
    public string Model
    {
        get => AiModel;
        set => AiModel = value;
    }

    /// <summary>
    /// Gets the API key state text for UI binding.
    /// </summary>
    public string ApiKeyStateText => AiApiKeyStatusText;

    /// <summary>
    /// Gets the projected opponent rows.
    /// </summary>
    public ObservableCollection<CarStateItemViewModel> OpponentCars { get; }

    /// <summary>
    /// Gets the projected recent lap summaries shown in the lap table.
    /// </summary>
    public ObservableCollection<LapSummaryItemViewModel> RecentLapSummaries { get; }

    /// <summary>
    /// Gets the event log entries.
    /// </summary>
    public ObservableCollection<LogEntryViewModel> EventLogs { get; }

    /// <summary>
    /// Gets the full unified log entries shown in LogsView.
    /// </summary>
    public ObservableCollection<LogEntryViewModel> LogEntries { get; }

    /// <summary>
    /// Gets the compressed, prioritized event summaries shown on the Overview page.
    /// </summary>
    public ObservableCollection<LogEntryViewModel> OverviewEventSummaries { get; }

    /// <summary>
    /// Gets the unified AI, TTS, and system log entries.
    /// </summary>
    public ObservableCollection<LogEntryViewModel> AiTtsLogs { get; }

    /// <summary>
    /// Gets the post-race AI summary lifecycle entries shown on the AI broadcast page.
    /// </summary>
    public ObservableCollection<LogEntryViewModel> AiAnalysisLogs { get; }

    /// <summary>
    /// Gets the selectable post-race AI completion modes.
    /// </summary>
    public ObservableCollection<PostRaceAiCompletionModeOptionViewModel> PostRaceAiCompletionModes { get; }

    /// <summary>
    /// Gets or sets the selected post-race AI completion mode.
    /// </summary>
    public PostRaceAiCompletionModeOptionViewModel? SelectedPostRaceAiCompletionMode
    {
        get => _selectedPostRaceAiCompletionMode;
        set
        {
            if (SetProperty(ref _selectedPostRaceAiCompletionMode, value))
            {
                RefreshPostRaceAiStatus(_sessionStateStore.CaptureState());
                if (value?.Mode == PostRaceAiCompletionMode.ForceComplete)
                {
                    var sessionState = _sessionStateStore.CaptureState();
                    _ = TriggerPostRaceAiAnalysisIfReadyAsync(sessionState, sessionState.PlayerCar, force: true);
                }
            }
        }
    }

    /// <summary>
    /// Gets the current post-race AI summary state.
    /// </summary>
    public string PostRaceAiStatusText
    {
        get => _postRaceAiStatusText;
        private set => SetProperty(ref _postRaceAiStatusText, value);
    }

    /// <summary>
    /// Gets the current completion evidence used for post-race AI gating.
    /// </summary>
    public string PostRaceAiCompletionText
    {
        get => _postRaceAiCompletionText;
        private set => SetProperty(ref _postRaceAiCompletionText, value);
    }

    /// <summary>
    /// Gets a value indicating whether the manual post-race AI summary command can run.
    /// </summary>
    public bool CanGeneratePostRaceAiSummary =>
        AiEnabled && !_isAiAnalysisRunning && CaptureAiSummaryLap(_sessionStateStore.CaptureState()) is not null;

    /// <summary>
    /// Gets the current-lap speed chart panel state.
    /// </summary>
    public ChartPanelViewModel SpeedChartPanel { get; }

    /// <summary>
    /// Gets the current-lap throttle and brake chart panel state.
    /// </summary>
    public ChartPanelViewModel InputsChartPanel { get; }

    /// <summary>
    /// Gets the multi-lap fuel trend chart panel state.
    /// </summary>
    public ChartPanelViewModel FuelTrendChartPanel { get; }

    /// <summary>
    /// Gets the multi-lap tyre wear trend chart panel state.
    /// </summary>
    public ChartPanelViewModel TyreWearTrendChartPanel { get; }

    /// <summary>
    /// Gets the chart placeholder panels.
    /// </summary>
    public ObservableCollection<DashboardPlaceholderViewModel> ChartPlaceholders { get; }

    /// <summary>
    /// Gets the start-listening command.
    /// </summary>
    public ICommand StartListeningCommand => _startListeningCommand;

    /// <summary>
    /// Gets the stop-listening command.
    /// </summary>
    public ICommand StopListeningCommand => _stopListeningCommand;

    /// <summary>
    /// Gets the command that opens the GitHub Releases download page.
    /// </summary>
    public ICommand DownloadLatestVersionCommand => _downloadLatestVersionCommand;

    /// <summary>
    /// Gets the command that expands or collapses the sidebar.
    /// </summary>
    public ICommand ToggleSidebarCommand => _toggleSidebarCommand;

    /// <summary>
    /// Gets the command that opens the raw UDP log directory.
    /// </summary>
    public ICommand OpenUdpRawLogDirectoryCommand => _openUdpRawLogDirectoryCommand;

    /// <summary>
    /// Gets the command that manually generates a post-race AI summary from staged race data.
    /// </summary>
    public ICommand GeneratePostRaceAiSummaryCommand => _generatePostRaceAiSummaryCommand;

    /// <summary>
    /// Gets or sets the UDP port text.
    /// </summary>
    public string PortText
    {
        get => _portText;
        set
        {
            if (SetProperty(ref _portText, value))
            {
                OnPropertyChanged(nameof(SidebarUdpPortText));
                OnPropertyChanged(nameof(SidebarUdpStatusTooltip));
                _startListeningCommand.RaiseCanExecuteChanged();
                QueuePersistUdpSettingsIfValid();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the listener is running.
    /// </summary>
    public bool IsListening
    {
        get => _isListening;
        private set
        {
            if (SetProperty(ref _isListening, value))
            {
                OnPropertyChanged(nameof(ConnectionStateText));
                OnPropertyChanged(nameof(SidebarUdpStatusTooltip));
                _startListeningCommand.RaiseCanExecuteChanged();
                _stopListeningCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether packets have been received recently.
    /// </summary>
    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetProperty(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(ConnectionStateText));
                OnPropertyChanged(nameof(SidebarUdpStatusTooltip));
            }
        }
    }

    /// <summary>
    /// Gets the connection state label.
    /// </summary>
    public string ConnectionStateText =>
        IsConnected ? "已连接" : IsListening ? "等待数据" : "未启动";

    /// <summary>
    /// Gets the compact UDP port text shown in the sidebar.
    /// </summary>
    public string SidebarUdpPortText => $"使用中: {GetSidebarUdpPortText()} UDP";

    /// <summary>
    /// Gets the full UDP sidebar tooltip.
    /// </summary>
    public string SidebarUdpStatusTooltip => $"{ConnectionStateText}，{SidebarUdpPortText}";

    /// <summary>
    /// Gets the active UDP listening port.
    /// </summary>
    public int? ListeningPort
    {
        get => _listeningPort;
        private set
        {
            if (SetProperty(ref _listeningPort, value))
            {
                OnPropertyChanged(nameof(ListeningPortText));
                OnPropertyChanged(nameof(SidebarUdpPortText));
                OnPropertyChanged(nameof(SidebarUdpStatusTooltip));
            }
        }
    }

    /// <summary>
    /// Gets the display text for the active UDP port.
    /// </summary>
    public string ListeningPortText => ListeningPort?.ToString() ?? "-";

    /// <summary>
    /// Gets the total UDP packet count.
    /// </summary>
    public long TotalPacketCount
    {
        get => _totalPacketCount;
        private set => SetProperty(ref _totalPacketCount, value);
    }

    /// <summary>
    /// Gets the current packets per second.
    /// </summary>
    public int PacketsPerSecond
    {
        get => _packetsPerSecond;
        private set => SetProperty(ref _packetsPerSecond, value);
    }

    /// <summary>
    /// Gets the latest status message.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Gets the current track summary.
    /// </summary>
    public string TrackText
    {
        get => _trackText;
        private set => SetProperty(ref _trackText, value);
    }

    /// <summary>
    /// Gets the current session type summary.
    /// </summary>
    public string SessionTypeText
    {
        get => _sessionTypeText;
        private set => SetProperty(ref _sessionTypeText, value);
    }

    /// <summary>
    /// Gets the current lap summary.
    /// </summary>
    public string LapText
    {
        get => _lapText;
        private set => SetProperty(ref _lapText, value);
    }

    /// <summary>
    /// Gets the weather summary.
    /// </summary>
    public string WeatherText
    {
        get => _weatherText;
        private set => SetProperty(ref _weatherText, value);
    }

    /// <summary>
    /// Gets the player display name.
    /// </summary>
    public string PlayerName
    {
        get => _playerName;
        private set => SetProperty(ref _playerName, value);
    }

    /// <summary>
    /// Gets the player current lap summary.
    /// </summary>
    public string PlayerCurrentLapText
    {
        get => _playerCurrentLapText;
        private set => SetProperty(ref _playerCurrentLapText, value);
    }

    /// <summary>
    /// Gets the player last-lap summary.
    /// </summary>
    public string PlayerLastLapText
    {
        get => _playerLastLapText;
        private set => SetProperty(ref _playerLastLapText, value);
    }

    /// <summary>
    /// Gets the player best-lap summary.
    /// </summary>
    public string PlayerBestLapText
    {
        get => _playerBestLapText;
        private set => SetProperty(ref _playerBestLapText, value);
    }

    /// <summary>
    /// Gets the player position summary.
    /// </summary>
    public string PlayerPositionText
    {
        get => _playerPositionText;
        private set => SetProperty(ref _playerPositionText, value);
    }

    /// <summary>
    /// Gets the player gap summary.
    /// </summary>
    public string PlayerGapText
    {
        get => _playerGapText;
        private set => SetProperty(ref _playerGapText, value);
    }

    /// <summary>
    /// Gets the player fuel summary.
    /// </summary>
    public string PlayerFuelText
    {
        get => _playerFuelText;
        private set => SetProperty(ref _playerFuelText, value);
    }

    /// <summary>
    /// Gets the player ERS summary.
    /// </summary>
    public string PlayerErsText
    {
        get => _playerErsText;
        private set => SetProperty(ref _playerErsText, value);
    }

    /// <summary>
    /// Gets the player tyre summary.
    /// </summary>
    public string PlayerTyreText
    {
        get => _playerTyreText;
        private set => SetProperty(ref _playerTyreText, value);
    }

    /// <summary>
    /// Gets the player tyre age summary.
    /// </summary>
    public string PlayerTyreAgeText
    {
        get => _playerTyreAgeText;
        private set => SetProperty(ref _playerTyreAgeText, value);
    }

    /// <summary>
    /// Gets the overview speed summary.
    /// </summary>
    public string OverviewSpeedText
    {
        get => _overviewSpeedText;
        private set => SetProperty(ref _overviewSpeedText, value);
    }

    /// <summary>
    /// Gets the overview gear summary.
    /// </summary>
    public string OverviewGearText
    {
        get => _overviewGearText;
        private set => SetProperty(ref _overviewGearText, value);
    }

    /// <summary>
    /// Gets the overview throttle summary.
    /// </summary>
    public string OverviewThrottleText
    {
        get => _overviewThrottleText;
        private set => SetProperty(ref _overviewThrottleText, value);
    }

    /// <summary>
    /// Gets the overview brake summary.
    /// </summary>
    public string OverviewBrakeText
    {
        get => _overviewBrakeText;
        private set => SetProperty(ref _overviewBrakeText, value);
    }

    /// <summary>
    /// Gets the overview DRS summary.
    /// </summary>
    public string OverviewDrsText
    {
        get => _overviewDrsText;
        private set => SetProperty(ref _overviewDrsText, value);
    }

    /// <summary>
    /// Gets the overview tyre wear summary.
    /// </summary>
    public string OverviewTyreWearText
    {
        get => _overviewTyreWearText;
        private set => SetProperty(ref _overviewTyreWearText, value);
    }

    /// <summary>
    /// Gets the overview player-car damage summary.
    /// </summary>
    public string OverviewDamageText
    {
        get => _overviewDamageText;
        private set => SetProperty(ref _overviewDamageText, value);
    }

    /// <summary>
    /// Gets the overview tyre temperature placeholder.
    /// </summary>
    public string OverviewTyreTemperatureText => "暂未接入";

    /// <summary>
    /// Gets the overview tyre pressure placeholder.
    /// </summary>
    public string OverviewTyrePressureText => "暂未接入";

    /// <summary>
    /// Gets the overview key opponent summary.
    /// </summary>
    public string OverviewKeyOpponentText
    {
        get => _overviewKeyOpponentText;
        private set => SetProperty(ref _overviewKeyOpponentText, value);
    }

    /// <summary>
    /// Gets the current session-specific overview focus text.
    /// </summary>
    public string OverviewSessionFocusText
    {
        get => _overviewSessionFocusText;
        private set => SetProperty(ref _overviewSessionFocusText, value);
    }

    /// <summary>
    /// Gets the latest AI suggestion summary for the overview page.
    /// </summary>
    public string OverviewRecentAiSuggestionText
    {
        get => _overviewRecentAiSuggestionText;
        private set => SetProperty(ref _overviewRecentAiSuggestionText, value);
    }

    /// <summary>
    /// Gets the latest TTS playback summary for the overview page.
    /// </summary>
    public string OverviewRecentTtsStatusText
    {
        get => _overviewRecentTtsStatusText;
        private set => SetProperty(ref _overviewRecentTtsStatusText, value);
    }

    /// <summary>
    /// Gets the highlighted best-lap summary text.
    /// </summary>
    public string BestLapSummaryText
    {
        get => _bestLapSummaryText;
        private set => SetProperty(ref _bestLapSummaryText, value);
    }

    /// <summary>
    /// Gets the highlighted last-lap summary text.
    /// </summary>
    public string LastLapSummaryText
    {
        get => _lastLapSummaryText;
        private set => SetProperty(ref _lastLapSummaryText, value);
    }

    /// <summary>
    /// Releases background workers and subscriptions before the shell exits.
    /// </summary>
    public Task ShutdownAsync()
    {
        lock (_shutdownGate)
        {
            _shutdownTask ??= ShutdownCoreAsync();
            return _shutdownTask;
        }
    }

    /// <summary>
    /// Releases the UDP subscriptions and timer resources owned by the view model.
    /// </summary>
    public void Dispose()
    {
        ShutdownAsync().GetAwaiter().GetResult();
    }

    private async Task ShutdownCoreAsync()
    {
        if (_disposed)
        {
            return;
        }

        StopUdpPortSaveTimer();
        await PersistCurrentUdpSettingsIfNeededAsync(force: true, CancellationToken.None);

        _disposed = true;
        TryCancelLifecycle();
        StopUiTimer();
        StopUdpPortSaveTimer(unsubscribe: true);

        _udpListener.DatagramReceived -= OnDatagramReceived;
        _udpListener.ReceiveFaulted -= OnReceiveFaulted;
        _packetDispatcher.PacketDispatched -= OnPacketDispatched;
        _storagePersistenceService.LogEmitted -= OnStorageLogEmitted;

        try
        {
            _raceEventSpeechSubscriber.Dispose();
        }
        catch
        {
        }

        try
        {
            _raceEventInsightBuffer.Dispose();
        }
        catch
        {
        }

        try
        {
            await _udpListener.DisposeAsync().AsTask().ConfigureAwait(false);
        }
        catch
        {
        }

        try
        {
            await _udpRawLogWriter.DisposeAsync().AsTask().ConfigureAwait(false);
        }
        catch
        {
        }

        try
        {
            _ttsQueue.Dispose();
        }
        catch
        {
        }

        try
        {
            await _storagePersistenceService.DisposeAsync().AsTask().ConfigureAwait(false);
        }
        catch
        {
        }
        finally
        {
            try
            {
                _lifecycleCts.Dispose();
            }
            catch
            {
            }

            try
            {
                _settingsGate.Dispose();
            }
            catch
            {
            }
        }
    }

    private void TryCancelLifecycle()
    {
        try
        {
            _lifecycleCts.Cancel();
        }
        catch
        {
        }
    }

    private void RequestHistoryBrowserRefreshIfNeeded()
    {
        if (_hasRequestedHistoryLoad || !IsLapHistorySelected)
        {
            return;
        }

        _hasRequestedHistoryLoad = true;
        _ = RefreshHistoryBrowserAsync();
    }

    private async Task RefreshHistoryBrowserAsync()
    {
        try
        {
            await HistoryBrowser.RefreshSessionsAsync(_lifecycleCts.Token);
        }
        catch (OperationCanceledException) when (_lifecycleCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to refresh history sessions: {ex}");
        }
    }

    private static HistorySessionBrowserViewModel CreateNoOpHistoryBrowser()
    {
        return new HistorySessionBrowserViewModel(new NoOpSessionRepository(), new NoOpLapRepository());
    }

    private void StopUiTimer()
    {
        void StopTimer()
        {
            _uiTimer.Stop();
            _uiTimer.Tick -= OnUiTimerTick;
        }

        if (_uiTimer.Dispatcher.CheckAccess())
        {
            StopTimer();
            return;
        }

        _uiTimer.Dispatcher.Invoke(StopTimer);
    }

    private void StopUdpPortSaveTimer(bool unsubscribe = false)
    {
        void StopTimer()
        {
            _udpPortSaveTimer.Stop();
            if (unsubscribe)
            {
                _udpPortSaveTimer.Tick -= OnUdpPortSaveTimerTick;
            }
        }

        if (_udpPortSaveTimer.Dispatcher.CheckAccess())
        {
            StopTimer();
            return;
        }

        _udpPortSaveTimer.Dispatcher.Invoke(StopTimer);
    }

    private void QueuePersistUdpSettingsIfValid()
    {
        if (_isApplyingSettings || _disposed)
        {
            return;
        }

        if (!TryParseUdpListenPort(PortText, out var port))
        {
            StopUdpPortSaveTimer();
            StatusMessage = "监听端口无效，请输入 1 到 65535 之间的端口。";
            return;
        }

        _lastValidUdpListenPort = port;
        if (port == _lastSavedUdpListenPort)
        {
            StopUdpPortSaveTimer();
            return;
        }

        RestartUdpPortSaveTimer();
    }

    private void RestartUdpPortSaveTimer()
    {
        void RestartTimer()
        {
            _udpPortSaveTimer.Stop();
            _udpPortSaveTimer.Start();
        }

        if (_udpPortSaveTimer.Dispatcher.CheckAccess())
        {
            RestartTimer();
            return;
        }

        _udpPortSaveTimer.Dispatcher.Invoke(RestartTimer);
    }

    private void OnUdpPortSaveTimerTick(object? sender, EventArgs e)
    {
        StopUdpPortSaveTimer();
        _ = PersistCurrentUdpSettingsIfNeededAsync(force: false, CancellationToken.None);
    }

    private async Task<bool> PersistCurrentUdpSettingsIfNeededAsync(bool force, CancellationToken cancellationToken)
    {
        if (!TryParseUdpListenPort(PortText, out var port))
        {
            return false;
        }

        return await PersistUdpSettingsAsync(port, force, cancellationToken);
    }

    private async Task<bool> PersistUdpSettingsAsync(int port, bool force, CancellationToken cancellationToken)
    {
        if (port is < UdpSettings.MinListenPort or > UdpSettings.MaxListenPort)
        {
            return false;
        }

        if (!force && _disposed)
        {
            return false;
        }

        if (port == _lastSavedUdpListenPort)
        {
            _lastValidUdpListenPort = port;
            return true;
        }

        var saveVersion = Interlocked.Increment(ref _udpSettingsSaveVersion);
        var gateHeld = false;
        try
        {
            await _settingsGate.WaitAsync(cancellationToken);
            gateHeld = true;
            if (!force && (saveVersion < Volatile.Read(ref _udpSettingsSaveVersion) || _disposed))
            {
                return false;
            }

            if (port == _lastSavedUdpListenPort)
            {
                _lastValidUdpListenPort = port;
                return true;
            }

            await _appSettingsStore.SaveUdpSettingsAsync(new UdpSettings { ListenPort = port }, CancellationToken.None);
            if (force || saveVersion == Volatile.Read(ref _udpSettingsSaveVersion))
            {
                _lastSavedUdpListenPort = port;
                _lastValidUdpListenPort = port;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            EnqueueAiTtsLog("System", $"UDP 端口设置保存失败：{ex.Message}");
            return false;
        }
        finally
        {
            if (gateHeld)
            {
                _settingsGate.Release();
            }
        }
    }

    private static bool TryParseUdpListenPort(string? value, out int port)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out port) &&
               port is >= UdpSettings.MinListenPort and <= UdpSettings.MaxListenPort;
    }

    private static int NormalizeUdpListenPort(int listenPort)
    {
        return listenPort is >= UdpSettings.MinListenPort and <= UdpSettings.MaxListenPort
            ? listenPort
            : UdpSettings.DefaultListenPort;
    }

    private string GetSidebarUdpPortText()
    {
        if (ListeningPort is { } listeningPort)
        {
            return listeningPort.ToString(CultureInfo.InvariantCulture);
        }

        return TryParseUdpListenPort(PortText, out var port)
            ? port.ToString(CultureInfo.InvariantCulture)
            : _lastValidUdpListenPort.ToString(CultureInfo.InvariantCulture);
    }

    private bool CanStartListening()
    {
        return !_isBusy && !IsListening;
    }

    private bool CanStopListening()
    {
        return !_isBusy && IsListening;
    }

    private void OpenGitHubReleases()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = VersionInfo.GitHubReleasesUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            EnqueueAiTtsLog("System", $"打开发布页失败：{ex.Message}");
        }
    }

    private async void OpenUdpRawLogDirectory()
    {
        try
        {
            var directoryPath = _udpRawLogWriter.Status.DirectoryPath;
            var result = await Task
                .Run(() => _udpRawLogDirectoryService.OpenDirectory(directoryPath))
                .ConfigureAwait(false);

            UpdateUdpRawLogUi(() =>
            {
                _udpRawLogDirectoryOpenErrorText = result.Succeeded ? string.Empty : result.ErrorMessage;
                RefreshUdpRawLogStatus();
            });
        }
        catch (Exception ex)
        {
            UpdateUdpRawLogUi(() =>
            {
                _udpRawLogDirectoryOpenErrorText = $"打开日志目录失败：{ex.Message}";
                RefreshUdpRawLogStatus();
            });
        }
    }

    private async Task StartListeningAsync()
    {
        if (!TryParseUdpListenPort(PortText, out var port))
        {
            StatusMessage = "监听端口无效，请输入 1 到 65535 之间的端口。";
            EnqueueEventLog("系统", StatusMessage);
            return;
        }

        _isBusy = true;
        _startListeningCommand.RaiseCanExecuteChanged();
        _stopListeningCommand.RaiseCanExecuteChanged();

        try
        {
            StopUdpPortSaveTimer();
            await PersistUdpSettingsAsync(port, force: true, CancellationToken.None);
            await _udpListener.StartAsync(port, _lifecycleCts.Token);
            ListeningPort = _udpListener.ListeningPort;
            IsListening = _udpListener.IsListening;
            if (IsListening)
            {
                _activeSessionUid = null;
                _lastPostRaceAiSummaryKey = null;
                _lastStagedPostRaceAiKey = null;
                _lastPersistedLapKey = null;
                _lastTrendRefreshLapNumber = null;
                ResetChartPanels();
            }
            IsConnected = false;
            Interlocked.Exchange(ref _lastPacketReceivedUnixMs, -1);
            _lastPacketsPerSecondSampleAt = DateTimeOffset.UtcNow;
            _lastPacketsPerSecondSampleCount = Interlocked.Read(ref _receivedPacketCount);
            PacketsPerSecond = 0;
            StatusMessage = $"UDP 监听已启动，端口 {ListeningPortText}。";
            EnqueueEventLog("系统", StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"启动 UDP 监听失败：{ex.Message}";
            EnqueueEventLog("异常", StatusMessage);
        }
        finally
        {
            _isBusy = false;
            _startListeningCommand.RaiseCanExecuteChanged();
            _stopListeningCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task StopListeningAsync()
    {
        _isBusy = true;
        _startListeningCommand.RaiseCanExecuteChanged();
        _stopListeningCommand.RaiseCanExecuteChanged();

        try
        {
            await _udpListener.StopAsync(_lifecycleCts.Token);
            await _storagePersistenceService.CompleteActiveSessionAsync(_lifecycleCts.Token);
            IsListening = false;
            IsConnected = false;
            ListeningPort = null;
            Interlocked.Exchange(ref _lastPacketReceivedUnixMs, -1);
            PacketsPerSecond = 0;
            MarkIncompleteRaceAsStaged(_sessionStateStore.CaptureState(), "UDP 监听已停止，未收到最终分类。");
            _activeSessionUid = null;
            _lastPostRaceAiSummaryKey = null;
            _lastStagedPostRaceAiKey = null;
            _lastPersistedLapKey = null;
            _lastTrendRefreshLapNumber = null;
            ResetChartPanels();
            StatusMessage = "UDP 监听已停止。";
            EnqueueEventLog("系统", StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"停止 UDP 监听失败：{ex.Message}";
            EnqueueEventLog("异常", StatusMessage);
        }
        finally
        {
            _isBusy = false;
            _startListeningCommand.RaiseCanExecuteChanged();
            _stopListeningCommand.RaiseCanExecuteChanged();
        }
    }

    private void OnDatagramReceived(object? sender, UdpDatagram datagram)
    {
        Interlocked.Increment(ref _receivedPacketCount);
        Interlocked.Exchange(ref _lastPacketReceivedUnixMs, datagram.ReceivedAt.ToUnixTimeMilliseconds());
        _udpRawLogWriter.TryEnqueue(datagram);

        if (!_packetDispatcher.TryDispatch(datagram, out var error) && !string.IsNullOrWhiteSpace(error))
        {
            EnqueueEventLog("协议", $"Header 解析失败：{error}");
        }
    }

    private void OnReceiveFaulted(object? sender, Exception exception)
    {
        EnqueueEventLog("异常", $"UDP 接收异常：{exception.Message}");
    }

    private void OnPacketDispatched(
        object? sender,
        PacketDispatchResult<PacketId, PacketHeader> dispatchResult)
    {
        if (dispatchResult.PacketId != PacketId.Session)
        {
            return;
        }

        var incomingSessionUid = dispatchResult.Packet.SessionUid;
        if (_activeSessionUid == incomingSessionUid)
        {
            return;
        }

        MarkIncompleteRaceAsStaged(_sessionStateStore.CaptureState(), "检测到新的 UDP session，上一场正赛未收到最终分类。");
        _sessionStateStore.Reset();
        _eventDetectionService.Reset();
        _lapAnalyzer.ResetForSession(incomingSessionUid);
        _lastPostRaceAiSummaryKey = null;
        _lastStagedPostRaceAiKey = null;
        _lastPersistedLapKey = null;
        _lastTrendRefreshLapNumber = null;
        _lastEventCode = null;
        _raceEventInsightBuffer.Reset();
        _ttsMessageFactory.Reset();
        _activeSessionUid = incomingSessionUid;
        ResetChartPanels();
        EnqueueEventLog("会话", $"检测到会话切换：SessionUid={incomingSessionUid}");
        EnqueueAiTtsLog("System", $"已切换到新会话（UID {incomingSessionUid}），圈历史已清空。");
    }

    private void OnStorageLogEmitted(object? sender, string message)
    {
        EnqueueEventLog("存储", message);
    }

    private void OnUiTimerTick(object? sender, EventArgs e)
    {
        DrainDetectedRaceEvents();
        DrainTtsPlaybackRecords();
        DrainPendingEventLogs();
        DrainPendingAiTtsLogs();
        DrainPendingAiAnalysisLogs();
        RefreshConnectionState();
        RefreshCounters();
        RefreshUdpRawLogStatus();
        RefreshCentralState();
    }

    private void DrainDetectedRaceEvents()
    {
        var raceEvents = _eventDetectionService
            .DrainPendingEvents()
            .OrderByDescending(GetRaceEventDrainPriority)
            .ThenBy(raceEvent => raceEvent.Timestamp)
            .ToArray();

        foreach (var raceEvent in raceEvents)
        {
            EnqueueEventLog(BuildEventCategory(raceEvent), raceEvent.Message);
            _storagePersistenceService.EnqueueRaceEvent(raceEvent);
            PublishRaceEvent(raceEvent);
        }
    }

    private void PublishRaceEvent(RaceEvent raceEvent)
    {
        try
        {
            _raceEventBus.Publish(raceEvent);
        }
        catch (AggregateException ex)
        {
            EnqueueEventLog("System", $"EventBus 发布 RaceEvent 失败：{ex.InnerExceptions.Count} 个订阅者异常。");
        }
        catch (Exception ex)
        {
            EnqueueEventLog("System", $"EventBus 发布 RaceEvent 失败：{ex.Message}");
        }
    }

    private void LogRaceEventSubscriberWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            var normalizedMessage = message
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            if (normalizedMessage.Length > 120)
            {
                normalizedMessage = normalizedMessage[..120] + "...";
            }

            EnqueueAiTtsLog("System", $"EventBus/TTS：{normalizedMessage}");
        }
        catch
        {
        }
    }

    private void DrainTtsPlaybackRecords()
    {
        foreach (var record in _ttsQueue.DrainPendingRecords())
        {
            EnqueueAiTtsLog(record.Source, record.Message);
        }
    }

    private void DrainPendingEventLogs()
    {
        while (true)
        {
            LogEntryViewModel? logEntry;
            lock (_pendingEventLogsLock)
            {
                if (!_pendingEventLogs.TryDequeue(out logEntry))
                {
                    break;
                }
            }

            EventLogs.Insert(0, logEntry);
            LogEntries.Insert(0, logEntry);

            while (EventLogs.Count > MaxLogEntries)
            {
                EventLogs.RemoveAt(EventLogs.Count - 1);
            }

            TrimUnifiedLogEntries();
            RebuildOverviewEventSummaries();
        }
    }

    private void RefreshConnectionState()
    {
        if (!IsListening)
        {
            IsConnected = false;
            return;
        }

        var lastPacketReceivedUnixMs = Interlocked.Read(ref _lastPacketReceivedUnixMs);
        if (lastPacketReceivedUnixMs < 0)
        {
            IsConnected = false;
            return;
        }

        var elapsedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastPacketReceivedUnixMs;
        IsConnected = elapsedMs <= 3000;
    }

    private void RefreshCounters()
    {
        TotalPacketCount = Interlocked.Read(ref _receivedPacketCount);

        var now = DateTimeOffset.UtcNow;
        if (now - _lastPacketsPerSecondSampleAt < TimeSpan.FromSeconds(1))
        {
            return;
        }

        var currentCount = Interlocked.Read(ref _receivedPacketCount);
        PacketsPerSecond = (int)Math.Max(0, currentCount - _lastPacketsPerSecondSampleCount);
        _lastPacketsPerSecondSampleCount = currentCount;
        _lastPacketsPerSecondSampleAt = now;
    }

    private void RefreshCentralState()
    {
        var sessionState = _sessionStateStore.CaptureState();
        var playerCar = sessionState.PlayerCar;
        var sessionMode = SessionModeFormatter.Resolve(sessionState.SessionType);

        TrackText = BuildTrackText(sessionState.TrackId);
        SessionTypeText = SessionModeFormatter.FormatDisplayName(sessionMode);
        OverviewSessionFocusText = SessionModeFormatter.FormatFocus(sessionMode);
        WeatherText = BuildWeatherText(sessionState);
        LapText = BuildLapText(sessionState, playerCar);
        UpdatePlayerCard(sessionState, playerCar);
        RebuildOpponentCars(sessionState.Opponents, playerCar);
        RefreshLapHistory();
        PersistLatestLapIfNeeded();
        TrackLatestEvent(sessionState.LastEventCode);
        RefreshPostRaceAiStatus(sessionState);
        _ = TriggerPostRaceAiAnalysisIfReadyAsync(sessionState, playerCar);
    }

    private void UpdatePlayerCard(SessionState sessionState, CarSnapshot? playerCar)
    {
        if (playerCar is null)
        {
            PlayerName = "等待玩家车辆状态。";
            PlayerCurrentLapText = "-";
            PlayerLastLapText = "-";
            PlayerBestLapText = "-";
            PlayerPositionText = "-";
            PlayerGapText = "-";
            PlayerFuelText = "-";
            PlayerErsText = "-";
            PlayerTyreText = "-";
            PlayerTyreAgeText = "-";
            OverviewSpeedText = "-";
            OverviewGearText = "-";
            OverviewThrottleText = "-";
            OverviewBrakeText = "-";
            OverviewDrsText = "-";
            OverviewTyreWearText = "-";
            OverviewDamageText = "等待 CarDamage 包";
            return;
        }

        PlayerName = string.IsNullOrWhiteSpace(playerCar.DriverName)
            ? $"车辆 {playerCar.CarIndex}"
            : playerCar.DriverName!;
        PlayerCurrentLapText = playerCar.CurrentLapNumber is null ? "-" : $"第 {playerCar.CurrentLapNumber} 圈";
        PlayerLastLapText = FormatLapTime(playerCar.LastLapTimeInMs);
        PlayerBestLapText = FormatLapTime(playerCar.BestLapTimeInMs);
        PlayerPositionText = playerCar.Position is null ? "-" : $"P{playerCar.Position}";
        PlayerGapText = BuildPlayerGapText(sessionState, playerCar);
        PlayerFuelText = BuildFuelText(playerCar);
        PlayerErsText = BuildErsText(playerCar);
        PlayerTyreText = BuildTyreText(playerCar);
        PlayerTyreAgeText = playerCar.TyresAgeLaps is null ? "-" : $"{playerCar.TyresAgeLaps} 圈";
        OverviewSpeedText = playerCar.Telemetry is null ? "-" : $"{playerCar.Telemetry.SpeedKph:0} km/h";
        OverviewGearText = FormatGear(playerCar.Gear);
        OverviewThrottleText = playerCar.Telemetry is null ? "-" : playerCar.Telemetry.Throttle.ToString("P0", CultureInfo.InvariantCulture);
        OverviewBrakeText = playerCar.Telemetry is null ? "-" : playerCar.Telemetry.Brake.ToString("P0", CultureInfo.InvariantCulture);
        OverviewDrsText = playerCar.IsDrsEnabled is null ? "-" : playerCar.IsDrsEnabled.Value ? "On" : "Off";
        OverviewTyreWearText = playerCar.TyreWear is null ? "-" : $"平均 {playerCar.TyreWear.Value:0.0}%";
        OverviewDamageText = DamageSummaryFormatter.Format(playerCar.Damage, "等待 CarDamage 包");
    }

    private void RebuildOpponentCars(IReadOnlyList<CarSnapshot> opponents, CarSnapshot? playerCar)
    {
        OpponentCars.Clear();

        foreach (var opponent in opponents)
        {
            OpponentCars.Add(CarStateItemViewModel.FromSnapshot(opponent, playerCar));
        }

        OverviewKeyOpponentText = OpponentCars.Count == 0
            ? "-"
            : $"{OpponentCars[0].DisplayName} - {OpponentCars[0].PositionText} - {OpponentCars[0].GapToPlayerText}";
    }

    private void RefreshLapHistory()
    {
        var recentLaps = _lapAnalyzer.CaptureRecentLaps(12);
        BestLapSummaryText = BuildHighlightedLapText("最佳", _lapAnalyzer.CaptureBestLap());
        LastLapSummaryText = BuildHighlightedLapText("最后", _lapAnalyzer.CaptureLastLap());

        RecentLapSummaries.Clear();
        foreach (var summary in recentLaps)
        {
            RecentLapSummaries.Add(LapSummaryItemViewModel.FromSummary(summary));
        }
    }

    private void RefreshCharts()
    {
        var currentLapSamples = _lapAnalyzer.CaptureCurrentLapSamples();
        SpeedChartPanel.UpdateFrom(_currentLapChartBuilder.BuildSpeedPanel(currentLapSamples));
        InputsChartPanel.UpdateFrom(_currentLapChartBuilder.BuildThrottleBrakePanel(currentLapSamples));

        var latestCompletedLapNumber = _lapAnalyzer.CaptureLastLap()?.LapNumber;
        if (_lastTrendRefreshLapNumber == latestCompletedLapNumber)
        {
            return;
        }

        _lastTrendRefreshLapNumber = latestCompletedLapNumber;
        var recentLaps = _lapAnalyzer.CaptureRecentLaps(12);
        FuelTrendChartPanel.UpdateFrom(_trendChartBuilder.BuildFuelTrendPanel(recentLaps));
        TyreWearTrendChartPanel.UpdateFrom(_trendChartBuilder.BuildTyreWearTrendPanel(recentLaps));
    }

    private void ResetChartPanels()
    {
        SpeedChartPanel.UpdateFrom(_currentLapChartBuilder.BuildSpeedPanel(Array.Empty<LapSample>()));
        InputsChartPanel.UpdateFrom(_currentLapChartBuilder.BuildThrottleBrakePanel(Array.Empty<LapSample>()));
        FuelTrendChartPanel.UpdateFrom(_trendChartBuilder.BuildFuelTrendPanel(Array.Empty<LapSummary>()));
        TyreWearTrendChartPanel.UpdateFrom(_trendChartBuilder.BuildTyreWearTrendPanel(Array.Empty<LapSummary>()));
    }

    private void PersistLatestLapIfNeeded()
    {
        var lastLap = _lapAnalyzer.CaptureLastLap();
        if (lastLap is null)
        {
            return;
        }

        var lapKey = BuildSessionLapKey(lastLap.LapNumber);
        if (string.Equals(_lastPersistedLapKey, lapKey, StringComparison.Ordinal))
        {
            return;
        }

        _lastPersistedLapKey = lapKey;
        _storagePersistenceService.EnqueueLapSummary(lastLap);
    }

    private void TrackLatestEvent(string? eventCode)
    {
        if (string.IsNullOrWhiteSpace(eventCode) || string.Equals(eventCode, _lastEventCode, StringComparison.Ordinal))
        {
            return;
        }

        _lastEventCode = eventCode;
        var display = RawEventCodeLogFormatter.Format(eventCode);
        EnqueueEventLog(display.Category, display.Message);
    }

    private void EnqueueEventLog(string category, string message)
    {
        lock (_pendingEventLogsLock)
        {
            _pendingEventLogs.Enqueue(CreateLogEntry(category, message));
            while (_pendingEventLogs.Count > MaxPendingEventLogs)
            {
                _pendingEventLogs.Dequeue();
            }
        }
    }

    private void DrainPendingAiTtsLogs()
    {
        while (true)
        {
            LogEntryViewModel? logEntry;
            lock (_pendingAiTtsLogsLock)
            {
                if (!_pendingAiTtsLogs.TryDequeue(out logEntry))
                {
                    break;
                }
            }

            UpdateOverviewAiTtsSummary(logEntry);
            AiTtsLogs.Insert(0, logEntry);
            LogEntries.Insert(0, logEntry);

            while (AiTtsLogs.Count > MaxLogEntries)
            {
                AiTtsLogs.RemoveAt(AiTtsLogs.Count - 1);
            }

            TrimUnifiedLogEntries();
            RebuildOverviewEventSummaries();
        }
    }

    private void EnqueueAiTtsLog(string category, string message)
    {
        lock (_pendingAiTtsLogsLock)
        {
            _pendingAiTtsLogs.Enqueue(CreateLogEntry(category, message));
            while (_pendingAiTtsLogs.Count > MaxPendingAiTtsLogs)
            {
                _pendingAiTtsLogs.Dequeue();
            }
        }
    }

    private void DrainPendingAiAnalysisLogs()
    {
        while (true)
        {
            LogEntryViewModel? logEntry;
            lock (_pendingAiAnalysisLogsLock)
            {
                if (!_pendingAiAnalysisLogs.TryDequeue(out logEntry))
                {
                    break;
                }
            }

            AiAnalysisLogs.Insert(0, logEntry);

            while (AiAnalysisLogs.Count > MaxLogEntries)
            {
                AiAnalysisLogs.RemoveAt(AiAnalysisLogs.Count - 1);
            }
        }
    }

    private void EnqueueAiAnalysisLog(string category, string message)
    {
        lock (_pendingAiAnalysisLogsLock)
        {
            _pendingAiAnalysisLogs.Enqueue(CreateLogEntry(category, message));
            while (_pendingAiAnalysisLogs.Count > MaxPendingAiAnalysisLogs)
            {
                _pendingAiAnalysisLogs.Dequeue();
            }
        }
    }

    private void UpdateOverviewAiTtsSummary(LogEntryViewModel logEntry)
    {
        if (string.Equals(logEntry.Category, "AI", StringComparison.OrdinalIgnoreCase))
        {
            OverviewRecentAiSuggestionText = logEntry.Message;
        }

        if (string.Equals(logEntry.Category, "TTS", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(logEntry.Category, "AI", StringComparison.OrdinalIgnoreCase))
        {
            OverviewRecentTtsStatusText = logEntry.Message;
        }
    }

    private void TrimUnifiedLogEntries()
    {
        while (LogEntries.Count > MaxLogEntries)
        {
            LogEntries.RemoveAt(LogEntries.Count - 1);
        }
    }

    private void RebuildOverviewEventSummaries()
    {
        OverviewEventSummaries.Clear();
        foreach (var summary in OverviewEventSummaryFormatter.BuildSummaries(
                     LogEntries,
                     MaxOverviewEventSummaries,
                     MaxOverviewEventSummaryChars))
        {
            OverviewEventSummaries.Add(summary);
        }
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _appSettingsStore.LoadAsync(_lifecycleCts.Token);
            _isApplyingSettings = true;
            var udpListenPort = NormalizeUdpListenPort(settings.Udp.ListenPort);
            _lastValidUdpListenPort = udpListenPort;
            _lastSavedUdpListenPort = udpListenPort;
            PortText = udpListenPort.ToString(CultureInfo.InvariantCulture);
            AiEnabled = settings.Ai.AiEnabled;
            AiBaseUrl = settings.Ai.BaseUrl;
            AiModel = settings.Ai.Model;
            AiApiKey = settings.Ai.ApiKey;
            _aiRequestTimeoutSeconds = settings.Ai.RequestTimeoutSeconds <= 0 ? 10 : settings.Ai.RequestTimeoutSeconds;
            _udpRawLogQueueCapacity = Math.Clamp(settings.UdpRawLog.QueueCapacity, 0, 100_000);
            _udpRawLogWriter.UpdateOptions(BuildUdpRawLogOptions(settings.UdpRawLog.Enabled, settings.UdpRawLog.DirectoryPath, settings.UdpRawLog.QueueCapacity));
            UdpRawLogEnabled = settings.UdpRawLog.Enabled;
            RefreshUdpRawLogStatus();
            TtsEnabled = settings.Tts.TtsEnabled;
            var loadedVoiceName = settings.Tts.VoiceName;
            TtsVoiceName = ResolveTtsVoiceName(loadedVoiceName);
            TtsVolume = settings.Tts.Volume;
            TtsRate = settings.Tts.Rate;
            _ttsCooldownSeconds = settings.Tts.CooldownSeconds <= 0 ? 8 : settings.Tts.CooldownSeconds;
            _isApplyingSettings = false;

            _ttsQueue.UpdateOptions(BuildTtsOptions());
            AiSettingsSaveStatusText = "设置已加载";
            if (string.IsNullOrWhiteSpace(loadedVoiceName) && !string.IsNullOrWhiteSpace(TtsVoiceName))
            {
                QueuePersistTtsSettings();
            }

            EnqueueAiTtsLog("System", $"设置已加载 · AI API Key {AiApiKeyStatusText} · TTS {(TtsEnabled ? "已启用" : "未启用")}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            EnqueueAiTtsLog("System", $"设置加载失败：{ex.Message}");
        }
        finally
        {
            _isApplyingSettings = false;
        }
    }

    private void QueuePersistAiSettings()
    {
        if (_isApplyingSettings)
        {
            return;
        }

        var saveVersion = Interlocked.Increment(ref _aiSettingsSaveVersion);
        var settings = BuildAiSettings();
        AiSettingsSaveStatusText = "正在保存...";
        _ = PersistAiSettingsAsync(settings, saveVersion);
    }

    private async Task PersistAiSettingsAsync(AISettings settings, int saveVersion)
    {
        var gateHeld = false;
        try
        {
            await _settingsGate.WaitAsync();
            gateHeld = true;
            if (saveVersion < Volatile.Read(ref _aiSettingsSaveVersion))
            {
                return;
            }

            await _appSettingsStore.SaveAiSettingsAsync(settings, CancellationToken.None);
            if (saveVersion == Volatile.Read(ref _aiSettingsSaveVersion))
            {
                AiSettingsSaveStatusText = "AI 设置已保存";
                EnqueueAiTtsLog("System", "AI 设置已保存。");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AiSettingsSaveStatusText = "AI 设置保存失败";
            EnqueueAiTtsLog("System", $"AI 设置保存失败：{ex.Message}");
        }
        finally
        {
            if (gateHeld)
            {
                _settingsGate.Release();
            }
        }
    }

    private void QueuePersistTtsSettings()
    {
        if (_isApplyingSettings)
        {
            return;
        }

        var saveVersion = Interlocked.Increment(ref _ttsSettingsSaveVersion);
        var options = BuildTtsOptions();
        _ = PersistTtsSettingsAsync(options, saveVersion);
    }

    private async Task PersistTtsSettingsAsync(TtsOptions options, int saveVersion)
    {
        var gateHeld = false;
        try
        {
            await _settingsGate.WaitAsync();
            gateHeld = true;
            if (saveVersion < Volatile.Read(ref _ttsSettingsSaveVersion))
            {
                return;
            }

            await _appSettingsStore.SaveTtsSettingsAsync(options, CancellationToken.None);
            if (saveVersion == Volatile.Read(ref _ttsSettingsSaveVersion))
            {
                EnqueueAiTtsLog("System", "TTS 设置已保存。");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            EnqueueAiTtsLog("System", $"TTS 设置保存失败：{ex.Message}");
        }
        finally
        {
            if (gateHeld)
            {
                _settingsGate.Release();
            }
        }
    }

    private void QueuePersistUdpRawLogOptions()
    {
        if (_isApplyingSettings)
        {
            return;
        }

        var saveVersion = Interlocked.Increment(ref _udpRawLogSettingsSaveVersion);
        var options = BuildUdpRawLogOptions();
        _ = PersistUdpRawLogOptionsAsync(options, saveVersion);
    }

    private async Task PersistUdpRawLogOptionsAsync(UdpRawLogOptions options, int saveVersion)
    {
        var gateHeld = false;
        try
        {
            await _settingsGate.WaitAsync();
            gateHeld = true;
            if (saveVersion < Volatile.Read(ref _udpRawLogSettingsSaveVersion))
            {
                return;
            }

            await _appSettingsStore.SaveUdpRawLogOptionsAsync(options, CancellationToken.None);
            if (saveVersion == Volatile.Read(ref _udpRawLogSettingsSaveVersion))
            {
                EnqueueAiTtsLog("System", options.Enabled ? "UDP Raw Log 已启用。" : "UDP Raw Log 已关闭。");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            EnqueueAiTtsLog("System", $"UDP Raw Log 设置保存失败：{ex.Message}");
        }
        finally
        {
            if (gateHeld)
            {
                _settingsGate.Release();
            }
        }
    }

    private void LoadAvailableVoices()
    {
        var result = _windowsVoiceCatalog.LoadVoices();
        AvailableVoices.Clear();
        foreach (var voiceName in result.VoiceNames)
        {
            AvailableVoices.Add(voiceName);
        }

        _defaultTtsVoiceName = result.DefaultVoiceName;
        TtsVoiceStatusText = result.StatusMessage;
        OnPropertyChanged(nameof(HasAvailableVoices));

        if (AvailableVoices.Count == 0)
        {
            EnqueueAiTtsLog("System", result.StatusMessage);
        }
    }

    private string ResolveTtsVoiceName(string voiceName)
    {
        if (!string.IsNullOrWhiteSpace(voiceName))
        {
            return voiceName;
        }

        if (!string.IsNullOrWhiteSpace(_defaultTtsVoiceName))
        {
            return _defaultTtsVoiceName;
        }

        return AvailableVoices.FirstOrDefault() ?? string.Empty;
    }

    private async Task TriggerPostRaceAiAnalysisIfReadyAsync(
        SessionState sessionState,
        CarSnapshot? playerCar,
        bool force = false)
    {
        if (_isAiAnalysisRunning || !AiEnabled)
        {
            return;
        }

        var lastLap = CaptureAiSummaryLap(sessionState);
        if (lastLap is null)
        {
            return;
        }

        var completion = EvaluatePostRaceAiCompletion(sessionState, force);
        if (!completion.ShouldGenerate)
        {
            return;
        }

        var summaryKey = BuildPostRaceAiSummaryKey(sessionState, lastLap.LapNumber, completion.IsManual);
        if (string.Equals(_lastPostRaceAiSummaryKey, summaryKey, StringComparison.Ordinal))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(AiApiKey))
        {
            _lastPostRaceAiSummaryKey = summaryKey;
            PostRaceAiStatusText = "赛后 AI 总结未上传：AI API Key 未配置。";
            EnqueueAiAnalysisLog("AI", BuildAiFailureLogText(lastLap.LapNumber, AIErrorMessageFormatter.MissingApiKey));
            return;
        }

        _lastPostRaceAiSummaryKey = summaryKey;
        _isAiAnalysisRunning = true;
        OnPropertyChanged(nameof(CanGeneratePostRaceAiSummary));
        _generatePostRaceAiSummaryCommand.RaiseCanExecuteChanged();
        var analysisSessionUid = _activeSessionUid;
        PostRaceAiStatusText = completion.IsManual
            ? "用户已标记完赛，正在生成赛后 AI 总结..."
            : "已收到 UDP 最终分类，正在生成赛后 AI 总结...";
        EnqueueAiAnalysisLog("AI", PostRaceAiStatusText);

        try
        {
            var result = await _aiAnalysisService.AnalyzeAsync(
                BuildAiAnalysisContext(sessionState, playerCar, lastLap),
                BuildAiSettings(),
                _lifecycleCts.Token);

            if (_activeSessionUid != analysisSessionUid ||
                !string.Equals(_lastPostRaceAiSummaryKey, summaryKey, StringComparison.Ordinal))
            {
                EnqueueAiAnalysisLog("System", $"已忽略过期赛后 AI 总结结果：Lap {lastLap.LapNumber}。");
                return;
            }

            _storagePersistenceService.EnqueueAiReport(lastLap.LapNumber, result);

            if (result.IsSuccess)
            {
                PostRaceAiStatusText = $"赛后 AI 总结已生成：{result.Summary}";
                EnqueueAiAnalysisLog("AI", PostRaceAiStatusText);
                EnqueueEventLog("AI", "赛后 AI 总结已生成。");
                TryEnqueueAiSpeech(lastLap, result);
            }
            else
            {
                PostRaceAiStatusText = $"赛后 AI 总结失败：{result.ErrorMessage}";
                EnqueueAiAnalysisLog("AI", BuildAiFailureLogText(lastLap.LapNumber, result.ErrorMessage));
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isAiAnalysisRunning = false;
            OnPropertyChanged(nameof(CanGeneratePostRaceAiSummary));
            _generatePostRaceAiSummaryCommand.RaiseCanExecuteChanged();
        }
    }

    private void RefreshPostRaceAiStatus(SessionState sessionState)
    {
        var completion = EvaluatePostRaceAiCompletion(sessionState, force: false);
        PostRaceAiCompletionText = completion.Evidence;

        if (_isAiAnalysisRunning)
        {
            return;
        }

        PostRaceAiStatusText = completion.ShouldGenerate
            ? "正赛已完成，等待生成赛后 AI 总结。"
            : completion.ShouldStage
                ? "正赛尚未完成；如中途退出，本次运行会暂存并等待完赛后总结。"
                : completion.StatusText;

        OnPropertyChanged(nameof(CanGeneratePostRaceAiSummary));
        _generatePostRaceAiSummaryCommand.RaiseCanExecuteChanged();
    }

    private PostRaceAiCompletionEvaluation EvaluatePostRaceAiCompletion(SessionState sessionState, bool force)
    {
        var mode = force
            ? PostRaceAiCompletionMode.ForceComplete
            : SelectedPostRaceAiCompletionMode?.Mode ?? PostRaceAiCompletionMode.Auto;
        var sessionMode = SessionModeFormatter.Resolve(sessionState.SessionType);
        var isRace = sessionMode == SessionMode.Race;

        if (mode == PostRaceAiCompletionMode.Hold)
        {
            return new PostRaceAiCompletionEvaluation(
                ShouldGenerate: false,
                ShouldStage: isRace,
                IsManual: false,
                Evidence: "用户选择暂存不总结，AI 不会上传赛后摘要。",
                StatusText: "当前设置为暂存不总结。");
        }

        if (mode == PostRaceAiCompletionMode.ForceComplete)
        {
            return new PostRaceAiCompletionEvaluation(
                ShouldGenerate: true,
                ShouldStage: false,
                IsManual: true,
                Evidence: "用户手动标记已完成。",
                StatusText: "用户已标记完赛，可生成赛后 AI 总结。");
        }

        if (!isRace)
        {
            return new PostRaceAiCompletionEvaluation(
                ShouldGenerate: false,
                ShouldStage: false,
                IsManual: false,
                Evidence: $"当前赛制为 {SessionModeFormatter.FormatDisplayName(sessionMode)}，自动总结只等待正赛最终分类。",
                StatusText: "非正赛样本不会自动生成赛后策略总结。");
        }

        if (sessionState.HasFinalClassification)
        {
            var lapText = sessionState.PlayerFinalClassificationLaps is null
                ? "-"
                : sessionState.PlayerFinalClassificationLaps.Value.ToString(CultureInfo.InvariantCulture);
            var positionText = sessionState.PlayerFinalClassificationPosition is null
                ? "-"
                : sessionState.PlayerFinalClassificationPosition.Value.ToString(CultureInfo.InvariantCulture);
            return new PostRaceAiCompletionEvaluation(
                ShouldGenerate: true,
                ShouldStage: false,
                IsManual: false,
                Evidence: $"UDP 已收到 FinalClassification：完赛圈 {lapText}，完赛名次 P{positionText}。",
                StatusText: "UDP 已确认正赛完成。");
        }

        if (string.Equals(sessionState.LastEventCode, "CHQF", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sessionState.LastEventCode, "RCWN", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sessionState.LastEventCode, "SEND", StringComparison.OrdinalIgnoreCase))
        {
            return new PostRaceAiCompletionEvaluation(
                ShouldGenerate: false,
                ShouldStage: true,
                IsManual: false,
                Evidence: $"UDP 已看到 {sessionState.LastEventCode}，仍等待 FinalClassification 确认可总结。",
                StatusText: "正赛可能已结束，等待最终分类包。");
        }

        return new PostRaceAiCompletionEvaluation(
            ShouldGenerate: false,
            ShouldStage: true,
            IsManual: false,
            Evidence: "正赛未收到 FinalClassification，按未完整跑完暂存。",
            StatusText: "正赛进行中或中途退出，等待完赛后生成总结。");
    }

    private void MarkIncompleteRaceAsStaged(SessionState sessionState, string reason)
    {
        if (SessionModeFormatter.Resolve(sessionState.SessionType) != SessionMode.Race ||
            sessionState.HasFinalClassification)
        {
            return;
        }

        var key = BuildPostRaceSessionKey(sessionState);
        if (string.Equals(_lastStagedPostRaceAiKey, key, StringComparison.Ordinal))
        {
            return;
        }

        _lastStagedPostRaceAiKey = key;
        PostRaceAiStatusText = $"正赛未完整结束，本次运行已暂存：{reason}";
        EnqueueAiAnalysisLog("AI", PostRaceAiStatusText);
        EnqueueEventLog("AI", "正赛 AI 总结已在本次运行暂存，等待完赛后再生成。");
    }

    private string BuildPostRaceAiSummaryKey(SessionState sessionState, int lapNumber, bool isManual)
    {
        return $"{BuildPostRaceSessionKey(sessionState)}:lap{lapNumber}:{(isManual ? "manual" : "auto")}";
    }

    private string BuildPostRaceSessionKey(SessionState sessionState)
    {
        if (sessionState.SeasonLinkIdentifier is not null &&
            sessionState.WeekendLinkIdentifier is not null &&
            sessionState.SessionLinkIdentifier is not null)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "season{0}:weekend{1}:session{2}",
                sessionState.SeasonLinkIdentifier,
                sessionState.WeekendLinkIdentifier,
                sessionState.SessionLinkIdentifier);
        }

        return _activeSessionUid?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
    }

    private sealed record PostRaceAiCompletionEvaluation(
        bool ShouldGenerate,
        bool ShouldStage,
        bool IsManual,
        string Evidence,
        string StatusText);

    private void TryEnqueueAiSpeech(LapSummary lastLap, AIAnalysisResult result)
    {
        var ttsMessage = _ttsMessageFactory.CreateForAiResult(lastLap, result, BuildTtsOptions());
        if (ttsMessage is null)
        {
            return;
        }

        _ttsQueue.TryEnqueue(ttsMessage);
    }

    private LapSummary? CaptureAiSummaryLap(SessionState sessionState)
    {
        var lastLap = _lapAnalyzer.CaptureLastLap();
        if (lastLap is not null)
        {
            return lastLap;
        }

        var fallbackLapNumber = sessionState.PlayerFinalClassificationLaps ??
                                sessionState.TotalLaps ??
                                sessionState.PlayerCar?.CurrentLapNumber;
        if (fallbackLapNumber is null || fallbackLapNumber == 0)
        {
            return null;
        }

        return new LapSummary
        {
            LapNumber = fallbackLapNumber.Value,
            IsValid = true,
            EndTyre = sessionState.PlayerCar is null ? "-" : BuildTyreText(sessionState.PlayerCar),
            ClosedAt = sessionState.FinalClassificationReceivedAt ?? DateTimeOffset.UtcNow
        };
    }

    private AIAnalysisContext BuildAiAnalysisContext(SessionState sessionState, CarSnapshot? playerCar, LapSummary lastLap)
    {
        var recentLaps = _lapAnalyzer.CaptureAllLaps()
            .Reverse()
            .Take(MaxPostRaceAiLaps)
            .ToArray();
        var currentLapSamples = _lapAnalyzer.CaptureCurrentLapSamples();
        var carBehind = playerCar?.Position is null
            ? null
            : sessionState.Cars.FirstOrDefault(car => car.Position == playerCar.Position + 1);
        var sessionMode = SessionModeFormatter.Resolve(sessionState.SessionType);
        var telemetryAnalysisSummary = _telemetryAnalysisSummaryBuilder.Build(currentLapSamples, recentLaps);

        return new AIAnalysisContext
        {
            SessionMode = sessionMode,
            SessionTypeText = SessionModeFormatter.FormatDisplayName(sessionMode),
            SessionFocusText = SessionModeFormatter.FormatFocus(sessionMode),
            LatestLap = lastLap,
            BestLap = _lapAnalyzer.CaptureBestLap(),
            RecentLaps = recentLaps,
            CurrentFuelRemainingLaps = playerCar?.FuelRemainingLaps,
            CurrentFuelInTank = playerCar?.FuelInTank,
            CurrentErsStoreEnergy = playerCar?.ErsStoreEnergy,
            CurrentTyre = playerCar is null ? "-" : BuildTyreText(playerCar),
            CurrentTyreAgeLaps = playerCar?.TyresAgeLaps,
            GapToFrontInMs = playerCar?.DeltaToCarInFrontInMs,
            GapToBehindInMs = carBehind?.DeltaToCarInFrontInMs,
            TelemetryAnalysisSummary = telemetryAnalysisSummary,
            DamageSummary = DamageSummaryFormatter.Format(playerCar?.Damage),
            RecentEvents = _raceEventInsightBuffer.CaptureMessages()
        };
    }

    private string BuildSessionLapKey(int lapNumber)
    {
        var sessionToken = _activeSessionUid?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
        return $"{sessionToken}:{lapNumber}";
    }

    private static string BuildAiFailureLogText(int lapNumber, string? errorMessage)
    {
        var normalizedMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? AIErrorMessageFormatter.NetworkError
            : errorMessage.Trim();

        return $"Lap {lapNumber} · {normalizedMessage}";
    }

    private TtsOptions BuildTtsOptions()
    {
        return new TtsOptions
        {
            TtsEnabled = TtsEnabled,
            VoiceName = TtsVoiceName,
            Volume = TtsVolume,
            Rate = TtsRate,
            CooldownSeconds = _ttsCooldownSeconds
        };
    }

    private AISettings BuildAiSettings()
    {
        return new AISettings
        {
            ApiKey = AiApiKey,
            BaseUrl = AiBaseUrl,
            Model = AiModel,
            AiEnabled = AiEnabled,
            RequestTimeoutSeconds = _aiRequestTimeoutSeconds <= 0 ? 10 : _aiRequestTimeoutSeconds
        };
    }

    private UdpRawLogOptions BuildUdpRawLogOptions()
    {
        var status = _udpRawLogWriter.Status;
        return BuildUdpRawLogOptions(UdpRawLogEnabled, status.DirectoryPath, _udpRawLogQueueCapacity);
    }

    private static UdpRawLogOptions BuildUdpRawLogOptions(bool enabled, string directoryPath, int queueCapacity)
    {
        return new UdpRawLogOptions
        {
            Enabled = enabled,
            DirectoryPath = directoryPath,
            QueueCapacity = Math.Clamp(queueCapacity, 0, 100_000)
        };
    }

    private void ApplyUdpRawLogOptions()
    {
        _udpRawLogWriter.UpdateOptions(BuildUdpRawLogOptions());
        RefreshUdpRawLogStatus();
    }

    private void RefreshUdpRawLogStatus()
    {
        var status = _udpRawLogWriter.Status;
        var latestFileInfo = _udpRawLogDirectoryService.GetLatestFileInfo(status);
        UdpRawLogDirectoryText = status.DirectoryPath;
        UdpRawLogLastFilePathText = latestFileInfo.FilePathText;
        UdpRawLogLastFileSizeText = latestFileInfo.FileSizeText;
        UdpRawLogLastWriteTimeText = latestFileInfo.LastWriteTimeText;
        UdpRawLogWrittenPacketCount = status.WrittenPacketCount;
        UdpRawLogDroppedPacketCount = status.DroppedPacketCount;
        UdpRawLogLastErrorText = BuildUdpRawLogErrorText(
            status.LastError,
            latestFileInfo.ErrorMessage,
            _udpRawLogDirectoryOpenErrorText);
        UdpRawLogStatusText = status.Enabled
            ? "Raw Log 已启用"
            : "Raw Log 未启用";
    }

    private void UpdateUdpRawLogUi(Action update)
    {
        try
        {
            if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
            {
                return;
            }

            if (_dispatcher.CheckAccess())
            {
                update();
                return;
            }

            _dispatcher.Invoke(update);
        }
        catch
        {
        }
    }

    private static string BuildUdpRawLogErrorText(params string[] messages)
    {
        var visibleMessages = messages
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Select(message => message.Trim())
            .ToArray();

        return visibleMessages.Length == 0
            ? "-"
            : string.Join("；", visibleMessages);
    }

    private static LogEntryViewModel CreateLogEntry(string category, string message)
    {
        var normalizedCategory = LogCategoryFormatter.Normalize(category, message);
        return new LogEntryViewModel
        {
            Timestamp = DateTimeOffset.Now.ToString("HH:mm:ss"),
            Category = normalizedCategory,
            Message = message
        };
    }

    private static string BuildEventCategory(RaceEvent raceEvent)
    {
        return raceEvent.EventType == EventType.DataQualityWarning ? "System" : "RaceEvent";
    }

    private static int GetRaceEventDrainPriority(RaceEvent raceEvent)
    {
        return raceEvent.EventType switch
        {
            EventType.SafetyCar or EventType.VirtualSafetyCar or EventType.YellowFlag or EventType.RedFlag => 600,
            EventType.LowFuel or EventType.HighTyreWear => 500,
            EventType.AttackWindow or EventType.DefenseWindow => 400,
            EventType.FrontCarPitted or EventType.RearCarPitted => 300,
            EventType.LowErs => 200,
            EventType.DataQualityWarning => 0,
            _ => raceEvent.Severity == EventSeverity.Warning ? 100 : 50
        };
    }

    private static string BuildTrackText(sbyte? trackId)
    {
        return TrackNameFormatter.Format(trackId);
    }

    private static string BuildWeatherText(SessionState sessionState)
    {
        var weatherText = sessionState.Weather switch
        {
            0 => "晴",
            1 => "少云",
            2 => "多云",
            3 => "阴",
            4 => "小雨",
            5 => "大雨",
            6 => "风暴",
            null => "-",
            _ => $"天气 {sessionState.Weather}"
        };

        if (sessionState.TrackTemperature is null && sessionState.AirTemperature is null)
        {
            return weatherText;
        }

        return $"{weatherText} · 赛道 {sessionState.TrackTemperature?.ToString() ?? "-"}°C · 空气 {sessionState.AirTemperature?.ToString() ?? "-"}°C";
    }

    private static string BuildLapText(SessionState sessionState, CarSnapshot? playerCar)
    {
        if (playerCar?.CurrentLapNumber is null && sessionState.TotalLaps is null)
        {
            return "-";
        }

        var currentLapNumber = playerCar?.CurrentLapNumber;
        if (currentLapNumber is not null &&
            sessionState.TotalLaps is > 0 &&
            currentLapNumber > sessionState.TotalLaps)
        {
            currentLapNumber = sessionState.TotalLaps;
        }

        return $"第 {currentLapNumber?.ToString() ?? "-"} 圈 / 共 {sessionState.TotalLaps?.ToString() ?? "-"} 圈";
    }

    private static string BuildPlayerGapText(SessionState sessionState, CarSnapshot playerCar)
    {
        var frontGapText = playerCar.Position is 1
            ? "前车 -"
            : $"前车 {FormatGapMs(playerCar.DeltaToCarInFrontInMs)}";

        if (playerCar.Position is null)
        {
            return $"{frontGapText} · 后车 -";
        }

        var carBehind = sessionState.Cars.FirstOrDefault(car => car.Position == playerCar.Position + 1);
        var backGapText = carBehind is null
            ? "后车 -"
            : $"后车 {FormatGapMs(carBehind.DeltaToCarInFrontInMs)}";

        return $"{frontGapText} · {backGapText}";
    }

    private static string BuildFuelText(CarSnapshot playerCar)
    {
        if (playerCar.FuelInTank is null && playerCar.FuelRemainingLaps is null)
        {
            return "-";
        }

        return $"{playerCar.FuelInTank?.ToString("0.0") ?? "-"} L · 剩余 {playerCar.FuelRemainingLaps?.ToString("0.0") ?? "-"} 圈";
    }

    private static string BuildErsText(CarSnapshot playerCar)
    {
        if (playerCar.ErsStoreEnergy is null)
        {
            return "-";
        }

        var energyMj = playerCar.ErsStoreEnergy.Value / 1_000_000f;
        var energyPercent = Math.Clamp(playerCar.ErsStoreEnergy.Value / 4_000_000f, 0f, 1f);
        return $"{energyMj:0.00} MJ · {energyPercent:P0}";
    }

    private static string BuildTyreText(CarSnapshot playerCar)
    {
        return TyreCompoundFormatter.Format(
            playerCar.VisualTyreCompound,
            playerCar.ActualTyreCompound,
            playerCar.HasTelemetryAccess);
    }

    private static string BuildHighlightedLapText(string label, LapSummary? summary)
    {
        if (summary is null)
        {
            return $"{label} -";
        }

        return $"{label} Lap {summary.LapNumber} · {FormatLapTime(summary.LapTimeInMs)} · {(summary.IsValid ? "有效" : "无效")}";
    }

    private static string FormatLapTime(uint? milliseconds)
    {
        if (milliseconds is null)
        {
            return "-";
        }

        var time = TimeSpan.FromMilliseconds(milliseconds.Value);
        return time.TotalMinutes >= 1
            ? $"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds:000}"
            : $"{time.Seconds}.{time.Milliseconds:000}s";
    }

    private static string FormatGear(sbyte? gear)
    {
        return gear switch
        {
            null => "-",
            < 0 => "R",
            0 => "N",
            _ => gear.Value.ToString(CultureInfo.InvariantCulture)
        };
    }

    private static string FormatGapMs(ushort? gapMs)
    {
        return gapMs is null ? "-" : $"{gapMs.Value / 1000d:0.000}s";
    }

    private sealed class NoOpSessionRepository : ISessionRepository
    {
        public Task CreateAsync(StoredSession session, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task EndAsync(string sessionId, DateTimeOffset endedAt, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredSession>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredSession>>(Array.Empty<StoredSession>());
        }
    }

    private sealed class NoOpLapRepository : ILapRepository
    {
        public Task AddAsync(string sessionId, LapSummary lapSummary, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredLap>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredLap>>(Array.Empty<StoredLap>());
        }
    }
}
