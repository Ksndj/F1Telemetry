namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents the latest aggregate session state exposed to the application.
/// </summary>
public sealed record SessionState
{
    /// <summary>
    /// Gets the player car index when known.
    /// </summary>
    public byte? PlayerCarIndex { get; init; }

    /// <summary>
    /// Gets the raw track identifier from the session packet.
    /// </summary>
    public sbyte? TrackId { get; init; }

    /// <summary>
    /// Gets the raw session type identifier from the session packet.
    /// </summary>
    public byte? SessionType { get; init; }

    /// <summary>
    /// Gets the raw weather identifier from the session packet.
    /// </summary>
    public byte? Weather { get; init; }

    /// <summary>
    /// Gets the current track temperature.
    /// </summary>
    public sbyte? TrackTemperature { get; init; }

    /// <summary>
    /// Gets the current air temperature.
    /// </summary>
    public sbyte? AirTemperature { get; init; }

    /// <summary>
    /// Gets the configured total laps for the session.
    /// </summary>
    public byte? TotalLaps { get; init; }

    /// <summary>
    /// Gets the remaining session time in seconds.
    /// </summary>
    public ushort? SessionTimeLeft { get; init; }

    /// <summary>
    /// Gets the total session duration in seconds.
    /// </summary>
    public ushort? SessionDuration { get; init; }

    /// <summary>
    /// Gets the pit speed limit in km/h.
    /// </summary>
    public byte? PitSpeedLimit { get; init; }

    /// <summary>
    /// Gets the number of active cars when known.
    /// </summary>
    public byte? ActiveCarCount { get; init; }

    /// <summary>
    /// Gets the most recent event code when known.
    /// </summary>
    public string? LastEventCode { get; init; }

    /// <summary>
    /// Gets the latest player car snapshot.
    /// </summary>
    public CarSnapshot? PlayerCar { get; init; }

    /// <summary>
    /// Gets the latest opponent car snapshots.
    /// </summary>
    public IReadOnlyList<CarSnapshot> Opponents { get; init; } = Array.Empty<CarSnapshot>();

    /// <summary>
    /// Gets all currently tracked cars.
    /// </summary>
    public IReadOnlyList<CarSnapshot> Cars { get; init; } = Array.Empty<CarSnapshot>();

    /// <summary>
    /// Gets the last time any tracked session field was updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
