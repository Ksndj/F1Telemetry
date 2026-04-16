using System.Net.Sockets;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;

namespace F1Telemetry.Udp.Services;

public sealed class UdpListener : IUdpListener
{
    private readonly SemaphoreSlim _stateGate = new(1, 1);
    private CancellationTokenSource? _listeningCts;
    private Task? _receiveLoopTask;
    private UdpClient? _udpClient;

    public event EventHandler<UdpDatagram>? DatagramReceived;

    public event EventHandler<Exception>? ReceiveFaulted;

    public bool IsListening { get; private set; }

    public int? ListeningPort { get; private set; }

    public async Task StartAsync(int port, CancellationToken cancellationToken = default)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "UDP port must be between 1 and 65535.");
        }

        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            if (IsListening)
            {
                if (ListeningPort == port)
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Listener is already running on port {ListeningPort}.");
            }

            _udpClient = new UdpClient(port);
            _listeningCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _receiveLoopTask = ReceiveLoopAsync(_udpClient, _listeningCts.Token);
            ListeningPort = port;
            IsListening = true;
        }
        catch
        {
            _udpClient?.Dispose();
            _udpClient = null;
            _listeningCts?.Dispose();
            _listeningCts = null;
            _receiveLoopTask = null;
            ListeningPort = null;
            IsListening = false;
            throw;
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? receiveLoopTask;
        CancellationTokenSource? listeningCts;
        UdpClient? udpClient;

        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            if (!IsListening && _receiveLoopTask is null)
            {
                return;
            }

            receiveLoopTask = _receiveLoopTask;
            listeningCts = _listeningCts;
            udpClient = _udpClient;

            _receiveLoopTask = null;
            _listeningCts = null;
            _udpClient = null;
            ListeningPort = null;
            IsListening = false;
        }
        finally
        {
            _stateGate.Release();
        }

        listeningCts?.Cancel();
        udpClient?.Dispose();

        if (receiveLoopTask is not null)
        {
            try
            {
                await receiveLoopTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        listeningCts?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
        }
        finally
        {
            _stateGate.Dispose();
        }
    }

    private async Task ReceiveLoopAsync(UdpClient udpClient, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var receiveResult = await udpClient.ReceiveAsync(cancellationToken);
                var datagram = new UdpDatagram(
                    receiveResult.Buffer,
                    receiveResult.RemoteEndPoint,
                    DateTimeOffset.UtcNow);

                DatagramReceived?.Invoke(this, datagram);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException ex)
            {
                ReceiveFaulted?.Invoke(this, ex);
                await DelayBeforeRetryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                ReceiveFaulted?.Invoke(this, ex);
                await DelayBeforeRetryAsync(cancellationToken);
            }
        }
    }

    private static async Task DelayBeforeRetryAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
