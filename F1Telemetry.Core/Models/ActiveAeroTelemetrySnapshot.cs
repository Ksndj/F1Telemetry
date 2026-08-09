namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents the latest F1 26 active-aero and overtake-assist telemetry for a car.
/// </summary>
public sealed record ActiveAeroTelemetrySnapshot
{
    /// <summary>
    /// Gets the raw active-aero mode reported by the game.
    /// </summary>
    public byte ActiveAeroMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether active aero is available.
    /// </summary>
    public bool IsActiveAeroAvailable { get; init; }

    /// <summary>
    /// Gets the active-aero activation distance in metres.
    /// </summary>
    public ushort ActiveAeroActivationDistanceMetres { get; init; }

    /// <summary>
    /// Gets a value indicating whether overtake assist is available.
    /// </summary>
    public bool IsOvertakeAvailable { get; init; }

    /// <summary>
    /// Gets a value indicating whether overtake assist is active.
    /// </summary>
    public bool IsOvertakeActive { get; init; }

    /// <summary>
    /// Gets the overtake-assist activation distance in metres.
    /// </summary>
    public ushort OvertakeActivationDistanceMetres { get; init; }

    /// <summary>
    /// Gets the raw 2026 regulations value reported by the game.
    /// </summary>
    public byte Regulations2026 { get; init; }

    /// <summary>
    /// Gets a value indicating whether the car is driving the wrong way.
    /// </summary>
    public bool IsDrivingWrongWay { get; init; }

    /// <summary>
    /// Gets the datagram timestamp at which this telemetry was captured.
    /// </summary>
    public DateTimeOffset CapturedAt { get; init; }
}
