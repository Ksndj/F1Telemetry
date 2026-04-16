using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;

namespace F1Telemetry.Analytics.Services;

public sealed class TelemetryAnalyzer : ITelemetryAnalyzer
{
    public Task AnalyzeAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
