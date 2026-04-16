using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;

namespace F1Telemetry.Udp.Services;

public sealed class UdpTelemetryListener : ITelemetryPacketSource
{
    public event EventHandler<TelemetrySnapshot>? SnapshotReceived;

    public bool IsRunning { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        IsRunning = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        IsRunning = false;
        return Task.CompletedTask;
    }

    public void PublishPlaceholderSnapshot(TelemetrySnapshot snapshot)
    {
        SnapshotReceived?.Invoke(this, snapshot);
    }
}
