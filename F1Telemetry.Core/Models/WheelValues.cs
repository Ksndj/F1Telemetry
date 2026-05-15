namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents one value per wheel in the F1 UDP wheel order.
/// </summary>
/// <typeparam name="T">The value type stored for each wheel.</typeparam>
public sealed record WheelValues<T>(
    T RearLeft,
    T RearRight,
    T FrontLeft,
    T FrontRight);
