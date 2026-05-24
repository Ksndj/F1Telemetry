namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents the persisted voice-to-AI query settings.
/// </summary>
public sealed record VoiceAiOptions
{
    /// <summary>
    /// Gets the sentinel value used when no hotkey is bound.
    /// </summary>
    public const string NoHotkey = "None";

    /// <summary>
    /// Gets a value indicating whether steering-wheel hotkey voice queries are enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the WPF key name that the steering-wheel button is mapped to.
    /// </summary>
    public string Hotkey { get; init; } = NoHotkey;
}
