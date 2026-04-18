using F1Telemetry.AI.Models;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.Storage.Interfaces;

/// <summary>
/// Persists AI analysis results for completed laps.
/// </summary>
public interface IAIReportRepository
{
    /// <summary>
    /// Inserts an AI analysis result for the specified session and lap.
    /// </summary>
    Task AddAsync(
        string sessionId,
        int lapNumber,
        AIAnalysisResult analysisResult,
        DateTimeOffset? createdAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent AI analysis results for the specified session ordered by creation time descending.
    /// </summary>
    Task<IReadOnlyList<StoredAiReport>> GetRecentAsync(string sessionId, int count, CancellationToken cancellationToken = default);
}
