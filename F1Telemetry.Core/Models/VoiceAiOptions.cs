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
    /// Gets a value indicating whether steering-wheel voice queries are enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the selected steering-wheel input binding.
    /// </summary>
    public VoiceAiInputBinding InputBinding { get; init; } = new();

    /// <summary>
    /// Gets the configured talk mode for the bound steering-wheel button.
    /// </summary>
    public VoiceAiTalkMode TalkMode { get; init; } = VoiceAiTalkMode.HoldToTalk;

    /// <summary>
    /// Gets the selected microphone device identifier.
    /// </summary>
    public string MicrophoneDeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the selected microphone display name.
    /// </summary>
    public string MicrophoneDeviceName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the legacy WPF key name from the previous keyboard-mapping implementation.
    /// </summary>
    public string Hotkey { get; init; } = NoHotkey;
}

/// <summary>
/// Represents the source type used by a voice AI input binding.
/// </summary>
public enum VoiceAiInputBindingKind
{
    /// <summary>
    /// No input is currently bound.
    /// </summary>
    None = 0,

    /// <summary>
    /// A Windows Raw Input HID button is bound.
    /// </summary>
    RawInputHidButton,

    /// <summary>
    /// A F1 UDP BUTN bit is bound as a fallback input.
    /// </summary>
    F1UdpButton
}

/// <summary>
/// Represents how the bound voice AI button controls recording.
/// </summary>
public enum VoiceAiTalkMode
{
    /// <summary>
    /// Record while the bound button is held down and submit after release.
    /// </summary>
    HoldToTalk = 0,

    /// <summary>
    /// Press once to start recording and press again to submit.
    /// </summary>
    ToggleToTalk
}

/// <summary>
/// Represents a persisted voice AI input binding.
/// </summary>
public sealed record VoiceAiInputBinding
{
    /// <summary>
    /// Gets the generic display name used when Raw Input exposes only a HID path.
    /// </summary>
    public const string DefaultDeviceDisplayName = "方向盘/手柄设备";

    /// <summary>
    /// Gets the input binding kind.
    /// </summary>
    public VoiceAiInputBindingKind Kind { get; init; } = VoiceAiInputBindingKind.None;

    /// <summary>
    /// Gets the device identifier supplied by the input source.
    /// </summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user-facing device name.
    /// </summary>
    public string DeviceName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the one-based button index on the input device.
    /// </summary>
    public int ButtonIndex { get; init; }

    /// <summary>
    /// Gets the button mask when the input source exposes one.
    /// </summary>
    public ulong ButtonMask { get; init; }

    /// <summary>
    /// Gets the user-facing binding label.
    /// </summary>
    public string DisplayText { get; init; } = string.Empty;

    /// <summary>
    /// Formats a stable user-facing binding label.
    /// </summary>
    /// <param name="buttonIndex">The one-based button index.</param>
    public static string FormatDisplayText(int buttonIndex)
    {
        return $"{DefaultDeviceDisplayName} · 按钮 {Math.Max(0, buttonIndex)}";
    }

    /// <summary>
    /// Removes raw HID paths and unreadable text from the user-facing device name.
    /// </summary>
    /// <param name="deviceName">The raw device name or path.</param>
    public static string SanitizeDeviceName(string? deviceName)
    {
        var normalized = deviceName?.Trim() ?? string.Empty;
        return IsRawOrUnreadableText(normalized) ||
               string.Equals(normalized, "Raw Input 设备", StringComparison.Ordinal)
            ? DefaultDeviceDisplayName
            : normalized;
    }

    /// <summary>
    /// Returns whether a persisted display label should be regenerated.
    /// </summary>
    /// <param name="displayText">The stored display text.</param>
    public static bool ShouldRegenerateDisplayText(string? displayText)
    {
        var normalized = displayText?.Trim() ?? string.Empty;
        return IsRawOrUnreadableText(normalized) ||
               normalized.Contains("Raw Input", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRawOrUnreadableText(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
        {
            return true;
        }

        if (value.Contains(@"\\?\", StringComparison.Ordinal) ||
            value.Contains("#vid_", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("#pid_", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("hid#", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var replacementCount = value.Count(character => character == '\uFFFD' || character == '?');
        if (replacementCount >= 2)
        {
            return true;
        }

        return value.Any(char.IsControl);
    }
}
