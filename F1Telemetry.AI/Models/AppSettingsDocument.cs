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
    /// Gets the manually configured race-weekend tyre plan block.
    /// </summary>
    public RaceWeekendTyrePlan RaceWeekendTyrePlan { get; init; } = new();

    /// <summary>
    /// Gets the raw UDP log settings block.
    /// </summary>
    public UdpRawLogOptions UdpRawLog { get; init; } = new();

    /// <summary>
    /// Gets the voice-to-AI query settings block.
    /// </summary>
    public VoiceAiOptions VoiceAi { get; init; } = new();

    /// <summary>
    /// Gets the first-version race assistant voice/text question settings block.
    /// </summary>
    public VoiceAssistantSettings VoiceAssistantSettings { get; init; } = new();

    /// <summary>
    /// Gets the UDP listener settings block.
    /// </summary>
    public UdpSettings Udp { get; init; } = new();
}
