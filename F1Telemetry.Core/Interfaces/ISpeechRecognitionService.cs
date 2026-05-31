using F1Telemetry.Core.Models;

namespace F1Telemetry.Core.Interfaces;

/// <summary>
/// Recognizes speech from captured microphone audio.
/// </summary>
public interface ISpeechRecognitionService
{
    /// <summary>
    /// Recognizes a single microphone utterance from recorded wave audio.
    /// </summary>
    /// <param name="recording">The completed microphone recording.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SpeechRecognitionResult> RecognizeAsync(VoiceRecordingResult recording, CancellationToken cancellationToken = default);
}
