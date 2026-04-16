using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;

namespace F1Telemetry.AI.Services;

public sealed class AiRaceEngineerService : IAiRaceEngineer
{
    public Task<string> GenerateAdviceAsync(
        TelemetrySnapshot snapshot,
        RaceWeekendContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult("AI race engineer placeholder advice.");
    }
}
