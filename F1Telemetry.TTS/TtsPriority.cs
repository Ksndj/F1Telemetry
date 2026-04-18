namespace F1Telemetry.TTS;

/// <summary>
/// Represents the playback priority for a queued TTS message.
/// </summary>
public enum TtsPriority
{
    /// <summary>
    /// Low-priority message that may wait behind more urgent items.
    /// </summary>
    Low,

    /// <summary>
    /// Normal-priority message.
    /// </summary>
    Normal,

    /// <summary>
    /// High-priority message that should run before queued lower-priority items.
    /// </summary>
    High
}
