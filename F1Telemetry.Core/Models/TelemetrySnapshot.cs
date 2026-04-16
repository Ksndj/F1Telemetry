namespace F1Telemetry.Core.Models;

public sealed record TelemetrySnapshot(
    DateTimeOffset Timestamp,
    int LapNumber,
    double SpeedKph,
    double Throttle,
    double Brake,
    string? TrackName);
