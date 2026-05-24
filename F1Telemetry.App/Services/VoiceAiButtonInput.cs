using F1Telemetry.Core.Models;

namespace F1Telemetry.App.Services;

/// <summary>
/// Represents one Raw Input steering-wheel button edge.
/// </summary>
public sealed record VoiceAiButtonInput
{
    /// <summary>
    /// Gets the device identifier supplied by Raw Input.
    /// </summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user-facing device name.
    /// </summary>
    public string DeviceName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the one-based button index inferred from the HID report bit.
    /// </summary>
    public int ButtonIndex { get; init; }

    /// <summary>
    /// Gets the button bit mask when the button index is within the first 64 bits.
    /// </summary>
    public ulong ButtonMask { get; init; }

    /// <summary>
    /// Gets a value indicating whether this edge is a press.
    /// </summary>
    public bool IsPressed { get; init; }

    /// <summary>
    /// Gets the number of pressed bits found in the same report.
    /// </summary>
    public int PressedChangeCount { get; init; }

    /// <summary>
    /// Gets the number of total changed bits found in the same report.
    /// </summary>
    public int ChangedBitCount { get; init; }

    /// <summary>
    /// Gets the event timestamp.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Converts this input into a persisted binding.
    /// </summary>
    public VoiceAiInputBinding ToBinding()
    {
        return new VoiceAiInputBinding
        {
            Kind = VoiceAiInputBindingKind.RawInputHidButton,
            DeviceId = DeviceId,
            DeviceName = VoiceAiInputBinding.SanitizeDeviceName(DeviceName),
            ButtonIndex = ButtonIndex,
            ButtonMask = ButtonMask,
            DisplayText = VoiceAiInputBinding.FormatDisplayText(ButtonIndex)
        };
    }
}
