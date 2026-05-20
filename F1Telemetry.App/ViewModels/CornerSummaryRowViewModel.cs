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
    /// Gets the time-difference summary.
    /// </summary>
    public string TimeLossText { get; init; } = "-";

    /// <summary>
    /// Gets the tooltip explaining time-difference semantics.
    /// </summary>
    public string TimeDifferenceTooltipText { get; init; } = "正数为比参考圈慢，负数为比参考圈快。";

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
    /// Gets detailed warning text for tooltips.
    /// </summary>
    public string WarningTooltipText { get; init; } = "数据完整";

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
    /// Gets the entry-speed current/reference comparison text.
    /// </summary>
    public string EntrySpeedComparisonText { get; init; } = "缺少参考数据";

    /// <summary>
    /// Gets the minimum-speed current/reference comparison text.
    /// </summary>
    public string MinimumSpeedComparisonText { get; init; } = "缺少参考数据";

    /// <summary>
    /// Gets the exit-speed current/reference comparison text.
    /// </summary>
    public string ExitSpeedComparisonText { get; init; } = "缺少参考数据";

    /// <summary>
    /// Gets the maximum-brake current/reference comparison text.
    /// </summary>
    public string BrakeComparisonText { get; init; } = "缺少参考数据";

    /// <summary>
    /// Gets the current-lap speed chart path for the selected corner.
    /// </summary>
    public string? SpeedCurrentPathData { get; init; }

    /// <summary>
    /// Gets the reference-lap speed chart path for the selected corner.
    /// </summary>
    public string? SpeedReferencePathData { get; init; }

    /// <summary>
    /// Gets the speed chart status text.
    /// </summary>
    public string SpeedChartStatusText { get; init; } = "采样不足，暂无法绘制";

    /// <summary>
    /// Gets the current-lap brake chart path for the selected corner.
    /// </summary>
    public string? BrakeCurrentPathData { get; init; }

    /// <summary>
    /// Gets the reference-lap brake chart path for the selected corner.
    /// </summary>
    public string? BrakeReferencePathData { get; init; }

    /// <summary>
    /// Gets the brake chart status text.
    /// </summary>
    public string BrakeChartStatusText { get; init; } = "采样不足，暂无法绘制";

    /// <summary>
    /// Gets the current-lap throttle chart path for the selected corner.
    /// </summary>
    public string? ThrottleCurrentPathData { get; init; }

    /// <summary>
    /// Gets the reference-lap throttle chart path for the selected corner.
    /// </summary>
    public string? ThrottleReferencePathData { get; init; }

    /// <summary>
    /// Gets the throttle chart status text.
    /// </summary>
    public string ThrottleChartStatusText { get; init; } = "采样不足，暂无法绘制";

    /// <summary>
    /// Gets the lightweight corner-position indicator text.
    /// </summary>
    public string PositionIndicatorText { get; init; } = "暂无赛道位置数据";

    /// <summary>
    /// Gets the corner-position data status text.
    /// </summary>
    public string PositionStatusText { get; init; } = "暂无赛道位置数据";

    /// <summary>
    /// Gets the normalized Motion track-map outline path.
    /// </summary>
    public string? TrackMapPathData { get; init; }

    /// <summary>
    /// Gets the selected-corner highlighted track-map path.
    /// </summary>
    public string? TrackMapHighlightPathData { get; init; }

    /// <summary>
    /// Gets the track-map status text.
    /// </summary>
    public string TrackMapStatusText { get; init; } = "等待 Motion 数据";

    /// <summary>
    /// Gets the track-map source text.
    /// </summary>
    public string TrackMapSourceText { get; init; } = "来源：Motion 轨迹";

    /// <summary>
    /// Gets the track-map quality text.
    /// </summary>
    public string TrackMapQualityText { get; init; } = "质量：Low";

    /// <summary>
    /// Gets the track-map warning text.
    /// </summary>
    public string TrackMapWarningText { get; init; } = "等待 Motion 数据";

    /// <summary>
    /// Gets the track-map marker X coordinate in canvas pixels.
    /// </summary>
    public double TrackMapMarkerX { get; init; }

    /// <summary>
    /// Gets the track-map marker Y coordinate in canvas pixels.
    /// </summary>
    public double TrackMapMarkerY { get; init; }

    /// <summary>
    /// Gets the track-map marker size in canvas pixels.
    /// </summary>
    public double TrackMapMarkerSize { get; init; }

    /// <summary>
    /// Gets the marker left coordinate for WPF Canvas positioning.
    /// </summary>
    public double TrackMapMarkerLeft => TrackMapMarkerX - TrackMapMarkerSize / 2d;

    /// <summary>
    /// Gets the marker top coordinate for WPF Canvas positioning.
    /// </summary>
    public double TrackMapMarkerTop => TrackMapMarkerY - TrackMapMarkerSize / 2d;

    /// <summary>
    /// Gets the selected-corner marker label.
    /// </summary>
    public string TrackMapCornerLabelText { get; init; } = "-";

    /// <summary>
    /// Creates a row from a corner summary.
    /// </summary>
    /// <param name="summary">The corner summary to project.</param>
    /// <param name="referenceEntrySpeedKph">The reference entry speed, when available.</param>
    /// <param name="referenceMinimumSpeedKph">The reference minimum speed, when available.</param>
    /// <param name="referenceExitSpeedKph">The reference exit speed, when available.</param>
    /// <param name="referenceMaxBrake">The reference maximum brake input, when available.</param>
    /// <param name="speedCurrentPathData">Current-lap speed chart path data.</param>
    /// <param name="speedReferencePathData">Reference-lap speed chart path data.</param>
    /// <param name="speedChartStatusText">Speed chart status text.</param>
    /// <param name="brakeCurrentPathData">Current-lap brake chart path data.</param>
    /// <param name="brakeReferencePathData">Reference-lap brake chart path data.</param>
    /// <param name="brakeChartStatusText">Brake chart status text.</param>
    /// <param name="throttleCurrentPathData">Current-lap throttle chart path data.</param>
    /// <param name="throttleReferencePathData">Reference-lap throttle chart path data.</param>
    /// <param name="throttleChartStatusText">Throttle chart status text.</param>
    /// <param name="positionIndicatorText">Lightweight corner-position indicator text.</param>
    /// <param name="positionStatusText">Corner-position data status text.</param>
    /// <param name="trackMapPathData">Motion track-map outline path data.</param>
    /// <param name="trackMapHighlightPathData">Selected-corner track-map highlight path data.</param>
    /// <param name="trackMapStatusText">Track-map status text.</param>
    /// <param name="trackMapSourceText">Track-map source text.</param>
    /// <param name="trackMapQualityText">Track-map quality text.</param>
    /// <param name="trackMapWarningText">Track-map warning text.</param>
    /// <param name="trackMapMarkerX">Track-map marker X coordinate.</param>
    /// <param name="trackMapMarkerY">Track-map marker Y coordinate.</param>
    /// <param name="trackMapMarkerSize">Track-map marker size.</param>
    /// <param name="trackMapCornerLabelText">Track-map marker label text.</param>
    public static CornerSummaryRowViewModel FromSummary(
        CornerSummary summary,
        double? referenceEntrySpeedKph = null,
        double? referenceMinimumSpeedKph = null,
        double? referenceExitSpeedKph = null,
        double? referenceMaxBrake = null,
        string? speedCurrentPathData = null,
        string? speedReferencePathData = null,
        string speedChartStatusText = "采样不足，暂无法绘制",
        string? brakeCurrentPathData = null,
        string? brakeReferencePathData = null,
        string brakeChartStatusText = "采样不足，暂无法绘制",
        string? throttleCurrentPathData = null,
        string? throttleReferencePathData = null,
        string throttleChartStatusText = "采样不足，暂无法绘制",
        string positionIndicatorText = "暂无赛道位置数据",
        string positionStatusText = "暂无赛道位置数据",
        string? trackMapPathData = null,
        string? trackMapHighlightPathData = null,
        string trackMapStatusText = "等待 Motion 数据",
        string trackMapSourceText = "来源：Motion 轨迹",
        string trackMapQualityText = "质量：Low",
        string trackMapWarningText = "等待 Motion 数据",
        double trackMapMarkerX = 0d,
        double trackMapMarkerY = 0d,
        double trackMapMarkerSize = 0d,
        string trackMapCornerLabelText = "-")
    {
        ArgumentNullException.ThrowIfNull(summary);
        var warningText = summary.Warnings.Count == 0 ? "-" : string.Join(" / ", summary.Warnings);
        var compactWarningText = FormatCompactWarningText(summary.Warnings, summary.Confidence);
        var hasWarnings = summary.Warnings.Count > 0 || summary.Confidence is ConfidenceLevel.Low or ConfidenceLevel.Unknown;
        var cornerNumberText = summary.Segment.CornerNumber?.ToString(CultureInfo.InvariantCulture) ?? "-";
        double? referenceMaxBrakePercent = referenceMaxBrake is null ? null : referenceMaxBrake.Value * 100d;

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
            TimeLossText = summary.TimeLossToReferenceInMs is null ? "缺少参考" : $"{summary.TimeLossToReferenceInMs.Value:+#;-#;0} ms",
            ConfidenceText = summary.Confidence.ToString(),
            WarningText = warningText,
            CompactWarningText = compactWarningText,
            WarningTooltipText = FormatWarningTooltipText(summary.Warnings, summary.Confidence),
            WarningDisplayText = hasWarnings ? compactWarningText : "数据完整",
            RowAccentBrush = ResolveRowAccentBrush(summary.TimeLossToReferenceInMs, hasWarnings),
            TimeLossBrush = ResolveTimeLossBrush(summary.TimeLossToReferenceInMs),
            ConfidenceBrush = ResolveConfidenceBrush(summary.Confidence),
            WarningBrush = hasWarnings ? "#FBBF24" : "#34D399",
            EntrySpeedComparisonText = FormatSpeedComparison(summary.EntrySpeedKph, referenceEntrySpeedKph),
            MinimumSpeedComparisonText = FormatSpeedComparison(summary.MinSpeedKph, referenceMinimumSpeedKph),
            ExitSpeedComparisonText = FormatSpeedComparison(summary.ExitSpeedKph, referenceExitSpeedKph),
            BrakeComparisonText = FormatPercentComparison(summary.MaxBrake is null ? null : summary.MaxBrake.Value * 100d, referenceMaxBrakePercent),
            SpeedCurrentPathData = speedCurrentPathData,
            SpeedReferencePathData = speedReferencePathData,
            SpeedChartStatusText = speedChartStatusText,
            BrakeCurrentPathData = brakeCurrentPathData,
            BrakeReferencePathData = brakeReferencePathData,
            BrakeChartStatusText = brakeChartStatusText,
            ThrottleCurrentPathData = throttleCurrentPathData,
            ThrottleReferencePathData = throttleReferencePathData,
            ThrottleChartStatusText = throttleChartStatusText,
            PositionIndicatorText = positionIndicatorText,
            PositionStatusText = positionStatusText,
            TrackMapPathData = trackMapPathData,
            TrackMapHighlightPathData = trackMapHighlightPathData,
            TrackMapStatusText = trackMapStatusText,
            TrackMapSourceText = trackMapSourceText,
            TrackMapQualityText = trackMapQualityText,
            TrackMapWarningText = trackMapWarningText,
            TrackMapMarkerX = trackMapMarkerX,
            TrackMapMarkerY = trackMapMarkerY,
            TrackMapMarkerSize = trackMapMarkerSize,
            TrackMapCornerLabelText = trackMapCornerLabelText
        };
    }

    private static string FormatCompactWarningText(IReadOnlyList<DataQualityWarning> warnings, ConfidenceLevel confidence)
    {
        var labels = warnings
            .Select(FormatWarningShort)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (confidence is ConfidenceLevel.Low or ConfidenceLevel.Unknown && !labels.Contains("低置信", StringComparer.Ordinal))
        {
            labels.Add("低置信");
        }

        return labels.Count == 0 ? "OK" : string.Join(" / ", labels);
    }

    private static string FormatWarningTooltipText(IReadOnlyList<DataQualityWarning> warnings, ConfidenceLevel confidence)
    {
        var details = warnings
            .Select(FormatWarningDetailed)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (confidence is ConfidenceLevel.Low or ConfidenceLevel.Unknown && !details.Contains("低置信度", StringComparer.Ordinal))
        {
            details.Add("低置信度");
        }

        return details.Count == 0 ? "数据完整" : string.Join(" / ", details);
    }

    private static string FormatWarningShort(DataQualityWarning warning)
    {
        return warning switch
        {
            DataQualityWarning.MissingReferenceLap => "缺参考",
            DataQualityWarning.EstimatedTrackMap => "估算",
            DataQualityWarning.LowSampleDensity => "采样少",
            DataQualityWarning.MissingSamples => "采样少",
            DataQualityWarning.MissingLapDistance => "采样少",
            DataQualityWarning.MissingTimingSamples => "采样少",
            DataQualityWarning.MissingSpeedSamples => "采样少",
            DataQualityWarning.MissingThrottleSamples => "采样少",
            DataQualityWarning.MissingBrakeSamples => "采样少",
            DataQualityWarning.MissingSteeringSamples => "采样少",
            DataQualityWarning.UnsupportedTrack => "低置信",
            _ => "低置信"
        };
    }

    private static string FormatWarningDetailed(DataQualityWarning warning)
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

    private static string FormatSpeedComparison(double? current, double? reference)
    {
        if (reference is null)
        {
            return $"当前 {FormatSpeed(current)} · 缺少参考数据";
        }

        var difference = current is null ? (double?)null : current.Value - reference.Value;
        return $"当前 {FormatSpeed(current)} · 参考 {FormatSpeed(reference)} · 差值 {FormatSignedNumber(difference, " km/h")}";
    }

    private static string FormatPercentComparison(double? currentPercent, double? referencePercent)
    {
        if (referencePercent is null)
        {
            return $"当前 {FormatPercent(currentPercent)} · 缺少参考数据";
        }

        var difference = currentPercent is null ? (double?)null : currentPercent.Value - referencePercent.Value;
        return $"当前 {FormatPercent(currentPercent)} · 参考 {FormatPercent(referencePercent)} · 差值 {FormatSignedNumber(difference, "pp")}";
    }

    private static string FormatPercent(double? value)
    {
        return value is null ? "-" : $"{value.Value:0}%";
    }

    private static string FormatSignedNumber(double? value, string suffix)
    {
        return value is null
            ? "-"
            : $"{value.Value:+0;-0;0}{suffix}";
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
            > 0 => "#F87171",
            < 0 => "#34D399",
            _ => "#AFC2DA"
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
