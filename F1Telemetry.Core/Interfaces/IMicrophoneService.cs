using F1Telemetry.Core.Models;

namespace F1Telemetry.Core.Interfaces;

/// <summary>
/// Enumerates, tests, and records from system microphone devices.
/// </summary>
public interface IMicrophoneService
{
    /// <summary>
    /// Returns the currently available microphone input devices.
    /// </summary>
    IReadOnlyList<MicrophoneDeviceInfo> GetDevices();

    /// <summary>
    /// Starts a microphone recording session.
    /// </summary>
    /// <param name="deviceId">The selected microphone device identifier, or blank for default.</param>
    IVoiceRecordingSession StartRecording(string? deviceId);

    /// <summary>
    /// Records a short sample and returns measured input levels.
    /// </summary>
    /// <param name="deviceId">The selected microphone device identifier, or blank for default.</param>
    /// <param name="duration">The test duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<MicrophoneTestResult> TestInputAsync(
        string? deviceId,
        TimeSpan duration,
        CancellationToken cancellationToken = default);
}
