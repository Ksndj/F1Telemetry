namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents the latest visible tyre temperature and pressure state for one car.
/// </summary>
public sealed record TyreConditionSnapshot
{
    /// <summary>
    /// Gets the timestamp when the tyre condition was observed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the surface temperature for each tyre in degrees Celsius.
    /// </summary>
    public WheelValues<byte> SurfaceTemperatureCelsius { get; init; } = new(0, 0, 0, 0);

    /// <summary>
    /// Gets the inner temperature for each tyre in degrees Celsius.
    /// </summary>
    public WheelValues<byte> InnerTemperatureCelsius { get; init; } = new(0, 0, 0, 0);

    /// <summary>
    /// Gets the tyre pressure for each tyre in PSI.
    /// </summary>
    public WheelValues<float> PressurePsi { get; init; } = new(0f, 0f, 0f, 0f);
}
