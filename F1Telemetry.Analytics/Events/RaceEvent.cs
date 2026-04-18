namespace F1Telemetry.Analytics.Events;

/// <summary>
/// Represents a deduplicated race event emitted by analytics for UI, TTS, and AI consumers.
/// </summary>
public sealed record RaceEvent
{
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public EventType EventType { get; init; }

    /// <summary>
    /// Gets the timestamp when the event was detected.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the lap number associated with the event when known.
    /// </summary>
    public int? LapNumber { get; init; }

    /// <summary>
    /// Gets the vehicle index associated with the event when known.
    /// </summary>
    public int? VehicleIdx { get; init; }

    /// <summary>
    /// Gets the driver name associated with the event when known.
    /// </summary>
    public string? DriverName { get; init; }

    /// <summary>
    /// Gets the event severity.
    /// </summary>
    public EventSeverity Severity { get; init; }

    /// <summary>
    /// Gets the user-facing event message.
    /// </summary>
    public string Message { get; init; } = "-";

    /// <summary>
    /// Gets the deduplication key used by the detection service.
    /// </summary>
    public string DedupKey { get; init; } = "-";

    /// <summary>
    /// Gets the optional serialized payload for downstream consumers.
    /// </summary>
    public string? PayloadJson { get; init; }
}
