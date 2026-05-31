using F1Telemetry.AI.Models;
using F1Telemetry.Core.Models;
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
    /// Gets the text question when the user uses the keyboard fallback.
    /// </summary>
    public string QuestionText { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current track text used by RaceAssistant audit logs.
    /// </summary>
    public string Track { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current session type text used by RaceAssistant audit logs.
    /// </summary>
    public string SessionType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current or latest UDP Raw Log file path used only as a filename summary.
    /// </summary>
    public string UdpRawLogFile { get; init; } = string.Empty;

    /// <summary>
    /// Gets the already-built strategy question context, usually for typed questions.
    /// </summary>
    public StrategyQuestionContext? StrategyQuestionContext { get; init; }

    /// <summary>
    /// Gets a strategy context factory that runs after speech recognition.
    /// </summary>
    public Func<string, StrategyQuestionContext>? BuildStrategyQuestionContext { get; init; }

    /// <summary>
    /// Gets a session UID accessor used to drop stale answers after the AI returns.
    /// </summary>
    public Func<ulong?>? CaptureCurrentSessionUid { get; init; }

    /// <summary>
    /// Gets a value indicating whether this answer may enter the TTS queue.
    /// </summary>
    public bool EnableTtsAnswer { get; init; } = true;

    /// <summary>
    /// Gets the maximum UI answer length.
    /// </summary>
    public int MaxAnswerLength { get; init; } = 240;

    /// <summary>
    /// Gets the completed microphone recording to recognize.
    /// </summary>
    public VoiceRecordingResult Recording { get; init; } = new();

    /// <summary>
    /// Gets the microphone preprocessing and recognition quality settings.
    /// </summary>
    public VoiceInputAudioSettings AudioSettings { get; init; } = new();
}
