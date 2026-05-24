namespace F1Telemetry.Core.Models;

/// <summary>
/// Contains a completed microphone recording for speech recognition.
/// </summary>
public sealed record VoiceRecordingResult
{
    /// <summary>
    /// Gets the recorded wave file bytes.
    /// </summary>
    public byte[] WaveBytes { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// Gets the recording duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the peak normalized level between 0 and 1.
    /// </summary>
    public double PeakLevel { get; init; }

    /// <summary>
    /// Gets the average normalized level between 0 and 1.
    /// </summary>
    public double AverageLevel { get; init; }

    /// <summary>
    /// Gets a value indicating whether meaningful input was detected.
    /// </summary>
    public bool HasInput { get; init; }
}
