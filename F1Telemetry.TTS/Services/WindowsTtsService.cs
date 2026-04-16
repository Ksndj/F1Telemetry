using F1Telemetry.Core.Interfaces;

namespace F1Telemetry.TTS.Services;

public sealed class WindowsTtsService : ITtsService
{
    public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
