using F1Telemetry.Analytics.Laps;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.Storage.Interfaces;

/// <summary>
/// Persists completed lap summaries.
/// </summary>
public interface ILapRepository
{
    /// <summary>
    /// Inserts a completed lap summary for the specified session.
    /// </summary>
    Task AddAsync(string sessionId, LapSummary lapSummary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent laps for the specified session ordered by creation time descending.
    /// </summary>
    Task<IReadOnlyList<StoredLap>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default);
}
