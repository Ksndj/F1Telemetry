using F1Telemetry.Analytics.Tracks;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Describes the effective reference lap used for corner-level comparison.
/// </summary>
public sealed record CornerReferenceInfo
{
    /// <summary>
    /// Gets the reference lap number, when one is available.
    /// </summary>
    public int? LapNumber { get; init; }

    /// <summary>
    /// Gets the source that selected this reference lap.
    /// </summary>
    public ReferenceLapSource Source { get; init; } = ReferenceLapSource.None;

    /// <summary>
    /// Gets the reference lap time in milliseconds.
    /// </summary>
    public int? LapTimeMs { get; init; }

    /// <summary>
    /// Gets the reference tyre compound label.
    /// </summary>
    public string Compound { get; init; } = "-";

    /// <summary>
    /// Gets the reference tyre age when known.
    /// </summary>
    public byte? TyreAge { get; init; }

    /// <summary>
    /// Gets the comparison confidence level.
    /// </summary>
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Unknown;

    /// <summary>
    /// Gets a user-facing warning or note for the reference lap.
    /// </summary>
    public string WarningText { get; init; } = "缺少可用参考圈";

    /// <summary>
    /// Gets a value indicating whether a usable reference lap is available.
    /// </summary>
    public bool HasReference => Source != ReferenceLapSource.None && LapNumber is not null;

    /// <summary>
    /// Gets the user-facing reference lap label.
    /// </summary>
    public string LapText => HasReference ? $"Lap {LapNumber}" : "缺少可用参考圈";

    /// <summary>
    /// Gets the user-facing source label.
    /// </summary>
    public string SourceText => Source switch
    {
        ReferenceLapSource.Manual => "手动选择",
        ReferenceLapSource.SameStintBest => "同 Stint 最快圈",
        ReferenceLapSource.SameCompoundBest => "同胎最快圈",
        ReferenceLapSource.SessionBest => "本场最快圈",
        ReferenceLapSource.PreviousLap => "上一圈",
        _ => "无"
    };

    /// <summary>
    /// Gets the user-facing data-quality label.
    /// </summary>
    public string QualityText => Confidence switch
    {
        ConfidenceLevel.High => "High",
        ConfidenceLevel.Medium => "Medium",
        ConfidenceLevel.Low => "Low",
        _ => "Low"
    };

    /// <summary>
    /// Creates an empty reference info value.
    /// </summary>
    public static CornerReferenceInfo None(string warningText = "缺少可用参考圈")
    {
        return new CornerReferenceInfo
        {
            Source = ReferenceLapSource.None,
            Confidence = ConfidenceLevel.Unknown,
            WarningText = warningText
        };
    }
}
