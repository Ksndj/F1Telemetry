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
using F1Telemetry.App.Windowing;
using F1Telemetry.Analytics.State;
using F1Telemetry.Core.Abstractions;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;
using F1Telemetry.Storage.Interfaces;
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
    private const double ExpandedSidebarWidth = 220d;
    private const double CollapsedSidebarWidth = 80d;
    private readonly IUdpListener _udpListener;
    private readonly IPacketDispatcher<PacketId, PacketHeader> _packetDispatcher;
    private readonly SessionStateStore _sessionStateStore;
    private readonly ILapAnalyzer _lapAnalyzer;
    private readonly IEventDetectionService _eventDetectionService;
    private readonly IAIAnalysisService _aiAnalysisService;
    private readonly IAppSettingsStore _appSettingsStore;
    private readonly TtsMessageFactory _ttsMessageFactory;
    private readonly TtsQueue _ttsQueue;
    private readonly WindowsVoiceCatalog _windowsVoiceCatalog;
    private readonly IStoragePersistenceService _storagePersistenceService;
    private readonly CurrentLapChartBuilder _currentLapChartBuilder;
    private readonly TrendChartBuilder _trendChartBuilder;
    private readonly DispatcherTimer _uiTimer;
    private readonly CancellationTokenSource _lifecycleCts = new();
    private readonly Queue<LogEntryViewModel> _pendingEventLogs = new();
    private readonly Queue<LogEntryViewModel> _pendingAiTtsLogs = new();
    private readonly object _pendingEventLogsLock = new();
    private readonly object _pendingAiTtsLogsLock = new();
    private readonly SemaphoreSlim _settingsGate = new(1, 1);
    private readonly Queue<string> _recentAiEvents = new();
    private readonly RelayCommand _startListeningCommand;
    private readonly RelayCommand _stopListeningCommand;
    private readonly RelayCommand _downloadLatestVersionCommand;
    private readonly RelayCommand _toggleSidebarCommand;
    private ShellNavigationItemViewModel? _selectedShellNavigationItem;
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
    private string _overviewKeyOpponentText = "-";
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
    private ulong? _activeSessionUid;
    private string? _lastAnalyzedLapKey;
    private string? _lastPersistedLapKey;
    private int? _lastTrendRefreshLapNumber;
    private readonly object _shutdownGate = new();
    private Task? _shutdownTask;
    private bool _disposed;

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
    /// <param name="ttsMessageFactory">The mapper that converts event and AI outputs into TTS queue messages.</param>
    /// <param name="ttsQueue">The TTS queue that plays race events and AI guidance.</param>
    /// <param name="storagePersistenceService">The background SQLite persistence coordinator.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="windowsVoiceCatalog">The optional Windows voice catalog used by the settings UI.</param>
    public DashboardViewModel(
        IUdpListener udpListener,
        IPacketDispatcher<PacketId, PacketHeader> packetDispatcher,
        SessionStateStore sessionStateStore,
        ILapAnalyzer lapAnalyzer,
        IEventDetectionService eventDetectionService,
        IAIAnalysisService aiAnalysisService,
        IAppSettingsStore appSettingsStore,
        TtsMessageFactory ttsMessageFactory,
        TtsQueue ttsQueue,
        IStoragePersistenceService storagePersistenceService,
        Dispatcher dispatcher,
        WindowsVoiceCatalog? windowsVoiceCatalog = null)
    {
        _udpListener = udpListener ?? throw new ArgumentNullException(nameof(udpListener));
        _packetDispatcher = packetDispatcher ?? throw new ArgumentNullException(nameof(packetDispatcher));
        _sessionStateStore = sessionStateStore ?? throw new ArgumentNullException(nameof(sessionStateStore));
        _lapAnalyzer = lapAnalyzer ?? throw new ArgumentNullException(nameof(lapAnalyzer));
        _eventDetectionService = eventDetectionService ?? throw new ArgumentNullException(nameof(eventDetectionService));
        _aiAnalysisService = aiAnalysisService ?? throw new ArgumentNullException(nameof(aiAnalysisService));
        _appSettingsStore = appSettingsStore ?? throw new ArgumentNullException(nameof(appSettingsStore));
        _ttsMessageFactory = ttsMessageFactory ?? throw new ArgumentNullException(nameof(ttsMessageFactory));
        _ttsQueue = ttsQueue ?? throw new ArgumentNullException(nameof(ttsQueue));
        _windowsVoiceCatalog = windowsVoiceCatalog ?? new WindowsVoiceCatalog();
        _storagePersistenceService = storagePersistenceService ?? throw new ArgumentNullException(nameof(storagePersistenceService));
        _currentLapChartBuilder = new CurrentLapChartBuilder();
        _trendChartBuilder = new TrendChartBuilder();
        _lastPacketsPerSecondSampleAt = DateTimeOffset.UtcNow;

        ShellNavigationItems = new ObservableCollection<ShellNavigationItemViewModel>(
            ShellNavigationItemViewModel.CreateDefaultItems());
        _selectedShellNavigationItem = ShellNavigationItems[0];
        OpponentCars = new ObservableCollection<CarStateItemViewModel>();
        RecentLapSummaries = new ObservableCollection<LapSummaryItemViewModel>();
        EventLogs = new ObservableCollection<LogEntryViewModel>();
        AiTtsLogs = new ObservableCollection<LogEntryViewModel>();
        AvailableVoices = new ObservableCollection<string>();
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
        SpeedChartPanel.UpdateFrom(_currentLapChartBuilder.BuildSpeedPanel(Array.Empty<LapSample>()));
        InputsChartPanel.UpdateFrom(_currentLapChartBuilder.BuildThrottleBrakePanel(Array.Empty<LapSample>()));
        FuelTrendChartPanel.UpdateFrom(_trendChartBuilder.BuildFuelTrendPanel(Array.Empty<LapSummary>()));
        TyreWearTrendChartPanel.UpdateFrom(_trendChartBuilder.BuildTyreWearTrendPanel(Array.Empty<LapSummary>()));

        AiTtsLogs.Add(CreateLogEntry("System", "AI / TTS 日志已准备就绪。"));
        LoadAvailableVoices();

        _startListeningCommand = new RelayCommand(() => _ = StartListeningAsync(), CanStartListening);
        _stopListeningCommand = new RelayCommand(() => _ = StopListeningAsync(), CanStopListening);
        _downloadLatestVersionCommand = new RelayCommand(OpenGitHubReleases);
        _toggleSidebarCommand = new RelayCommand(ToggleSidebar);

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
    public string Subtitle => "Milestone 10 · 实时图表";

    /// <summary>
    /// Gets the application version text displayed in the shell.
    /// </summary>
    public string ApplicationVersionText => VersionInfo.DisplayVersion;

    /// <summary>
    /// Gets the fixed V1.0.2-M1 shell navigation items.
    /// </summary>
    public ObservableCollection<ShellNavigationItemViewModel> ShellNavigationItems { get; }

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
                OnPropertyChanged(nameof(AiApiKeyStatusText));
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
                OnPropertyChanged(nameof(AiApiKeyStatusText));
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
    /// Gets the unified AI, TTS, and system log entries.
    /// </summary>
    public ObservableCollection<LogEntryViewModel> AiTtsLogs { get; }

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
    public string SidebarUdpPortText => $"使用中: {(ListeningPort?.ToString(CultureInfo.InvariantCulture) ?? PortText)} UDP";

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

        _disposed = true;
        TryCancelLifecycle();
        StopUiTimer();

        _udpListener.DatagramReceived -= OnDatagramReceived;
        _udpListener.ReceiveFaulted -= OnReceiveFaulted;
        _packetDispatcher.PacketDispatched -= OnPacketDispatched;
        _storagePersistenceService.LogEmitted -= OnStorageLogEmitted;

        try
        {
            await _udpListener.DisposeAsync().AsTask().ConfigureAwait(false);
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

    private async Task StartListeningAsync()
    {
        if (!int.TryParse(PortText, out var port) || port is < 1 or > 65535)
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
            await _udpListener.StartAsync(port, _lifecycleCts.Token);
            ListeningPort = _udpListener.ListeningPort;
            IsListening = _udpListener.IsListening;
            if (IsListening)
            {
                _activeSessionUid = null;
                _lastAnalyzedLapKey = null;
                _lastPersistedLapKey = null;
                _lastTrendRefreshLapNumber = null;
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
            _activeSessionUid = null;
            _lastAnalyzedLapKey = null;
            _lastPersistedLapKey = null;
            _lastTrendRefreshLapNumber = null;
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

        _sessionStateStore.Reset();
        _eventDetectionService.Reset();
        _lapAnalyzer.ResetForSession(incomingSessionUid);
        _lastAnalyzedLapKey = null;
        _lastPersistedLapKey = null;
        _lastTrendRefreshLapNumber = null;
        _lastEventCode = null;
        _recentAiEvents.Clear();
        _activeSessionUid = incomingSessionUid;
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
        RefreshConnectionState();
        RefreshCounters();
        RefreshCentralState();
    }

    private void DrainDetectedRaceEvents()
    {
        foreach (var raceEvent in _eventDetectionService.DrainPendingEvents())
        {
            EnqueueEventLog(BuildEventCategory(raceEvent), raceEvent.Message);
            AddRecentAiEvent(raceEvent.Message);
            TryEnqueueRaceEventSpeech(raceEvent);
            _storagePersistenceService.EnqueueRaceEvent(raceEvent);
        }
    }

    private void DrainTtsPlaybackRecords()
    {
        foreach (var record in _ttsQueue.DrainPendingRecords())
        {
            EnqueueAiTtsLog(record.Source, record.Message);
        }
    }

    private void AddRecentAiEvent(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _recentAiEvents.Enqueue(message);
        while (_recentAiEvents.Count > 8)
        {
            _recentAiEvents.Dequeue();
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

            while (EventLogs.Count > MaxLogEntries)
            {
                EventLogs.RemoveAt(EventLogs.Count - 1);
            }
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

        TrackText = BuildTrackText(sessionState.TrackId);
        SessionTypeText = SessionTypeFormatter.Format(sessionState.SessionType);
        WeatherText = BuildWeatherText(sessionState);
        LapText = BuildLapText(sessionState, playerCar);
        UpdatePlayerCard(sessionState, playerCar);
        RebuildOpponentCars(sessionState.Opponents, playerCar);
        RefreshLapHistory();
        RefreshCharts();
        PersistLatestLapIfNeeded();
        TrackLatestEvent(sessionState.LastEventCode);
        _ = TriggerAiAnalysisIfNeededAsync(sessionState, playerCar);
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
            return;
        }

        PlayerName = string.IsNullOrWhiteSpace(playerCar.DriverName)
            ? $"车辆 {playerCar.CarIndex}"
            : playerCar.DriverName!;
        PlayerCurrentLapText = $"Lap {playerCar.CurrentLapNumber?.ToString() ?? "-"} / {sessionState.TotalLaps?.ToString() ?? "-"}";
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
        EnqueueEventLog("事件", $"收到赛道事件：{eventCode}");
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

            while (AiTtsLogs.Count > MaxLogEntries)
            {
                AiTtsLogs.RemoveAt(AiTtsLogs.Count - 1);
            }
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

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _appSettingsStore.LoadAsync(_lifecycleCts.Token);
            _isApplyingSettings = true;
            AiEnabled = settings.Ai.AiEnabled;
            AiBaseUrl = settings.Ai.BaseUrl;
            AiModel = settings.Ai.Model;
            AiApiKey = settings.Ai.ApiKey;
            _aiRequestTimeoutSeconds = settings.Ai.RequestTimeoutSeconds <= 0 ? 10 : settings.Ai.RequestTimeoutSeconds;
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

    private async Task TriggerAiAnalysisIfNeededAsync(SessionState sessionState, CarSnapshot? playerCar)
    {
        if (_isAiAnalysisRunning || !AiEnabled)
        {
            return;
        }

        var lastLap = _lapAnalyzer.CaptureLastLap();
        if (lastLap is null)
        {
            return;
        }

        var lapKey = BuildSessionLapKey(lastLap.LapNumber);
        if (string.Equals(_lastAnalyzedLapKey, lapKey, StringComparison.Ordinal))
        {
            return;
        }

        _lastAnalyzedLapKey = lapKey;
        _isAiAnalysisRunning = true;
        var analysisSessionUid = _activeSessionUid;

        try
        {
            var result = await _aiAnalysisService.AnalyzeAsync(
                BuildAiAnalysisContext(sessionState, playerCar, lastLap),
                new AISettings
                {
                    ApiKey = AiApiKey,
                    BaseUrl = AiBaseUrl,
                    Model = AiModel,
                    AiEnabled = AiEnabled,
                    RequestTimeoutSeconds = _aiRequestTimeoutSeconds <= 0 ? 10 : _aiRequestTimeoutSeconds
                },
                _lifecycleCts.Token);

            if (_activeSessionUid != analysisSessionUid ||
                !string.Equals(_lastAnalyzedLapKey, lapKey, StringComparison.Ordinal))
            {
                EnqueueAiTtsLog("System", $"已忽略过期 AI 分析结果：Lap {lastLap.LapNumber}。");
                return;
            }

            _storagePersistenceService.EnqueueAiReport(lastLap.LapNumber, result);

            if (result.IsSuccess)
            {
                EnqueueAiTtsLog("AI", $"Lap {lastLap.LapNumber} · {result.Summary}");
                TryEnqueueAiSpeech(lastLap, result);
            }
            else
            {
                EnqueueAiTtsLog("AI", $"Lap {lastLap.LapNumber} 分析失败：{result.ErrorMessage}");
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isAiAnalysisRunning = false;
        }
    }

    private void TryEnqueueRaceEventSpeech(RaceEvent raceEvent)
    {
        var ttsMessage = _ttsMessageFactory.CreateForRaceEvent(raceEvent, BuildTtsOptions());
        if (ttsMessage is null)
        {
            return;
        }

        _ttsQueue.TryEnqueue(ttsMessage);
    }

    private void TryEnqueueAiSpeech(LapSummary lastLap, AIAnalysisResult result)
    {
        var ttsMessage = _ttsMessageFactory.CreateForAiResult(lastLap, result, BuildTtsOptions());
        if (ttsMessage is null)
        {
            return;
        }

        _ttsQueue.TryEnqueue(ttsMessage);
    }

    private AIAnalysisContext BuildAiAnalysisContext(SessionState sessionState, CarSnapshot? playerCar, LapSummary lastLap)
    {
        var recentLaps = _lapAnalyzer.CaptureRecentLaps(5);
        var carBehind = playerCar?.Position is null
            ? null
            : sessionState.Cars.FirstOrDefault(car => car.Position == playerCar.Position + 1);

        return new AIAnalysisContext
        {
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
            RecentEvents = _recentAiEvents.ToArray()
        };
    }

    private string BuildSessionLapKey(int lapNumber)
    {
        var sessionToken = _activeSessionUid?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
        return $"{sessionToken}:{lapNumber}";
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

    private static LogEntryViewModel CreateLogEntry(string category, string message)
    {
        return new LogEntryViewModel
        {
            Timestamp = DateTimeOffset.Now.ToString("HH:mm:ss"),
            Category = category,
            Message = message
        };
    }

    private static string BuildEventCategory(RaceEvent raceEvent)
    {
        return raceEvent.Severity == EventSeverity.Warning ? "告警" : "事件";
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

        return $"第 {playerCar?.CurrentLapNumber?.ToString() ?? "-"} 圈 / 共 {sessionState.TotalLaps?.ToString() ?? "-"} 圈";
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
}
