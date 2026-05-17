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
    /// Gets the career season link identifier from the latest session packet.
    /// </summary>
    public uint? SeasonLinkIdentifier { get; init; }

    /// <summary>
    /// Gets the race-weekend link identifier from the latest session packet.
    /// </summary>
    public uint? WeekendLinkIdentifier { get; init; }

    /// <summary>
    /// Gets the session link identifier from the latest session packet.
    /// </summary>
    public uint? SessionLinkIdentifier { get; init; }

    /// <summary>
    /// Gets the number of sessions in the current weekend when known.
    /// </summary>
    public byte? NumSessionsInWeekend { get; init; }

    /// <summary>
    /// Gets the raw weekend session type sequence when known.
    /// </summary>
    public IReadOnlyList<byte> WeekendStructure { get; init; } = Array.Empty<byte>();

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
    /// Gets the ideal pit window lap from the session packet.
    /// </summary>
    public byte? PitStopWindowIdealLap { get; init; }

    /// <summary>
    /// Gets the latest pit window lap from the session packet.
    /// </summary>
    public byte? PitStopWindowLatestLap { get; init; }

    /// <summary>
    /// Gets the estimated rejoin position after pitting.
    /// </summary>
    public byte? PitStopRejoinPosition { get; init; }

    /// <summary>
    /// Gets compact weather forecast samples for the current session.
    /// </summary>
    public IReadOnlyList<WeatherForecastSummary> WeatherForecastSamples { get; init; } = Array.Empty<WeatherForecastSummary>();

    /// <summary>
    /// Gets the latest raw safety car status from the session packet.
    /// </summary>
    public byte? SafetyCarStatus { get; init; }

    /// <summary>
    /// Gets the latest marshal-zone flag values by zone index.
    /// </summary>
    public IReadOnlyDictionary<int, sbyte> MarshalZoneFlags { get; init; } = new Dictionary<int, sbyte>();

    /// <summary>
    /// Gets the number of active cars when known.
    /// </summary>
    public byte? ActiveCarCount { get; init; }

    /// <summary>
    /// Gets the most recent event code when known.
    /// </summary>
    public string? LastEventCode { get; init; }

    /// <summary>
    /// Gets a value indicating whether a final classification packet was observed for this session.
    /// </summary>
    public bool HasFinalClassification { get; init; }

    /// <summary>
    /// Gets the timestamp when final classification was observed.
    /// </summary>
    public DateTimeOffset? FinalClassificationReceivedAt { get; init; }

    /// <summary>
    /// Gets the player's final classified position when available.
    /// </summary>
    public byte? PlayerFinalClassificationPosition { get; init; }

    /// <summary>
    /// Gets the player's final classified lap count when available.
    /// </summary>
    public byte? PlayerFinalClassificationLaps { get; init; }

    /// <summary>
    /// Gets the player's raw final classification status when available.
    /// </summary>
    public byte? PlayerFinalClassificationStatus { get; init; }

    /// <summary>
    /// Gets the latest player car snapshot.
    /// </summary>
    public CarSnapshot? PlayerCar { get; init; }

    /// <summary>
    /// Gets the latest game-reported player tyre inventory.
    /// </summary>
    public TyreInventorySnapshot? PlayerTyreInventory { get; init; }

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
