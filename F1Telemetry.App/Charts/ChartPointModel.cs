namespace F1Telemetry.App.Charts;

/// <summary>
/// Represents a single plotted chart point.
/// </summary>
public sealed record ChartPointModel
{
    /// <summary>
    /// Gets the X-axis value for the point.
    /// </summary>
    public double X { get; init; }

    /// <summary>
    /// Gets the Y-axis value for the point.
    /// </summary>
    public double Y { get; init; }
}
