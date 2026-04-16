namespace F1Telemetry.Core.Interfaces;

public interface ITtsService
{
    Task SpeakAsync(string text, CancellationToken cancellationToken = default);
}
