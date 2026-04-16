namespace F1Telemetry.Analytics.Laps;

/// <summary>
/// Represents a single player-lap sample captured from the real-time session state.
/// </summary>
public sealed record LapSample
{
    /// <summary>
    /// Gets the time when the sample was captured.
    /// </summary>
    public DateTimeOffset SampledAt { get; init; }

    /// <summary>
    /// Gets the current frame identifier from the UDP header.
    /// </summary>
    public uint FrameIdentifier { get; init; }

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
    public uint? CurrentLapTimeInMs { get; init; }

    /// <summary>
    /// Gets the previous completed lap time in milliseconds.
    /// </summary>
    public uint? LastLapTimeInMs { get; init; }

    /// <summary>
    /// Gets the current speed in km/h.
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
    public sbyte? Gear { get; init; }

    /// <summary>
    /// Gets the remaining fuel in litres.
    /// </summary>
    public float? FuelRemaining { get; init; }

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
    /// Gets the current race position.
    /// </summary>
    public byte? Position { get; init; }

    /// <summary>
    /// Gets the delta to the car in front in milliseconds.
    /// </summary>
    public ushort? DeltaFrontInMs { get; init; }

    /// <summary>
    /// Gets the delta to the race leader in milliseconds.
    /// </summary>
    public ushort? DeltaLeaderInMs { get; init; }

    /// <summary>
    /// Gets the raw pit status value.
    /// </summary>
    public byte? PitStatus { get; init; }

    /// <summary>
    /// Gets a value indicating whether the lap is currently valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the raw visual tyre compound identifier.
    /// </summary>
    public byte? VisualTyreCompound { get; init; }

    /// <summary>
    /// Gets the raw actual tyre compound identifier.
    /// </summary>
    public byte? ActualTyreCompound { get; init; }
}
