namespace F1Telemetry.Analytics.Events;

/// <summary>
/// Represents a validated previous-lap comparison against an adjacent car.
/// </summary>
public sealed record AdjacentLapComparison
{
    /// <summary>
    /// Gets the completed lap number shared by the player and opponent.
    /// </summary>
    public int CompletedLapNumber { get; init; }

    /// <summary>
    /// Gets the compared opponent relation, such as front or rear.
    /// </summary>
    public string Relation { get; init; } = "-";

    /// <summary>
    /// Gets the opponent car index.
    /// </summary>
    public int OpponentCarIndex { get; init; }

    /// <summary>
    /// Gets the player's previous lap time in milliseconds.
    /// </summary>
    public uint PlayerLapTimeInMs { get; init; }

    /// <summary>
    /// Gets the opponent's previous lap time in milliseconds.
    /// </summary>
    public uint OpponentLapTimeInMs { get; init; }

    /// <summary>
    /// Gets the signed difference where negative means the player was faster.
    /// </summary>
    public int DeltaInMs => (int)PlayerLapTimeInMs - (int)OpponentLapTimeInMs;

    /// <summary>
    /// Gets a value indicating whether the player was faster than the opponent.
    /// </summary>
    public bool PlayerWasFaster => DeltaInMs < 0;
}
