namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents speech recognition text and confidence for a microphone utterance.
/// </summary>
public sealed record SpeechRecognitionResult
{
    /// <summary>
    /// Gets an empty recognition result.
    /// </summary>
    public static SpeechRecognitionResult Empty { get; } = new();

    /// <summary>
    /// Gets the recognized text.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Gets the recognition confidence between 0 and 1.
    /// </summary>
    public double Confidence { get; init; }
}
