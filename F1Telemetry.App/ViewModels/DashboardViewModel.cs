using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Formatting;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Interfaces;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.App.Charts;
using F1Telemetry.App.Formatting;
using F1Telemetry.App.Logging;
using F1Telemetry.App.Services;
using F1Telemetry.App.TrackMaps;
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
    private const int VoiceAiBindingCaptureSeconds = 15;
    private const double ExpandedSidebarWidth = 220d;
    private const double CollapsedSidebarWidth = 80d;
    private const double CompactSidebarAutoCollapseWidth = 1180d;
    private const double ExpandedSidebarAutoRestoreWidth = 1280d;
    private static readonly TimeSpan VoiceAiBindingCaptureArmingDelay = TimeSpan.FromMilliseconds(400);
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
    private readonly ITrackMapTrajectoryStore? _trackMapTrajectoryStore;
    private readonly RealtimeCornerAdviceService _realtimeCornerAdviceService;
    private readonly IEventBus<RaceEvent> _raceEventBus;
    private readonly RaceEventSpeechSubscriber _raceEventSpeechSubscriber;
    private readonly RaceEventInsightBuffer _raceEventInsightBuffer;
    private readonly IUdpRawLogDirectoryService _udpRawLogDirectoryService;
    private readonly AppFileLogger _appFileLogger;
    private readonly RaceAssistantAuditLogger _raceAssistantAuditLogger;
    private readonly LogDirectoryService _logDirectoryService;
    private readonly VoiceAiQueryService _voiceAiQueryService;
    private readonly IMicrophoneService _microphoneService;
    private readonly RaceAssistantSnapshotBuilder _raceAssistantSnapshotBuilder = new();
    private readonly VoiceQuestionIntentClassifier _voiceQuestionIntentClassifier = new();
    private readonly StrategyQuestionContextBuilder _strategyQuestionContextBuilder = new();
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
    private readonly Dictionary<string, VoiceAiButtonRuntimeState> _voiceAiButtonStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _settingsGate = new(1, 1);
    private readonly RelayCommand _startListeningCommand;
    private readonly RelayCommand _stopListeningCommand;
    private readonly RelayCommand _downloadLatestVersionCommand;
    private readonly RelayCommand _toggleSidebarCommand;
    private readonly RelayCommand _openUdpRawLogDirectoryCommand;
    private readonly RelayCommand _openAppLogDirectoryCommand;
    private readonly RelayCommand _openRaceAssistantLogDirectoryCommand;
    private readonly RelayCommand _generatePostRaceAiSummaryCommand;
    private readonly RelayCommand _readTyreSetsInventoryCommand;
    private readonly RelayCommand _clearTyreInventoryCommand;
    private readonly RelayCommand _saveTyreInventoryCommand;
    private readonly RelayCommand _bindVoiceAiInputCommand;
    private readonly RelayCommand _clearVoiceAiInputCommand;
    private readonly RelayCommand _refreshMicrophonesCommand;
    private readonly RelayCommand _testMicrophoneCommand;
    private readonly RelayCommand _askRaceAssistantQuestionCommand;
    private readonly RelayCommand _cancelRaceAssistantQuestionCommand;
    private readonly RelayCommand _toggleRaceAssistantVoiceCommand;
    private readonly RelayCommand _openRaceAssistantCommand;
    private ShellNavigationItemViewModel? _selectedShellNavigationItem;
    private PostRaceAiCompletionModeOptionViewModel? _selectedPostRaceAiCompletionMode;
    private bool _isSidebarExpanded = true;
    private bool _sidebarCollapsedByViewport;
    private bool _isBusy;
    private bool _isListening;
    private bool _isConnected;
    private int? _listeningPort;
    private long _totalPacketCount;
    private int _packetsPerSecond;
    private string _portText = "20777";
    private string _statusMessage = "准备监听 F1 25 UDP。";
    private bool _enableAppFileLog = true;
    private bool _enableRaceAssistantAuditLog = true;
    private bool _raceAssistantLogPromptSummary;
    private string _maxLogFileSizeMbText = "20";
    private string _maxLogRetentionDaysText = "14";
    private string _appLogDirectoryText = string.Empty;
    private string _appLogLastFilePathText = "无";
    private string _appLogLastFileSizeText = "无";
    private string _appLogLastWriteTimeText = "无";
    private string _raceAssistantLogDirectoryText = string.Empty;
    private string _raceAssistantLogLastFilePathText = "无";
    private string _raceAssistantLogLastFileSizeText = "无";
    private string _raceAssistantLogLastWriteTimeText = "无";
    private string _logSettingsStatusText = "日志设置待加载。";
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
    private string _overviewTyreTemperatureText = "等待数据";
    private string _overviewTyrePressureText = "等待数据";
    private string _overviewDamageText = "等待 CarDamage 包";
    private string _overviewKeyOpponentText = "-";
    private string _overviewLapComparisonText = "-";
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
    private int _raceWeekendTyreMaxWearPercent = RaceWeekendTyrePlan.DefaultMaxRecommendedWearPercent;
    private string _raceWeekendTyrePlanStatusText = "等待保存";
    private string _raceWeekendTyreSetsStatusText = "等待 TyreSets 包";
    private int _raceWeekendTyrePlanSaveVersion;
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
    private int _logSettingsSaveVersion;
    private bool _voiceAiEnabled;
    private VoiceAiInputBinding _voiceAiInputBinding = new();
    private VoiceAiTalkMode _voiceAiTalkMode = VoiceAiTalkMode.HoldToTalk;
    private VoiceAiTalkModeOptionViewModel? _selectedVoiceAiTalkModeOption;
    private string _voiceAiBindingText = "未绑定方向盘按钮";
    private string _voiceAiStatusText = "请先绑定方向盘按钮";
    private string _voiceAiMicrophoneDeviceId = string.Empty;
    private string _voiceAiMicrophoneDeviceName = string.Empty;
    private string _voiceAiMicrophoneStatusText = "尚未测试麦克风";
    private double _voiceAiMicrophoneTestLevel;
    private bool _voiceAiNoiseReductionEnabled = true;
    private bool _voiceAiHighPassFilterEnabled = true;
    private string _voiceAiHighPassCutoffHzText = "120";
    private bool _voiceAiNoiseGateEnabled = true;
    private string _voiceAiNoiseGateThresholdDbText = "-40";
    private bool _voiceAiVadEnabled = true;
    private string _voiceAiPreSpeechPaddingMsText = "150";
    private string _voiceAiPostSpeechPaddingMsText = "250";
    private bool _voiceAiAutoGainEnabled = true;
    private string _voiceAiMaxRecordingSecondsText = "8";
    private string _voiceAiMinSpeechDurationMsText = "300";
    private string _voiceAiMinRecognitionConfidenceText = "0.35";
    private string _voiceAiRecognitionStatusDetailText = "录音时长 - · 人声时长 - · 检测到语音 - · 识别文本 - · 识别置信度 - · 失败原因 -";
    private bool _voiceAiBindingCaptureActive;
    private bool _voiceAiRawInputReady;
    private string _voiceAiRawInputStatusText = "方向盘 Raw Input 等待窗口注册。";
    private bool _isVoiceAiRecording;
    private bool _isVoiceAiQueryRunning;
    private bool _isVoiceAiMicrophoneTesting;
    private bool _isStoppingVoiceAiRecording;
    private IVoiceRecordingSession? _voiceAiRecordingSession;
    private CancellationTokenSource? _voiceAiRecordingTimeoutCts;
    private CancellationTokenSource? _voiceAiBindingCaptureCts;
    private CancellationTokenSource? _voiceAiQueryCts;
    private int _voiceAiQueryVersion;
    private DateTimeOffset _voiceAiBindingCaptureArmedAt = DateTimeOffset.MinValue;
    private bool _voiceAiToggleWaitingForRelease;
    private int _voiceAiSettingsSaveVersion;
    private bool _voiceAssistantEnabled;
    private string _voiceAssistantQuestionText = string.Empty;
    private string _voiceAssistantRecognizedText = string.Empty;
    private string _voiceAssistantIntentText = "-";
    private string _voiceAssistantModeText = "-";
    private string _voiceAssistantStatusText = "未录音";
    private string _voiceAssistantAnswerText = string.Empty;
    private string _voiceAssistantAdviceTypeText = "-";
    private string _voiceAssistantSummaryText = string.Empty;
    private string _voiceAssistantReasonText = string.Empty;
    private string _voiceAssistantRecommendedActionText = string.Empty;
    private string _voiceAssistantConfidenceText = "-";
    private string _voiceAssistantRiskLevelText = "-";
    private string _voiceAssistantMissingDataText = "-";
    private string _voiceAssistantMissingDataDetailText = string.Empty;
    private string _voiceAssistantTelemetryNoticeText = string.Empty;
    private bool _voiceAssistantEnableTtsAnswer = true;
    private int _voiceAssistantMaxAnswerLength = 240;
    private int _voiceAssistantRepeatQuestionCooldownSeconds = 12;
    private readonly Dictionary<string, DateTimeOffset> _recentVoiceAssistantQuestions = new(StringComparer.Ordinal);
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
    private readonly Dictionary<string, int> _persistedLapQualityByKey = new(StringComparer.Ordinal);
    private int? _lastTrendRefreshLapNumber;
    private string _postRaceAiStatusText = "等待完整正赛结束后生成 AI 总结。";
    private string _postRaceAiCompletionText = "自动判断：等待 UDP 最终分类。";
    private readonly object _shutdownGate = new();
    private Task? _shutdownTask;
    private bool _disposed;
    private bool _hasRequestedHistoryLoad;
    private bool _isPostRaceReviewRefreshRunning;
    private bool _isSessionComparisonRefreshRunning;
    private bool _isCornerAnalysisRefreshRunning;

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
    /// <param name="postRaceReview">The optional post-race review page view model.</param>
    /// <param name="sessionComparison">The optional multi-session comparison page view model.</param>
    /// <param name="trackMapTrajectoryStore">The optional in-memory track-map trajectory store.</param>
    /// <param name="realtimeCornerAdviceService">The optional realtime corner advice service.</param>
    /// <param name="appFileLogger">The optional categorized app file logger.</param>
    /// <param name="raceAssistantAuditLogger">The optional RaceAssistant audit logger.</param>
    /// <param name="logDirectoryService">The optional log directory helper used by Settings.</param>
    /// <param name="voiceAiQueryService">The optional voice-to-AI query service.</param>
    /// <param name="microphoneService">The optional microphone device service.</param>
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
        HistorySessionBrowserViewModel? historyBrowser = null,
        PostRaceReviewViewModel? postRaceReview = null,
        SessionComparisonViewModel? sessionComparison = null,
        CornerAnalysisViewModel? cornerAnalysis = null,
        ITrackMapTrajectoryStore? trackMapTrajectoryStore = null,
        RealtimeCornerAdviceService? realtimeCornerAdviceService = null,
        AppFileLogger? appFileLogger = null,
        RaceAssistantAuditLogger? raceAssistantAuditLogger = null,
        LogDirectoryService? logDirectoryService = null,
        VoiceAiQueryService? voiceAiQueryService = null,
        IMicrophoneService? microphoneService = null)
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
        _trackMapTrajectoryStore = trackMapTrajectoryStore;
        _realtimeCornerAdviceService = realtimeCornerAdviceService ?? new RealtimeCornerAdviceService(
            _aiAnalysisService,
            _ttsMessageFactory,
            _ttsQueue,
            logSink: (category, message) => EnqueueAiTtsLog(category, message));
        HistoryBrowser = historyBrowser ?? CreateNoOpHistoryBrowser();
        PostRaceReview = postRaceReview ?? CreateNoOpPostRaceReview();
        SessionComparison = sessionComparison ?? CreateNoOpSessionComparison();
        CornerAnalysis = cornerAnalysis ?? CreateNoOpCornerAnalysis();
        _raceEventBus = raceEventBus ?? new InMemoryEventBus<RaceEvent>();
        _raceEventSpeechSubscriber = new RaceEventSpeechSubscriber(
            _raceEventBus,
            _ttsMessageFactory,
            _ttsQueue,
            () => ResolveSessionMode(_sessionStateStore.CaptureState()),
            BuildTtsOptions,
            LogRaceEventSubscriberWarning,
            () => _sessionStateStore.CaptureState().HasFinalClassification);
        _raceEventInsightBuffer = new RaceEventInsightBuffer(_raceEventBus);
        _udpRawLogDirectoryService = udpRawLogDirectoryService ?? new UdpRawLogDirectoryService();
        var runContext = new AppRunContext();
        _appFileLogger = appFileLogger ?? new AppFileLogger(runContext);
        _raceAssistantAuditLogger = raceAssistantAuditLogger ?? new RaceAssistantAuditLogger(runContext);
        _logDirectoryService = logDirectoryService ?? new LogDirectoryService();
        _voiceAiQueryService = voiceAiQueryService ?? new VoiceAiQueryService(
            new WindowsSpeechRecognitionService(),
            _aiAnalysisService,
            _ttsMessageFactory,
            _ttsQueue,
            _raceAssistantAuditLogger);
        _microphoneService = microphoneService ?? new WindowsMicrophoneService();
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
        VoiceAiTalkModeOptions = new ObservableCollection<VoiceAiTalkModeOptionViewModel>(CreateVoiceAiTalkModeOptions());
        _selectedVoiceAiTalkModeOption = VoiceAiTalkModeOptions[0];
        VoiceAiMicrophoneDevices = new ObservableCollection<MicrophoneDeviceInfo>();
        RaceAssistantHistory = new ObservableCollection<RaceAssistantHistoryItemViewModel>();
        RaceWeekendTyreInventoryItems = CreateRaceWeekendTyreInventoryItems();
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
        RefreshVoiceAiMicrophones(persistSelection: false);
        RefreshUdpRawLogStatus();
        RefreshRuntimeLogStatus();

        _startListeningCommand = new RelayCommand(() => _ = StartListeningAsync(), CanStartListening);
        _stopListeningCommand = new RelayCommand(() => _ = StopListeningAsync(), CanStopListening);
        _downloadLatestVersionCommand = new RelayCommand(OpenGitHubReleases);
        _toggleSidebarCommand = new RelayCommand(ToggleSidebar);
        _openUdpRawLogDirectoryCommand = new RelayCommand(OpenUdpRawLogDirectory);
        _openAppLogDirectoryCommand = new RelayCommand(OpenAppLogDirectory);
        _openRaceAssistantLogDirectoryCommand = new RelayCommand(OpenRaceAssistantLogDirectory);
        _readTyreSetsInventoryCommand = new RelayCommand(ReadInventoryFromTyreSets);
        _clearTyreInventoryCommand = new RelayCommand(ClearTyreInventory);
        _saveTyreInventoryCommand = new RelayCommand(ForcePersistRaceWeekendTyrePlan);
        _bindVoiceAiInputCommand = new RelayCommand(StartVoiceAiInputBindingCapture);
        _clearVoiceAiInputCommand = new RelayCommand(ClearVoiceAiInputBinding);
        _refreshMicrophonesCommand = new RelayCommand(RefreshVoiceAiMicrophones);
        _testMicrophoneCommand = new RelayCommand(
            () => _ = TestVoiceAiMicrophoneAsync(),
            () => !IsVoiceAiMicrophoneTesting);
        _askRaceAssistantQuestionCommand = new RelayCommand(
            () => _ = AskRaceAssistantTextQuestionAsync(),
            CanAskRaceAssistantTextQuestion);
        _cancelRaceAssistantQuestionCommand = new RelayCommand(
            () => CancelActiveVoiceAssistantQuery("已取消当前问答。", logAsCanceled: true),
            () => IsVoiceAiQueryRunning);
        _toggleRaceAssistantVoiceCommand = new RelayCommand(
            () =>
            {
                if (IsVoiceAiRecording)
                {
                    _ = StopVoiceAiRecordingAndAskAsync();
                }
                else
                {
                    _ = StartVoiceAiRecordingAsync();
                }
            },
            () => VoiceAssistantEnabled || VoiceAiEnabled);
        _openRaceAssistantCommand = new RelayCommand(OpenRaceAssistantPanel);
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
    /// Gets the post-race review page view model.
    /// </summary>
    public PostRaceReviewViewModel PostRaceReview { get; }

    /// <summary>
    /// Gets the multi-session comparison page view model.
    /// </summary>
    public SessionComparisonViewModel SessionComparison { get; }

    /// <summary>
    /// Gets the V3 corner analysis page view model.
    /// </summary>
    public CornerAnalysisViewModel CornerAnalysis { get; }

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
    /// Applies shell-only responsive sizing rules for the sidebar.
    /// </summary>
    /// <param name="viewportWidth">The current main window width.</param>
    public void ApplyShellViewportWidth(double viewportWidth)
    {
        if (viewportWidth <= 0d)
        {
            return;
        }

        if (viewportWidth <= CompactSidebarAutoCollapseWidth && IsSidebarExpanded)
        {
            _sidebarCollapsedByViewport = true;
            IsSidebarExpanded = false;
            return;
        }

        if (viewportWidth >= ExpandedSidebarAutoRestoreWidth && _sidebarCollapsedByViewport && !IsSidebarExpanded)
        {
            _sidebarCollapsedByViewport = false;
            IsSidebarExpanded = true;
        }
    }

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
                OnPropertyChanged(nameof(IsPostRaceReviewSelected));
                OnPropertyChanged(nameof(IsSessionComparisonSelected));
                OnPropertyChanged(nameof(IsCornerAnalysisSelected));
                OnPropertyChanged(nameof(IsOpponentsSelected));
                OnPropertyChanged(nameof(IsLogsSelected));
                OnPropertyChanged(nameof(IsAiTtsSelected));
                OnPropertyChanged(nameof(IsSettingsSelected));
                OnPropertyChanged(nameof(IsPlaceholderNavigationSelected));
                OnPropertyChanged(nameof(IsLegacyDashboardSelected));
                OnPropertyChanged(nameof(SelectedShellNavigationTitle));
                RequestHistoryBrowserRefreshIfNeeded();
                RequestPostRaceReviewRefreshIfNeeded();
                RequestSessionComparisonRefreshIfNeeded();
                RequestCornerAnalysisRefreshIfNeeded();
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
    /// Gets a value indicating whether the post-race review page is selected.
    /// </summary>
    public bool IsPostRaceReviewSelected => IsSelectedShellNavigationKey("post-race-review");

    /// <summary>
    /// Gets a value indicating whether the multi-session comparison page is selected.
    /// </summary>
    public bool IsSessionComparisonSelected => IsSelectedShellNavigationKey("session-comparison");

    /// <summary>
    /// Gets a value indicating whether the V3 corner analysis page is selected.
    /// </summary>
    public bool IsCornerAnalysisSelected => IsSelectedShellNavigationKey("corner-analysis");

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
        !IsPostRaceReviewSelected &&
        !IsSessionComparisonSelected &&
        !IsCornerAnalysisSelected &&
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
        _sidebarCollapsedByViewport = false;
        IsSidebarExpanded = !IsSidebarExpanded;
    }

    /// <summary>
    /// Observes a steering-wheel Raw Input button edge for binding capture or voice AI triggering.
    /// </summary>
    /// <param name="input">The button edge reported by Raw Input.</param>
    public void ObserveVoiceAiButtonInput(VoiceAiButtonInput input)
    {
        if (_disposed)
        {
            return;
        }

        if (!_dispatcher.CheckAccess())
        {
            _ = _dispatcher.BeginInvoke(new Action(() => ObserveVoiceAiButtonInput(input)));
            return;
        }

        var acceptedInput = TryAcceptVoiceAiButtonEdge(input);
        if (acceptedInput is null)
        {
            return;
        }

        if (VoiceAiBindingCaptureActive)
        {
            TryCaptureVoiceAiInputBinding(acceptedInput);
            return;
        }

        if ((!VoiceAiEnabled && !VoiceAssistantEnabled) || !VoiceAiInputMatchesBinding(acceptedInput))
        {
            return;
        }

        if (acceptedInput.IsPressed)
        {
            HandleBoundVoiceAiButtonPressed();
        }
        else
        {
            HandleBoundVoiceAiButtonReleased();
        }
    }

    private VoiceAiButtonInput? TryAcceptVoiceAiButtonEdge(VoiceAiButtonInput input)
    {
        if (input.ButtonIndex <= 0 || string.IsNullOrWhiteSpace(input.DeviceId))
        {
            return null;
        }

        var key = BuildVoiceAiButtonStateKey(input);
        if (!_voiceAiButtonStates.TryGetValue(key, out var previousState))
        {
            _voiceAiButtonStates[key] = new VoiceAiButtonRuntimeState(input.IsPressed, input.ReceivedAt);
            return input.IsPressed ? input : null;
        }

        if (previousState.IsPressed == input.IsPressed)
        {
            return null;
        }

        _voiceAiButtonStates[key] = new VoiceAiButtonRuntimeState(input.IsPressed, input.ReceivedAt);
        return input;
    }

    /// <summary>
    /// Updates the UI status for the Raw Input listener owned by the shell window.
    /// </summary>
    /// <param name="statusText">The latest Raw Input listener status.</param>
    /// <param name="isReady">Whether Raw Input registration is active.</param>
    public void UpdateVoiceAiRawInputStatus(string statusText, bool isReady)
    {
        _voiceAiRawInputReady = isReady;
        _voiceAiRawInputStatusText = string.IsNullOrWhiteSpace(statusText)
            ? "方向盘 Raw Input 状态未知。"
            : statusText.Trim();

        if (!isReady)
        {
            VoiceAiStatusText = $"{_voiceAiRawInputStatusText} 请检查方向盘驱动、游戏控制器模式或管理员权限。";
            return;
        }

        RefreshVoiceAiStatusText();
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
    /// Gets the structured editable tyre inventory rows.
    /// </summary>
    public ObservableCollection<RaceWeekendTyreInventoryItemViewModel> RaceWeekendTyreInventoryItems { get; }

    /// <summary>
    /// Gets or sets the maximum tyre wear percentage allowed for replacement recommendations.
    /// </summary>
    public int RaceWeekendTyreMaxWearPercent
    {
        get => _raceWeekendTyreMaxWearPercent;
        set
        {
            var normalizedValue = Math.Clamp(value, 0, 100);
            if (SetProperty(ref _raceWeekendTyreMaxWearPercent, normalizedValue))
            {
                _eventDetectionService.UpdateRaceAdviceThresholds(normalizedValue);
                QueuePersistRaceWeekendTyrePlan();
            }
        }
    }

    /// <summary>
    /// Gets the current race-weekend tyre plan save status.
    /// </summary>
    public string RaceWeekendTyrePlanStatusText
    {
        get => _raceWeekendTyrePlanStatusText;
        private set => SetProperty(ref _raceWeekendTyrePlanStatusText, value);
    }

    /// <summary>
    /// Gets the status of the last TyreSets inventory import attempt.
    /// </summary>
    public string RaceWeekendTyreSetsStatusText
    {
        get => _raceWeekendTyreSetsStatusText;
        private set => SetProperty(ref _raceWeekendTyreSetsStatusText, value);
    }

    /// <summary>
    /// Gets the command that imports tyre counts from the latest TyreSets packet.
    /// </summary>
    public ICommand ReadTyreSetsInventoryCommand => _readTyreSetsInventoryCommand;

    /// <summary>
    /// Gets the command that clears all tyre inventory counts.
    /// </summary>
    public ICommand ClearTyreInventoryCommand => _clearTyreInventoryCommand;

    /// <summary>
    /// Gets the command that explicitly saves the current tyre inventory.
    /// </summary>
    public ICommand SaveTyreInventoryCommand => _saveTyreInventoryCommand;

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
    /// Gets or sets a value indicating whether categorized app file logs are enabled.
    /// </summary>
    public bool EnableAppFileLog
    {
        get => _enableAppFileLog;
        set
        {
            if (SetProperty(ref _enableAppFileLog, value))
            {
                ApplyLogSettings();
                QueuePersistLogSettings();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether RaceAssistant audit JSONL is enabled.
    /// </summary>
    public bool EnableRaceAssistantAuditLog
    {
        get => _enableRaceAssistantAuditLog;
        set
        {
            if (SetProperty(ref _enableRaceAssistantAuditLog, value))
            {
                ApplyLogSettings();
                QueuePersistLogSettings();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether prompt summaries may be logged.
    /// </summary>
    public bool RaceAssistantLogPromptSummary
    {
        get => _raceAssistantLogPromptSummary;
        set
        {
            if (SetProperty(ref _raceAssistantLogPromptSummary, value))
            {
                QueuePersistLogSettings();
            }
        }
    }

    /// <summary>
    /// Gets or sets the maximum log file size text in megabytes.
    /// </summary>
    public string MaxLogFileSizeMbText
    {
        get => _maxLogFileSizeMbText;
        set
        {
            if (SetProperty(ref _maxLogFileSizeMbText, value))
            {
                ApplyLogSettings();
                QueuePersistLogSettings();
            }
        }
    }

    /// <summary>
    /// Gets or sets the log retention days text.
    /// </summary>
    public string MaxLogRetentionDaysText
    {
        get => _maxLogRetentionDaysText;
        set
        {
            if (SetProperty(ref _maxLogRetentionDaysText, value))
            {
                ApplyLogSettings();
                QueuePersistLogSettings();
            }
        }
    }

    /// <summary>
    /// Gets the categorized app log directory shown in Settings.
    /// </summary>
    public string AppLogDirectoryText
    {
        get => _appLogDirectoryText;
        private set => SetProperty(ref _appLogDirectoryText, value);
    }

    /// <summary>
    /// Gets the latest categorized app log file path.
    /// </summary>
    public string AppLogLastFilePathText
    {
        get => _appLogLastFilePathText;
        private set => SetProperty(ref _appLogLastFilePathText, value);
    }

    /// <summary>
    /// Gets the latest categorized app log file size.
    /// </summary>
    public string AppLogLastFileSizeText
    {
        get => _appLogLastFileSizeText;
        private set => SetProperty(ref _appLogLastFileSizeText, value);
    }

    /// <summary>
    /// Gets the latest categorized app log write time.
    /// </summary>
    public string AppLogLastWriteTimeText
    {
        get => _appLogLastWriteTimeText;
        private set => SetProperty(ref _appLogLastWriteTimeText, value);
    }

    /// <summary>
    /// Gets the RaceAssistant audit log directory shown in Settings.
    /// </summary>
    public string RaceAssistantLogDirectoryText
    {
        get => _raceAssistantLogDirectoryText;
        private set => SetProperty(ref _raceAssistantLogDirectoryText, value);
    }

    /// <summary>
    /// Gets the latest RaceAssistant audit log file path.
    /// </summary>
    public string RaceAssistantLogLastFilePathText
    {
        get => _raceAssistantLogLastFilePathText;
        private set => SetProperty(ref _raceAssistantLogLastFilePathText, value);
    }

    /// <summary>
    /// Gets the latest RaceAssistant audit log file size.
    /// </summary>
    public string RaceAssistantLogLastFileSizeText
    {
        get => _raceAssistantLogLastFileSizeText;
        private set => SetProperty(ref _raceAssistantLogLastFileSizeText, value);
    }

    /// <summary>
    /// Gets the latest RaceAssistant audit log write time.
    /// </summary>
    public string RaceAssistantLogLastWriteTimeText
    {
        get => _raceAssistantLogLastWriteTimeText;
        private set => SetProperty(ref _raceAssistantLogLastWriteTimeText, value);
    }

    /// <summary>
    /// Gets the runtime log settings status text.
    /// </summary>
    public string LogSettingsStatusText
    {
        get => _logSettingsStatusText;
        private set => SetProperty(ref _logSettingsStatusText, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether microphone AI queries can be triggered by the bound key.
    /// </summary>
    public bool VoiceAiEnabled
    {
        get => _voiceAiEnabled;
        set
        {
            if (SetProperty(ref _voiceAiEnabled, value))
            {
                RefreshVoiceAiStatusText();
                QueuePersistVoiceAiOptions();
                _toggleRaceAssistantVoiceCommand?.RaiseCanExecuteChanged();
                _askRaceAssistantQuestionCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the persisted steering-wheel button binding.
    /// </summary>
    public VoiceAiInputBinding VoiceAiInputBinding
    {
        get => _voiceAiInputBinding;
        private set
        {
            if (SetProperty(ref _voiceAiInputBinding, NormalizeVoiceAiInputBinding(value)))
            {
                ResetVoiceAiButtonRuntimeState();
                RefreshVoiceAiBindingText();
                RefreshVoiceAiStatusText();
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets the user-facing steering-wheel binding label.
    /// </summary>
    public string VoiceAiBindingText
    {
        get => _voiceAiBindingText;
        private set => SetProperty(ref _voiceAiBindingText, value);
    }

    /// <summary>
    /// Gets a value indicating whether the settings page is waiting for a steering-wheel button.
    /// </summary>
    public bool VoiceAiBindingCaptureActive
    {
        get => _voiceAiBindingCaptureActive;
        private set => SetProperty(ref _voiceAiBindingCaptureActive, value);
    }

    /// <summary>
    /// Gets selectable push-to-talk modes.
    /// </summary>
    public ObservableCollection<VoiceAiTalkModeOptionViewModel> VoiceAiTalkModeOptions { get; }

    /// <summary>
    /// Gets or sets the selected push-to-talk mode option.
    /// </summary>
    public VoiceAiTalkModeOptionViewModel? SelectedVoiceAiTalkModeOption
    {
        get => _selectedVoiceAiTalkModeOption;
        set
        {
            var normalized = value ?? VoiceAiTalkModeOptions.FirstOrDefault();
            if (SetProperty(ref _selectedVoiceAiTalkModeOption, normalized))
            {
                VoiceAiTalkMode = normalized?.Mode ?? VoiceAiTalkMode.HoldToTalk;
            }
        }
    }

    /// <summary>
    /// Gets or sets how the bound button controls microphone recording.
    /// </summary>
    public VoiceAiTalkMode VoiceAiTalkMode
    {
        get => _voiceAiTalkMode;
        set
        {
            var normalized = Enum.IsDefined(typeof(VoiceAiTalkMode), value) ? value : VoiceAiTalkMode.HoldToTalk;
            if (SetProperty(ref _voiceAiTalkMode, normalized))
            {
                _voiceAiToggleWaitingForRelease = false;
                var selectedOption = VoiceAiTalkModeOptions.FirstOrDefault(option => option.Mode == normalized)
                                     ?? VoiceAiTalkModeOptions.FirstOrDefault();
                if (!Equals(_selectedVoiceAiTalkModeOption, selectedOption))
                {
                    _selectedVoiceAiTalkModeOption = selectedOption;
                    OnPropertyChanged(nameof(SelectedVoiceAiTalkModeOption));
                }

                RefreshVoiceAiStatusText();
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets available system microphones for voice AI recording.
    /// </summary>
    public ObservableCollection<MicrophoneDeviceInfo> VoiceAiMicrophoneDevices { get; }

    /// <summary>
    /// Gets or sets the selected microphone device identifier.
    /// </summary>
    public string VoiceAiMicrophoneDeviceId
    {
        get => _voiceAiMicrophoneDeviceId;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (SetProperty(ref _voiceAiMicrophoneDeviceId, normalized))
            {
                var deviceName = ResolveVoiceAiMicrophoneDeviceName(normalized);
                if (!string.IsNullOrWhiteSpace(deviceName))
                {
                    VoiceAiMicrophoneDeviceName = deviceName;
                }

                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets the selected microphone display name persisted with the settings.
    /// </summary>
    public string VoiceAiMicrophoneDeviceName
    {
        get => _voiceAiMicrophoneDeviceName;
        private set => SetProperty(ref _voiceAiMicrophoneDeviceName, value?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// Gets the microphone test status shown in Settings.
    /// </summary>
    public string VoiceAiMicrophoneStatusText
    {
        get => _voiceAiMicrophoneStatusText;
        private set => SetProperty(ref _voiceAiMicrophoneStatusText, value);
    }

    /// <summary>
    /// Gets the latest normalized microphone input test level.
    /// </summary>
    public double VoiceAiMicrophoneTestLevel
    {
        get => _voiceAiMicrophoneTestLevel;
        private set => SetProperty(ref _voiceAiMicrophoneTestLevel, Math.Clamp(value, 0d, 1d));
    }

    /// <summary>
    /// Gets or sets a value indicating whether microphone preprocessing is enabled.
    /// </summary>
    public bool VoiceAiNoiseReductionEnabled
    {
        get => _voiceAiNoiseReductionEnabled;
        set
        {
            if (SetProperty(ref _voiceAiNoiseReductionEnabled, value))
            {
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the wind-noise high-pass filter is enabled.
    /// </summary>
    public bool VoiceAiHighPassFilterEnabled
    {
        get => _voiceAiHighPassFilterEnabled;
        set
        {
            if (SetProperty(ref _voiceAiHighPassFilterEnabled, value))
            {
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets or sets the high-pass filter cutoff text in hertz.
    /// </summary>
    public string VoiceAiHighPassCutoffHzText
    {
        get => _voiceAiHighPassCutoffHzText;
        set
        {
            if (SetProperty(ref _voiceAiHighPassCutoffHzText, value?.Trim() ?? string.Empty))
            {
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the microphone noise gate is enabled.
    /// </summary>
    public bool VoiceAiNoiseGateEnabled
    {
        get => _voiceAiNoiseGateEnabled;
        set
        {
            if (SetProperty(ref _voiceAiNoiseGateEnabled, value))
            {
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets or sets the noise gate threshold text in dBFS.
    /// </summary>
    public string VoiceAiNoiseGateThresholdDbText
    {
        get => _voiceAiNoiseGateThresholdDbText;
        set
        {
            if (SetProperty(ref _voiceAiNoiseGateThresholdDbText, value?.Trim() ?? string.Empty))
            {
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether voice activity detection is enabled.
    /// </summary>
    public bool VoiceAiVadEnabled
    {
        get => _voiceAiVadEnabled;
        set
        {
            if (SetProperty(ref _voiceAiVadEnabled, value))
            {
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets or sets the VAD pre-speech padding text in milliseconds.
    /// </summary>
    public string VoiceAiPreSpeechPaddingMsText
    {
        get => _voiceAiPreSpeechPaddingMsText;
        set
        {
            if (SetProperty(ref _voiceAiPreSpeechPaddingMsText, value?.Trim() ?? string.Empty))
            {
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets or sets the VAD post-speech padding text in milliseconds.
    /// </summary>
    public string VoiceAiPostSpeechPaddingMsText
    {
        get => _voiceAiPostSpeechPaddingMsText;
        set
        {
            if (SetProperty(ref _voiceAiPostSpeechPaddingMsText, value?.Trim() ?? string.Empty))
            {
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether automatic microphone gain is enabled.
    /// </summary>
    public bool VoiceAiAutoGainEnabled
    {
        get => _voiceAiAutoGainEnabled;
        set
        {
            if (SetProperty(ref _voiceAiAutoGainEnabled, value))
            {
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets or sets the maximum recording duration text in seconds.
    /// </summary>
    public string VoiceAiMaxRecordingSecondsText
    {
        get => _voiceAiMaxRecordingSecondsText;
        set
        {
            if (SetProperty(ref _voiceAiMaxRecordingSecondsText, value?.Trim() ?? string.Empty))
            {
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets or sets the minimum speech duration text in milliseconds.
    /// </summary>
    public string VoiceAiMinSpeechDurationMsText
    {
        get => _voiceAiMinSpeechDurationMsText;
        set
        {
            if (SetProperty(ref _voiceAiMinSpeechDurationMsText, value?.Trim() ?? string.Empty))
            {
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets or sets the minimum speech recognition confidence text.
    /// </summary>
    public string VoiceAiMinRecognitionConfidenceText
    {
        get => _voiceAiMinRecognitionConfidenceText;
        set
        {
            if (SetProperty(ref _voiceAiMinRecognitionConfidenceText, value?.Trim() ?? string.Empty))
            {
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets the latest microphone preprocessing and recognition status details.
    /// </summary>
    public string VoiceAiRecognitionStatusDetailText
    {
        get => _voiceAiRecognitionStatusDetailText;
        private set => SetProperty(ref _voiceAiRecognitionStatusDetailText, value);
    }

    /// <summary>
    /// Gets a value indicating whether microphone recording is active.
    /// </summary>
    public bool IsVoiceAiRecording
    {
        get => _isVoiceAiRecording;
        private set => SetProperty(ref _isVoiceAiRecording, value);
    }

    /// <summary>
    /// Gets a value indicating whether a microphone input test is running.
    /// </summary>
    public bool IsVoiceAiMicrophoneTesting
    {
        get => _isVoiceAiMicrophoneTesting;
        private set
        {
            if (SetProperty(ref _isVoiceAiMicrophoneTesting, value))
            {
                _testMicrophoneCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the command that captures the next Raw Input steering-wheel button press.
    /// </summary>
    public ICommand BindVoiceAiInputCommand => _bindVoiceAiInputCommand;

    /// <summary>
    /// Gets the command that clears the saved steering-wheel voice AI binding.
    /// </summary>
    public ICommand ClearVoiceAiInputCommand => _clearVoiceAiInputCommand;

    /// <summary>
    /// Gets the command that refreshes the system microphone list.
    /// </summary>
    public ICommand RefreshMicrophonesCommand => _refreshMicrophonesCommand;

    /// <summary>
    /// Gets the command that records a short microphone input test.
    /// </summary>
    public ICommand TestMicrophoneCommand => _testMicrophoneCommand;

    /// <summary>
    /// Gets the current microphone AI query status shown in Settings.
    /// </summary>
    public string VoiceAiStatusText
    {
        get => _voiceAiStatusText;
        private set => SetProperty(ref _voiceAiStatusText, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the V1 race assistant question panel is enabled.
    /// </summary>
    public bool VoiceAssistantEnabled
    {
        get => _voiceAssistantEnabled;
        set
        {
            if (SetProperty(ref _voiceAssistantEnabled, value))
            {
                QueuePersistVoiceAiOptions();
                _toggleRaceAssistantVoiceCommand?.RaiseCanExecuteChanged();
                _askRaceAssistantQuestionCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the typed fallback question for the race assistant.
    /// </summary>
    public string VoiceAssistantQuestionText
    {
        get => _voiceAssistantQuestionText;
        set
        {
            if (SetProperty(ref _voiceAssistantQuestionText, value ?? string.Empty))
            {
                _askRaceAssistantQuestionCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the latest recognized voice or typed question text.
    /// </summary>
    public string VoiceAssistantRecognizedText
    {
        get => _voiceAssistantRecognizedText;
        private set => SetProperty(ref _voiceAssistantRecognizedText, value);
    }

    /// <summary>
    /// Gets the latest recognized intent label.
    /// </summary>
    public string VoiceAssistantIntentText
    {
        get => _voiceAssistantIntentText;
        private set => SetProperty(ref _voiceAssistantIntentText, value);
    }

    /// <summary>
    /// Gets the latest race-assistant mode label.
    /// </summary>
    public string VoiceAssistantModeText
    {
        get => _voiceAssistantModeText;
        private set => SetProperty(ref _voiceAssistantModeText, value);
    }

    /// <summary>
    /// Gets the race assistant request status text.
    /// </summary>
    public string VoiceAssistantStatusText
    {
        get => _voiceAssistantStatusText;
        private set => SetProperty(ref _voiceAssistantStatusText, value);
    }

    /// <summary>
    /// Gets the latest short race-assistant answer.
    /// </summary>
    public string VoiceAssistantAnswerText
    {
        get => _voiceAssistantAnswerText;
        private set => SetProperty(ref _voiceAssistantAnswerText, value);
    }

    /// <summary>
    /// Gets the latest structured advice type.
    /// </summary>
    public string VoiceAssistantAdviceTypeText
    {
        get => _voiceAssistantAdviceTypeText;
        private set => SetProperty(ref _voiceAssistantAdviceTypeText, value);
    }

    /// <summary>
    /// Gets the latest structured advice summary.
    /// </summary>
    public string VoiceAssistantSummaryText
    {
        get => _voiceAssistantSummaryText;
        private set => SetProperty(ref _voiceAssistantSummaryText, value);
    }

    /// <summary>
    /// Gets the latest structured advice reason.
    /// </summary>
    public string VoiceAssistantReasonText
    {
        get => _voiceAssistantReasonText;
        private set => SetProperty(ref _voiceAssistantReasonText, value);
    }

    /// <summary>
    /// Gets the latest structured recommended action.
    /// </summary>
    public string VoiceAssistantRecommendedActionText
    {
        get => _voiceAssistantRecommendedActionText;
        private set => SetProperty(ref _voiceAssistantRecommendedActionText, value);
    }

    /// <summary>
    /// Gets the latest confidence label.
    /// </summary>
    public string VoiceAssistantConfidenceText
    {
        get => _voiceAssistantConfidenceText;
        private set => SetProperty(ref _voiceAssistantConfidenceText, value);
    }

    /// <summary>
    /// Gets the latest risk label.
    /// </summary>
    public string VoiceAssistantRiskLevelText
    {
        get => _voiceAssistantRiskLevelText;
        private set => SetProperty(ref _voiceAssistantRiskLevelText, value);
    }

    /// <summary>
    /// Gets the latest missing data label.
    /// </summary>
    public string VoiceAssistantMissingDataText
    {
        get => _voiceAssistantMissingDataText;
        private set => SetProperty(ref _voiceAssistantMissingDataText, value);
    }

    /// <summary>
    /// Gets the latest complete localized missing-data details.
    /// </summary>
    public string VoiceAssistantMissingDataDetailText
    {
        get => _voiceAssistantMissingDataDetailText;
        private set => SetProperty(ref _voiceAssistantMissingDataDetailText, value);
    }

    /// <summary>
    /// Gets the telemetry availability notice shown above the race-assistant panel.
    /// </summary>
    public string VoiceAssistantTelemetryNoticeText
    {
        get => _voiceAssistantTelemetryNoticeText;
        private set => SetProperty(ref _voiceAssistantTelemetryNoticeText, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether race assistant answers may use TTS.
    /// </summary>
    public bool VoiceAssistantEnableTtsAnswer
    {
        get => _voiceAssistantEnableTtsAnswer;
        set
        {
            if (SetProperty(ref _voiceAssistantEnableTtsAnswer, value))
            {
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets or sets the maximum displayed race assistant answer length.
    /// </summary>
    public int VoiceAssistantMaxAnswerLength
    {
        get => _voiceAssistantMaxAnswerLength;
        set
        {
            var normalized = Math.Clamp(value, 35, 2_000);
            if (SetProperty(ref _voiceAssistantMaxAnswerLength, normalized))
            {
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets or sets the repeated question cooldown in seconds.
    /// </summary>
    public int VoiceAssistantRepeatQuestionCooldownSeconds
    {
        get => _voiceAssistantRepeatQuestionCooldownSeconds;
        set
        {
            var normalized = Math.Clamp(value, 5, 600);
            if (SetProperty(ref _voiceAssistantRepeatQuestionCooldownSeconds, normalized))
            {
                QueuePersistVoiceAiOptions();
            }
        }
    }

    /// <summary>
    /// Gets the in-memory race assistant question history.
    /// </summary>
    public ObservableCollection<RaceAssistantHistoryItemViewModel> RaceAssistantHistory { get; }

    /// <summary>
    /// Gets the command that submits the typed race assistant question.
    /// </summary>
    public ICommand AskRaceAssistantQuestionCommand => _askRaceAssistantQuestionCommand;

    /// <summary>
    /// Gets the command that submits the typed race assistant question.
    /// </summary>
    public ICommand AskEngineerCommand => _askRaceAssistantQuestionCommand;

    /// <summary>
    /// Gets the command that cancels the active race assistant question.
    /// </summary>
    public ICommand CancelRaceAssistantQuestionCommand => _cancelRaceAssistantQuestionCommand;

    /// <summary>
    /// Gets the command that starts or stops push-to-talk recording from the AI/TTS page.
    /// </summary>
    public ICommand ToggleRaceAssistantVoiceCommand => _toggleRaceAssistantVoiceCommand;

    /// <summary>
    /// Gets the command that opens the AI/TTS page from the overview.
    /// </summary>
    public ICommand OpenRaceAssistantCommand => _openRaceAssistantCommand;

    /// <summary>
    /// Gets a value indicating whether a voice AI query is currently in progress.
    /// </summary>
    public bool IsVoiceAiQueryRunning
    {
        get => _isVoiceAiQueryRunning;
        private set
        {
            if (SetProperty(ref _isVoiceAiQueryRunning, value))
            {
                _askRaceAssistantQuestionCommand?.RaiseCanExecuteChanged();
                _cancelRaceAssistantQuestionCommand?.RaiseCanExecuteChanged();
            }
        }
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
    /// Gets the command that opens the categorized app log directory.
    /// </summary>
    public ICommand OpenAppLogDirectoryCommand => _openAppLogDirectoryCommand;

    /// <summary>
    /// Gets the command that opens the RaceAssistant audit log directory.
    /// </summary>
    public ICommand OpenRaceAssistantLogDirectoryCommand => _openRaceAssistantLogDirectoryCommand;

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
    /// Gets the overview tyre temperature summary.
    /// </summary>
    public string OverviewTyreTemperatureText
    {
        get => _overviewTyreTemperatureText;
        private set => SetProperty(ref _overviewTyreTemperatureText, value);
    }

    /// <summary>
    /// Gets the overview tyre pressure summary.
    /// </summary>
    public string OverviewTyrePressureText
    {
        get => _overviewTyrePressureText;
        private set => SetProperty(ref _overviewTyrePressureText, value);
    }

    /// <summary>
    /// Gets the overview key opponent summary.
    /// </summary>
    public string OverviewKeyOpponentText
    {
        get => _overviewKeyOpponentText;
        private set => SetProperty(ref _overviewKeyOpponentText, value);
    }

    /// <summary>
    /// Gets the compact previous-lap comparison against adjacent cars.
    /// </summary>
    public string OverviewLapComparisonText
    {
        get => _overviewLapComparisonText;
        private set => SetProperty(ref _overviewLapComparisonText, value);
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
        CancelActiveVoiceAssistantQuery("语音问答已取消。", logAsCanceled: true);
        CancelVoiceAiBindingCapture();
        DisposeVoiceAiRecordingSession();
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
            if ((object)PostRaceReview is IDisposable disposablePostRaceReview)
            {
                disposablePostRaceReview.Dispose();
            }
        }
        catch
        {
        }

        try
        {
            if ((object)SessionComparison is IDisposable disposableSessionComparison)
            {
                disposableSessionComparison.Dispose();
            }
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

        await FlushLoggersAsync().ConfigureAwait(false);

        try
        {
            await _raceAssistantAuditLogger.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
        }

        try
        {
            await _appFileLogger.DisposeAsync().ConfigureAwait(false);
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

    private void RequestPostRaceReviewRefreshIfNeeded()
    {
        if (_isPostRaceReviewRefreshRunning || !IsPostRaceReviewSelected)
        {
            return;
        }

        _ = RefreshPostRaceReviewAsync();
    }

    private async Task RefreshPostRaceReviewAsync()
    {
        _isPostRaceReviewRefreshRunning = true;
        try
        {
            await PostRaceReview.RefreshAsync(_lifecycleCts.Token);
        }
        catch (OperationCanceledException) when (_lifecycleCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            StatusMessage = $"赛后复盘刷新失败：{ex.Message}";
            EnqueueEventLog("System", StatusMessage);
            Debug.WriteLine($"Failed to refresh post-race review: {ex}");
        }
        finally
        {
            _isPostRaceReviewRefreshRunning = false;
        }
    }

    private void RequestSessionComparisonRefreshIfNeeded()
    {
        if (_isSessionComparisonRefreshRunning || !IsSessionComparisonSelected)
        {
            return;
        }

        _ = RefreshSessionComparisonAsync();
    }

    private async Task RefreshSessionComparisonAsync()
    {
        _isSessionComparisonRefreshRunning = true;
        try
        {
            await SessionComparison.RefreshAsync(_lifecycleCts.Token);
        }
        catch (OperationCanceledException) when (_lifecycleCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            StatusMessage = $"多会话对比刷新失败：{ex.Message}";
            EnqueueEventLog("System", StatusMessage);
            Debug.WriteLine($"Failed to refresh session comparison: {ex}");
        }
        finally
        {
            _isSessionComparisonRefreshRunning = false;
        }
    }

    private void RequestCornerAnalysisRefreshIfNeeded()
    {
        if (_isCornerAnalysisRefreshRunning || !IsCornerAnalysisSelected)
        {
            return;
        }

        _ = RefreshCornerAnalysisAsync();
    }

    private async Task RefreshCornerAnalysisAsync()
    {
        _isCornerAnalysisRefreshRunning = true;
        try
        {
            await CornerAnalysis.RefreshAsync(_lifecycleCts.Token);
        }
        catch (OperationCanceledException) when (_lifecycleCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            StatusMessage = $"弯角分析刷新失败：{ex.Message}";
            EnqueueEventLog("System", StatusMessage);
            Debug.WriteLine($"Failed to refresh corner analysis: {ex}");
        }
        finally
        {
            _isCornerAnalysisRefreshRunning = false;
        }
    }

    private static HistorySessionBrowserViewModel CreateNoOpHistoryBrowser()
    {
        return new HistorySessionBrowserViewModel(new NoOpSessionRepository(), new NoOpLapRepository());
    }

    private static PostRaceReviewViewModel CreateNoOpPostRaceReview()
    {
        var historyBrowser = CreateNoOpHistoryBrowser();
        return new PostRaceReviewViewModel(
            historyBrowser,
            new NoOpLapRepository(),
            new NoOpEventRepository(),
            new NoOpAiReportRepository());
    }

    private static SessionComparisonViewModel CreateNoOpSessionComparison()
    {
        return new SessionComparisonViewModel(new NoOpSessionRepository(), new NoOpLapRepository());
    }

    private static CornerAnalysisViewModel CreateNoOpCornerAnalysis()
    {
        return new CornerAnalysisViewModel(CreateNoOpHistoryBrowser(), new NoOpLapSampleRepository());
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

    private async Task FlushLoggersAsync()
    {
        try
        {
            await Task.WhenAll(
                _appFileLogger.FlushAsync(TimeSpan.FromMilliseconds(1500)),
                _raceAssistantAuditLogger.FlushAsync(TimeSpan.FromMilliseconds(1500))).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private async void OpenAppLogDirectory()
    {
        await OpenRuntimeLogDirectoryAsync(_appFileLogger.Status.DirectoryPath, isRaceAssistant: false);
    }

    private async void OpenRaceAssistantLogDirectory()
    {
        await OpenRuntimeLogDirectoryAsync(_raceAssistantAuditLogger.Status.DirectoryPath, isRaceAssistant: true);
    }

    private async Task OpenRuntimeLogDirectoryAsync(string directoryPath, bool isRaceAssistant)
    {
        try
        {
            var result = await Task
                .Run(() => _logDirectoryService.OpenDirectory(directoryPath))
                .ConfigureAwait(false);

            _dispatcher.Invoke(() =>
            {
                LogSettingsStatusText = result.Succeeded ? "日志目录已打开" : result.ErrorMessage;
                RefreshRuntimeLogStatus();
            });
        }
        catch (Exception ex)
        {
            _dispatcher.Invoke(() =>
            {
                LogSettingsStatusText = isRaceAssistant
                    ? $"打开 RaceAssistant 日志目录失败：{ex.Message}"
                    : $"打开 App 日志目录失败：{ex.Message}";
                RefreshRuntimeLogStatus();
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
                CancelActiveVoiceAssistantQuery("会话已变化，已忽略旧回答", logAsCanceled: true);
                _activeSessionUid = null;
                _lastPostRaceAiSummaryKey = null;
                _lastStagedPostRaceAiKey = null;
                _realtimeCornerAdviceService.Reset();
                _persistedLapQualityByKey.Clear();
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
            CancelActiveVoiceAssistantQuery("会话已变化，已忽略旧回答", logAsCanceled: true);
            _activeSessionUid = null;
            _lastPostRaceAiSummaryKey = null;
            _lastStagedPostRaceAiKey = null;
            _realtimeCornerAdviceService.Reset();
            _persistedLapQualityByKey.Clear();
            _lastTrendRefreshLapNumber = null;
            ResetChartPanels();
            StatusMessage = "UDP 监听已停止。";
            EnqueueEventLog("系统", StatusMessage);
            await FlushLoggersAsync();
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

        CancelActiveVoiceAssistantQuery("会话已变化，已忽略旧回答", logAsCanceled: true);
        _ = FlushLoggersAsync();
        MarkIncompleteRaceAsStaged(_sessionStateStore.CaptureState(), "检测到新的 UDP session，上一场正赛未收到最终分类。");
        _sessionStateStore.Reset();
        _eventDetectionService.Reset();
        _lapAnalyzer.ResetForSession(incomingSessionUid);
        _lastPostRaceAiSummaryKey = null;
        _lastStagedPostRaceAiKey = null;
        _realtimeCornerAdviceService.Reset();
        _persistedLapQualityByKey.Clear();
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
        RefreshRuntimeLogStatus();
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
        var sessionMode = ResolveSessionMode(sessionState);

        TrackText = BuildTrackText(sessionState.TrackId);
        SessionTypeText = SessionModeFormatter.FormatDisplayName(sessionMode);
        OverviewSessionFocusText = SessionModeFormatter.FormatFocus(sessionMode);
        WeatherText = BuildWeatherText(sessionState);
        LapText = BuildLapText(sessionState, playerCar);
        UpdatePlayerCard(sessionState, playerCar);
        RebuildOpponentCars(sessionState.Opponents, playerCar);
        RefreshLapHistory();
        PersistUnpersistedLapsIfNeeded();
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
            OverviewTyreTemperatureText = "等待数据";
            OverviewTyrePressureText = "等待数据";
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
        OverviewTyreTemperatureText = BuildTyreTemperatureText(playerCar.TyreCondition);
        OverviewTyrePressureText = BuildTyrePressureText(playerCar.TyreCondition);
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
        OverviewLapComparisonText = BuildOverviewLapComparisonText(playerCar);
    }

    private string BuildOverviewLapComparisonText(CarSnapshot? playerCar)
    {
        if (playerCar is null || playerCar.Position is null)
        {
            return "-";
        }

        var front = _sessionStateStore.CaptureState().Cars.FirstOrDefault(car => car.Position == playerCar.Position - 1);
        var rear = _sessionStateStore.CaptureState().Cars.FirstOrDefault(car => car.Position == playerCar.Position + 1);
        var frontComparison = AdjacentLapComparisonBuilder.BuildFrontComparison(playerCar, front);
        var rearComparison = AdjacentLapComparisonBuilder.BuildRearComparison(playerCar, rear);
        var parts = new[]
            {
                FormatOverviewComparison("前车", frontComparison),
                FormatOverviewComparison("后车", rearComparison)
            }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0 ? "等待同圈数据" : string.Join(" / ", parts);
    }

    private static string? FormatOverviewComparison(string label, AdjacentLapComparison? comparison)
    {
        if (comparison is null)
        {
            return null;
        }

        var deltaSeconds = Math.Abs(comparison.DeltaInMs) / 1000d;
        var sign = comparison.DeltaInMs < 0 ? "-" : "+";
        return $"{label} {sign}{deltaSeconds:0.000}s";
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

    private void PersistUnpersistedLapsIfNeeded()
    {
        var laps = _lapAnalyzer.CaptureAllLaps();
        if (laps.Count == 0)
        {
            return;
        }

        foreach (var lap in laps.OrderBy(lap => lap.LapNumber))
        {
            var lapKey = BuildSessionLapKey(lap.LapNumber);
            var quality = CalculateLapPersistenceQuality(lap);
            if (_persistedLapQualityByKey.TryGetValue(lapKey, out var persistedQuality)
                && persistedQuality >= quality)
            {
                continue;
            }

            _persistedLapQualityByKey[lapKey] = quality;
            _storagePersistenceService.EnqueueLapSummary(lap);
            var lapSamples = _lapAnalyzer.CaptureCompletedLapSamples(lap.LapNumber);
            _storagePersistenceService.EnqueueLapSamples(lap.LapNumber, lapSamples);
            RecordTrackMapTrajectory(lap.LapNumber, lapSamples);
            TriggerRealtimeCornerAdvice(lap, lapSamples);
        }
    }

    private void TriggerRealtimeCornerAdvice(LapSummary lap, IReadOnlyList<LapSample> lapSamples)
    {
        if (lapSamples.Count == 0)
        {
            return;
        }

        var sessionState = _sessionStateStore.CaptureState();
        var request = new RealtimeCornerAdviceRequest
        {
            SessionState = sessionState,
            ActiveSessionUid = _activeSessionUid,
            CompletedLap = lap,
            LapSamples = lapSamples,
            RecentCompletedLaps = _lapAnalyzer.CaptureRecentLaps(8),
            AiSettings = BuildAiSettings(),
            TtsOptions = BuildTtsOptions()
        };
        _ = _realtimeCornerAdviceService.EvaluateCompletedLapAsync(request, _lifecycleCts.Token);
    }

    private async Task StartVoiceAiRecordingAsync()
    {
        if (_disposed || IsVoiceAiRecording)
        {
            return;
        }

        if (IsVoiceAiQueryRunning || _isStoppingVoiceAiRecording)
        {
            VoiceAiStatusText = "AI 正在处理上一条问题，请稍后再试";
            VoiceAssistantStatusText = "AI生成中";
            return;
        }

        try
        {
            _voiceAiRecordingSession = _microphoneService.StartRecording(VoiceAiMicrophoneDeviceId);
            IsVoiceAiRecording = true;
            VoiceAiStatusText = VoiceAiTalkMode == VoiceAiTalkMode.HoldToTalk
                ? "正在录音，松开方向盘按钮后提交"
                : "正在录音，再按一次方向盘按钮后提交";
            VoiceAssistantStatusText = "正在听";
            EnqueueAiTtsLog("VoiceAI", $"语音 AI 开始录音：{VoiceAiBindingText}");
            StartVoiceAiRecordingTimeout();
        }
        catch (Exception ex)
        {
            VoiceAiStatusText = $"麦克风启动失败：{FormatVoiceAiMicrophoneError(ex)}";
            VoiceAssistantStatusText = "失败：麦克风不可用";
            EnqueueAiTtsLog("VoiceAI", VoiceAiStatusText);
            DisposeVoiceAiRecordingSession();
        }
    }

    private async Task StopVoiceAiRecordingAndAskAsync()
    {
        if (_isStoppingVoiceAiRecording)
        {
            return;
        }

        var session = _voiceAiRecordingSession;
        if (session is null)
        {
            return;
        }

        _isStoppingVoiceAiRecording = true;
        CancelVoiceAiRecordingTimeout();
        _voiceAiRecordingSession = null;
        IsVoiceAiRecording = false;
        VoiceAiStatusText = "正在整理录音...";
        VoiceAssistantStatusText = "正在识别";

        try
        {
            VoiceRecordingResult recording;
            try
            {
                recording = await session.StopAsync(_lifecycleCts.Token);
            }
            finally
            {
                session.Dispose();
            }

            VoiceAiMicrophoneTestLevel = recording.PeakLevel;
            await RunVoiceAiQueryAsync(recording);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            VoiceAiStatusText = $"语音录音失败：{ex.Message}";
            VoiceAssistantStatusText = FormatVoiceAssistantFailure(ex.Message);
            EnqueueAiTtsLog("VoiceAI", VoiceAiStatusText);
        }
        finally
        {
            _isStoppingVoiceAiRecording = false;
        }
    }

    private async Task RunVoiceAiQueryAsync(VoiceRecordingResult recording)
    {
        var (requestVersion, requestCts) = BeginVoiceAssistantQuery("正在识别");
        try
        {
            VoiceAiStatusText = "正在识别语音问题...";
            VoiceAssistantStatusText = "正在识别";
            EnqueueAiTtsLog("VoiceAI", $"语音 AI 已触发：{VoiceAiBindingText}");

            var result = await _voiceAiQueryService.AskAsync(
                BuildVoiceAiQueryRequest(recording),
                requestCts.Token);

            if (requestVersion != _voiceAiQueryVersion)
            {
                return;
            }

            ApplyVoiceAssistantResult(result);
            if (!result.IsSuccess)
            {
                return;
            }
        }
        catch (OperationCanceledException)
        {
            if (requestVersion == _voiceAiQueryVersion)
            {
                VoiceAiStatusText = "语音问答已取消";
                VoiceAssistantStatusText = "未录音";
                EnqueueAiTtsLog("VoiceAI", "语音 AI 已取消。");
            }
        }
        catch (Exception ex)
        {
            VoiceAiStatusText = $"语音 AI 失败：{ex.Message}";
            VoiceAssistantStatusText = FormatVoiceAssistantFailure(ex.Message);
            EnqueueAiTtsLog("VoiceAI", VoiceAiStatusText);
        }
        finally
        {
            CompleteVoiceAssistantQuery(requestVersion, requestCts);
        }
    }

    private async Task AskRaceAssistantTextQuestionAsync()
    {
        var question = VoiceAssistantQuestionText.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            VoiceAssistantStatusText = "失败：请输入问题";
            return;
        }

        if (!TryReserveVoiceAssistantQuestion(question, out var cooldownMessage))
        {
            VoiceAssistantStatusText = cooldownMessage;
            EnqueueAiTtsLog("VoiceAI", cooldownMessage);
            return;
        }

        var (requestVersion, requestCts) = BeginVoiceAssistantQuery("AI生成中");
        try
        {
            VoiceAssistantStatusText = "AI生成中";
            VoiceAssistantRecognizedText = question;
            var context = BuildStrategyQuestionContext(question);
            var result = await _voiceAiQueryService.AskTextAsync(
                BuildVoiceAiQueryRequest(question, context),
                requestCts.Token);

            if (requestVersion != _voiceAiQueryVersion)
            {
                return;
            }

            ApplyVoiceAssistantResult(result);
        }
        catch (OperationCanceledException)
        {
            if (requestVersion == _voiceAiQueryVersion)
            {
                VoiceAssistantStatusText = "未录音";
                VoiceAiStatusText = "语音问答已取消";
                EnqueueAiTtsLog("VoiceAI", "文字问答已取消。");
            }
        }
        catch (Exception ex)
        {
            var failure = FormatVoiceAssistantFailure(ex.Message);
            VoiceAssistantStatusText = failure;
            VoiceAiStatusText = failure;
            EnqueueAiTtsLog("VoiceAI", failure);
        }
        finally
        {
            CompleteVoiceAssistantQuery(requestVersion, requestCts);
        }
    }

    private VoiceAiQueryRequest BuildVoiceAiQueryRequest(VoiceRecordingResult recording)
    {
        var sessionState = _sessionStateStore.CaptureState();
        var context = BuildAiAnalysisContext(sessionState, sessionState.PlayerCar, _lapAnalyzer.CaptureLastLap());
        return new VoiceAiQueryRequest
        {
            BaseContext = context,
            AiSettings = BuildAiSettings(),
            TtsOptions = BuildTtsOptions(),
            AdviceKey = string.Empty,
            Track = BuildTrackText(sessionState.TrackId),
            SessionType = SessionModeFormatter.FormatDisplayName(ResolveSessionMode(sessionState)),
            UdpRawLogFile = _udpRawLogWriter.Status.CurrentFilePath,
            Recording = recording,
            AudioSettings = BuildVoiceInputAudioSettings(),
            BuildStrategyQuestionContext = question =>
            {
                if (!TryReserveVoiceAssistantQuestion(question, out var cooldownMessage))
                {
                    throw new InvalidOperationException(cooldownMessage);
                }

                return BuildStrategyQuestionContext(question);
            },
            CaptureCurrentSessionUid = () => _activeSessionUid,
            EnableTtsAnswer = VoiceAssistantEnableTtsAnswer,
            MaxAnswerLength = VoiceAssistantMaxAnswerLength
        };
    }

    private VoiceAiQueryRequest BuildVoiceAiQueryRequest(
        string question,
        StrategyQuestionContext strategyContext)
    {
        var sessionState = _sessionStateStore.CaptureState();
        var context = BuildAiAnalysisContext(sessionState, sessionState.PlayerCar, _lapAnalyzer.CaptureLastLap());
        return new VoiceAiQueryRequest
        {
            BaseContext = context,
            AiSettings = BuildAiSettings(),
            TtsOptions = BuildTtsOptions(),
            AdviceKey = BuildVoiceAiAdviceKey(question, strategyContext.Intent),
            QuestionText = question,
            Track = BuildTrackText(sessionState.TrackId),
            SessionType = SessionModeFormatter.FormatDisplayName(ResolveSessionMode(sessionState)),
            UdpRawLogFile = _udpRawLogWriter.Status.CurrentFilePath,
            StrategyQuestionContext = strategyContext,
            CaptureCurrentSessionUid = () => _activeSessionUid,
            EnableTtsAnswer = VoiceAssistantEnableTtsAnswer,
            MaxAnswerLength = VoiceAssistantMaxAnswerLength
        };
    }

    private string BuildVoiceAiAdviceKey(string question, VoiceQuestionIntent intent)
    {
        var sessionToken = _activeSessionUid?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
        var normalizedQuestion = NormalizeVoiceAssistantQuestion(question);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedQuestion));
        var hash = Convert.ToHexString(hashBytes)[..12];
        return $"voice-ai:{sessionToken}:{intent}:{hash}";
    }

    private (int Version, CancellationTokenSource TokenSource) BeginVoiceAssistantQuery(string statusText)
    {
        CancelActiveVoiceAssistantQuery("已取消上一个未完成问答。", logAsCanceled: true);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifecycleCts.Token);
        _voiceAiQueryCts = cts;
        var version = ++_voiceAiQueryVersion;
        IsVoiceAiQueryRunning = true;
        VoiceAssistantStatusText = statusText;
        return (version, cts);
    }

    private void CompleteVoiceAssistantQuery(int requestVersion, CancellationTokenSource requestCts)
    {
        if (requestVersion == _voiceAiQueryVersion)
        {
            IsVoiceAiQueryRunning = false;
            if (ReferenceEquals(_voiceAiQueryCts, requestCts))
            {
                _voiceAiQueryCts = null;
            }
        }

        try
        {
            requestCts.Dispose();
        }
        catch
        {
        }
    }

    private void CancelActiveVoiceAssistantQuery(string statusText, bool logAsCanceled)
    {
        var cts = _voiceAiQueryCts;
        if (cts is null && !IsVoiceAiQueryRunning)
        {
            return;
        }

        _voiceAiQueryVersion++;
        _voiceAiQueryCts = null;
        try
        {
            cts?.Cancel();
        }
        catch
        {
        }

        try
        {
            cts?.Dispose();
        }
        catch
        {
        }

        IsVoiceAiQueryRunning = false;
        VoiceAssistantStatusText = statusText;
        VoiceAiStatusText = statusText;
        if (logAsCanceled)
        {
            EnqueueAiTtsLog("VoiceAI", statusText);
            EnqueueAiTtsLog("RaceAssistant", statusText);
        }
    }

    private StrategyQuestionContext BuildStrategyQuestionContext(string question)
    {
        var sessionState = _sessionStateStore.CaptureState();
        var snapshot = _raceAssistantSnapshotBuilder.Build(
            _activeSessionUid,
            sessionState,
            _lapAnalyzer.CaptureRecentLaps(5),
            _raceEventInsightBuffer.CaptureMessages(),
            DateTimeOffset.UtcNow,
            BuildRaceWeekendTyrePlan(),
            IsListening);
        var intent = _voiceQuestionIntentClassifier.Classify(question);
        return _strategyQuestionContextBuilder.Build(snapshot, question, intent);
    }

    private void ApplyVoiceAssistantResult(VoiceAiQueryResult result)
    {
        VoiceAssistantRecognizedText = result.RecognizedQuestion;
        VoiceAssistantIntentText = RaceAssistantDisplayFormatter.FormatIntent(result.Intent);
        VoiceAssistantModeText = RaceAssistantDisplayFormatter.FormatMode(result.Mode);
        VoiceAiRecognitionStatusDetailText = FormatVoiceInputRecognitionStatus(result);
        EnqueueVoiceInputMetricsLog(result);
        VoiceAssistantTelemetryNoticeText = IsTelemetryLimitedMode(result.Mode)
            ? "当前未接入实时遥测，仅能给通用建议。"
            : string.Empty;

        if (!result.IsSuccess)
        {
            var failure = result.WasIgnoredBecauseSessionChanged
                ? "会话已变化，已忽略旧回答"
                : FormatVoiceAssistantFailure(result.ErrorMessage);
            VoiceAssistantStatusText = failure;
            VoiceAiStatusText = failure;
            EnqueueAiTtsLog("VoiceAI", failure, "Warning", result.QuestionId, result.SessionUid);
            EnqueueAiTtsLog("RaceAssistant", failure, "Warning", result.QuestionId, result.SessionUid);
            return;
        }

        var advice = result.Advice;
        VoiceAssistantStatusText = result.WasQueuedForSpeech
            ? "播放回答"
            : string.Equals(result.SpeechSkippedReason, "缺少实时遥测", StringComparison.Ordinal)
                ? "未播报：缺少实时遥测"
                : "未录音";
        VoiceAiStatusText = result.WasQueuedForSpeech
            ? $"已回答：{result.SpeechText}"
            : string.Equals(result.SpeechSkippedReason, "缺少实时遥测", StringComparison.Ordinal)
                ? $"已生成回答，未播报：缺少实时遥测：{result.SpeechText}"
                : $"已生成回答，TTS 未启用：{result.SpeechText}";
        VoiceAssistantAnswerText = result.SpeechText;
        VoiceAssistantAdviceTypeText = advice is null ? "-" : RaceAssistantDisplayFormatter.FormatAdviceType(advice.AdviceType);
        VoiceAssistantSummaryText = advice?.Summary ?? result.SpeechText;
        VoiceAssistantReasonText = advice?.Reason ?? string.Empty;
        VoiceAssistantRecommendedActionText = advice?.RecommendedAction ?? result.SpeechText;
        VoiceAssistantConfidenceText = advice is null ? "-" : RaceAssistantDisplayFormatter.FormatConfidence(advice.Confidence);
        VoiceAssistantRiskLevelText = advice is null ? "-" : RaceAssistantDisplayFormatter.FormatRiskLevel(advice.RiskLevel);
        (VoiceAssistantMissingDataText, VoiceAssistantMissingDataDetailText) = FormatMissingData(advice?.MissingData ?? Array.Empty<string>());

        RaceAssistantHistory.Insert(
            0,
            new RaceAssistantHistoryItemViewModel
            {
                Timestamp = DateTimeOffset.Now,
                Question = result.RecognizedQuestion,
                Intent = VoiceAssistantIntentText,
                Answer = result.SpeechText,
                Confidence = VoiceAssistantConfidenceText
            });
        while (RaceAssistantHistory.Count > 12)
        {
            RaceAssistantHistory.RemoveAt(RaceAssistantHistory.Count - 1);
        }

        EnqueueAiTtsLog("VoiceAI", $"问题：{result.RecognizedQuestion}", questionId: result.QuestionId, sessionUid: result.SessionUid);
        EnqueueAiTtsLog("RaceAssistant", FormatRaceAssistantUserLog(result, advice), questionId: result.QuestionId, sessionUid: result.SessionUid);
    }

    private static string FormatVoiceInputRecognitionStatus(VoiceAiQueryResult? result)
    {
        if (result is null)
        {
            return "录音时长 - · 人声时长 - · 检测到语音 - · 识别文本 - · 识别置信度 - · 失败原因 -";
        }

        var recognizedText = string.IsNullOrWhiteSpace(result.RecognizedQuestion) ? "-" : result.RecognizedQuestion;
        var failureReason = string.IsNullOrWhiteSpace(result.RecognitionFailedReason)
            ? string.IsNullOrWhiteSpace(result.ErrorMessage) ? "-" : result.ErrorMessage
            : result.RecognitionFailedReason;
        return string.Join(
            " · ",
            $"录音时长 {result.RecordingDurationMs} ms",
            $"人声时长 {result.SpeechDurationMs} ms",
            $"检测到语音 {(result.VadDetected ? "是" : "否")}",
            $"识别文本 {recognizedText}",
            $"识别置信度 {result.RecognitionConfidence:0.00}",
            $"失败原因 {failureReason}");
    }

    private void EnqueueVoiceInputMetricsLog(VoiceAiQueryResult result)
    {
        if (result.RecordingDurationMs <= 0 &&
            result.SpeechDurationMs <= 0 &&
            !result.PreprocessingEnabled &&
            result.RawRmsDb == 0d &&
            result.ProcessedRmsDb == 0d &&
            result.PeakDb == 0d)
        {
            return;
        }

        var reason = string.IsNullOrWhiteSpace(result.RecognitionFailedReason)
            ? "-"
            : result.RecognitionFailedReason;
        EnqueueAiTtsLog(
            "VoiceAI",
            string.Join(
                " ",
                $"recordingDurationMs={result.RecordingDurationMs}",
                $"speechDurationMs={result.SpeechDurationMs}",
                $"vadDetected={result.VadDetected}",
                $"preprocessingEnabled={result.PreprocessingEnabled}",
                $"recognitionFailedReason={reason}",
                $"rawRmsDb={result.RawRmsDb:0.0}",
                $"processedRmsDb={result.ProcessedRmsDb:0.0}",
                $"peakDb={result.PeakDb:0.0}",
                $"wasClipped={result.WasClipped}"),
            questionId: result.QuestionId,
            sessionUid: result.SessionUid);
    }

    private static bool IsTelemetryLimitedMode(RaceAssistantMode mode)
    {
        return mode is RaceAssistantMode.NoTelemetry or RaceAssistantMode.WaitingForTelemetry;
    }

    private static (string Summary, string Detail) FormatMissingData(IReadOnlyList<string> missingData)
    {
        var labels = missingData
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .Select(RaceAssistantDisplayFormatter.FormatMissingDataKey)
            .ToArray();
        if (labels.Length == 0)
        {
            return ("-", string.Empty);
        }

        var preview = string.Join("、", labels.Take(5));
        var summary = string.IsNullOrWhiteSpace(preview)
            ? $"缺失数据：{labels.Length} 项"
            : $"缺失数据：{labels.Length} 项：{preview}";
        return (summary, string.Join("、", labels));
    }

    private static string FormatRaceAssistantUserLog(VoiceAiQueryResult result, StrategyAdviceResult? advice)
    {
        var broadcastText = result.WasQueuedForSpeech ? "已播报" : "未播报";
        if (advice?.MissingData.Count > 0)
        {
            return $"问工程师：数据不足，{broadcastText}。";
        }

        var intent = RaceAssistantDisplayFormatter.FormatIntent(result.Intent);
        var confidence = advice is null
            ? "低"
            : RaceAssistantDisplayFormatter.FormatConfidence(advice.Confidence);
        return $"问工程师：{intent}，置信度{confidence}，{broadcastText}。";
    }

    private bool CanAskRaceAssistantTextQuestion()
    {
        return (VoiceAssistantEnabled || VoiceAiEnabled) &&
               !IsVoiceAiQueryRunning &&
               !string.IsNullOrWhiteSpace(VoiceAssistantQuestionText);
    }

    private void OpenRaceAssistantPanel()
    {
        var item = ShellNavigationItems.FirstOrDefault(navigationItem => string.Equals(navigationItem.Key, "ai-tts", StringComparison.Ordinal));
        if (item is not null)
        {
            SelectedShellNavigationItem = item;
        }
    }

    private bool TryReserveVoiceAssistantQuestion(string question, out string failureMessage)
    {
        failureMessage = string.Empty;
        var key = NormalizeVoiceAssistantQuestion(question);
        if (string.IsNullOrWhiteSpace(key))
        {
            failureMessage = "失败：请输入问题";
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        if (_recentVoiceAssistantQuestions.TryGetValue(key, out var lastAskedAt) &&
            now - lastAskedAt < TimeSpan.FromSeconds(VoiceAssistantRepeatQuestionCooldownSeconds))
        {
            failureMessage = "连续重复提问过快，请稍后再问。";
            return false;
        }

        _recentVoiceAssistantQuestions[key] = now;
        return true;
    }

    private static string NormalizeVoiceAssistantQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(question.Length);
        foreach (var character in question.Trim())
        {
            if (!char.IsWhiteSpace(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static string FormatVoiceAssistantFailure(string? failure)
    {
        if (string.IsNullOrWhiteSpace(failure))
        {
            return "失败：网络失败";
        }

        var normalized = failure.Trim();
        if (normalized.Contains("API Key", StringComparison.OrdinalIgnoreCase))
        {
            return "失败：API Key 未配置";
        }

        if (normalized.Contains("取消", StringComparison.Ordinal) ||
            normalized.Contains("已忽略", StringComparison.Ordinal))
        {
            return normalized;
        }

        if (normalized.Contains("超时", StringComparison.Ordinal) ||
            normalized.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "失败：请求超时";
        }

        if (normalized.Contains("格式无效", StringComparison.Ordinal) ||
            normalized.Contains("JSON", StringComparison.OrdinalIgnoreCase))
        {
            return "失败：AI 返回格式无效";
        }

        if (normalized.Contains("数据不足", StringComparison.Ordinal))
        {
            return "失败：数据不足";
        }

        if (normalized.Contains("识别", StringComparison.Ordinal) ||
            normalized.Contains("麦克风", StringComparison.Ordinal))
        {
            return normalized.Contains("不可用", StringComparison.Ordinal)
                ? "失败：麦克风不可用"
                : "失败：识别失败";
        }

        if (normalized.Contains("network", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("网络", StringComparison.Ordinal))
        {
            return "失败：网络失败";
        }

        return $"失败：{normalized}";
    }

    private void StartVoiceAiInputBindingCapture()
    {
        CancelVoiceAiBindingCapture();
        ResetVoiceAiButtonRuntimeState();
        VoiceAiBindingCaptureActive = true;
        _voiceAiBindingCaptureArmedAt = DateTimeOffset.UtcNow + VoiceAiBindingCaptureArmingDelay;
        VoiceAiStatusText = _voiceAiRawInputReady
            ? $"请在 {VoiceAiBindingCaptureSeconds} 秒内按下方向盘按钮"
            : $"{_voiceAiRawInputStatusText} 请检查方向盘驱动、游戏控制器模式或管理员权限。";

        _voiceAiBindingCaptureCts = new CancellationTokenSource();
        _ = CompleteVoiceAiBindingCaptureAfterTimeoutAsync(_voiceAiBindingCaptureCts.Token);
    }

    private void ClearVoiceAiInputBinding()
    {
        CancelVoiceAiBindingCapture();
        ResetVoiceAiButtonRuntimeState();
        VoiceAiInputBinding = new VoiceAiInputBinding();
        VoiceAiStatusText = "已清除方向盘按钮绑定";
    }

    private void TryCaptureVoiceAiInputBinding(VoiceAiButtonInput input)
    {
        if (input.ReceivedAt < _voiceAiBindingCaptureArmedAt)
        {
            VoiceAiStatusText = "正在等待方向盘输入稳定，请松开后再按目标按钮";
            return;
        }

        if (!input.IsPressed)
        {
            return;
        }

        if (input.PressedChangeCount != 1 || input.ChangedBitCount != 1)
        {
            VoiceAiStatusText = "检测到多个输入变化，请松开后只按一个方向盘按钮";
            return;
        }

        CancelVoiceAiBindingCapture();
        VoiceAiInputBinding = input.ToBinding();
        VoiceAiStatusText = $"已绑定 {VoiceAiBindingText}";
        EnqueueAiTtsLog("System", $"语音 AI 已绑定：{VoiceAiBindingText}");
    }

    private bool VoiceAiInputMatchesBinding(VoiceAiButtonInput input)
    {
        var binding = VoiceAiInputBinding;
        if (binding.Kind != VoiceAiInputBindingKind.RawInputHidButton)
        {
            return false;
        }

        return string.Equals(binding.DeviceId, input.DeviceId, StringComparison.OrdinalIgnoreCase) &&
               binding.ButtonIndex == input.ButtonIndex &&
               (binding.ButtonMask == 0 || input.ButtonMask == 0 || binding.ButtonMask == input.ButtonMask);
    }

    private void ResetVoiceAiButtonRuntimeState()
    {
        _voiceAiButtonStates.Clear();
        _voiceAiToggleWaitingForRelease = false;
    }

    private static string BuildVoiceAiButtonStateKey(VoiceAiButtonInput input)
    {
        return $"{input.DeviceId.Trim()}\u001F{input.ButtonIndex.ToString(CultureInfo.InvariantCulture)}";
    }

    private readonly record struct VoiceAiButtonRuntimeState(bool IsPressed, DateTimeOffset ChangedAt);

    private void HandleBoundVoiceAiButtonPressed()
    {
        if (IsVoiceAiQueryRunning || _isStoppingVoiceAiRecording)
        {
            VoiceAiStatusText = "AI 正在处理上一条问题，请稍后再试";
            return;
        }

        if (VoiceAiTalkMode == VoiceAiTalkMode.ToggleToTalk)
        {
            if (_voiceAiToggleWaitingForRelease)
            {
                return;
            }

            _voiceAiToggleWaitingForRelease = true;
            if (IsVoiceAiRecording)
            {
                _ = StopVoiceAiRecordingAndAskAsync();
            }
            else
            {
                _ = StartVoiceAiRecordingAsync();
            }

            return;
        }

        if (!IsVoiceAiRecording)
        {
            _ = StartVoiceAiRecordingAsync();
        }
    }

    private void HandleBoundVoiceAiButtonReleased()
    {
        if (VoiceAiTalkMode == VoiceAiTalkMode.ToggleToTalk)
        {
            _voiceAiToggleWaitingForRelease = false;
            return;
        }

        if (VoiceAiTalkMode != VoiceAiTalkMode.HoldToTalk || !IsVoiceAiRecording)
        {
            return;
        }

        _ = StopVoiceAiRecordingAndAskAsync();
    }

    private void StartVoiceAiRecordingTimeout()
    {
        CancelVoiceAiRecordingTimeout();
        _voiceAiRecordingTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_lifecycleCts.Token);
        _ = StopVoiceAiRecordingAfterTimeoutAsync(_voiceAiRecordingTimeoutCts.Token);
    }

    private async Task StopVoiceAiRecordingAfterTimeoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            var maxRecordingSeconds = BuildVoiceInputAudioSettings().MaxRecordingSeconds;
            await Task.Delay(TimeSpan.FromSeconds(maxRecordingSeconds), cancellationToken);
            await _dispatcher.InvokeAsync(() =>
            {
                if (IsVoiceAiRecording)
                {
                    VoiceAiStatusText = $"录音已达到 {maxRecordingSeconds} 秒，正在自动提交";
                    VoiceAssistantStatusText = "正在识别";
                    _ = StopVoiceAiRecordingAndAskAsync();
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CompleteVoiceAiBindingCaptureAfterTimeoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(VoiceAiBindingCaptureSeconds), cancellationToken);
            await _dispatcher.InvokeAsync(() =>
            {
                if (VoiceAiBindingCaptureActive)
                {
                    VoiceAiBindingCaptureActive = false;
                    RefreshVoiceAiStatusText();
                    if (VoiceAiInputBinding.Kind == VoiceAiInputBindingKind.None)
                    {
                        VoiceAiStatusText = "未捕获到方向盘按钮，请检查方向盘驱动、游戏控制器模式或管理员权限";
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelVoiceAiRecordingTimeout()
    {
        try
        {
            _voiceAiRecordingTimeoutCts?.Cancel();
        }
        catch
        {
        }
        finally
        {
            _voiceAiRecordingTimeoutCts?.Dispose();
            _voiceAiRecordingTimeoutCts = null;
        }
    }

    private void CancelVoiceAiBindingCapture()
    {
        try
        {
            _voiceAiBindingCaptureCts?.Cancel();
        }
        catch
        {
        }
        finally
        {
            _voiceAiBindingCaptureCts?.Dispose();
            _voiceAiBindingCaptureCts = null;
            VoiceAiBindingCaptureActive = false;
            _voiceAiBindingCaptureArmedAt = DateTimeOffset.MinValue;
        }
    }

    private void DisposeVoiceAiRecordingSession()
    {
        CancelVoiceAiRecordingTimeout();
        try
        {
            _voiceAiRecordingSession?.Dispose();
        }
        catch
        {
        }
        finally
        {
            _voiceAiRecordingSession = null;
            IsVoiceAiRecording = false;
        }
    }

    private void RecordTrackMapTrajectory(int lapNumber, IReadOnlyList<LapSample> lapSamples)
    {
        if (_trackMapTrajectoryStore is null || _activeSessionUid is null || lapSamples.Count == 0)
        {
            return;
        }

        var state = _sessionStateStore.CaptureState();
        _trackMapTrajectoryStore.RecordCompletedLap(
            _activeSessionUid.Value.ToString(CultureInfo.InvariantCulture),
            state.TrackId,
            lapNumber,
            lapSamples);
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

    private void EnqueueEventLog(
        string category,
        string message,
        string level = "Info",
        string? questionId = null,
        ulong? sessionUid = null,
        int? lap = null,
        string? exception = null)
    {
        _appFileLogger.TryEnqueue(category, message, level, sessionUid, lap, questionId, exception);
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

    private void EnqueueAiTtsLog(
        string category,
        string message,
        string level = "Info",
        string? questionId = null,
        ulong? sessionUid = null,
        int? lap = null,
        string? exception = null)
    {
        _appFileLogger.TryEnqueue(category, message, level, sessionUid, lap, questionId, exception);
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

    private void EnqueueAiAnalysisLog(
        string category,
        string message,
        string level = "Info",
        string? questionId = null,
        ulong? sessionUid = null,
        int? lap = null,
        string? exception = null)
    {
        _appFileLogger.TryEnqueue(category, message, level, sessionUid, lap, questionId, exception);
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
        if (string.Equals(logEntry.Category, "AI", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(logEntry.Category, "RaceAssistant", StringComparison.OrdinalIgnoreCase))
        {
            OverviewRecentAiSuggestionText = logEntry.Message;
        }

        if (string.Equals(logEntry.Category, "TTS", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(logEntry.Category, "AI", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(logEntry.Category, "VoiceAI", StringComparison.OrdinalIgnoreCase))
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
            ApplyRaceWeekendTyrePlan(settings.RaceWeekendTyrePlan);
            RaceWeekendTyreMaxWearPercent = settings.RaceWeekendTyrePlan.MaxRecommendedWearPercent;
            _udpRawLogQueueCapacity = Math.Clamp(settings.UdpRawLog.QueueCapacity, 0, 100_000);
            _udpRawLogWriter.UpdateOptions(BuildUdpRawLogOptions(settings.UdpRawLog.Enabled, settings.UdpRawLog.DirectoryPath, settings.UdpRawLog.QueueCapacity));
            UdpRawLogEnabled = settings.UdpRawLog.Enabled;
            RefreshUdpRawLogStatus();
            ApplyLoadedLogSettings(settings.Logs);
            RefreshRuntimeLogStatus();
            VoiceAiEnabled = settings.VoiceAi.Enabled;
            VoiceAiInputBinding = settings.VoiceAi.InputBinding;
            VoiceAiTalkMode = settings.VoiceAi.TalkMode;
            VoiceAiMicrophoneDeviceId = settings.VoiceAi.MicrophoneDeviceId;
            ApplyVoiceAssistantSettings(settings.VoiceAi.AssistantSettings);
            ApplyVoiceInputAudioSettings(settings.VoiceAi.AudioSettings);
            if (!string.IsNullOrWhiteSpace(settings.VoiceAi.MicrophoneDeviceName))
            {
                VoiceAiMicrophoneDeviceName = settings.VoiceAi.MicrophoneDeviceName;
            }

            RefreshVoiceAiStatusText();
            TtsEnabled = settings.Tts.TtsEnabled;
            var loadedVoiceName = settings.Tts.VoiceName;
            TtsVoiceName = ResolveTtsVoiceName(loadedVoiceName);
            TtsVolume = settings.Tts.Volume;
            TtsRate = settings.Tts.Rate;
            _ttsCooldownSeconds = settings.Tts.CooldownSeconds <= 0 ? 8 : settings.Tts.CooldownSeconds;
            _isApplyingSettings = false;

            _ttsQueue.UpdateOptions(BuildTtsOptions());
            AiSettingsSaveStatusText = "设置已加载";
            RaceWeekendTyrePlanStatusText = "轮胎库存已加载";
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

    private ObservableCollection<RaceWeekendTyreInventoryItemViewModel> CreateRaceWeekendTyreInventoryItems()
    {
        return new ObservableCollection<RaceWeekendTyreInventoryItemViewModel>
        {
            new("Soft", "红胎", "Soft", "#E84855", QueuePersistRaceWeekendTyrePlan),
            new("Medium", "黄胎", "Medium", "#F4C430", QueuePersistRaceWeekendTyrePlan),
            new("Hard", "白胎", "Hard", "#E8EEF6", QueuePersistRaceWeekendTyrePlan),
            new("Intermediate", "半雨胎", "Intermediate", "#36B37E", QueuePersistRaceWeekendTyrePlan),
            new("Wet", "全雨胎", "Wet", "#4C9BFF", QueuePersistRaceWeekendTyrePlan)
        };
    }

    private void ApplyRaceWeekendTyrePlan(RaceWeekendTyrePlan plan)
    {
        var normalized = plan.Normalize();
        SetInventoryCount("Soft", normalized.SoftCount, silent: true);
        SetInventoryCount("Medium", normalized.MediumCount, silent: true);
        SetInventoryCount("Hard", normalized.HardCount, silent: true);
        SetInventoryCount("Intermediate", normalized.IntermediateCount, silent: true);
        SetInventoryCount("Wet", normalized.WetCount, silent: true);
    }

    private void ApplyVoiceAssistantSettings(VoiceAssistantSettings settings)
    {
        var normalized = settings.Normalize();
        VoiceAssistantEnabled = normalized.EnableVoiceAssistant || VoiceAiEnabled;
        VoiceAssistantEnableTtsAnswer = normalized.EnableTtsAnswer;
        VoiceAssistantMaxAnswerLength = normalized.MaxAnswerLength;
        VoiceAssistantRepeatQuestionCooldownSeconds = normalized.RepeatQuestionCooldownSeconds;
    }

    private void ApplyVoiceInputAudioSettings(VoiceInputAudioSettings settings)
    {
        var normalized = settings.Normalize();
        VoiceAiNoiseReductionEnabled = normalized.EnableNoiseReduction;
        VoiceAiHighPassFilterEnabled = normalized.EnableHighPassFilter;
        VoiceAiHighPassCutoffHzText = normalized.HighPassCutoffHz.ToString("0.##", CultureInfo.InvariantCulture);
        VoiceAiNoiseGateEnabled = normalized.EnableNoiseGate;
        VoiceAiNoiseGateThresholdDbText = normalized.NoiseGateThresholdDb.ToString("0.##", CultureInfo.InvariantCulture);
        VoiceAiVadEnabled = normalized.EnableVad;
        VoiceAiPreSpeechPaddingMsText = normalized.PreSpeechPaddingMs.ToString(CultureInfo.InvariantCulture);
        VoiceAiPostSpeechPaddingMsText = normalized.PostSpeechPaddingMs.ToString(CultureInfo.InvariantCulture);
        VoiceAiAutoGainEnabled = normalized.EnableAutoGain;
        VoiceAiMaxRecordingSecondsText = normalized.MaxRecordingSeconds.ToString(CultureInfo.InvariantCulture);
        VoiceAiMinSpeechDurationMsText = normalized.MinSpeechDurationMs.ToString(CultureInfo.InvariantCulture);
        VoiceAiMinRecognitionConfidenceText = normalized.MinRecognitionConfidence.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void ApplyLoadedLogSettings(LogSettings settings)
    {
        var normalized = NormalizeLogSettings(settings);
        EnableAppFileLog = normalized.EnableAppFileLog;
        EnableRaceAssistantAuditLog = normalized.EnableRaceAssistantAuditLog;
        RaceAssistantLogPromptSummary = normalized.RaceAssistantLogPromptSummary;
        MaxLogFileSizeMbText = normalized.MaxLogFileSizeMB.ToString(CultureInfo.InvariantCulture);
        MaxLogRetentionDaysText = normalized.MaxLogRetentionDays.ToString(CultureInfo.InvariantCulture);
        ApplyLogSettings(normalized);
        LogSettingsStatusText = "日志设置已加载";
    }

    private void ApplyLogSettings()
    {
        ApplyLogSettings(BuildLogSettings());
    }

    private void ApplyLogSettings(LogSettings settings)
    {
        var normalized = NormalizeLogSettings(settings);
        _appFileLogger.UpdateSettings(normalized);
        _raceAssistantAuditLogger.UpdateSettings(normalized);
        RefreshRuntimeLogStatus();
    }

    private void ReadInventoryFromTyreSets()
    {
        var inventory = _sessionStateStore.CaptureState().PlayerTyreInventory;
        if (inventory is null || inventory.Sets.Count == 0)
        {
            RaceWeekendTyreSetsStatusText = "暂无 TyreSets 数据";
            return;
        }

        var counts = CountAvailableTyreSets(inventory);
        SetInventoryCount("Soft", counts.Soft, silent: true);
        SetInventoryCount("Medium", counts.Medium, silent: true);
        SetInventoryCount("Hard", counts.Hard, silent: true);
        SetInventoryCount("Intermediate", counts.Intermediate, silent: true);
        SetInventoryCount("Wet", counts.Wet, silent: true);
        RaceWeekendTyreSetsStatusText = $"已从 TyreSets 读取 · {inventory.Sets.Count} 套";
        ForcePersistRaceWeekendTyrePlan();
    }

    private void ClearTyreInventory()
    {
        SetInventoryCount("Soft", 0, silent: true);
        SetInventoryCount("Medium", 0, silent: true);
        SetInventoryCount("Hard", 0, silent: true);
        SetInventoryCount("Intermediate", 0, silent: true);
        SetInventoryCount("Wet", 0, silent: true);
        RaceWeekendTyreSetsStatusText = "已清零";
        ForcePersistRaceWeekendTyrePlan();
    }

    private void ForcePersistRaceWeekendTyrePlan()
    {
        QueuePersistRaceWeekendTyrePlan();
    }

    private void SetInventoryCount(string compound, int count, bool silent = false)
    {
        var item = RaceWeekendTyreInventoryItems.FirstOrDefault(
            row => string.Equals(row.Compound, compound, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return;
        }

        if (silent)
        {
            item.SetCountSilently(count);
        }
        else
        {
            item.Count = count;
        }
    }

    private static TyreInventoryCounts CountAvailableTyreSets(TyreInventorySnapshot inventory)
    {
        var soft = 0;
        var medium = 0;
        var hard = 0;
        var intermediate = 0;
        var wet = 0;
        foreach (var set in inventory.Sets.Where(set => set.Available))
        {
            switch (set.VisualTyreCompound)
            {
                case 16:
                    soft++;
                    break;
                case 17:
                    medium++;
                    break;
                case 18:
                    hard++;
                    break;
                case 7:
                    intermediate++;
                    break;
                case 8:
                    wet++;
                    break;
            }
        }

        return new TyreInventoryCounts(soft, medium, hard, intermediate, wet);
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

    private void QueuePersistRaceWeekendTyrePlan()
    {
        if (_isApplyingSettings)
        {
            return;
        }

        var saveVersion = Interlocked.Increment(ref _raceWeekendTyrePlanSaveVersion);
        var plan = BuildRaceWeekendTyrePlan();
        RaceWeekendTyrePlanStatusText = "正在保存...";
        _ = PersistRaceWeekendTyrePlanAsync(plan, saveVersion);
    }

    private async Task PersistRaceWeekendTyrePlanAsync(RaceWeekendTyrePlan plan, int saveVersion)
    {
        var gateHeld = false;
        try
        {
            await _settingsGate.WaitAsync();
            gateHeld = true;
            if (saveVersion < Volatile.Read(ref _raceWeekendTyrePlanSaveVersion))
            {
                return;
            }

            await _appSettingsStore.SaveRaceWeekendTyrePlanAsync(plan, CancellationToken.None);
            if (saveVersion == Volatile.Read(ref _raceWeekendTyrePlanSaveVersion))
            {
                RaceWeekendTyrePlanStatusText = "轮胎库存已保存";
                EnqueueAiTtsLog("System", "赛前轮胎库存已保存。");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            RaceWeekendTyrePlanStatusText = "轮胎库存保存失败";
            EnqueueAiTtsLog("System", $"轮胎库存保存失败：{ex.Message}");
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

    private void QueuePersistLogSettings()
    {
        if (_isApplyingSettings)
        {
            return;
        }

        var saveVersion = Interlocked.Increment(ref _logSettingsSaveVersion);
        var settings = BuildLogSettings();
        _ = PersistLogSettingsAsync(settings, saveVersion);
    }

    private async Task PersistLogSettingsAsync(LogSettings settings, int saveVersion)
    {
        var gateHeld = false;
        try
        {
            await _settingsGate.WaitAsync();
            gateHeld = true;
            if (saveVersion < Volatile.Read(ref _logSettingsSaveVersion))
            {
                return;
            }

            await _appSettingsStore.SaveLogSettingsAsync(settings, CancellationToken.None);
            if (saveVersion == Volatile.Read(ref _logSettingsSaveVersion))
            {
                LogSettingsStatusText = "日志设置已保存";
                EnqueueAiTtsLog("System", "日志设置已保存。");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogSettingsStatusText = "日志设置保存失败";
            EnqueueAiTtsLog("System", $"日志设置保存失败：{ex.Message}");
        }
        finally
        {
            if (gateHeld)
            {
                _settingsGate.Release();
            }
        }
    }

    private void QueuePersistVoiceAiOptions()
    {
        if (_isApplyingSettings)
        {
            return;
        }

        var saveVersion = Interlocked.Increment(ref _voiceAiSettingsSaveVersion);
        var options = BuildVoiceAiOptions();
        _ = PersistVoiceAiOptionsAsync(options, saveVersion);
    }

    private async Task PersistVoiceAiOptionsAsync(VoiceAiOptions options, int saveVersion)
    {
        var gateHeld = false;
        try
        {
            await _settingsGate.WaitAsync();
            gateHeld = true;
            if (saveVersion < Volatile.Read(ref _voiceAiSettingsSaveVersion))
            {
                return;
            }

            await _appSettingsStore.SaveVoiceAiOptionsAsync(options, CancellationToken.None);
            if (saveVersion == Volatile.Read(ref _voiceAiSettingsSaveVersion))
            {
                EnqueueAiTtsLog("System", "语音 AI 按键设置已保存。");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            EnqueueAiTtsLog("System", $"语音 AI 按键设置保存失败：{ex.Message}");
        }
        finally
        {
            if (gateHeld)
            {
                _settingsGate.Release();
            }
        }
    }

    private void RefreshVoiceAiMicrophones()
    {
        RefreshVoiceAiMicrophones(persistSelection: true);
    }

    private void RefreshVoiceAiMicrophones(bool persistSelection)
    {
        try
        {
            var devices = _microphoneService.GetDevices();
            VoiceAiMicrophoneDevices.Clear();
            foreach (var device in devices)
            {
                VoiceAiMicrophoneDevices.Add(device);
            }

            if (VoiceAiMicrophoneDevices.Count == 0)
            {
                VoiceAiMicrophoneStatusText = "未找到系统麦克风";
                return;
            }

            if (string.IsNullOrWhiteSpace(VoiceAiMicrophoneDeviceId) ||
                VoiceAiMicrophoneDevices.All(device => !string.Equals(device.DeviceId, VoiceAiMicrophoneDeviceId, StringComparison.Ordinal)))
            {
                var preferredDevice = VoiceAiMicrophoneDevices.FirstOrDefault(device => device.IsDefault)
                                      ?? VoiceAiMicrophoneDevices[0];
                if (persistSelection)
                {
                    VoiceAiMicrophoneDeviceId = preferredDevice.DeviceId;
                }
                else
                {
                    _voiceAiMicrophoneDeviceId = preferredDevice.DeviceId;
                    OnPropertyChanged(nameof(VoiceAiMicrophoneDeviceId));
                }

                VoiceAiMicrophoneDeviceName = preferredDevice.DisplayName;
            }

            VoiceAiMicrophoneStatusText = $"已发现 {VoiceAiMicrophoneDevices.Count} 个麦克风";
        }
        catch (Exception ex)
        {
            VoiceAiMicrophoneStatusText = $"读取麦克风失败：{FormatVoiceAiMicrophoneError(ex)}";
        }
    }

    private async Task TestVoiceAiMicrophoneAsync()
    {
        if (IsVoiceAiMicrophoneTesting)
        {
            return;
        }

        try
        {
            IsVoiceAiMicrophoneTesting = true;
            var testDuration = TimeSpan.FromSeconds(Math.Min(3, BuildVoiceInputAudioSettings().MaxRecordingSeconds));
            VoiceAiMicrophoneStatusText = $"正在测试麦克风 {testDuration.TotalSeconds:0} 秒...";
            VoiceAiMicrophoneTestLevel = 0d;
            VoiceAiRecognitionStatusDetailText = FormatVoiceInputRecognitionStatus(null);
            using var session = _microphoneService.StartRecording(VoiceAiMicrophoneDeviceId);
            await Task.Delay(testDuration, _lifecycleCts.Token);
            var recording = await session.StopAsync(_lifecycleCts.Token);
            VoiceAiMicrophoneTestLevel = recording.PeakLevel;
            var result = await _voiceAiQueryService.RecognizeOnlyAsync(
                recording,
                BuildVoiceInputAudioSettings(),
                _lifecycleCts.Token);
            VoiceAiRecognitionStatusDetailText = FormatVoiceInputRecognitionStatus(result);
            VoiceAiMicrophoneStatusText = result.IsSuccess
                ? "麦克风识别测试完成"
                : string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "未检测到清晰语音"
                    : result.ErrorMessage;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            VoiceAiMicrophoneStatusText = $"麦克风测试失败：{FormatVoiceAiMicrophoneError(ex)}";
        }
        finally
        {
            IsVoiceAiMicrophoneTesting = false;
        }
    }

    private static string FormatVoiceAiMicrophoneError(Exception exception)
    {
        return exception is System.IO.FileNotFoundException or System.IO.FileLoadException or BadImageFormatException or TypeLoadException
            ? "音频组件缺失或不可用，请重新安装新版应用"
            : "设备不可用，请检查系统麦克风权限、设备占用或驱动状态";
    }

    private string ResolveVoiceAiMicrophoneDeviceName(string deviceId)
    {
        return VoiceAiMicrophoneDevices
            .FirstOrDefault(device => string.Equals(device.DeviceId, deviceId, StringComparison.Ordinal))
            ?.DisplayName ?? VoiceAiMicrophoneDeviceName;
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
                _storagePersistenceService.EnqueueRaceEngineerReport(BuildStoredRaceEngineerReport(lastLap, result));
            }

            if (result.IsSuccess)
            {
                PostRaceAiStatusText = $"赛后 AI 总结已生成：{result.Summary}";
                EnqueueAiAnalysisLog("AI", PostRaceAiStatusText);
                EnqueueAiAnalysisLog("AI", BuildPostRaceAiDetailLogText(result));
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
        var sessionMode = ResolveSessionMode(sessionState);
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
        if (ResolveSessionMode(sessionState) != SessionMode.Race ||
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

    private static StoredRaceEngineerReport BuildStoredRaceEngineerReport(LapSummary lastLap, AIAnalysisResult result)
    {
        var detailJson = JsonSerializer.Serialize(new
        {
            summary = result.Summary,
            keyProblems = result.KeyProblems,
            strategyReview = result.StrategyReview,
            tyreReview = result.TyreReview,
            ersFuelReview = result.ErsFuelReview,
            opponentReview = result.OpponentReview,
            improvements = result.Improvements
        });

        return new StoredRaceEngineerReport
        {
            LapNumber = lastLap.LapNumber,
            ReportType = "post-race-ai",
            Summary = string.IsNullOrWhiteSpace(result.Summary) ? "-" : result.Summary,
            SpokenText = string.IsNullOrWhiteSpace(result.TtsText) ? "-" : result.TtsText,
            DetailJson = detailJson,
            IsSuccess = result.IsSuccess,
            ErrorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "-" : result.ErrorMessage,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static string BuildPostRaceAiDetailLogText(AIAnalysisResult result)
    {
        var problems = result.KeyProblems.Count == 0
            ? "主要问题：-"
            : $"主要问题：{string.Join("；", result.KeyProblems)}";
        var improvements = result.Improvements.Count == 0
            ? "下次改进：-"
            : $"下次改进：{string.Join("；", result.Improvements)}";

        return string.Join(
            Environment.NewLine,
            $"比赛结论：{result.Summary}",
            problems,
            $"策略判断：{result.StrategyReview}",
            $"轮胎判断：{result.TyreReview}",
            $"ERS/燃油：{result.ErsFuelReview}",
            $"攻防判断：{result.OpponentReview}",
            improvements);
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

    private AIAnalysisContext BuildAiAnalysisContext(SessionState sessionState, CarSnapshot? playerCar, LapSummary? lastLap)
    {
        var recentLaps = _lapAnalyzer.CaptureAllLaps()
            .Reverse()
            .Take(MaxPostRaceAiLaps)
            .ToArray();
        var currentLapSamples = _lapAnalyzer.CaptureCurrentLapSamples();
        var carAhead = playerCar?.Position is null
            ? null
            : sessionState.Cars.FirstOrDefault(car => car.Position == playerCar.Position - 1);
        var carBehind = playerCar?.Position is null
            ? null
            : sessionState.Cars.FirstOrDefault(car => car.Position == playerCar.Position + 1);
        var sessionMode = ResolveSessionMode(sessionState);
        var telemetryAnalysisSummary = _telemetryAnalysisSummaryBuilder.Build(currentLapSamples, recentLaps);
        var tyrePlan = BuildRaceWeekendTyrePlan();

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
            GapToFrontInMs = NormalizeGapMs(playerCar?.DeltaToCarInFrontInMs),
            GapToBehindInMs = NormalizeGapMs(carBehind?.DeltaToCarInFrontInMs),
            WeatherForecastSummary = BuildWeatherForecastSummary(sessionState),
            PitWindowSummary = BuildPitWindowSummary(sessionState),
            PositionStrategySummary = BuildPositionStrategySummary(sessionState, playerCar, carAhead, carBehind),
            OpponentStrategySummary = BuildOpponentStrategySummary(carAhead, carBehind),
            TyreInventorySummary = BuildTyreInventorySummary(sessionState, tyrePlan),
            HistoricalSessionSummary = BuildHistoricalSessionSummary(sessionState, recentLaps),
            TelemetryAnalysisSummary = telemetryAnalysisSummary,
            DamageSummary = DamageSummaryFormatter.Format(playerCar?.Damage),
            RecentEvents = _raceEventInsightBuffer.CaptureMessages()
        };
    }

    private string BuildWeatherForecastSummary(SessionState sessionState)
    {
        var currentWeather = $"当前 {FormatWeather(sessionState.Weather)}，赛道温度 {FormatNullableTemperature(sessionState.TrackTemperature)}，气温 {FormatNullableTemperature(sessionState.AirTemperature)}";
        var forecastSamples = sessionState.WeatherForecastSamples
            .Take(4)
            .Select(sample => string.Format(
                CultureInfo.InvariantCulture,
                "+{0}min {1} 雨{2}% 赛道{3} 气温{4}",
                sample.TimeOffsetMinutes,
                FormatWeather(sample.Weather),
                sample.RainPercentage,
                FormatTemperature(sample.TrackTemperature),
                FormatTemperature(sample.AirTemperature)))
            .ToArray();

        return forecastSamples.Length == 0
            ? currentWeather
            : $"{currentWeather}；预报：{string.Join("；", forecastSamples)}";
    }

    private static string BuildPitWindowSummary(SessionState sessionState)
    {
        if (sessionState.PitStopWindowIdealLap is null or 0 &&
            sessionState.PitStopWindowLatestLap is null or 0 &&
            sessionState.PitStopRejoinPosition is null or 0)
        {
            return string.Empty;
        }

        var idealLap = FormatNullableByte(sessionState.PitStopWindowIdealLap);
        var latestLap = FormatNullableByte(sessionState.PitStopWindowLatestLap);
        var rejoinPosition = FormatNullableByte(sessionState.PitStopRejoinPosition);
        var speedLimit = FormatNullableByte(sessionState.PitSpeedLimit);
        return $"进站窗口 Lap {idealLap}-{latestLap}，预计出站 P{rejoinPosition}，维修区限速 {speedLimit} km/h";
    }

    private static string BuildPositionStrategySummary(
        SessionState sessionState,
        CarSnapshot? playerCar,
        CarSnapshot? carAhead,
        CarSnapshot? carBehind)
    {
        if (playerCar is null)
        {
            return string.Empty;
        }

        var activeCars = sessionState.ActiveCarCount ?? (byte)sessionState.Cars.Count;
        var currentLap = FormatNullableByte(playerCar.CurrentLapNumber);
        var totalLaps = FormatNullableByte(sessionState.TotalLaps);
        var frontGap = FormatGap(playerCar.DeltaToCarInFrontInMs);
        var rearGap = FormatGap(carBehind?.DeltaToCarInFrontInMs);
        var frontLap = FormatLapTime(carAhead?.LastLapTimeInMs);
        var rearLap = FormatLapTime(carBehind?.LastLapTimeInMs);

        return $"当前 P{FormatNullableByte(playerCar.Position)}/{activeCars}，Lap {currentLap}/{totalLaps}；前车差 {frontGap}，前车上圈 {frontLap}；后车差 {rearGap}，后车上圈 {rearLap}";
    }

    private string BuildOpponentStrategySummary(CarSnapshot? carAhead, CarSnapshot? carBehind)
    {
        var summaries = new List<string>(2);
        var ahead = DescribeOpponent("前车", carAhead);
        if (!string.IsNullOrWhiteSpace(ahead))
        {
            summaries.Add(ahead);
        }

        var behind = DescribeOpponent("后车", carBehind);
        if (!string.IsNullOrWhiteSpace(behind))
        {
            summaries.Add(behind);
        }

        return summaries.Count == 0 ? string.Empty : string.Join("；", summaries);
    }

    private string BuildTyreInventorySummary(SessionState sessionState, RaceWeekendTyrePlan tyrePlan)
    {
        var summaryParts = new List<string>
        {
            $"赛前手动库存：{tyrePlan.InventoryText}",
            $"推荐磨损上限 {tyrePlan.MaxRecommendedWearPercent}%"
        };

        var inventory = sessionState.PlayerTyreInventory;
        if (inventory is null || inventory.Sets.Count == 0)
        {
            summaryParts.Add("未收到游戏轮胎库存校正，禁止推荐赛前未输入的轮胎");
            return string.Join("；", summaryParts);
        }

        var fittedSet = inventory.FittedIndex is null
            ? null
            : inventory.Sets.FirstOrDefault(set => set.Index == inventory.FittedIndex.Value);
        if (fittedSet is not null)
        {
            summaryParts.Add($"当前已装 #{fittedSet.Index + 1} {FormatTyreSet(fittedSet)}");
        }

        var candidateSets = inventory.Sets
            .Where(set => set.Available && set.Wear <= tyrePlan.MaxRecommendedWearPercent)
            .OrderBy(set => set.Wear)
            .ThenBy(set => set.LapDeltaTime)
            .Take(8)
            .Select(set => $"#{set.Index + 1} {FormatTyreSet(set)}")
            .ToArray();

        summaryParts.Add(candidateSets.Length == 0
            ? "游戏库存中没有低于磨损上限的可推荐胎"
            : $"游戏库存可推荐：{string.Join("，", candidateSets)}");
        summaryParts.Add("硬约束：不得推荐不可用、超磨损上限、赛前未输入或游戏库存不存在的轮胎");
        return string.Join("；", summaryParts);
    }

    private string BuildHistoricalSessionSummary(SessionState sessionState, IReadOnlyList<LapSummary> recentLaps)
    {
        var selectedSessions = SessionComparison.SelectedSessions
            .Take(4)
            .Select(session => session.SummaryText)
            .ToArray();

        var parts = new List<string>();
        if (selectedSessions.Length > 0)
        {
            parts.Add($"已选择历史对比：{string.Join("；", selectedSessions)}");
        }

        if (sessionState.SeasonLinkIdentifier is not null &&
            sessionState.WeekendLinkIdentifier is not null &&
            sessionState.SessionLinkIdentifier is not null)
        {
            parts.Add(string.Format(
                CultureInfo.InvariantCulture,
                "周末链路 season {0} / weekend {1} / session {2}",
                sessionState.SeasonLinkIdentifier,
                sessionState.WeekendLinkIdentifier,
                sessionState.SessionLinkIdentifier));
        }

        var validLaps = recentLaps
            .Where(lap => lap.LapTimeInMs is not null)
            .Take(5)
            .ToArray();
        if (validLaps.Length > 0)
        {
            var bestLap = validLaps.Min(lap => lap.LapTimeInMs!.Value);
            var averageLap = validLaps.Average(lap => lap.LapTimeInMs!.Value);
            parts.Add(string.Format(
                CultureInfo.InvariantCulture,
                "最近 {0} 圈最佳 {1}，均值 {2}",
                validLaps.Length,
                FormatLapTime(bestLap),
                FormatLapTime((uint)Math.Round(averageLap))));
        }

        return parts.Count == 0 ? string.Empty : string.Join("；", parts);
    }

    private string DescribeOpponent(string label, CarSnapshot? car)
    {
        if (car is null)
        {
            return string.Empty;
        }

        return $"{label} P{FormatNullableByte(car.Position)} {TyreCompoundFormatter.Format(car.VisualTyreCompound, car.ActualTyreCompound, car.HasTelemetryAccess)}，胎龄 {FormatNullableByte(car.TyresAgeLaps)}，磨损 {FormatNullableFloat(car.TyreWear)}%，进站 {FormatNullableByte(car.NumPitStops)}，上圈 {FormatLapTime(car.LastLapTimeInMs)}";
    }

    private static string FormatTyreSet(TyreSetSnapshot set)
    {
        var tyre = TyreCompoundFormatter.Format(set.VisualTyreCompound, set.ActualTyreCompound, hasTelemetryAccess: true);
        var fittedText = set.Fitted ? "已装" : "备用";
        return $"{tyre} 磨损{set.Wear}% 可用{(set.Available ? "是" : "否")} {fittedText} 寿命{set.UsableLife}/{set.LifeSpan}";
    }

    private static string FormatWeather(byte? weather)
    {
        return weather switch
        {
            0 => "晴",
            1 => "少云",
            2 => "阴",
            3 => "小雨",
            4 => "大雨",
            5 => "暴雨",
            null => "未知天气",
            _ => $"天气编码 {weather.Value}"
        };
    }

    private static string FormatNullableTemperature(sbyte? value)
    {
        return value.HasValue ? FormatTemperature(value.Value) : "-";
    }

    private static string FormatTemperature(sbyte value)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}°C", value);
    }

    private static string FormatNullableByte(byte? value)
    {
        return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "-";
    }

    private static string FormatNullableFloat(float? value)
    {
        return value.HasValue ? value.Value.ToString("0.0", CultureInfo.InvariantCulture) : "-";
    }

    private static string FormatGap(ushort? gapMs)
    {
        return gapMs.HasValue
            ? string.Format(CultureInfo.InvariantCulture, "{0:0.000}s", gapMs.Value / 1000d)
            : "-";
    }

    private string BuildSessionLapKey(int lapNumber)
    {
        var sessionToken = _activeSessionUid?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
        return $"{sessionToken}:{lapNumber}";
    }

    private static SessionMode ResolveSessionMode(SessionState sessionState)
    {
        return SessionModeFormatter.Resolve(
            sessionState.SessionType,
            sessionState.TotalLaps,
            sessionState.WeekendStructure);
    }

    private static int CalculateLapPersistenceQuality(LapSummary lap)
    {
        var quality = 0;
        quality += lap.LapTimeInMs is null ? 0 : 1;
        quality += lap.Sector1TimeInMs is null ? 0 : 1;
        quality += lap.Sector2TimeInMs is null ? 0 : 1;
        quality += lap.Sector3TimeInMs is null ? 0 : 1;
        quality += lap.AverageSpeedKph is null ? 0 : 2;
        quality += lap.FuelUsedLitres is null ? 0 : 2;
        quality += lap.ErsUsed is null ? 0 : 2;
        quality += lap.TyreWearDelta is null ? 0 : 1;
        quality += string.IsNullOrWhiteSpace(lap.StartTyre) || lap.StartTyre == "-" ? 0 : 1;
        quality += string.IsNullOrWhiteSpace(lap.EndTyre) || lap.EndTyre == "-" ? 0 : 1;

        return quality;
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

    private RaceWeekendTyrePlan BuildRaceWeekendTyrePlan()
    {
        return new RaceWeekendTyrePlan
        {
            SoftCount = GetInventoryCount("Soft"),
            MediumCount = GetInventoryCount("Medium"),
            HardCount = GetInventoryCount("Hard"),
            IntermediateCount = GetInventoryCount("Intermediate"),
            WetCount = GetInventoryCount("Wet"),
            MaxRecommendedWearPercent = Math.Clamp(RaceWeekendTyreMaxWearPercent, 0, 100)
        }.Normalize();
    }

    private int GetInventoryCount(string compound)
    {
        return RaceWeekendTyreInventoryItems
            .FirstOrDefault(row => string.Equals(row.Compound, compound, StringComparison.OrdinalIgnoreCase))
            ?.Count ?? 0;
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

    private LogSettings BuildLogSettings()
    {
        return NormalizeLogSettings(
            new LogSettings
            {
                EnableAppFileLog = EnableAppFileLog,
                EnableRaceAssistantAuditLog = EnableRaceAssistantAuditLog,
                RaceAssistantLogPromptSummary = RaceAssistantLogPromptSummary,
                MaxLogFileSizeMB = ParsePositiveInt(MaxLogFileSizeMbText, 20),
                MaxLogRetentionDays = ParsePositiveInt(MaxLogRetentionDaysText, 14)
            });
    }

    private static LogSettings NormalizeLogSettings(LogSettings settings)
    {
        return settings with
        {
            MaxLogFileSizeMB = Math.Clamp(settings.MaxLogFileSizeMB, 1, 1024),
            MaxLogRetentionDays = Math.Clamp(settings.MaxLogRetentionDays, 1, 366)
        };
    }

    private static int ParsePositiveInt(string value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : fallback;
    }

    private static double ParseFiniteDouble(string value, double fallback)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
               double.IsFinite(parsed)
            ? parsed
            : fallback;
    }

    private VoiceInputAudioSettings BuildVoiceInputAudioSettings()
    {
        return new VoiceInputAudioSettings
        {
            EnableNoiseReduction = VoiceAiNoiseReductionEnabled,
            EnableHighPassFilter = VoiceAiHighPassFilterEnabled,
            HighPassCutoffHz = ParseFiniteDouble(VoiceAiHighPassCutoffHzText, 120d),
            EnableNoiseGate = VoiceAiNoiseGateEnabled,
            NoiseGateThresholdDb = ParseFiniteDouble(VoiceAiNoiseGateThresholdDbText, -40d),
            EnableVad = VoiceAiVadEnabled,
            PreSpeechPaddingMs = ParsePositiveInt(VoiceAiPreSpeechPaddingMsText, 150),
            PostSpeechPaddingMs = ParsePositiveInt(VoiceAiPostSpeechPaddingMsText, 250),
            EnableAutoGain = VoiceAiAutoGainEnabled,
            MaxRecordingSeconds = ParsePositiveInt(VoiceAiMaxRecordingSecondsText, 8),
            MinSpeechDurationMs = ParsePositiveInt(VoiceAiMinSpeechDurationMsText, 300),
            MinRecognitionConfidence = ParseFiniteDouble(VoiceAiMinRecognitionConfidenceText, 0.35d)
        }.Normalize();
    }

    private VoiceAiOptions BuildVoiceAiOptions()
    {
        return new VoiceAiOptions
        {
            Enabled = VoiceAiEnabled,
            InputBinding = VoiceAiInputBinding,
            TalkMode = VoiceAiTalkMode,
            MicrophoneDeviceId = VoiceAiMicrophoneDeviceId,
            MicrophoneDeviceName = VoiceAiMicrophoneDeviceName,
            Hotkey = VoiceAiOptions.NoHotkey,
            AssistantSettings = new VoiceAssistantSettings
            {
                EnableVoiceAssistant = VoiceAssistantEnabled,
                PushToTalkKey = VoiceAiOptions.NoHotkey,
                PushToTalkButton = VoiceAiBindingText,
                EnableTtsAnswer = VoiceAssistantEnableTtsAnswer,
                MaxAnswerLength = VoiceAssistantMaxAnswerLength,
                RepeatQuestionCooldownSeconds = VoiceAssistantRepeatQuestionCooldownSeconds
            },
            AudioSettings = BuildVoiceInputAudioSettings()
        };
    }

    private static IReadOnlyList<VoiceAiTalkModeOptionViewModel> CreateVoiceAiTalkModeOptions()
    {
        return
        [
            new VoiceAiTalkModeOptionViewModel
            {
                Mode = VoiceAiTalkMode.HoldToTalk,
                DisplayName = "按住说话",
                Description = "按住方向盘按钮录音，松开后提交。"
            },
            new VoiceAiTalkModeOptionViewModel
            {
                Mode = VoiceAiTalkMode.ToggleToTalk,
                DisplayName = "按下开始/结束",
                Description = "按一次开始录音，再按一次结束并提交。"
            }
        ];
    }

    private static VoiceAiInputBinding NormalizeVoiceAiInputBinding(VoiceAiInputBinding? binding)
    {
        if (binding is null ||
            binding.Kind == VoiceAiInputBindingKind.None ||
            binding.ButtonIndex <= 0)
        {
            return new VoiceAiInputBinding();
        }

        var displayText = binding.Kind == VoiceAiInputBindingKind.RawInputHidButton ||
                          VoiceAiInputBinding.ShouldRegenerateDisplayText(binding.DisplayText)
            ? VoiceAiInputBinding.FormatDisplayText(binding.ButtonIndex)
            : binding.DisplayText.Trim();

        return binding with
        {
            DeviceId = binding.DeviceId?.Trim() ?? string.Empty,
            DeviceName = VoiceAiInputBinding.SanitizeDeviceName(binding.DeviceName),
            DisplayText = displayText
        };
    }

    private void RefreshVoiceAiBindingText()
    {
        var binding = VoiceAiInputBinding;
        VoiceAiBindingText = binding.Kind == VoiceAiInputBindingKind.None
            ? "未绑定方向盘按钮"
            : (binding.Kind == VoiceAiInputBindingKind.RawInputHidButton ||
               VoiceAiInputBinding.ShouldRegenerateDisplayText(binding.DisplayText)
                ? VoiceAiInputBinding.FormatDisplayText(binding.ButtonIndex)
                : binding.DisplayText);
    }

    private void RefreshVoiceAiStatusText()
    {
        if (VoiceAiBindingCaptureActive || IsVoiceAiRecording || IsVoiceAiQueryRunning)
        {
            return;
        }

        if (!VoiceAiEnabled && !VoiceAssistantEnabled)
        {
            VoiceAiStatusText = "语音 AI 未启用";
            VoiceAssistantStatusText = "未录音";
            return;
        }

        if (!_voiceAiRawInputReady)
        {
            VoiceAiStatusText = $"{_voiceAiRawInputStatusText} 请检查方向盘驱动、游戏控制器模式或管理员权限。";
            return;
        }

        if (VoiceAiInputBinding.Kind == VoiceAiInputBindingKind.None)
        {
            VoiceAiStatusText = "请先绑定方向盘按钮";
            VoiceAssistantStatusText = "未录音";
            return;
        }

        var modeText = VoiceAiTalkMode == VoiceAiTalkMode.HoldToTalk
            ? "按住说话，松开后提交"
            : "按一下开始，再按一下结束并提交";
        VoiceAiStatusText = $"已绑定 {VoiceAiBindingText}，{modeText}";
        VoiceAssistantStatusText = "未录音";
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

    private void RefreshRuntimeLogStatus()
    {
        var appStatus = _appFileLogger.Status;
        var appLatestFileInfo = _logDirectoryService.GetLatestFileInfo(appStatus, "app-*.log");
        AppLogDirectoryText = appStatus.DirectoryPath;
        AppLogLastFilePathText = appLatestFileInfo.FilePathText;
        AppLogLastFileSizeText = appLatestFileInfo.FileSizeText;
        AppLogLastWriteTimeText = appLatestFileInfo.LastWriteTimeText;

        var auditStatus = _raceAssistantAuditLogger.Status;
        var auditLatestFileInfo = _logDirectoryService.GetLatestFileInfo(auditStatus, "race-assistant-*.jsonl");
        RaceAssistantLogDirectoryText = auditStatus.DirectoryPath;
        RaceAssistantLogLastFilePathText = auditLatestFileInfo.FilePathText;
        RaceAssistantLogLastFileSizeText = auditLatestFileInfo.FileSizeText;
        RaceAssistantLogLastWriteTimeText = auditLatestFileInfo.LastWriteTimeText;

        var warnings = BuildUdpRawLogErrorText(
            appStatus.LastWarning,
            appLatestFileInfo.ErrorMessage,
            auditStatus.LastWarning,
            auditLatestFileInfo.ErrorMessage);
        if (!string.IsNullOrWhiteSpace(warnings))
        {
            LogSettingsStatusText = warnings;
        }
        else if (string.IsNullOrWhiteSpace(LogSettingsStatusText) || LogSettingsStatusText.Contains("失败", StringComparison.Ordinal))
        {
            LogSettingsStatusText = "日志设置正常";
        }
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
            EventType.SafetyCar
                or EventType.VirtualSafetyCar
                or EventType.YellowFlag
                or EventType.RedFlag
                or EventType.SafetyCarRestart
                or EventType.RedFlagTyreChange => 600,
            EventType.LowFuel
                or EventType.HighTyreWear
                or EventType.HighTyreTemperature
                or EventType.LowTyreTemperature => 500,
            EventType.FrontOldTyreRisk
                or EventType.RearNewTyrePressure
                or EventType.TrafficRisk
                or EventType.RacePitWindow => 450,
            EventType.QualifyingCleanAirWindow => 425,
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
            2 => "阴",
            3 => "小雨",
            4 => "大雨",
            5 => "风暴",
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
        return EnergyFormatter.FormatErs(playerCar.ErsStoreEnergy);
    }

    private static string BuildTyreText(CarSnapshot playerCar)
    {
        return TyreCompoundFormatter.Format(
            playerCar.VisualTyreCompound,
            playerCar.ActualTyreCompound,
            playerCar.HasTelemetryAccess);
    }

    private static string BuildTyreTemperatureText(TyreConditionSnapshot? tyreCondition)
    {
        if (tyreCondition is null)
        {
            return "等待数据";
        }

        var surfaceValues = EnumerateWheelValues(tyreCondition.SurfaceTemperatureCelsius).ToArray();
        var innerValues = EnumerateWheelValues(tyreCondition.InnerTemperatureCelsius).ToArray();
        return $"表 {surfaceValues.Min():0}-{surfaceValues.Max():0}°C · 内 {innerValues.Min():0}-{innerValues.Max():0}°C";
    }

    private static string BuildTyrePressureText(TyreConditionSnapshot? tyreCondition)
    {
        if (tyreCondition is null)
        {
            return "等待数据";
        }

        var pressureValues = EnumerateWheelValues(tyreCondition.PressurePsi).ToArray();
        return $"{pressureValues.Min():0.0}-{pressureValues.Max():0.0} psi";
    }

    private static IEnumerable<double> EnumerateWheelValues(WheelValues<byte> values)
    {
        yield return values.RearLeft;
        yield return values.RearRight;
        yield return values.FrontLeft;
        yield return values.FrontRight;
    }

    private static IEnumerable<double> EnumerateWheelValues(WheelValues<float> values)
    {
        yield return values.RearLeft;
        yield return values.RearRight;
        yield return values.FrontLeft;
        yield return values.FrontRight;
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
        var normalizedGap = NormalizeGapMs(gapMs);
        return normalizedGap is null ? "-" : $"{normalizedGap.Value / 1000d:0.000}s";
    }

    private static ushort? NormalizeGapMs(ushort? gapMs)
    {
        return gapMs.HasValue && gapMs.Value > 0 ? gapMs.Value : null;
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

        public Task<bool> DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
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

    private sealed class NoOpEventRepository : IEventRepository
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

    private sealed class NoOpAiReportRepository : IAIReportRepository
    {
        public Task AddAsync(
            string sessionId,
            int lapNumber,
            AIAnalysisResult analysisResult,
            DateTimeOffset? createdAt = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredAiReport>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredAiReport>>(Array.Empty<StoredAiReport>());
        }
    }

    private sealed class NoOpLapSampleRepository : ILapSampleRepository
    {
        public Task AddAsync(StoredLapSample sample, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<StoredLapSample> samples, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredLapSample>> GetForLapAsync(
            string sessionId,
            int lapNumber,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredLapSample>>(Array.Empty<StoredLapSample>());
        }

        public Task<IReadOnlyList<StoredLapTyreWearTrendPoint>> GetTyreWearTrendAsync(
            string sessionId,
            int count,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredLapTyreWearTrendPoint>>(Array.Empty<StoredLapTyreWearTrendPoint>());
        }
    }
}
