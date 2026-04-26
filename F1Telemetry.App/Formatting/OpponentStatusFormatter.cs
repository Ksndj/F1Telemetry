using F1Telemetry.Core.Models;

namespace F1Telemetry.App.Formatting;

/// <summary>
/// Formats opponent status fields for readable WPF row display.
/// </summary>
public static class OpponentStatusFormatter
{
    /// <summary>
    /// Formats an opponent's relative gap to the player.
    /// </summary>
    /// <param name="snapshot">The opponent car snapshot.</param>
    /// <param name="playerCar">The current player car snapshot.</param>
    public static string FormatGapToPlayer(CarSnapshot snapshot, CarSnapshot? playerCar)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (playerCar?.Position is null ||
            snapshot.Position is null ||
            snapshot.DeltaToRaceLeaderInMs is null ||
            playerCar.DeltaToRaceLeaderInMs is null)
        {
            return "不可用";
        }

        if (snapshot.DeltaToRaceLeaderInMs == playerCar.DeltaToRaceLeaderInMs ||
            snapshot.Position == playerCar.Position)
        {
            return "同圈";
        }

        var gapSeconds = Math.Abs(snapshot.DeltaToRaceLeaderInMs.Value - playerCar.DeltaToRaceLeaderInMs.Value) / 1000d;
        var prefix = snapshot.Position < playerCar.Position ? "前" : "后";
        return $"{prefix} {gapSeconds:0.000}s";
    }
}
