using F1Telemetry.Storage.Models;

namespace F1Telemetry.Storage.Interfaces;

/// <summary>
/// Persists race-engineer reports and speech text.
/// </summary>
public interface IRaceEngineerReportRepository
{
    /// <summary>
    /// Inserts a race-engineer report.
    /// </summary>
    Task AddAsync(StoredRaceEngineerReport report, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns recent race-engineer reports for the specified session.
    /// </summary>
    Task<IReadOnlyList<StoredRaceEngineerReport>> GetRecentAsync(
        string sessionId,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns recent race-engineer reports for the specified session lap.
    /// </summary>
    Task<IReadOnlyList<StoredRaceEngineerReport>> GetForLapAsync(
        string sessionId,
        int lapNumber,
        int count,
        CancellationToken cancellationToken = default);
}
