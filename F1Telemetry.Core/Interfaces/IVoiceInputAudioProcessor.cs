using F1Telemetry.Core.Models;

namespace F1Telemetry.Core.Interfaces;

/// <summary>
/// Processes microphone recordings before speech recognition.
/// </summary>
public interface IVoiceInputAudioProcessor
{
    /// <summary>
    /// Applies configured preprocessing stages and returns the recording for recognition.
    /// </summary>
    /// <param name="recording">The raw microphone recording.</param>
    /// <param name="settings">The audio preprocessing settings.</param>
    VoiceInputAudioProcessingResult Process(VoiceRecordingResult recording, VoiceInputAudioSettings settings);
}
