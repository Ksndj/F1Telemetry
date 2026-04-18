namespace F1Telemetry.TTS.Models;

/// <summary>
/// Represents a queued TTS message.
/// </summary>
public sealed record TtsMessage
{
    /// <summary>
    /// Gets the source category shown in logs.
    /// </summary>
    public string Source { get; init; } = "TTS";

    /// <summary>
    /// Gets the logical message type used when building deduplication keys.
    /// </summary>
    public string Type { get; init; } = "message";

    /// <summary>
    /// Gets the text that should be spoken.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Gets the deduplication key used for queue and cooldown checks.
    /// </summary>
    public string DedupKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the playback priority.
    /// </summary>
    public TtsPriority Priority { get; init; } = TtsPriority.Normal;

    /// <summary>
    /// Gets the cooldown window applied after a successful playback.
    /// </summary>
    public TimeSpan Cooldown { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets the timestamp when the message was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
