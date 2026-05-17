using System.Globalization;
using F1Telemetry.Analytics.Corners;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Projects a V3 corner summary into WPF-friendly text.
/// </summary>
public sealed class CornerSummaryRowViewModel
{
    /// <summary>
    /// Gets the corner label.
    /// </summary>
    public string CornerText { get; init; } = "-";

    /// <summary>
    /// Gets the minimum speed text.
    /// </summary>
    public string MinimumSpeedText { get; init; } = "-";

    /// <summary>
    /// Gets the entry and exit speed summary.
    /// </summary>
    public string SpeedWindowText { get; init; } = "-";

    /// <summary>
    /// Gets the braking input summary.
    /// </summary>
    public string BrakeText { get; init; } = "-";

    /// <summary>
    /// Gets the time-loss summary.
    /// </summary>
    public string TimeLossText { get; init; } = "-";

    /// <summary>
    /// Gets the confidence summary.
    /// </summary>
    public string ConfidenceText { get; init; } = "-";

    /// <summary>
    /// Gets compact warning text.
    /// </summary>
    public string WarningText { get; init; } = "-";

    /// <summary>
    /// Creates a row from a corner summary.
    /// </summary>
    /// <param name="summary">The corner summary to project.</param>
    public static CornerSummaryRowViewModel FromSummary(CornerSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new CornerSummaryRowViewModel
        {
            CornerText = $"{summary.Segment.CornerNumber?.ToString(CultureInfo.InvariantCulture) ?? "-"} · {summary.Segment.Name}",
            MinimumSpeedText = FormatSpeed(summary.MinSpeedKph),
            SpeedWindowText = $"{FormatSpeed(summary.EntrySpeedKph)} -> {FormatSpeed(summary.ExitSpeedKph)}",
            BrakeText = summary.MaxBrake is null ? "-" : $"{summary.MaxBrake.Value:P0}",
            TimeLossText = summary.TimeLossToReferenceInMs is null ? "缺少参考圈" : $"{summary.TimeLossToReferenceInMs.Value:+#;-#;0} ms",
            ConfidenceText = summary.Confidence.ToString(),
            WarningText = summary.Warnings.Count == 0 ? "-" : string.Join(" / ", summary.Warnings)
        };
    }

    private static string FormatSpeed(double? speedKph)
    {
        return speedKph is null ? "-" : $"{speedKph.Value:0} km/h";
    }
}
