namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents a single tyre set reported by the game inventory packet.
/// </summary>
public sealed record TyreSetSnapshot
{
    /// <summary>
    /// Gets the zero-based tyre set index.
    /// </summary>
    public byte Index { get; init; }

    /// <summary>
    /// Gets the actual tyre compound identifier.
    /// </summary>
    public byte ActualTyreCompound { get; init; }

    /// <summary>
    /// Gets the visible tyre compound identifier.
    /// </summary>
    public byte VisualTyreCompound { get; init; }

    /// <summary>
    /// Gets the tyre set wear percentage.
    /// </summary>
    public byte Wear { get; init; }

    /// <summary>
    /// Gets a value indicating whether the set is available for use.
    /// </summary>
    public bool Available { get; init; }

    /// <summary>
    /// Gets the session for which the game recommends this set.
    /// </summary>
    public byte RecommendedSession { get; init; }

    /// <summary>
    /// Gets the game-reported life span in laps.
    /// </summary>
    public byte LifeSpan { get; init; }

    /// <summary>
    /// Gets the game-reported usable life in laps.
    /// </summary>
    public byte UsableLife { get; init; }

    /// <summary>
    /// Gets the estimated lap delta time in milliseconds.
    /// </summary>
    public short LapDeltaTime { get; init; }

    /// <summary>
    /// Gets a value indicating whether this set is currently fitted.
    /// </summary>
    public bool Fitted { get; init; }
}
