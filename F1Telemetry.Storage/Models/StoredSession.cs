namespace F1Telemetry.Storage.Models;

/// <summary>
/// Represents a persisted session row.
/// </summary>
public sealed record StoredSession
{
    /// <summary>
    /// Gets the storage identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the game session UID serialized as text.
    /// </summary>
    public string SessionUid { get; init; } = string.Empty;

    /// <summary>
    /// Gets the track identifier when known.
    /// </summary>
    public int? TrackId { get; init; }

    /// <summary>
    /// Gets the session type when known.
    /// </summary>
    public int? SessionType { get; init; }

    /// <summary>
    /// Gets the configured lap count when known.
    /// </summary>
    public int? TotalLaps { get; init; }

    /// <summary>
    /// Gets the number of sessions in the weekend when known.
    /// </summary>
    public int? NumSessionsInWeekend { get; init; }

    /// <summary>
    /// Gets the raw weekend session type sequence when known.
    /// </summary>
    public IReadOnlyList<byte> WeekendStructure { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// Gets the session start timestamp.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Gets the session end timestamp when known.
    /// </summary>
    public DateTimeOffset? EndedAt { get; init; }
}
