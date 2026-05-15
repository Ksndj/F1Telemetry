namespace F1Telemetry.Analytics.Events;

/// <summary>
/// Represents configurable thresholds and cooldowns for race-event detection.
/// </summary>
public sealed class EventDetectionOptions
{
    /// <summary>
    /// Gets or sets the remaining fuel-lap threshold below which a low-fuel event is emitted.
    /// </summary>
    public float LowFuelLapsThreshold { get; set; } = 3.0f;

    /// <summary>
    /// Gets or sets the average tyre-wear threshold above which a high-wear event is emitted.
    /// </summary>
    public float HighTyreWearThreshold { get; set; } = 70.0f;

    /// <summary>
    /// Gets or sets the baseline track temperature used to normalize dynamic tyre temperature thresholds.
    /// </summary>
    public float TyreTemperatureBaselineTrackCelsius { get; set; } = 30.0f;

    /// <summary>
    /// Gets or sets the baseline surface temperature above which hot tyre alerts can be emitted.
    /// </summary>
    public float HighTyreSurfaceTemperatureBaselineCelsius { get; set; } = 105.0f;

    /// <summary>
    /// Gets or sets the baseline inner temperature above which hot tyre alerts can be emitted.
    /// </summary>
    public float HighTyreInnerTemperatureBaselineCelsius { get; set; } = 100.0f;

    /// <summary>
    /// Gets or sets the baseline surface temperature below which cold tyre alerts can be emitted.
    /// </summary>
    public float LowTyreSurfaceTemperatureBaselineCelsius { get; set; } = 75.0f;

    /// <summary>
    /// Gets or sets the baseline inner temperature below which cold tyre alerts can be emitted.
    /// </summary>
    public float LowTyreInnerTemperatureBaselineCelsius { get; set; } = 80.0f;

    /// <summary>
    /// Gets or sets the recovery hysteresis for tyre temperature alerts in degrees Celsius.
    /// </summary>
    public float TyreTemperatureRecoveryHysteresisCelsius { get; set; } = 3.0f;

    /// <summary>
    /// Gets or sets the minimum cooldown in seconds between duplicate events with the same dedup key.
    /// </summary>
    public int EventCooldownSeconds { get; set; } = 20;

    /// <summary>
    /// Gets or sets the attack or defense gap threshold in milliseconds.
    /// </summary>
    public uint AttackDefenseGapThresholdMs { get; set; } = 1_000;

    /// <summary>
    /// Gets or sets the gap threshold above which attack or defense windows re-arm.
    /// </summary>
    public uint GapWindowResetThresholdMs { get; set; } = 1_500;

    /// <summary>
    /// Gets or sets the same-type cooldown in seconds for race-window events.
    /// </summary>
    public int RaceWindowCooldownSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the ERS store threshold in joules below which a low-ERS event is emitted.
    /// </summary>
    public float LowErsStoreEnergyThresholdJoules { get; set; } = 1_000_000f;

    /// <summary>
    /// Gets or sets how many laps older the directly ahead car's tyres must be before old-tyre risk advice is emitted.
    /// </summary>
    public int OldTyreAgeDeltaLapsThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets how many laps newer the directly behind car's tyres must be before pressure advice is emitted.
    /// </summary>
    public int NewTyrePressureAgeDeltaLapsThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the same-lap gap threshold in milliseconds below which traffic risk advice is emitted.
    /// </summary>
    public uint TrafficRiskGapMs { get; set; } = 1_200;

    /// <summary>
    /// Gets or sets the same-lap front and rear gap threshold in milliseconds required for qualifying clean-air advice.
    /// </summary>
    public uint QualifyingCleanAirGapMs { get; set; } = 3_000;

    /// <summary>
    /// Gets or sets the tyre age in laps at which race pit-window advice can be emitted.
    /// </summary>
    public int RacePitWindowTyreAgeLapsThreshold { get; set; } = 12;

    /// <summary>
    /// Gets or sets the average tyre-wear percentage at which race pit-window advice can be emitted.
    /// </summary>
    public float RacePitWindowTyreWearThreshold { get; set; } = 60.0f;

    /// <summary>
    /// Gets or sets the cooldown in seconds between repeated lightweight advice events.
    /// </summary>
    public int AdviceCooldownSeconds { get; set; } = 45;

    /// <summary>
    /// Gets or sets the cooldown in seconds between repeated safety-car restart advice events.
    /// </summary>
    public int SafetyCarRestartCooldownSeconds { get; set; } = 60;
}
