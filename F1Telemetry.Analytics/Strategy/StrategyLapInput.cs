namespace F1Telemetry.Analytics.Strategy;

/// <summary>
/// Represents the compact lap fields needed by the V3 strategy foundation without depending on storage models.
/// </summary>
public sealed record StrategyLapInput
{
    /// <summary>
    /// Gets the completed lap number.
    /// </summary>
    public int LapNumber { get; init; }

    /// <summary>
    /// Gets the lap time in milliseconds when available.
    /// </summary>
    public uint? LapTimeInMs { get; init; }

    /// <summary>
    /// Gets a value indicating whether the lap was valid.
    /// </summary>
    public bool IsValid { get; init; } = true;

    /// <summary>
    /// Gets the fuel used during the lap in litres when available.
    /// </summary>
    public float? FuelUsedLitres { get; init; }

    /// <summary>
    /// Gets the ERS used during the lap when available.
    /// </summary>
    public float? ErsUsed { get; init; }

    /// <summary>
    /// Gets the tyre label at lap start.
    /// </summary>
    public string StartTyre { get; init; } = "-";

    /// <summary>
    /// Gets the tyre label at lap end.
    /// </summary>
    public string EndTyre { get; init; } = "-";

    /// <summary>
    /// Gets a value indicating whether the lap started in the pit lane or pit box.
    /// </summary>
    public bool StartedInPit { get; init; }

    /// <summary>
    /// Gets a value indicating whether the lap ended in the pit lane or pit box.
    /// </summary>
    public bool EndedInPit { get; init; }
}
