namespace F1Telemetry.Storage.Models;

/// <summary>
/// Represents a persisted high-frequency lap sample used for offline corner analysis.
/// </summary>
public sealed record StoredLapSample
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
    /// Gets the stable order of the sample within the captured lap.
    /// </summary>
    public int SampleIndex { get; init; }

    /// <summary>
    /// Gets the time when the sample was captured.
    /// </summary>
    public DateTimeOffset SampledAt { get; init; }

    /// <summary>
    /// Gets the UDP frame identifier associated with the sample.
    /// </summary>
    public long FrameIdentifier { get; init; }

    /// <summary>
    /// Gets the player lap number at the time of the sample.
    /// </summary>
    public int LapNumber { get; init; }

    /// <summary>
    /// Gets the current lap distance in metres.
    /// </summary>
    public float? LapDistance { get; init; }

    /// <summary>
    /// Gets the current total distance in metres.
    /// </summary>
    public float? TotalDistance { get; init; }

    /// <summary>
    /// Gets the current lap time in milliseconds.
    /// </summary>
    public int? CurrentLapTimeInMs { get; init; }

    /// <summary>
    /// Gets the previous completed lap time in milliseconds.
    /// </summary>
    public int? LastLapTimeInMs { get; init; }

    /// <summary>
    /// Gets the current speed in KPH.
    /// </summary>
    public double? SpeedKph { get; init; }

    /// <summary>
    /// Gets the current throttle input.
    /// </summary>
    public double? Throttle { get; init; }

    /// <summary>
    /// Gets the current brake input.
    /// </summary>
    public double? Brake { get; init; }

    /// <summary>
    /// Gets the current steering input.
    /// </summary>
    public float? Steering { get; init; }

    /// <summary>
    /// Gets the current selected gear.
    /// </summary>
    public int? Gear { get; init; }

    /// <summary>
    /// Gets the remaining fuel in litres.
    /// </summary>
    public float? FuelRemainingLitres { get; init; }

    /// <summary>
    /// Gets the estimated fuel laps remaining.
    /// </summary>
    public float? FuelLapsRemaining { get; init; }

    /// <summary>
    /// Gets the stored ERS energy in joules.
    /// </summary>
    public float? ErsStoreEnergy { get; init; }

    /// <summary>
    /// Gets the average tyre wear percentage.
    /// </summary>
    public float? TyreWear { get; init; }

    /// <summary>
    /// Gets the front-left tyre wear percentage.
    /// </summary>
    public float? TyreWearFrontLeft { get; init; }

    /// <summary>
    /// Gets the front-right tyre wear percentage.
    /// </summary>
    public float? TyreWearFrontRight { get; init; }

    /// <summary>
    /// Gets the rear-left tyre wear percentage.
    /// </summary>
    public float? TyreWearRearLeft { get; init; }

    /// <summary>
    /// Gets the rear-right tyre wear percentage.
    /// </summary>
    public float? TyreWearRearRight { get; init; }

    /// <summary>
    /// Gets the current race position.
    /// </summary>
    public int? Position { get; init; }

    /// <summary>
    /// Gets the delta to the car in front in milliseconds.
    /// </summary>
    public int? DeltaFrontInMs { get; init; }

    /// <summary>
    /// Gets the delta to the race leader in milliseconds.
    /// </summary>
    public int? DeltaLeaderInMs { get; init; }

    /// <summary>
    /// Gets the raw pit status value.
    /// </summary>
    public int? PitStatus { get; init; }

    /// <summary>
    /// Gets a value indicating whether the lap was valid at capture time.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the raw visual tyre compound identifier.
    /// </summary>
    public int? VisualTyreCompound { get; init; }

    /// <summary>
    /// Gets the raw actual tyre compound identifier.
    /// </summary>
    public int? ActualTyreCompound { get; init; }

    /// <summary>
    /// Gets the row creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
}
