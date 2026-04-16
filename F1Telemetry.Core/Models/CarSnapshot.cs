namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents the latest real-time snapshot for a single car in the session.
/// </summary>
public sealed record CarSnapshot
{
    /// <summary>
    /// Gets the in-session car index.
    /// </summary>
    public int CarIndex { get; init; }

    /// <summary>
    /// Gets a value indicating whether this snapshot belongs to the player car.
    /// </summary>
    public bool IsPlayer { get; init; }

    /// <summary>
    /// Gets a value indicating whether the car is AI controlled.
    /// </summary>
    public bool IsAiControlled { get; init; }

    /// <summary>
    /// Gets a value indicating whether telemetry-sensitive fields are restricted for this car.
    /// </summary>
    public bool IsTelemetryRestricted { get; init; }

    /// <summary>
    /// Gets a value indicating whether telemetry-sensitive fields are visible to the UI.
    /// </summary>
    public bool HasTelemetryAccess => IsPlayer || !IsTelemetryRestricted;

    /// <summary>
    /// Gets the last known driver name.
    /// </summary>
    public string? DriverName { get; init; }

    /// <summary>
    /// Gets the displayed race number.
    /// </summary>
    public byte? RaceNumber { get; init; }

    /// <summary>
    /// Gets the raw team identifier from the UDP packet.
    /// </summary>
    public byte? TeamId { get; init; }

    /// <summary>
    /// Gets the raw nationality identifier from the UDP packet.
    /// </summary>
    public byte? Nationality { get; init; }

    /// <summary>
    /// Gets the current on-track position.
    /// </summary>
    public byte? Position { get; init; }

    /// <summary>
    /// Gets the current lap number.
    /// </summary>
    public byte? CurrentLapNumber { get; init; }

    /// <summary>
    /// Gets the last lap time in milliseconds.
    /// </summary>
    public uint? LastLapTimeInMs { get; init; }

    /// <summary>
    /// Gets the current lap time in milliseconds.
    /// </summary>
    public uint? CurrentLapTimeInMs { get; init; }

    /// <summary>
    /// Gets the best lap time in milliseconds when known.
    /// </summary>
    public uint? BestLapTimeInMs { get; init; }

    /// <summary>
    /// Gets the lap distance in metres.
    /// </summary>
    public float? LapDistance { get; init; }

    /// <summary>
    /// Gets the total distance in metres.
    /// </summary>
    public float? TotalDistance { get; init; }

    /// <summary>
    /// Gets the delta to the car in front in milliseconds.
    /// </summary>
    public ushort? DeltaToCarInFrontInMs { get; init; }

    /// <summary>
    /// Gets the delta to the leader in milliseconds.
    /// </summary>
    public ushort? DeltaToRaceLeaderInMs { get; init; }

    /// <summary>
    /// Gets the latest telemetry snapshot when available.
    /// </summary>
    public TelemetrySnapshot? Telemetry { get; init; }

    /// <summary>
    /// Gets the current steering input when visible.
    /// </summary>
    public float? SteeringInput { get; init; }

    /// <summary>
    /// Gets the current gear when visible.
    /// </summary>
    public sbyte? Gear { get; init; }

    /// <summary>
    /// Gets the current engine RPM when visible.
    /// </summary>
    public ushort? EngineRpm { get; init; }

    /// <summary>
    /// Gets a value indicating whether DRS is enabled when visible.
    /// </summary>
    public bool? IsDrsEnabled { get; init; }

    /// <summary>
    /// Gets the remaining fuel in tank when visible.
    /// </summary>
    public float? FuelInTank { get; init; }

    /// <summary>
    /// Gets the estimated remaining laps of fuel when visible.
    /// </summary>
    public float? FuelRemainingLaps { get; init; }

    /// <summary>
    /// Gets the stored ERS energy in joules when visible.
    /// </summary>
    public float? ErsStoreEnergy { get; init; }

    /// <summary>
    /// Gets the raw actual tyre compound identifier when visible.
    /// </summary>
    public byte? ActualTyreCompound { get; init; }

    /// <summary>
    /// Gets the raw visual tyre compound identifier when visible.
    /// </summary>
    public byte? VisualTyreCompound { get; init; }

    /// <summary>
    /// Gets the tyre age in laps when visible.
    /// </summary>
    public byte? TyresAgeLaps { get; init; }

    /// <summary>
    /// Gets the average tyre wear percentage when visible.
    /// </summary>
    public float? TyreWear { get; init; }

    /// <summary>
    /// Gets the raw pit status value from lap data when known.
    /// </summary>
    public byte? PitStatus { get; init; }

    /// <summary>
    /// Gets a value indicating whether the current lap is valid when known.
    /// </summary>
    public bool? IsCurrentLapValid { get; init; }

    /// <summary>
    /// Gets the front-left wing damage percentage when visible.
    /// </summary>
    public byte? FrontLeftWingDamage { get; init; }

    /// <summary>
    /// Gets the front-right wing damage percentage when visible.
    /// </summary>
    public byte? FrontRightWingDamage { get; init; }

    /// <summary>
    /// Gets the rear wing damage percentage when visible.
    /// </summary>
    public byte? RearWingDamage { get; init; }

    /// <summary>
    /// Gets the latest world X position when visible.
    /// </summary>
    public float? WorldPositionX { get; init; }

    /// <summary>
    /// Gets the latest world Y position when visible.
    /// </summary>
    public float? WorldPositionY { get; init; }

    /// <summary>
    /// Gets the latest world Z position when visible.
    /// </summary>
    public float? WorldPositionZ { get; init; }

    /// <summary>
    /// Gets the last time this car snapshot was updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
