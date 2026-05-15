using F1Telemetry.Analytics.Laps;
using F1Telemetry.App.Formatting;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents a WPF-friendly row for the lap history table.
/// </summary>
public sealed class LapSummaryItemViewModel
{
    private const string DefaultSectorForeground = "#D7E4F3";
    private const string FastestSector1Foreground = "#50E3A4";
    private const string FastestSector2Foreground = "#58A6FF";
    private const string FastestSector3Foreground = "#F6C453";

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
    /// Gets the formatted sector 1 time.
    /// </summary>
    public string Sector1Text { get; init; } = "-";

    /// <summary>
    /// Gets the formatted sector 2 time.
    /// </summary>
    public string Sector2Text { get; init; } = "-";

    /// <summary>
    /// Gets the formatted sector 3 time.
    /// </summary>
    public string Sector3Text { get; init; } = "-";

    /// <summary>
    /// Gets a value indicating whether sector 1 is the fastest sector 1 in the selected history session.
    /// </summary>
    public bool IsFastestSector1 { get; init; }

    /// <summary>
    /// Gets a value indicating whether sector 2 is the fastest sector 2 in the selected history session.
    /// </summary>
    public bool IsFastestSector2 { get; init; }

    /// <summary>
    /// Gets a value indicating whether sector 3 is the fastest sector 3 in the selected history session.
    /// </summary>
    public bool IsFastestSector3 { get; init; }

    /// <summary>
    /// Gets the display brush value for sector 1.
    /// </summary>
    public string Sector1Foreground => IsFastestSector1 ? FastestSector1Foreground : DefaultSectorForeground;

    /// <summary>
    /// Gets the display brush value for sector 2.
    /// </summary>
    public string Sector2Foreground => IsFastestSector2 ? FastestSector2Foreground : DefaultSectorForeground;

    /// <summary>
    /// Gets the display brush value for sector 3.
    /// </summary>
    public string Sector3Foreground => IsFastestSector3 ? FastestSector3Foreground : DefaultSectorForeground;

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

        var sector1Text = FormatLapTime(summary.Sector1TimeInMs);
        var sector2Text = FormatLapTime(summary.Sector2TimeInMs);
        var sector3Text = FormatLapTime(
            ResolveSector3Time(
                summary.LapTimeInMs,
                summary.Sector1TimeInMs,
                summary.Sector2TimeInMs,
                summary.Sector3TimeInMs));

        return new LapSummaryItemViewModel
        {
            LapText = $"Lap {summary.LapNumber}",
            LapTimeText = FormatLapTime(summary.LapTimeInMs),
            SectorsText = FormatSectorsText(sector1Text, sector2Text, sector3Text),
            Sector1Text = sector1Text,
            Sector2Text = sector2Text,
            Sector3Text = sector3Text,
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
    /// <param name="isFastestSector1">Whether this row owns the fastest sector 1 time.</param>
    /// <param name="isFastestSector2">Whether this row owns the fastest sector 2 time.</param>
    /// <param name="isFastestSector3">Whether this row owns the fastest sector 3 time.</param>
    public static LapSummaryItemViewModel FromStoredLap(
        StoredLap lap,
        bool isFastestSector1 = false,
        bool isFastestSector2 = false,
        bool isFastestSector3 = false)
    {
        ArgumentNullException.ThrowIfNull(lap);

        var sector1Text = FormatLapTime(lap.Sector1TimeInMs);
        var sector2Text = FormatLapTime(lap.Sector2TimeInMs);
        var sector3Text = FormatLapTime(ResolveStoredSector3Time(lap));

        return new LapSummaryItemViewModel
        {
            LapText = $"Lap {lap.LapNumber}",
            LapTimeText = FormatLapTime(lap.LapTimeInMs),
            SectorsText = FormatSectorsText(sector1Text, sector2Text, sector3Text),
            Sector1Text = sector1Text,
            Sector2Text = sector2Text,
            Sector3Text = sector3Text,
            IsFastestSector1 = isFastestSector1,
            IsFastestSector2 = isFastestSector2,
            IsFastestSector3 = isFastestSector3,
            AverageSpeedText = lap.AverageSpeedKph is null ? "-" : $"{lap.AverageSpeedKph:0} km/h",
            FuelUsedLitresText = lap.FuelUsedLitres is null ? "-" : $"{lap.FuelUsedLitres:0.00} L",
            ErsUsedText = lap.ErsUsed is null ? "-" : $"{lap.ErsUsed.Value / 1_000_000f:0.00} MJ",
            TyreWearDeltaText = "-",
            ValidityText = lap.IsValid ? "有效" : "无效",
            TyreWindowText = FormatStoredTyreWindow(lap.StartTyre, lap.EndTyre),
            PitWindowText = "-"
        };
    }

    internal static int? ResolveStoredSector3Time(StoredLap lap)
    {
        ArgumentNullException.ThrowIfNull(lap);

        return ResolveSector3Time(
            lap.LapTimeInMs,
            lap.Sector1TimeInMs,
            lap.Sector2TimeInMs,
            lap.Sector3TimeInMs);
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

    private static string FormatSectorsText(string sector1Text, string sector2Text, string sector3Text)
    {
        return $"{sector1Text} / {sector2Text} / {sector3Text}";
    }

    private static int? ResolveSector3Time(int? lapTime, int? sector1, int? sector2, int? sector3)
    {
        if (sector3 is > 0)
        {
            return sector3;
        }

        if (lapTime is null || sector1 is null || sector2 is null)
        {
            return sector3;
        }

        var inferredSector3 = lapTime.Value - sector1.Value - sector2.Value;
        return inferredSector3 > 0 ? inferredSector3 : sector3;
    }

    private static uint? ResolveSector3Time(uint? lapTime, uint? sector1, uint? sector2, uint? sector3)
    {
        if (sector3 is > 0)
        {
            return sector3;
        }

        if (lapTime is null || sector1 is null || sector2 is null)
        {
            return sector3;
        }

        var sector12 = sector1.Value + sector2.Value;
        return lapTime.Value > sector12 ? lapTime.Value - sector12 : sector3;
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
