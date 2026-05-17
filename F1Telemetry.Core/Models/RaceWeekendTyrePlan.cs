namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents manually configured race-weekend tyre availability for strategy prompts.
/// </summary>
public sealed record RaceWeekendTyrePlan
{
    /// <summary>
    /// Gets the default free-form tyre inventory text shown to the user.
    /// </summary>
    public const string DefaultInventoryText = "Soft=0; Medium=0; Hard=0; Intermediate=0; Wet=0";

    /// <summary>
    /// Gets the default wear ceiling used when filtering recommended tyre sets.
    /// </summary>
    public const int DefaultMaxRecommendedWearPercent = 62;

    /// <summary>
    /// Gets the manually entered tyre type and quantity summary for the race weekend.
    /// </summary>
    public string InventoryText { get; init; } = DefaultInventoryText;

    /// <summary>
    /// Gets the maximum tyre wear percentage allowed for a recommended replacement set.
    /// </summary>
    public int MaxRecommendedWearPercent { get; init; } = DefaultMaxRecommendedWearPercent;
}
