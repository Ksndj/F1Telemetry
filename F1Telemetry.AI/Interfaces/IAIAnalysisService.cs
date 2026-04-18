using F1Telemetry.AI.Models;

namespace F1Telemetry.AI.Interfaces;

/// <summary>
/// Provides a single AI analysis operation for a race snapshot.
/// </summary>
public interface IAIAnalysisService
{
    /// <summary>
    /// Produces an AI analysis result for the supplied context and settings.
    /// </summary>
    Task<AIAnalysisResult> AnalyzeAsync(
        AIAnalysisContext context,
        AISettings settings,
        CancellationToken cancellationToken = default);
}
