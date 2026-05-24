using F1Telemetry.AI.Models;
using F1Telemetry.TTS.Models;

namespace F1Telemetry.App.Services;

/// <summary>
/// Carries the microphone query state needed to ask AI for live race advice.
/// </summary>
public sealed record VoiceAiQueryRequest
{
    /// <summary>
    /// Gets the current race context used as the AI prompt base.
    /// </summary>
    public AIAnalysisContext BaseContext { get; init; } = new();

    /// <summary>
    /// Gets the current AI settings snapshot.
    /// </summary>
    public AISettings AiSettings { get; init; } = new();

    /// <summary>
    /// Gets the current TTS settings snapshot.
    /// </summary>
    public TtsOptions TtsOptions { get; init; } = new();

    /// <summary>
    /// Gets the unique key used for TTS pacing of this voice query.
    /// </summary>
    public string AdviceKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the maximum microphone listening window.
    /// </summary>
    public TimeSpan RecognitionTimeout { get; init; } = TimeSpan.FromSeconds(8);
}
