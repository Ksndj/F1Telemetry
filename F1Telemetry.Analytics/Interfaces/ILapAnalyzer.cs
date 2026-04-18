using F1Telemetry.Analytics.Laps;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Analytics.Interfaces;

/// <summary>
/// Builds per-lap summaries from the real-time player state stream.
/// </summary>
public interface ILapAnalyzer
{
    /// <summary>
    /// Observes a parsed packet after it has been aggregated into the latest central state.
    /// </summary>
    /// <param name="parsedPacket">The parsed UDP packet.</param>
    /// <param name="sessionState">The latest central session state.</param>
    void Observe(ParsedPacket parsedPacket, Core.Models.SessionState sessionState);

    /// <summary>
    /// Clears in-memory lap history and resets lap state for a new F1 session.
    /// </summary>
    /// <param name="sessionUid">Session unique identifier from packet header.</param>
    void ResetForSession(ulong sessionUid);

    /// <summary>
    /// Returns all lap summaries collected during the current session.
    /// </summary>
    IReadOnlyList<LapSummary> CaptureAllLaps();

    /// <summary>
    /// Returns an immutable snapshot of the current in-flight lap samples.
    /// </summary>
    IReadOnlyList<LapSample> CaptureCurrentLapSamples();

    /// <summary>
    /// Returns the most recent completed laps, ordered newest first.
    /// </summary>
    /// <param name="maxCount">The maximum number of laps to return.</param>
    IReadOnlyList<LapSummary> CaptureRecentLaps(int maxCount);

    /// <summary>
    /// Returns the best known completed lap.
    /// </summary>
    LapSummary? CaptureBestLap();

    /// <summary>
    /// Returns the most recently completed lap.
    /// </summary>
    LapSummary? CaptureLastLap();
}
