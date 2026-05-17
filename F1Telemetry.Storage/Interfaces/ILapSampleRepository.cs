using F1Telemetry.Storage.Models;

namespace F1Telemetry.Storage.Interfaces;

/// <summary>
/// Persists high-frequency lap samples for offline analysis.
/// </summary>
public interface ILapSampleRepository
{
    /// <summary>
    /// Inserts a single lap sample.
    /// </summary>
    Task AddAsync(StoredLapSample sample, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts multiple lap samples in a single transaction.
    /// </summary>
    Task AddRangeAsync(IEnumerable<StoredLapSample> samples, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns samples for a session lap ordered by capture sequence.
    /// </summary>
    Task<IReadOnlyList<StoredLapSample>> GetForLapAsync(
        string sessionId,
        int lapNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one complete four-wheel tyre wear point per lap for a session.
    /// </summary>
    Task<IReadOnlyList<StoredLapTyreWearTrendPoint>> GetTyreWearTrendAsync(
        string sessionId,
        int count,
        CancellationToken cancellationToken = default);
}
