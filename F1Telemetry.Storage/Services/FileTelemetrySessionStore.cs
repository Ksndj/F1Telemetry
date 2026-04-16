using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;

namespace F1Telemetry.Storage.Services;

public sealed class FileTelemetrySessionStore : ITelemetrySessionStore
{
    public Task SaveSnapshotAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TelemetrySnapshot>> LoadRecentSnapshotsAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TelemetrySnapshot> snapshots = Array.Empty<TelemetrySnapshot>();
        return Task.FromResult(snapshots);
    }
}
