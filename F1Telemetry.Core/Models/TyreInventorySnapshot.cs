namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents the latest game-reported tyre set inventory for a car.
/// </summary>
public sealed record TyreInventorySnapshot
{
    /// <summary>
    /// Gets the car index that owns the tyre set list.
    /// </summary>
    public byte CarIndex { get; init; }

    /// <summary>
    /// Gets the fitted tyre set index when known.
    /// </summary>
    public byte? FittedIndex { get; init; }

    /// <summary>
    /// Gets the tyre sets reported by the game.
    /// </summary>
    public IReadOnlyList<TyreSetSnapshot> Sets { get; init; } = Array.Empty<TyreSetSnapshot>();

    /// <summary>
    /// Gets the timestamp when the inventory was observed.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
