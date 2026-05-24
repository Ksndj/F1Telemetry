namespace F1Telemetry.Core.Models;

/// <summary>
/// Describes a selectable system microphone device.
/// </summary>
public sealed record MicrophoneDeviceInfo
{
    /// <summary>
    /// Gets the stable device identifier used by the audio backend.
    /// </summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user-facing microphone name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether this is the default input device.
    /// </summary>
    public bool IsDefault { get; init; }
}
