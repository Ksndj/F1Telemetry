namespace F1Telemetry.Analytics.Events;

/// <summary>
/// Defines the supported race-event categories detected from live aggregate state.
/// </summary>
public enum EventType
{
    /// <summary>
    /// Indicates that the car directly ahead has committed to a pit stop.
    /// </summary>
    FrontCarPitted,

    /// <summary>
    /// Indicates that the car directly behind has committed to a pit stop.
    /// </summary>
    RearCarPitted,

    /// <summary>
    /// Indicates that the player's current lap has become invalid.
    /// </summary>
    PlayerLapInvalidated,

    /// <summary>
    /// Indicates that the player's remaining fuel laps are below the configured threshold.
    /// </summary>
    LowFuel,

    /// <summary>
    /// Indicates that the player's tyre wear is above the configured threshold.
    /// </summary>
    HighTyreWear
}
