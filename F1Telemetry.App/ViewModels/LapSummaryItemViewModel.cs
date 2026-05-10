using F1Telemetry.Analytics.Laps;
using F1Telemetry.App.Formatting;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents a WPF-friendly row for the lap history table.
/// </summary>
public sealed class LapSummaryItemViewModel
{
    /// <summary>
    /// Gets the completed lap label.
    /// </summary>
    public string LapText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted lap time.
    /// </summary>
    public string LapTimeText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted sector summary.
    /// </summary>
    public string SectorsText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted average speed summary.
    /// </summary>
    public string AverageSpeedText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted fuel usage summary in litres.
    /// </summary>
    public string FuelUsedLitresText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted ERS usage summary.
    /// </summary>
    public string ErsUsedText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted tyre wear delta summary.
    /// </summary>
    public string TyreWearDeltaText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted validity summary.
    /// </summary>
    public string ValidityText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted tyre transition summary.
    /// </summary>
    public string TyreWindowText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted pit transition summary.
    /// </summary>
    public string PitWindowText { get; init; } = "-";

    /// <summary>
    /// Creates a UI row from the specified lap summary.
    /// </summary>
    /// <param name="summary">The summary to project.</param>
    public static LapSummaryItemViewModel FromSummary(LapSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new LapSummaryItemViewModel
        {
            LapText = $"Lap {summary.LapNumber}",
            LapTimeText = FormatLapTime(summary.LapTimeInMs),
            SectorsText = $"{FormatLapTime(summary.Sector1TimeInMs)} / {FormatLapTime(summary.Sector2TimeInMs)} / {FormatLapTime(summary.Sector3TimeInMs)}",
            AverageSpeedText = summary.AverageSpeedKph is null ? "-" : $"{summary.AverageSpeedKph:0} km/h",
            FuelUsedLitresText = summary.FuelUsedLitres is null ? "-" : $"{summary.FuelUsedLitres:0.00} L",
            ErsUsedText = summary.ErsUsed is null ? "-" : $"{summary.ErsUsed.Value / 1_000_000f:0.00} MJ",
            TyreWearDeltaText = summary.TyreWearDelta is null ? "-" : $"{summary.TyreWearDelta:0.0}%",
            ValidityText = summary.IsValid ? "有效" : "无效",
            TyreWindowText = $"{TyreCompoundFormatter.FormatRawCompoundText(summary.StartTyre)} -> {TyreCompoundFormatter.FormatRawCompoundText(summary.EndTyre)}",
            PitWindowText = $"{FormatPitState(summary.StartedInPit)} -> {FormatPitState(summary.EndedInPit)}"
        };
    }

    /// <summary>
    /// Creates a UI row from the specified stored lap row.
    /// </summary>
    /// <param name="lap">The stored lap row to project.</param>
    public static LapSummaryItemViewModel FromStoredLap(StoredLap lap)
    {
        ArgumentNullException.ThrowIfNull(lap);

        return new LapSummaryItemViewModel
        {
            LapText = $"Lap {lap.LapNumber}",
            LapTimeText = FormatLapTime(lap.LapTimeInMs),
            SectorsText = $"{FormatLapTime(lap.Sector1TimeInMs)} / {FormatLapTime(lap.Sector2TimeInMs)} / {FormatLapTime(lap.Sector3TimeInMs)}",
            AverageSpeedText = lap.AverageSpeedKph is null ? "-" : $"{lap.AverageSpeedKph:0} km/h",
            FuelUsedLitresText = lap.FuelUsedLitres is null ? "-" : $"{lap.FuelUsedLitres:0.00} L",
            ErsUsedText = lap.ErsUsed is null ? "-" : $"{lap.ErsUsed.Value / 1_000_000f:0.00} MJ",
            TyreWearDeltaText = "-",
            ValidityText = lap.IsValid ? "有效" : "无效",
            TyreWindowText = FormatStoredTyreWindow(lap.StartTyre, lap.EndTyre),
            PitWindowText = "-"
        };
    }

    private static string FormatPitState(bool inPit)
    {
        return inPit ? "Pit" : "Track";
    }

    private static string FormatLapTime(int? milliseconds)
    {
        if (milliseconds is null || milliseconds.Value < 0)
        {
            return "-";
        }

        return FormatLapTime((uint)milliseconds.Value);
    }

    private static string FormatLapTime(uint? milliseconds)
    {
        if (milliseconds is null)
        {
            return "-";
        }

        var time = TimeSpan.FromMilliseconds(milliseconds.Value);
        return time.TotalMinutes >= 1
            ? $"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds:000}"
            : $"{time.Seconds}.{time.Milliseconds:000}s";
    }

    private static string FormatStoredTyreWindow(string? startTyre, string? endTyre)
    {
        var startText = FormatStoredTyre(startTyre);
        var endText = FormatStoredTyre(endTyre);

        return startText == "-" && endText == "-"
            ? "-"
            : $"{startText} -> {endText}";
    }

    private static string FormatStoredTyre(string? tyre)
    {
        if (string.IsNullOrWhiteSpace(tyre) || tyre.Trim() == "-" || !tyre.Any(char.IsDigit))
        {
            return "-";
        }

        return TyreCompoundFormatter.FormatRawCompoundText(tyre);
    }
}
