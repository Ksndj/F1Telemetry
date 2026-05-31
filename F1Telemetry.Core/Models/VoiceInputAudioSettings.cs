namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents microphone preprocessing and recognition quality settings for VoiceAI input.
/// </summary>
public sealed record VoiceInputAudioSettings
{
    /// <summary>
    /// Gets a value indicating whether any microphone preprocessing is enabled.
    /// </summary>
    public bool EnableNoiseReduction { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether low-frequency wind and vibration noise should be filtered.
    /// </summary>
    public bool EnableHighPassFilter { get; init; } = true;

    /// <summary>
    /// Gets the high-pass filter cutoff frequency in hertz.
    /// </summary>
    public double HighPassCutoffHz { get; init; } = 120d;

    /// <summary>
    /// Gets a value indicating whether low-level steady noise should be gated to silence.
    /// </summary>
    public bool EnableNoiseGate { get; init; } = true;

    /// <summary>
    /// Gets the noise gate threshold in dBFS.
    /// </summary>
    public double NoiseGateThresholdDb { get; init; } = -40d;

    /// <summary>
    /// Gets a value indicating whether leading and trailing silence should be trimmed.
    /// </summary>
    public bool EnableVad { get; init; } = true;

    /// <summary>
    /// Gets the amount of audio to keep before detected speech starts.
    /// </summary>
    public int PreSpeechPaddingMs { get; init; } = 150;

    /// <summary>
    /// Gets the amount of audio to keep after detected speech ends.
    /// </summary>
    public int PostSpeechPaddingMs { get; init; } = 250;

    /// <summary>
    /// Gets a value indicating whether voice level should be normalized before recognition.
    /// </summary>
    public bool EnableAutoGain { get; init; } = true;

    /// <summary>
    /// Gets the maximum push-to-talk recording duration in seconds.
    /// </summary>
    public int MaxRecordingSeconds { get; init; } = 8;

    /// <summary>
    /// Gets the minimum detected speech duration required before recognition is attempted.
    /// </summary>
    public int MinSpeechDurationMs { get; init; } = 300;

    /// <summary>
    /// Gets the minimum speech recognition confidence required before VoiceAI is invoked.
    /// </summary>
    public double MinRecognitionConfidence { get; init; } = 0.35d;

    /// <summary>
    /// Returns settings clamped to safe runtime limits.
    /// </summary>
    public VoiceInputAudioSettings Normalize()
    {
        return this with
        {
            HighPassCutoffHz = double.IsFinite(HighPassCutoffHz)
                ? Math.Clamp(HighPassCutoffHz, 20d, 800d)
                : 120d,
            NoiseGateThresholdDb = double.IsFinite(NoiseGateThresholdDb)
                ? Math.Clamp(NoiseGateThresholdDb, -80d, -10d)
                : -40d,
            PreSpeechPaddingMs = Math.Clamp(PreSpeechPaddingMs, 0, 1000),
            PostSpeechPaddingMs = Math.Clamp(PostSpeechPaddingMs, 0, 1500),
            MaxRecordingSeconds = Math.Clamp(MaxRecordingSeconds, 1, 30),
            MinSpeechDurationMs = Math.Clamp(MinSpeechDurationMs, 50, 3000),
            MinRecognitionConfidence = double.IsFinite(MinRecognitionConfidence)
                ? Math.Clamp(MinRecognitionConfidence, 0d, 1d)
                : 0.35d
        };
    }
}
