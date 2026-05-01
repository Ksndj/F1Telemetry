namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents a selectable post-race AI completion policy in the dashboard.
/// </summary>
public sealed record PostRaceAiCompletionModeOptionViewModel
{
    /// <summary>
    /// Gets the underlying completion mode.
    /// </summary>
    public PostRaceAiCompletionMode Mode { get; init; }

    /// <summary>
    /// Gets the user-facing option label.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user-facing option description.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
