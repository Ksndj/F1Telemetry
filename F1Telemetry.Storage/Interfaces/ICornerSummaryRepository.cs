using F1Telemetry.Storage.Models;

namespace F1Telemetry.Storage.Interfaces;

/// <summary>
/// Persists offline corner-analysis summaries.
/// </summary>
public interface ICornerSummaryRepository
{
    /// <summary>
    /// Inserts a corner summary.
    /// </summary>
    Task AddAsync(StoredCornerSummary summary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns corner summaries for a session lap ordered by corner number.
    /// </summary>
    Task<IReadOnlyList<StoredCornerSummary>> GetForLapAsync(
        string sessionId,
        int lapNumber,
        CancellationToken cancellationToken = default);
}
