namespace F1Telemetry.Core.Models;

/// <summary>
/// Describes a short microphone input test result.
/// </summary>
public sealed record MicrophoneTestResult
{
    /// <summary>
    /// Gets a value indicating whether meaningful input was detected.
    /// </summary>
    public bool HasInput { get; init; }

    /// <summary>
    /// Gets the peak normalized level between 0 and 1.
    /// </summary>
    public double PeakLevel { get; init; }

    /// <summary>
    /// Gets the average normalized level between 0 and 1.
    /// </summary>
    public double AverageLevel { get; init; }

    /// <summary>
    /// Gets the test duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the user-facing status text.
    /// </summary>
    public string StatusText { get; init; } = string.Empty;
}
