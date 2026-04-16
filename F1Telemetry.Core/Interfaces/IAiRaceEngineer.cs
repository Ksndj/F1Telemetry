using F1Telemetry.Core.Models;

namespace F1Telemetry.Core.Interfaces;

public interface IAiRaceEngineer
{
    Task<string> GenerateAdviceAsync(
        TelemetrySnapshot snapshot,
        RaceWeekendContext context,
        CancellationToken cancellationToken = default);
}
