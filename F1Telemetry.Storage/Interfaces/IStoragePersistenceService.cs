using F1Telemetry.AI.Models;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Storage.Interfaces;

/// <summary>
/// Queues storage writes behind the real-time pipeline without blocking the UI thread.
/// </summary>
public interface IStoragePersistenceService : IAsyncDisposable
{
    /// <summary>
    /// Emitted when storage work should be surfaced as a non-fatal UI log.
    /// </summary>
    event EventHandler<string>? LogEmitted;

    /// <summary>
    /// Observes parsed UDP packets for session lifecycle transitions.
    /// </summary>
    void ObserveParsedPacket(ParsedPacket parsedPacket);

    /// <summary>
    /// Queues a completed lap summary for persistence.
    /// </summary>
    void EnqueueLapSummary(LapSummary lapSummary);

    /// <summary>
    /// Queues high-frequency lap samples for persistence when the active session is known.
    /// </summary>
    /// <param name="lapNumber">The completed lap number associated with the samples.</param>
    /// <param name="lapSamples">The samples to persist for offline analysis.</param>
    void EnqueueLapSamples(int lapNumber, IReadOnlyList<LapSample> lapSamples)
    {
    }

    /// <summary>
    /// Queues a detected race event for persistence.
    /// </summary>
    void EnqueueRaceEvent(RaceEvent raceEvent);

    /// <summary>
    /// Queues an AI analysis result for persistence.
    /// </summary>
    void EnqueueAiReport(int lapNumber, AIAnalysisResult analysisResult);

    /// <summary>
    /// Completes the active session record when one exists.
    /// </summary>
    Task CompleteActiveSessionAsync(CancellationToken cancellationToken = default);
}
