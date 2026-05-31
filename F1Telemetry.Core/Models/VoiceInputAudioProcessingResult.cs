namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents the output and metrics from VoiceAI microphone preprocessing.
/// </summary>
public sealed record VoiceInputAudioProcessingResult
{
    /// <summary>
    /// Gets the recording that should be sent to speech recognition.
    /// </summary>
    public VoiceRecordingResult Recording { get; init; } = new();

    /// <summary>
    /// Gets the raw input RMS level in dBFS.
    /// </summary>
    public double RawRmsDb { get; init; } = VoiceInputAudioMetrics.FloorDb;

    /// <summary>
    /// Gets the processed output RMS level in dBFS.
    /// </summary>
    public double ProcessedRmsDb { get; init; } = VoiceInputAudioMetrics.FloorDb;

    /// <summary>
    /// Gets the processed peak level in dBFS.
    /// </summary>
    public double PeakDb { get; init; } = VoiceInputAudioMetrics.FloorDb;

    /// <summary>
    /// Gets the detected speech duration in milliseconds.
    /// </summary>
    public int SpeechDurationMs { get; init; }

    /// <summary>
    /// Gets a value indicating whether limiting clipped the processed signal.
    /// </summary>
    public bool WasClipped { get; init; }

    /// <summary>
    /// Gets a value indicating whether voice activity was detected.
    /// </summary>
    public bool VadDetected { get; init; }

    /// <summary>
    /// Gets a value indicating whether preprocessing stages were enabled for this recording.
    /// </summary>
    public bool PreprocessingEnabled { get; init; }

    /// <summary>
    /// Gets the reason recognition should not proceed, if preprocessing failed quality checks.
    /// </summary>
    public string RecognitionFailedReason { get; init; } = string.Empty;
}

/// <summary>
/// Provides shared helpers for VoiceAI audio metric formatting.
/// </summary>
public static class VoiceInputAudioMetrics
{
    /// <summary>
    /// Gets the floor value used when a dBFS metric is effectively silent.
    /// </summary>
    public const double FloorDb = -90d;
}

/// <summary>
/// Provides stable failure reason values for VoiceAI audio quality checks.
/// </summary>
public static class VoiceInputAudioFailureReasons
{
    /// <summary>
    /// Indicates the recording contained no usable audio bytes.
    /// </summary>
    public const string EmptyInput = "EmptyInput";

    /// <summary>
    /// Indicates voice activity detection did not find enough clear speech.
    /// </summary>
    public const string NoSpeechDetected = "NoSpeechDetected";

    /// <summary>
    /// Indicates speech recognition produced no text.
    /// </summary>
    public const string EmptyRecognition = "EmptyRecognition";

    /// <summary>
    /// Indicates speech recognition confidence was below the configured threshold.
    /// </summary>
    public const string LowConfidence = "LowConfidence";

    /// <summary>
    /// Indicates the speech recognition engine failed.
    /// </summary>
    public const string RecognitionError = "RecognitionError";
}
