namespace F1Telemetry.Core.Models;

public sealed record RaceWeekendContext(
    string SessionType,
    string TeamName,
    string DriverName,
    string? TrackName);
