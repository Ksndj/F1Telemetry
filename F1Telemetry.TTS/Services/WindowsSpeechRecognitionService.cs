using System.Speech.Recognition;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;

namespace F1Telemetry.TTS.Services;

/// <summary>
/// Uses Windows speech recognition to transcribe recorded microphone audio.
/// </summary>
public sealed class WindowsSpeechRecognitionService : ISpeechRecognitionService
{
    /// <inheritdoc />
    public Task<string> RecognizeAsync(
        VoiceRecordingResult recording,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recording);

        if (recording.WaveBytes.Length == 0 || !recording.HasInput)
        {
            return Task.FromResult(string.Empty);
        }

        return Task.Run(() => RecognizeCore(recording.WaveBytes, cancellationToken), cancellationToken);
    }

    private static string RecognizeCore(byte[] waveBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var recognizer = new SpeechRecognitionEngine();
            recognizer.LoadGrammar(new DictationGrammar());
            using var stream = new MemoryStream(waveBytes);
            recognizer.SetInputToWaveStream(stream);
            var result = recognizer.Recognize();
            cancellationToken.ThrowIfCancellationRequested();
            return result?.Text.Trim() ?? string.Empty;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Windows speech recognition is unavailable.", ex);
        }
    }
}
