using System.Text.Json.Serialization;

namespace F1Telemetry.TTS.Models;

/// <summary>
/// Represents persisted TTS playback settings.
/// </summary>
public sealed record TtsOptions
{
    /// <summary>
    /// Gets a value indicating whether TTS playback is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool TtsEnabled { get; init; }

    /// <summary>
    /// Gets the configured Windows voice name.
    /// </summary>
    public string VoiceName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the playback volume in the range 0-100.
    /// </summary>
    public int Volume { get; init; } = 100;

    /// <summary>
    /// Gets the playback rate in the range -10 to 10.
    /// </summary>
    public int Rate { get; init; }

    /// <summary>
    /// Gets the default cooldown window applied to event speech in seconds.
    /// </summary>
    public int CooldownSeconds { get; init; } = 8;
}
