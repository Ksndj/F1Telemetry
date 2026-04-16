using F1Telemetry.Core.Models;

namespace F1Telemetry.Core.Interfaces;

public interface ITelemetryAnalyzer
{
    Task AnalyzeAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken = default);
}
