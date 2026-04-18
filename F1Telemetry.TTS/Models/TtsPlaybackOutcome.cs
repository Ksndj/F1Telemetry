namespace F1Telemetry.TTS.Models;

/// <summary>
/// Describes the queue or playback outcome captured for the dashboard log area.
/// </summary>
public enum TtsPlaybackOutcome
{
    /// <summary>
    /// Indicates that playback has started.
    /// </summary>
    PlaybackStarted,

    /// <summary>
    /// Indicates that playback completed successfully.
    /// </summary>
    PlaybackCompleted,

    /// <summary>
    /// Indicates that a duplicate message was rejected before entering the queue.
    /// </summary>
    Deduplicated,

    /// <summary>
    /// Indicates that a message was rejected because it is still inside the cooldown window.
    /// </summary>
    CooledDown,

    /// <summary>
    /// Indicates that a message was dropped because the queue was full or disabled.
    /// </summary>
    Dropped,

    /// <summary>
    /// Indicates that playback failed.
    /// </summary>
    Failed
}
