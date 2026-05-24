using F1Telemetry.Core.Models;

namespace F1Telemetry.Core.Interfaces;

/// <summary>
/// Represents an active microphone recording session.
/// </summary>
public interface IVoiceRecordingSession : IDisposable
{
    /// <summary>
    /// Stops recording and returns the captured wave audio.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<VoiceRecordingResult> StopAsync(CancellationToken cancellationToken = default);
}
