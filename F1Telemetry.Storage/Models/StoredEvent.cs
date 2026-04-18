using F1Telemetry.Analytics.Events;

namespace F1Telemetry.Storage.Models;

/// <summary>
/// Represents a persisted race event row.
/// </summary>
public sealed record StoredEvent
{
    /// <summary>
    /// Gets the auto-incremented row identifier.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// Gets the associated session identifier.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the event type.
    /// </summary>
    public EventType EventType { get; init; }

    /// <summary>
    /// Gets the event severity.
    /// </summary>
    public EventSeverity Severity { get; init; }

    /// <summary>
    /// Gets the lap number when known.
    /// </summary>
    public int? LapNumber { get; init; }

    /// <summary>
    /// Gets the vehicle index when known.
    /// </summary>
    public int? VehicleIdx { get; init; }

    /// <summary>
    /// Gets the driver name when known.
    /// </summary>
    public string? DriverName { get; init; }

    /// <summary>
    /// Gets the event message.
    /// </summary>
    public string Message { get; init; } = "-";

    /// <summary>
    /// Gets the serialized payload when known.
    /// </summary>
    public string? PayloadJson { get; init; }

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
}
