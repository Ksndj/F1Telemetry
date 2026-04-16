using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using F1Telemetry.Analytics.Interfaces;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.State;
using F1Telemetry.Core.Abstractions;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Drives the milestone 5 real-time dashboard and projects the central state store for WPF binding.
/// </summary>
public sealed class DashboardViewModel : ViewModelBase, IDisposable
{
    private const int MaxLogEntries = 50;
    private readonly IUdpListener _udpListener;
    private readonly IPacketDispatcher<PacketId, PacketHeader> _packetDispatcher;
    private readonly SessionStateStore _sessionStateStore;
    private readonly ILapAnalyzer _lapAnalyzer;
    private readonly DispatcherTimer _uiTimer;
    private readonly CancellationTokenSource _lifecycleCts = new();
    private readonly ConcurrentQueue<LogEntryViewModel> _pendingEventLogs = new();
    private readonly RelayCommand _startListeningCommand;
    private readonly RelayCommand _stopListeningCommand;
    private bool _isBusy;
    private bool _isListening;
    private bool _isConnected;
    private int? _listeningPort;
    private long _totalPacketCount;
    private int _packetsPerSecond;
    private string _portText = "20777";
    private string _statusMessage = "准备监听 F1 25 UDP。";
    private string _trackText = "等待 Session 包。";
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
    private long _receivedPacketCount;
    private long _lastPacketReceivedUnixMs = -1;
    private long _lastPacketsPerSecondSampleCount;
    private DateTimeOffset _lastPacketsPerSecondSampleAt;
    private string? _lastEventCode;
    private bool _disposed;

    /// <summary>
    /// Initializes a new dashboard view model.
    /// </summary>
    /// <param name="udpListener">The UDP listener service.</param>
    /// <param name="packetDispatcher">The packet dispatcher used for header validation.</param>
    /// <param name="sessionStateStore">The central session state store.</param>
    /// <param name="lapAnalyzer">The lap analyzer that exposes completed player laps.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    public DashboardViewModel(
        IUdpListener udpListener,
        IPacketDispatcher<PacketId, PacketHeader> packetDispatcher,
        SessionStateStore sessionStateStore,
        ILapAnalyzer lapAnalyzer,
        Dispatcher dispatcher)
    {
        _udpListener = udpListener ?? throw new ArgumentNullException(nameof(udpListener));
        _packetDispatcher = packetDispatcher ?? throw new ArgumentNullException(nameof(packetDispatcher));
        _sessionStateStore = sessionStateStore ?? throw new ArgumentNullException(nameof(sessionStateStore));
        _lapAnalyzer = lapAnalyzer ?? throw new ArgumentNullException(nameof(lapAnalyzer));
        _lastPacketsPerSecondSampleAt = DateTimeOffset.UtcNow;

        OpponentCars = new ObservableCollection<CarStateItemViewModel>();
        RecentLapSummaries = new ObservableCollection<LapSummaryItemViewModel>();
        EventLogs = new ObservableCollection<LogEntryViewModel>();
        AiBroadcastLogs = new ObservableCollection<LogEntryViewModel>();
        ChartPlaceholders = new ObservableCollection<DashboardPlaceholderViewModel>
        {
            new() { Title = "速度曲线", Description = "后续接入实时速度与速度陷阱走势。" },
            new() { Title = "输入曲线", Description = "后续接入油门、刹车、转向时间序列。" },
            new() { Title = "轮胎窗口", Description = "后续接入温度、磨损与轮胎工作区间。" },
            new() { Title = "能量管理", Description = "后续接入 ERS、燃油与部署策略图表。" }
        };

        AiBroadcastLogs.Add(CreateLogEntry("AI", "AI 播报模块尚未接入，本区域先保留日志占位。"));

        _startListeningCommand = new RelayCommand(() => _ = StartListeningAsync(), CanStartListening);
        _stopListeningCommand = new RelayCommand(() => _ = StopListeningAsync(), CanStopListening);

        _udpListener.DatagramReceived += OnDatagramReceived;
        _udpListener.ReceiveFaulted += OnReceiveFaulted;

        _uiTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _uiTimer.Tick += OnUiTimerTick;
        _uiTimer.Start();
    }

    /// <summary>
    /// Gets the window title.
    /// </summary>
    public string Title => "F1 25 遥测软件 V1";

    /// <summary>
    /// Gets the window subtitle.
    /// </summary>
    public string Subtitle => "Milestone 5 · 单圈聚合与单圈表";

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
    /// Gets the AI broadcast placeholder log entries.
    /// </summary>
    public ObservableCollection<LogEntryViewModel> AiBroadcastLogs { get; }

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
    /// Gets or sets the UDP port text.
    /// </summary>
    public string PortText
    {
        get => _portText;
        set
        {
            if (SetProperty(ref _portText, value))
            {
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
            }
        }
    }

    /// <summary>
    /// Gets the connection state label.
    /// </summary>
    public string ConnectionStateText =>
        IsConnected ? "已连接" : IsListening ? "等待数据" : "未启动";

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
    /// Releases the UDP subscriptions and timer resources owned by the view model.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _uiTimer.Stop();
        _uiTimer.Tick -= OnUiTimerTick;

        _udpListener.DatagramReceived -= OnDatagramReceived;
        _udpListener.ReceiveFaulted -= OnReceiveFaulted;

        _lifecycleCts.Cancel();

        try
        {
            _udpListener.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
        }
        finally
        {
            _lifecycleCts.Dispose();
        }
    }

    private bool CanStartListening()
    {
        return !_isBusy && !IsListening;
    }

    private bool CanStopListening()
    {
        return !_isBusy && IsListening;
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
            IsListening = false;
            IsConnected = false;
            ListeningPort = null;
            Interlocked.Exchange(ref _lastPacketReceivedUnixMs, -1);
            PacketsPerSecond = 0;
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

    private void OnUiTimerTick(object? sender, EventArgs e)
    {
        DrainPendingEventLogs();
        RefreshConnectionState();
        RefreshCounters();
        RefreshCentralState();
    }

    private void DrainPendingEventLogs()
    {
        while (_pendingEventLogs.TryDequeue(out var logEntry))
        {
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
        WeatherText = BuildWeatherText(sessionState);
        LapText = BuildLapText(sessionState, playerCar);
        UpdatePlayerCard(sessionState, playerCar);
        RebuildOpponentCars(sessionState.Opponents, playerCar);
        RefreshLapHistory();
        TrackLatestEvent(sessionState.LastEventCode);
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
    }

    private void RebuildOpponentCars(IReadOnlyList<CarSnapshot> opponents, CarSnapshot? playerCar)
    {
        OpponentCars.Clear();

        foreach (var opponent in opponents)
        {
            OpponentCars.Add(CarStateItemViewModel.FromSnapshot(opponent, playerCar));
        }
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
        _pendingEventLogs.Enqueue(CreateLogEntry(category, message));
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

    private static string BuildTrackText(sbyte? trackId)
    {
        return trackId is null ? "等待 Session 包。" : $"赛道 ID {trackId}";
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
        if (playerCar.VisualTyreCompound is null && playerCar.ActualTyreCompound is null)
        {
            return playerCar.HasTelemetryAccess ? "-" : "不可见";
        }

        return $"V{playerCar.VisualTyreCompound?.ToString() ?? "-"} / A{playerCar.ActualTyreCompound?.ToString() ?? "-"}";
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

    private static string FormatGapMs(ushort? gapMs)
    {
        return gapMs is null ? "-" : $"{gapMs.Value / 1000d:0.000}s";
    }
}
