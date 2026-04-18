namespace F1Telemetry.TTS.Models;

/// <summary>
/// Represents a playback outcome that can be surfaced in the dashboard log area.
/// </summary>
public sealed record TtsPlaybackRecord
{
    /// <summary>
    /// Gets the record source shown in logs.
    /// </summary>
    public string Source { get; init; } = "TTS";

    /// <summary>
    /// Gets the spoken or diagnostic message text.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the deduplication key associated with the playback attempt.
    /// </summary>
    public string DedupKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the playback outcome type.
    /// </summary>
    public TtsPlaybackOutcome Outcome { get; init; } = TtsPlaybackOutcome.PlaybackCompleted;

    /// <summary>
    /// Gets the timestamp when the record was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets a value indicating whether playback succeeded.
    /// </summary>
    public bool IsSuccess => Outcome is TtsPlaybackOutcome.PlaybackStarted or TtsPlaybackOutcome.PlaybackCompleted;
}
