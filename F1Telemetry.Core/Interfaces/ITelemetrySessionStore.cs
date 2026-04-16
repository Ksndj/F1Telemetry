using F1Telemetry.Core.Models;

namespace F1Telemetry.Core.Interfaces;

public interface ITelemetrySessionStore
{
    Task SaveSnapshotAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TelemetrySnapshot>> LoadRecentSnapshotsAsync(
        int count,
        CancellationToken cancellationToken = default);
}
