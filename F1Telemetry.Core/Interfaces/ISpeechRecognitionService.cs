namespace F1Telemetry.Core.Interfaces;

/// <summary>
/// Captures one short speech utterance from the user's microphone and returns recognized text.
/// </summary>
public interface ISpeechRecognitionService
{
    /// <summary>
    /// Recognizes a single microphone utterance within the provided timeout.
    /// </summary>
    /// <param name="timeout">The maximum listening window.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string> RecognizeOnceAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
