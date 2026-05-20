using F1Telemetry.Core.Models;

namespace F1Telemetry.Analytics.Events;

/// <summary>
/// Builds previous-lap comparisons only when player and opponent data reference the same completed lap.
/// </summary>
public static class AdjacentLapComparisonBuilder
{
    /// <summary>
    /// Builds a validated comparison against the car directly ahead.
    /// </summary>
    public static AdjacentLapComparison? BuildFrontComparison(CarSnapshot player, CarSnapshot? frontCar)
    {
        return Build(player, frontCar, "front");
    }

    /// <summary>
    /// Builds a validated comparison against the car directly behind.
    /// </summary>
    public static AdjacentLapComparison? BuildRearComparison(CarSnapshot player, CarSnapshot? rearCar)
    {
        return Build(player, rearCar, "rear");
    }

    private static AdjacentLapComparison? Build(CarSnapshot player, CarSnapshot? opponent, string relation)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (opponent is null ||
            player.CurrentLapNumber is null ||
            opponent.CurrentLapNumber is null ||
            player.CurrentLapNumber.Value == 0 ||
            player.CurrentLapNumber != opponent.CurrentLapNumber ||
            player.LastLapTimeInMs is null or 0 ||
            opponent.LastLapTimeInMs is null or 0)
        {
            return null;
        }

        return new AdjacentLapComparison
        {
            CompletedLapNumber = player.CurrentLapNumber.Value - 1,
            Relation = relation,
            OpponentCarIndex = opponent.CarIndex,
            PlayerLapTimeInMs = player.LastLapTimeInMs.Value,
            OpponentLapTimeInMs = opponent.LastLapTimeInMs.Value
        };
    }
}
