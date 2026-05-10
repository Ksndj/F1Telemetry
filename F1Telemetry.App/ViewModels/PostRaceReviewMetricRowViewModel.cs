namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents one summary metric row in the post-race review.
/// </summary>
public sealed record PostRaceReviewMetricRowViewModel
{
    /// <summary>
    /// Gets the metric label.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Gets the metric value.
    /// </summary>
    public string Value { get; init; } = "-";

    /// <summary>
    /// Gets the metric value text used by WPF bindings.
    /// </summary>
    public string ValueText => Value;

    /// <summary>
    /// Gets optional supporting text for the metric.
    /// </summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>
    /// Gets the supporting detail text used by WPF bindings.
    /// </summary>
    public string DetailText => Detail;
}
