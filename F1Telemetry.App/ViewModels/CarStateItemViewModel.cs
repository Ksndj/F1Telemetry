using System.Globalization;
using F1Telemetry.Analytics.Events;
using F1Telemetry.App.Formatting;
using F1Telemetry.Core.Models;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents a WPF-friendly opponent row derived from the central session store.
/// </summary>
public sealed class CarStateItemViewModel
{
    /// <summary>
    /// Gets the car index in the current session.
    /// </summary>
    public int CarIndex { get; init; }

    /// <summary>
    /// Gets the display name shown in the UI.
    /// </summary>
    public string DisplayName { get; init; } = "-";

    /// <summary>
    /// Gets the display text for the race position.
    /// </summary>
    public string PositionText { get; init; } = "-";

    /// <summary>
    /// Gets the current tyre summary.
    /// </summary>
    public string TyreText { get; init; } = "-";

    /// <summary>
    /// Gets the tyre age summary.
    /// </summary>
    public string TyreAgeText { get; init; } = "-";

    /// <summary>
    /// Gets the pit status summary.
    /// </summary>
    public string PitStatusText { get; init; } = "-";

    /// <summary>
    /// Gets the relative gap to the player.
    /// </summary>
    public string GapToPlayerText { get; init; } = "-";

    /// <summary>
    /// Gets the opponent's previous lap time.
    /// </summary>
    public string LastLapText { get; init; } = "-";

    /// <summary>
    /// Gets the previous-lap comparison against the player when data is validated.
    /// </summary>
    public string LapComparisonText { get; init; } = "-";

    /// <summary>
    /// Creates a UI projection from the specified car snapshot.
    /// </summary>
    /// <param name="snapshot">The source snapshot.</param>
    /// <param name="playerCar">The current player car snapshot.</param>
    public static CarStateItemViewModel FromSnapshot(CarSnapshot snapshot, CarSnapshot? playerCar)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new CarStateItemViewModel
        {
            CarIndex = snapshot.CarIndex,
            DisplayName = string.IsNullOrWhiteSpace(snapshot.DriverName)
                ? $"车辆 {snapshot.CarIndex}"
                : snapshot.DriverName!,
            PositionText = snapshot.Position is null ? "-" : $"P{snapshot.Position}",
            TyreText = FormatTyre(snapshot),
            TyreAgeText = snapshot.TyresAgeLaps is null ? "-" : $"{snapshot.TyresAgeLaps} 圈",
            PitStatusText = PitStatusFormatter.Format(snapshot.PitStatus, snapshot.NumPitStops),
            GapToPlayerText = FormatGapToPlayer(snapshot, playerCar),
            LastLapText = FormatLapTime(snapshot.LastLapTimeInMs),
            LapComparisonText = FormatLapComparison(snapshot, playerCar)
        };
    }

    private static string FormatTyre(CarSnapshot snapshot)
    {
        return TyreCompoundFormatter.Format(
            snapshot.VisualTyreCompound,
            snapshot.ActualTyreCompound,
            snapshot.HasTelemetryAccess);
    }

    private static string FormatGapToPlayer(CarSnapshot snapshot, CarSnapshot? playerCar)
    {
        return OpponentStatusFormatter.FormatGapToPlayer(snapshot, playerCar);
    }

    private static string FormatLapComparison(CarSnapshot snapshot, CarSnapshot? playerCar)
    {
        if (playerCar?.Position is null || snapshot.Position is null)
        {
            return "-";
        }

        AdjacentLapComparison? comparison = snapshot.Position == playerCar.Position - 1
            ? AdjacentLapComparisonBuilder.BuildFrontComparison(playerCar, snapshot)
            : snapshot.Position == playerCar.Position + 1
                ? AdjacentLapComparisonBuilder.BuildRearComparison(playerCar, snapshot)
                : null;
        if (comparison is null)
        {
            return snapshot.Position == playerCar.Position - 1 || snapshot.Position == playerCar.Position + 1
                ? "等待同圈数据"
                : "-";
        }

        var relationText = comparison.Relation == "front" ? "前车" : "后车";
        var fasterText = comparison.PlayerWasFaster ? "快" : "慢";
        var seconds = Math.Abs(comparison.DeltaInMs) / 1000d;
        return $"比{relationText}{fasterText} {seconds.ToString("0.000", CultureInfo.InvariantCulture)}s";
    }

    private static string FormatLapTime(uint? milliseconds)
    {
        if (milliseconds is null or 0)
        {
            return "-";
        }

        var time = TimeSpan.FromMilliseconds(milliseconds.Value);
        return time.TotalMinutes >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds:000}")
            : string.Create(CultureInfo.InvariantCulture, $"{time.Seconds}.{time.Milliseconds:000}s");
    }
}
