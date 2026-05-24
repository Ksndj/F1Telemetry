using F1Telemetry.Core.Models;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents a selectable voice AI push-to-talk mode.
/// </summary>
public sealed record VoiceAiTalkModeOptionViewModel
{
    /// <summary>
    /// Gets the underlying talk mode.
    /// </summary>
    public VoiceAiTalkMode Mode { get; init; }

    /// <summary>
    /// Gets the user-facing option label.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user-facing option description.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
