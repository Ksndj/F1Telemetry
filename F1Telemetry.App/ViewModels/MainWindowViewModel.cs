using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using F1Telemetry.Analytics.State;
using F1Telemetry.Core.Abstractions;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Drives the UDP debug page and projects the central state store for WPF binding.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IUdpListener _udpListener;
    private readonly IPacketDispatcher<PacketId, PacketHeader> _packetDispatcher;
    private readonly SessionStateStore _sessionStateStore;
    private readonly DispatcherTimer _uiTimer;
    private readonly CancellationTokenSource _lifecycleCts = new();
    private readonly ConcurrentQueue<PacketLogItemViewModel> _pendingPacketLogs = new();
    private readonly ConcurrentQueue<string> _pendingStatusMessages = new();
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
    private string _sessionDescriptor = "等待 Session 包。";
    private string _sessionConditions = "-";
    private string _activeCarsText = "-";
    private string _lastEventText = "-";
    private string _playerCarName = "等待玩家车辆状态。";
    private string _playerPositionAndLap = "-";
    private string _playerSpeed = "-";
    private string _playerInputs = "-";
    private string _playerStatus = "-";
    private string _playerTelemetryAccess = "-";
    private long _receivedPacketCount;
    private long _lastPacketReceivedUnixMs = -1;
    private long _lastPacketsPerSecondSampleCount;
    private DateTimeOffset _lastPacketsPerSecondSampleAt;
    private bool _disposed;

    public MainWindowViewModel(
        IUdpListener udpListener,
        IPacketDispatcher<PacketId, PacketHeader> packetDispatcher,
        SessionStateStore sessionStateStore,
        Dispatcher dispatcher)
    {
        _udpListener = udpListener ?? throw new ArgumentNullException(nameof(udpListener));
        _packetDispatcher = packetDispatcher ?? throw new ArgumentNullException(nameof(packetDispatcher));
        _sessionStateStore = sessionStateStore ?? throw new ArgumentNullException(nameof(sessionStateStore));
        _lastPacketsPerSecondSampleAt = DateTimeOffset.UtcNow;

        RecentPackets = new ObservableCollection<PacketLogItemViewModel>();
        OpponentCars = new ObservableCollection<CarStateItemViewModel>();
        _startListeningCommand = new RelayCommand(() => _ = StartListeningAsync(), CanStartListening);
        _stopListeningCommand = new RelayCommand(() => _ = StopListeningAsync(), CanStopListening);

        _udpListener.DatagramReceived += OnDatagramReceived;
        _udpListener.ReceiveFaulted += OnReceiveFaulted;
        _packetDispatcher.PacketDispatched += OnPacketDispatched;

        _uiTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _uiTimer.Tick += OnUiTimerTick;
        _uiTimer.Start();
    }

    public string Title => "F1 25 遥测软件 V1";

    public string Subtitle => "UDP debug page with real-time session state, player car and opponent snapshots.";

    public ObservableCollection<PacketLogItemViewModel> RecentPackets { get; }

    public ObservableCollection<CarStateItemViewModel> OpponentCars { get; }

    public ICommand StartListeningCommand => _startListeningCommand;

    public ICommand StopListeningCommand => _stopListeningCommand;

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

    public string ConnectionStateText =>
        IsConnected ? "已连接" : IsListening ? "等待数据" : "未启动";

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

    public string ListeningPortText => ListeningPort?.ToString() ?? "-";

    public long TotalPacketCount
    {
        get => _totalPacketCount;
        private set => SetProperty(ref _totalPacketCount, value);
    }

    public int PacketsPerSecond
    {
        get => _packetsPerSecond;
        private set => SetProperty(ref _packetsPerSecond, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string SessionDescriptor
    {
        get => _sessionDescriptor;
        private set => SetProperty(ref _sessionDescriptor, value);
    }

    public string SessionConditions
    {
        get => _sessionConditions;
        private set => SetProperty(ref _sessionConditions, value);
    }

    public string ActiveCarsText
    {
        get => _activeCarsText;
        private set => SetProperty(ref _activeCarsText, value);
    }

    public string LastEventText
    {
        get => _lastEventText;
        private set => SetProperty(ref _lastEventText, value);
    }

    public string PlayerCarName
    {
        get => _playerCarName;
        private set => SetProperty(ref _playerCarName, value);
    }

    public string PlayerPositionAndLap
    {
        get => _playerPositionAndLap;
        private set => SetProperty(ref _playerPositionAndLap, value);
    }

    public string PlayerSpeed
    {
        get => _playerSpeed;
        private set => SetProperty(ref _playerSpeed, value);
    }

    public string PlayerInputs
    {
        get => _playerInputs;
        private set => SetProperty(ref _playerInputs, value);
    }

    public string PlayerStatus
    {
        get => _playerStatus;
        private set => SetProperty(ref _playerStatus, value);
    }

    public string PlayerTelemetryAccess
    {
        get => _playerTelemetryAccess;
        private set => SetProperty(ref _playerTelemetryAccess, value);
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
        _packetDispatcher.PacketDispatched -= OnPacketDispatched;

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
        }
        catch (Exception ex)
        {
            StatusMessage = $"启动 UDP 监听失败：{ex.Message}";
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
        }
        catch (Exception ex)
        {
            StatusMessage = $"停止 UDP 监听失败：{ex.Message}";
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
            _pendingStatusMessages.Enqueue($"Header 解析失败：{error}");
        }
    }

    private void OnPacketDispatched(
        object? sender,
        PacketDispatchResult<PacketId, PacketHeader> packetDispatchResult)
    {
        _pendingPacketLogs.Enqueue(new PacketLogItemViewModel
        {
            ReceivedAt = packetDispatchResult.Datagram.ReceivedAt.ToLocalTime().ToString("HH:mm:ss.fff"),
            PacketType = packetDispatchResult.Packet.PacketTypeName
        });
    }

    private void OnReceiveFaulted(object? sender, Exception exception)
    {
        _pendingStatusMessages.Enqueue($"UDP 接收异常：{exception.Message}");
    }

    private void OnUiTimerTick(object? sender, EventArgs e)
    {
        DrainPendingPacketLogs();
        DrainPendingStatusMessages();
        RefreshConnectionState();
        RefreshCounters();
        RefreshCentralState();
    }

    private void DrainPendingPacketLogs()
    {
        while (_pendingPacketLogs.TryDequeue(out var packetLog))
        {
            RecentPackets.Insert(0, packetLog);

            while (RecentPackets.Count > 50)
            {
                RecentPackets.RemoveAt(RecentPackets.Count - 1);
            }
        }
    }

    private void DrainPendingStatusMessages()
    {
        string? latestStatusMessage = null;
        while (_pendingStatusMessages.TryDequeue(out var statusMessage))
        {
            latestStatusMessage = statusMessage;
        }

        if (!string.IsNullOrWhiteSpace(latestStatusMessage))
        {
            StatusMessage = latestStatusMessage;
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

        SessionDescriptor = BuildSessionDescriptor(sessionState);
        SessionConditions = BuildSessionConditions(sessionState);
        ActiveCarsText = sessionState.ActiveCarCount?.ToString() ?? "-";
        LastEventText = string.IsNullOrWhiteSpace(sessionState.LastEventCode) ? "-" : sessionState.LastEventCode!;
        UpdatePlayerCar(sessionState.PlayerCar);
        RebuildOpponentCars(sessionState.Opponents);
    }

    private void UpdatePlayerCar(CarSnapshot? playerCar)
    {
        if (playerCar is null)
        {
            PlayerCarName = "等待玩家车辆状态。";
            PlayerPositionAndLap = "-";
            PlayerSpeed = "-";
            PlayerInputs = "-";
            PlayerStatus = "-";
            PlayerTelemetryAccess = "-";
            return;
        }

        PlayerCarName = string.IsNullOrWhiteSpace(playerCar.DriverName)
            ? $"车辆 {playerCar.CarIndex}"
            : playerCar.DriverName!;
        PlayerPositionAndLap = $"P{playerCar.Position?.ToString() ?? "-"} / Lap {playerCar.CurrentLapNumber?.ToString() ?? "-"}";
        PlayerSpeed = playerCar.Telemetry is null
            ? "速度不可见"
            : $"{playerCar.Telemetry.SpeedKph:0} km/h · Gear {playerCar.Gear?.ToString() ?? "-"}";
        PlayerInputs = playerCar.Telemetry is null
            ? "输入不可见"
            : $"Throttle {playerCar.Telemetry.Throttle:P0} · Brake {playerCar.Telemetry.Brake:P0}";
        PlayerStatus = $"Fuel {FormatNullable(playerCar.FuelInTank, "0.0")} · Tyre {playerCar.VisualTyreCompound?.ToString() ?? "-"} · Age {playerCar.TyresAgeLaps?.ToString() ?? "-"}";
        PlayerTelemetryAccess = playerCar.HasTelemetryAccess ? "玩家车遥测完整可见" : "玩家车遥测受限";
    }

    private void RebuildOpponentCars(IReadOnlyList<CarSnapshot> opponents)
    {
        OpponentCars.Clear();

        foreach (var opponent in opponents)
        {
            OpponentCars.Add(CarStateItemViewModel.FromSnapshot(opponent, playerCar: null));
        }
    }

    private static string BuildSessionDescriptor(SessionState sessionState)
    {
        if (sessionState.TrackId is null && sessionState.SessionType is null)
        {
            return "等待 Session 包。";
        }

        return $"Track {sessionState.TrackId?.ToString() ?? "-"} · Session {sessionState.SessionType?.ToString() ?? "-"}";
    }

    private static string BuildSessionConditions(SessionState sessionState)
    {
        if (sessionState.Weather is null &&
            sessionState.TrackTemperature is null &&
            sessionState.AirTemperature is null &&
            sessionState.TotalLaps is null)
        {
            return "-";
        }

        return $"Weather {sessionState.Weather?.ToString() ?? "-"} · Track {sessionState.TrackTemperature?.ToString() ?? "-"}°C · Air {sessionState.AirTemperature?.ToString() ?? "-"}°C · Laps {sessionState.TotalLaps?.ToString() ?? "-"}";
    }

    private static string FormatNullable(float? value, string format)
    {
        return value?.ToString(format) ?? "-";
    }
}
