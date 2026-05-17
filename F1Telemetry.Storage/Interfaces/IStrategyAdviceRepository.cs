using F1Telemetry.Storage.Models;

namespace F1Telemetry.Storage.Interfaces;

/// <summary>
/// Persists strategy recommendations produced during a session.
/// </summary>
public interface IStrategyAdviceRepository
{
    /// <summary>
    /// Inserts a strategy recommendation.
    /// </summary>
    Task AddAsync(StoredStrategyAdvice advice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns recent strategy recommendations for the specified session.
    /// </summary>
    Task<IReadOnlyList<StoredStrategyAdvice>> GetRecentAsync(
        string sessionId,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns recent strategy recommendations for the specified session lap.
    /// </summary>
    Task<IReadOnlyList<StoredStrategyAdvice>> GetForLapAsync(
        string sessionId,
        int lapNumber,
        int count,
        CancellationToken cancellationToken = default);
}
