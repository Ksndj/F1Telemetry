using System.Globalization;
using F1Telemetry.Analytics.Corners;
using F1Telemetry.Analytics.Tracks;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Projects a V3 corner summary into WPF-friendly text.
/// </summary>
public sealed class CornerSummaryRowViewModel
{
    /// <summary>
    /// Gets the stable corner number when known.
    /// </summary>
    public int? CornerNumber { get; init; }

    /// <summary>
    /// Gets the corner label.
    /// </summary>
    public string CornerText { get; init; } = "-";

    /// <summary>
    /// Gets the compact corner display number.
    /// </summary>
    public string CornerNumberText { get; init; } = "-";

    /// <summary>
    /// Gets the corner name without the number prefix.
    /// </summary>
    public string CornerNameText { get; init; } = "-";

    /// <summary>
    /// Gets the entry speed in kilometres per hour.
    /// </summary>
    public double? EntrySpeedKph { get; init; }

    /// <summary>
    /// Gets the entry speed text.
    /// </summary>
    public string EntrySpeedText { get; init; } = "-";

    /// <summary>
    /// Gets the minimum speed in kilometres per hour.
    /// </summary>
    public double? MinimumSpeedKph { get; init; }

    /// <summary>
    /// Gets the exit speed in kilometres per hour.
    /// </summary>
    public double? ExitSpeedKph { get; init; }

    /// <summary>
    /// Gets the exit speed text.
    /// </summary>
    public string ExitSpeedText { get; init; } = "-";

    /// <summary>
    /// Gets the maximum brake input as a percentage.
    /// </summary>
    public double? MaxBrakePercent { get; init; }

    /// <summary>
    /// Gets the time loss to the reference in milliseconds.
    /// </summary>
    public int? TimeLossInMs { get; init; }

    /// <summary>
    /// Gets the positive time loss used for aggregate loss cards.
    /// </summary>
    public int PositiveTimeLossInMs => TimeLossInMs is > 0 ? TimeLossInMs.Value : 0;

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
    /// Gets compact warning text for table display.
    /// </summary>
    public string CompactWarningText { get; init; } = "-";

    /// <summary>
    /// Gets the warning text displayed in the row status pill.
    /// </summary>
    public string WarningDisplayText { get; init; } = "数据完整";

    /// <summary>
    /// Gets the accent brush value for the row leading bar.
    /// </summary>
    public string RowAccentBrush { get; init; } = "#38BDF8";

    /// <summary>
    /// Gets the brush value used for time-loss text.
    /// </summary>
    public string TimeLossBrush { get; init; } = "#9AB0C9";

    /// <summary>
    /// Gets the brush value used for confidence text.
    /// </summary>
    public string ConfidenceBrush { get; init; } = "#9AB0C9";

    /// <summary>
    /// Gets the brush value used for warning text.
    /// </summary>
    public string WarningBrush { get; init; } = "#34D399";

    /// <summary>
    /// Creates a row from a corner summary.
    /// </summary>
    /// <param name="summary">The corner summary to project.</param>
    public static CornerSummaryRowViewModel FromSummary(CornerSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var warningText = summary.Warnings.Count == 0 ? "-" : string.Join(" / ", summary.Warnings);
        var compactWarningText = summary.Warnings.Count == 0
            ? "OK"
            : string.Join(" / ", summary.Warnings.Select(FormatWarning));
        var hasWarnings = summary.Warnings.Count > 0;
        var cornerNumberText = summary.Segment.CornerNumber?.ToString(CultureInfo.InvariantCulture) ?? "-";

        return new CornerSummaryRowViewModel
        {
            CornerNumber = summary.Segment.CornerNumber,
            CornerText = $"{cornerNumberText} · {summary.Segment.Name}",
            CornerNumberText = cornerNumberText,
            CornerNameText = string.IsNullOrWhiteSpace(summary.Segment.Name) ? "-" : summary.Segment.Name,
            EntrySpeedKph = summary.EntrySpeedKph,
            MinimumSpeedKph = summary.MinSpeedKph,
            ExitSpeedKph = summary.ExitSpeedKph,
            EntrySpeedText = FormatSpeed(summary.EntrySpeedKph),
            ExitSpeedText = FormatSpeed(summary.ExitSpeedKph),
            MaxBrakePercent = summary.MaxBrake is null ? null : summary.MaxBrake.Value * 100,
            TimeLossInMs = summary.TimeLossToReferenceInMs,
            MinimumSpeedText = FormatSpeed(summary.MinSpeedKph),
            SpeedWindowText = $"{FormatSpeed(summary.EntrySpeedKph)} -> {FormatSpeed(summary.ExitSpeedKph)}",
            BrakeText = summary.MaxBrake is null ? "-" : $"{summary.MaxBrake.Value:P0}",
            TimeLossText = summary.TimeLossToReferenceInMs is null ? "缺少参考圈" : $"{summary.TimeLossToReferenceInMs.Value:+#;-#;0} ms",
            ConfidenceText = summary.Confidence.ToString(),
            WarningText = warningText,
            CompactWarningText = compactWarningText,
            WarningDisplayText = hasWarnings ? compactWarningText : "数据完整",
            RowAccentBrush = ResolveRowAccentBrush(summary.TimeLossToReferenceInMs, hasWarnings),
            TimeLossBrush = ResolveTimeLossBrush(summary.TimeLossToReferenceInMs),
            ConfidenceBrush = ResolveConfidenceBrush(summary.Confidence),
            WarningBrush = hasWarnings ? "#FBBF24" : "#34D399"
        };
    }

    private static string FormatWarning(DataQualityWarning warning)
    {
        return warning switch
        {
            DataQualityWarning.MissingReferenceLap => "缺少参考圈",
            DataQualityWarning.EstimatedTrackMap => "估算赛道图",
            DataQualityWarning.LowSampleDensity => "采样偏少",
            DataQualityWarning.MissingSamples => "缺少采样",
            DataQualityWarning.MissingLapDistance => "缺少距离",
            DataQualityWarning.MissingTimingSamples => "缺少计时",
            DataQualityWarning.MissingSpeedSamples => "缺少速度",
            DataQualityWarning.MissingThrottleSamples => "缺少油门",
            DataQualityWarning.MissingBrakeSamples => "缺少刹车",
            DataQualityWarning.MissingSteeringSamples => "缺少转向",
            DataQualityWarning.UnsupportedTrack => "暂不支持",
            _ => warning.ToString()
        };
    }

    private static string FormatSpeed(double? speedKph)
    {
        return speedKph is null ? "-" : $"{speedKph.Value:0} km/h";
    }

    private static string ResolveRowAccentBrush(int? timeLossInMs, bool hasWarnings)
    {
        if (timeLossInMs is > 0)
        {
            return "#F59E0B";
        }

        if (hasWarnings)
        {
            return "#38BDF8";
        }

        return "#34D399";
    }

    private static string ResolveTimeLossBrush(int? timeLossInMs)
    {
        return timeLossInMs switch
        {
            null => "#FBBF24",
            > 100 => "#F87171",
            > 0 => "#FBBF24",
            _ => "#34D399"
        };
    }

    private static string ResolveConfidenceBrush(ConfidenceLevel confidence)
    {
        return confidence switch
        {
            ConfidenceLevel.High => "#34D399",
            ConfidenceLevel.Medium => "#FBBF24",
            ConfidenceLevel.Low => "#60A5FA",
            _ => "#94A3B8"
        };
    }
}
