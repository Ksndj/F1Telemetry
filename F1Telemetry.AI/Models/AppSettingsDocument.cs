using F1Telemetry.Core.Models;
using F1Telemetry.TTS.Models;

namespace F1Telemetry.AI.Models;

/// <summary>
/// Represents the persisted app settings blocks stored in the local settings file.
/// </summary>
public sealed record AppSettingsDocument
{
    /// <summary>
    /// Gets the AI settings block.
    /// </summary>
    public AISettings Ai { get; init; } = new();

    /// <summary>
    /// Gets the TTS settings block.
    /// </summary>
    public TtsOptions Tts { get; init; } = new();

    /// <summary>
    /// Gets the raw UDP log settings block.
    /// </summary>
    public UdpRawLogOptions UdpRawLog { get; init; } = new();

    /// <summary>
    /// Gets the UDP listener settings block.
    /// </summary>
    public UdpSettings Udp { get; init; } = new();
}
