namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents one summary row in the multi-session comparison.
/// </summary>
public sealed record SessionComparisonMetricRowViewModel
{
    /// <summary>
    /// Gets the selected session label.
    /// </summary>
    public string SessionLabel { get; init; } = string.Empty;

    /// <summary>
    /// Gets the best valid lap time text.
    /// </summary>
    public string BestLapText { get; init; } = "-";

    /// <summary>
    /// Gets the average valid lap time text.
    /// </summary>
    public string AverageLapText { get; init; } = "-";

    /// <summary>
    /// Gets the valid lap count text.
    /// </summary>
    public string ValidLapCountText { get; init; } = "-";

    /// <summary>
    /// Gets the average fuel usage text.
    /// </summary>
    public string AverageFuelText { get; init; } = "-";

    /// <summary>
    /// Gets the average ERS usage text.
    /// </summary>
    public string AverageErsText { get; init; } = "-";
}
