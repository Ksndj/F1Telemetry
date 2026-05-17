namespace F1Telemetry.Analytics.Strategy;

/// <summary>
/// Summarizes a tyre stint using raw lap evidence and adjusted metrics that exclude known distortions.
/// </summary>
public sealed record StintSummary
{
    /// <summary>
    /// Gets the one-based stint number.
    /// </summary>
    public int StintNumber { get; init; }

    /// <summary>
    /// Gets the first lap included in the stint.
    /// </summary>
    public int StartLap { get; init; }

    /// <summary>
    /// Gets the last lap included in the stint.
    /// </summary>
    public int EndLap { get; init; }

    /// <summary>
    /// Gets the tyre label used to infer the stint.
    /// </summary>
    public string Tyre { get; init; } = "-";

    /// <summary>
    /// Gets the number of laps included in the stint.
    /// </summary>
    public int LapCount { get; init; }

    /// <summary>
    /// Gets the number of laps marked valid by the source data.
    /// </summary>
    public int ValidLapCount { get; init; }

    /// <summary>
    /// Gets all lap numbers in the stint.
    /// </summary>
    public IReadOnlyList<int> LapNumbers { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Gets lap numbers used by adjusted pace metrics.
    /// </summary>
    public IReadOnlyList<int> AdjustedLapNumbers { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Gets lap numbers influenced by safety car or virtual safety car events.
    /// </summary>
    public IReadOnlyList<int> SafetyCarLapNumbers { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Gets lap numbers influenced by red flag events.
    /// </summary>
    public IReadOnlyList<int> RedFlagLapNumbers { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Gets the raw average lap time in milliseconds, including all timed laps.
    /// </summary>
    public double? RawAverageLapTimeMs { get; init; }

    /// <summary>
    /// Gets the adjusted average lap time in milliseconds, excluding invalid and neutralized laps.
    /// </summary>
    public double? AdjustedAverageLapTimeMs { get; init; }

    /// <summary>
    /// Gets the raw best lap time in milliseconds, including all timed laps.
    /// </summary>
    public uint? RawBestLapTimeMs { get; init; }

    /// <summary>
    /// Gets the adjusted best lap time in milliseconds, excluding invalid and neutralized laps.
    /// </summary>
    public uint? AdjustedBestLapTimeMs { get; init; }

    /// <summary>
    /// Gets the average fuel used per lap in litres when available.
    /// </summary>
    public double? AverageFuelUsedLitres { get; init; }

    /// <summary>
    /// Gets the average ERS used per lap when available.
    /// </summary>
    public double? AverageErsUsed { get; init; }

    /// <summary>
    /// Gets a value indicating whether the stint contains safety car influenced laps.
    /// </summary>
    public bool HasSafetyCarInfluence { get; init; }

    /// <summary>
    /// Gets a value indicating whether the stint contains red flag influenced laps.
    /// </summary>
    public bool HasRedFlagInfluence { get; init; }

    /// <summary>
    /// Gets compact notes about inference or data quality.
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
