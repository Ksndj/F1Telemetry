namespace F1Telemetry.Core.Interfaces;

/// <summary>
/// Provides low-level text-to-speech playback.
/// </summary>
public interface ITtsService
{
    /// <summary>
    /// Applies the voice settings that should be used for subsequent playback.
    /// </summary>
    void Configure(string? voiceName, int volume, int rate);

    /// <summary>
    /// Speaks the supplied text asynchronously.
    /// </summary>
    Task SpeakAsync(string text, CancellationToken cancellationToken = default);
}
