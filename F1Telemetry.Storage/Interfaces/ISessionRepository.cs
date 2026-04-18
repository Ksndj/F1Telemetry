using F1Telemetry.Storage.Models;

namespace F1Telemetry.Storage.Interfaces;

/// <summary>
/// Persists session lifecycle records.
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// Creates a new persisted session row.
    /// </summary>
    Task CreateAsync(StoredSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the end timestamp for an existing session.
    /// </summary>
    Task EndAsync(string sessionId, DateTimeOffset endedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent persisted sessions ordered by start time descending.
    /// </summary>
    Task<IReadOnlyList<StoredSession>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
