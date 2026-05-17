namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents a compact weather forecast sample from the session state.
/// </summary>
public sealed record WeatherForecastSummary
{
    /// <summary>
    /// Gets the session type for which the forecast applies.
    /// </summary>
    public byte SessionType { get; init; }

    /// <summary>
    /// Gets the forecast time offset in minutes.
    /// </summary>
    public byte TimeOffsetMinutes { get; init; }

    /// <summary>
    /// Gets the raw weather identifier.
    /// </summary>
    public byte Weather { get; init; }

    /// <summary>
    /// Gets the forecast track temperature.
    /// </summary>
    public sbyte TrackTemperature { get; init; }

    /// <summary>
    /// Gets the track temperature change direction.
    /// </summary>
    public sbyte TrackTemperatureChange { get; init; }

    /// <summary>
    /// Gets the forecast air temperature.
    /// </summary>
    public sbyte AirTemperature { get; init; }

    /// <summary>
    /// Gets the air temperature change direction.
    /// </summary>
    public sbyte AirTemperatureChange { get; init; }

    /// <summary>
    /// Gets the rain probability percentage.
    /// </summary>
    public byte RainPercentage { get; init; }
}
