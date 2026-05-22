namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents a compact current/reference metric comparison for the corner detail panel.
/// </summary>
public sealed class CornerMetricComparisonViewModel
{
    /// <summary>
    /// Gets the metric label.
    /// </summary>
    public string Title { get; init; } = "-";

    /// <summary>
    /// Gets the current-lap value.
    /// </summary>
    public string CurrentText { get; init; } = "-";

    /// <summary>
    /// Gets the reference-lap value.
    /// </summary>
    public string ReferenceText { get; init; } = "-";

    /// <summary>
    /// Gets the signed difference text.
    /// </summary>
    public string DifferenceText { get; init; } = "-";

    /// <summary>
    /// Gets the brush value used for the signed difference and bar.
    /// </summary>
    public string DifferenceBrush { get; init; } = "#93A9C8";

    /// <summary>
    /// Gets the compact difference bar width.
    /// </summary>
    public double DifferenceBarWidth { get; init; }

    /// <summary>
    /// Gets a value indicating whether reference data is missing.
    /// </summary>
    public bool IsReferenceMissing { get; init; }
}
