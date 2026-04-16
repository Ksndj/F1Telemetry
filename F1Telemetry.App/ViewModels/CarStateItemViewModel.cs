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
            PitStatusText = FormatPitStatus(snapshot.PitStatus),
            GapToPlayerText = FormatGapToPlayer(snapshot, playerCar)
        };
    }

    private static string FormatTyre(CarSnapshot snapshot)
    {
        return snapshot.VisualTyreCompound is null && snapshot.ActualTyreCompound is null
            ? snapshot.HasTelemetryAccess ? "-" : "不可见"
            : $"V{snapshot.VisualTyreCompound?.ToString() ?? "-"} / A{snapshot.ActualTyreCompound?.ToString() ?? "-"}";
    }

    private static string FormatPitStatus(byte? pitStatus)
    {
        return pitStatus switch
        {
            0 => "赛道",
            1 => "进站",
            2 => "Pit 内",
            null => "-",
            _ => $"未知({pitStatus})"
        };
    }

    private static string FormatGapToPlayer(CarSnapshot snapshot, CarSnapshot? playerCar)
    {
        if (playerCar?.Position is null || snapshot.Position is null)
        {
            return "-";
        }

        if (snapshot.Position == playerCar.Position)
        {
            return "同位置";
        }

        if (snapshot.DeltaToRaceLeaderInMs is null || playerCar.DeltaToRaceLeaderInMs is null)
        {
            return snapshot.Position < playerCar.Position ? "前 -" : "后 -";
        }

        var gapSeconds = Math.Abs(snapshot.DeltaToRaceLeaderInMs.Value - playerCar.DeltaToRaceLeaderInMs.Value) / 1000d;
        var prefix = snapshot.Position < playerCar.Position ? "前" : "后";
        return $"{prefix} {gapSeconds:0.000}s";
    }
}
