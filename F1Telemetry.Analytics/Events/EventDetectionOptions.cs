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
}
