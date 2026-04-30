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
    HighTyreWear,

    /// <summary>
    /// Indicates that a full safety car status is active.
    /// </summary>
    SafetyCar,

    /// <summary>
    /// Indicates that a virtual safety car status is active.
    /// </summary>
    VirtualSafetyCar,

    /// <summary>
    /// Indicates that a yellow flag marshal zone became active.
    /// </summary>
    YellowFlag,

    /// <summary>
    /// Indicates that a red flag marshal zone became active.
    /// </summary>
    RedFlag,

    /// <summary>
    /// Indicates that the player entered an attack gap window.
    /// </summary>
    AttackWindow,

    /// <summary>
    /// Indicates that the player entered a defense gap window.
    /// </summary>
    DefenseWindow,

    /// <summary>
    /// Indicates that the player's ERS store is below the configured threshold.
    /// </summary>
    LowErs,

    /// <summary>
    /// Indicates that required evidence was missing or uncertain.
    /// </summary>
    DataQualityWarning
}
