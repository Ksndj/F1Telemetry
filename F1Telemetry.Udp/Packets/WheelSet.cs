namespace F1Telemetry.Udp.Packets;

public sealed record WheelSet<T>(
    T RearLeft,
    T RearRight,
    T FrontLeft,
    T FrontRight);
