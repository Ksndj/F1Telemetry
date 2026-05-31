using F1Telemetry.AI.Models;

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
    /// Gets the per-question correlation id shared by app and audit logs.
    /// </summary>
    public string QuestionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the session UID used by the answered request.
    /// </summary>
    public ulong? SessionUid { get; init; }

    /// <summary>
    /// Gets the recognized question intent.
    /// </summary>
    public VoiceQuestionIntent Intent { get; init; } = VoiceQuestionIntent.UNKNOWN;

    /// <summary>
    /// Gets the assistant mode used for the answer.
    /// </summary>
    public RaceAssistantMode Mode { get; init; } = RaceAssistantMode.NoTelemetry;

    /// <summary>
    /// Gets the structured strategy advice.
    /// </summary>
    public StrategyAdviceResult? Advice { get; init; }

    /// <summary>
    /// Gets the short answer selected for TTS playback.
    /// </summary>
    public string SpeechText { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the answer entered the TTS queue.
    /// </summary>
    public bool WasQueuedForSpeech { get; init; }

    /// <summary>
    /// Gets the reason why speech playback was skipped.
    /// </summary>
    public string SpeechSkippedReason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the short user-facing failure reason.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the result was ignored because the session changed.
    /// </summary>
    public bool WasIgnoredBecauseSessionChanged { get; init; }

    /// <summary>
    /// Gets a value indicating whether the request was canceled before completion.
    /// </summary>
    public bool WasCanceled { get; init; }

    /// <summary>
    /// Gets the raw microphone recording duration in milliseconds.
    /// </summary>
    public int RecordingDurationMs { get; init; }

    /// <summary>
    /// Gets the detected speech duration in milliseconds.
    /// </summary>
    public int SpeechDurationMs { get; init; }

    /// <summary>
    /// Gets a value indicating whether voice activity was detected.
    /// </summary>
    public bool VadDetected { get; init; }

    /// <summary>
    /// Gets a value indicating whether microphone preprocessing was enabled.
    /// </summary>
    public bool PreprocessingEnabled { get; init; }

    /// <summary>
    /// Gets the VoiceAI recognition failure reason key.
    /// </summary>
    public string RecognitionFailedReason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the raw microphone RMS level in dBFS.
    /// </summary>
    public double RawRmsDb { get; init; }

    /// <summary>
    /// Gets the processed microphone RMS level in dBFS.
    /// </summary>
    public double ProcessedRmsDb { get; init; }

    /// <summary>
    /// Gets the processed microphone peak level in dBFS.
    /// </summary>
    public double PeakDb { get; init; }

    /// <summary>
    /// Gets a value indicating whether peak limiting clipped the processed audio.
    /// </summary>
    public bool WasClipped { get; init; }

    /// <summary>
    /// Gets the speech recognition confidence between 0 and 1.
    /// </summary>
    public double RecognitionConfidence { get; init; }
}
