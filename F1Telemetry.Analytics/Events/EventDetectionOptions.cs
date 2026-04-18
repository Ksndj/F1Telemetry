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
}
