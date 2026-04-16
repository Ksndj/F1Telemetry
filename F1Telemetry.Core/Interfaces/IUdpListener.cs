using F1Telemetry.Core.Models;

namespace F1Telemetry.Core.Interfaces;

public interface IUdpListener : IAsyncDisposable
{
    event EventHandler<UdpDatagram>? DatagramReceived;

    event EventHandler<Exception>? ReceiveFaulted;

    bool IsListening { get; }

    int? ListeningPort { get; }

    Task StartAsync(int port, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
