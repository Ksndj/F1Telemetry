namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents configurable behavior for the race-assistant voice and text question feature.
/// </summary>
public sealed record VoiceAssistantSettings
{
    /// <summary>
    /// Gets a value indicating whether the race assistant is enabled.
    /// </summary>
    public bool EnableVoiceAssistant { get; init; }

    /// <summary>
    /// Gets the optional keyboard push-to-talk key name.
    /// </summary>
    public string PushToTalkKey { get; init; } = VoiceAiOptions.NoHotkey;

    /// <summary>
    /// Gets the optional bound button display text.
    /// </summary>
    public string PushToTalkButton { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether race-assistant answers may enter the TTS queue.
    /// </summary>
    public bool EnableTtsAnswer { get; init; } = true;

    /// <summary>
    /// Gets the maximum displayed answer length.
    /// </summary>
    public int MaxAnswerLength { get; init; } = 240;

    /// <summary>
    /// Gets the repeated-question cooldown in seconds.
    /// </summary>
    public int RepeatQuestionCooldownSeconds { get; init; } = 12;

    /// <summary>
    /// Returns a normalized settings snapshot constrained to supported ranges.
    /// </summary>
    public VoiceAssistantSettings Normalize()
    {
        return this with
        {
            PushToTalkKey = string.IsNullOrWhiteSpace(PushToTalkKey) ? VoiceAiOptions.NoHotkey : PushToTalkKey.Trim(),
            PushToTalkButton = PushToTalkButton?.Trim() ?? string.Empty,
            MaxAnswerLength = Math.Clamp(MaxAnswerLength, 35, 2_000),
            RepeatQuestionCooldownSeconds = Math.Clamp(RepeatQuestionCooldownSeconds, 5, 600)
        };
    }
}
