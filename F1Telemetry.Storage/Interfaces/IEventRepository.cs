using F1Telemetry.Analytics.Events;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.Storage.Interfaces;

/// <summary>
/// Persists detected race events.
/// </summary>
public interface IEventRepository
{
    /// <summary>
    /// Inserts a detected race event for the specified session.
    /// </summary>
    Task AddAsync(string sessionId, RaceEvent raceEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent race events for the specified session ordered by creation time descending.
    /// </summary>
    Task<IReadOnlyList<StoredEvent>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default);
}
