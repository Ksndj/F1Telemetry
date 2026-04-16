namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents a placeholder panel in the dashboard chart area.
/// </summary>
public sealed class DashboardPlaceholderViewModel
{
    /// <summary>
    /// Gets the panel title.
    /// </summary>
    public string Title { get; init; } = "-";

    /// <summary>
    /// Gets the panel description.
    /// </summary>
    public string Description { get; init; } = "-";
}
