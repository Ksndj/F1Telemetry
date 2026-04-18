using System.Windows.Media;

namespace F1Telemetry.App.Charts;

/// <summary>
/// Represents a named chart series ready for UI rendering.
/// </summary>
public sealed record ChartSeriesModel
{
    /// <summary>
    /// Gets the display name shown in the chart legend.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the stroke brush used for the rendered series.
    /// </summary>
    public Brush StrokeBrush { get; init; } = Brushes.White;

    /// <summary>
    /// Gets the ordered chart points for the series.
    /// </summary>
    public IReadOnlyList<ChartPointModel> Points { get; init; } = Array.Empty<ChartPointModel>();
}
