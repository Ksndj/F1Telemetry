namespace F1Telemetry.App.Services;

/// <summary>
/// Describes the outcome of a microphone-triggered AI race engineer query.
/// </summary>
public sealed record VoiceAiQueryResult
{
    /// <summary>
    /// Gets a value indicating whether the AI request completed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the text recognized from the driver's microphone.
    /// </summary>
    public string RecognizedQuestion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the short answer selected for TTS playback.
    /// </summary>
    public string SpeechText { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the answer entered the TTS queue.
    /// </summary>
    public bool WasQueuedForSpeech { get; init; }

    /// <summary>
    /// Gets the short user-facing failure reason.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;
}
