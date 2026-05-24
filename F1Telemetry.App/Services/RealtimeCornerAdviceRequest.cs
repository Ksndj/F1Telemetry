using F1Telemetry.AI.Models;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Core.Models;
using F1Telemetry.TTS.Models;

namespace F1Telemetry.App.Services;

/// <summary>
/// Carries the completed-lap state needed to generate realtime corner engineer advice.
/// </summary>
public sealed record RealtimeCornerAdviceRequest
{
    /// <summary>
    /// Gets the current aggregate session state.
    /// </summary>
    public SessionState SessionState { get; init; } = new();

    /// <summary>
    /// Gets the active game session UID.
    /// </summary>
    public ulong? ActiveSessionUid { get; init; }

    /// <summary>
    /// Gets the completed lap summary that closed this trigger.
    /// </summary>
    public LapSummary CompletedLap { get; init; } = new();

    /// <summary>
    /// Gets the completed-lap samples already held in memory.
    /// </summary>
    public IReadOnlyList<LapSample> LapSamples { get; init; } = Array.Empty<LapSample>();

    /// <summary>
    /// Gets recently completed laps for trigger pacing and prompt context.
    /// </summary>
    public IReadOnlyList<LapSummary> RecentCompletedLaps { get; init; } = Array.Empty<LapSummary>();

    /// <summary>
    /// Gets the current AI settings snapshot.
    /// </summary>
    public AISettings AiSettings { get; init; } = new();

    /// <summary>
    /// Gets the current TTS settings snapshot.
    /// </summary>
    public TtsOptions TtsOptions { get; init; } = new();
}
