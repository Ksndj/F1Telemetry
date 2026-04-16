using F1Telemetry.Core.Models;

namespace F1Telemetry.Core.Interfaces;

public interface ITelemetryPacketSource
{
    event EventHandler<TelemetrySnapshot>? SnapshotReceived;

    bool IsRunning { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
