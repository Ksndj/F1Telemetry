using System.Globalization;
using System.Runtime.Versioning;
using System.Speech.Recognition;
using F1Telemetry.Core.Interfaces;

namespace F1Telemetry.TTS.Services;

/// <summary>
/// Uses the default Windows speech recognizer and microphone for one-shot dictation.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsSpeechRecognitionService : ISpeechRecognitionService
{
    /// <inheritdoc />
    public Task<string> RecognizeOnceAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero)
        {
            return Task.FromResult(string.Empty);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => RecognizeCore(timeout, cancellationToken), cancellationToken);
    }

    private static string RecognizeCore(TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var recognizer = CreateRecognizer();
            recognizer.LoadGrammar(new DictationGrammar());
            recognizer.SetInputToDefaultAudioDevice();
            var result = recognizer.Recognize(timeout);
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

    private static SpeechRecognitionEngine CreateRecognizer()
    {
        try
        {
            return new SpeechRecognitionEngine(CultureInfo.CurrentCulture);
        }
        catch
        {
            return new SpeechRecognitionEngine();
        }
    }
}
