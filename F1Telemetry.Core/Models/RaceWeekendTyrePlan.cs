namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents manually configured race-weekend tyre availability for strategy prompts.
/// </summary>
public sealed record RaceWeekendTyrePlan
{
    /// <summary>
    /// Gets the maximum editable count for one tyre compound.
    /// </summary>
    public const int MaxInventoryCount = 99;

    /// <summary>
    /// Gets the legacy free-form tyre inventory text used only for migration and summaries.
    /// </summary>
    public const string DefaultInventoryText = "Soft=0; Medium=0; Hard=0; Intermediate=0; Wet=0";

    /// <summary>
    /// Gets the default wear ceiling used when filtering recommended tyre sets.
    /// </summary>
    public const int DefaultMaxRecommendedWearPercent = 62;

    /// <summary>
    /// Gets the available soft tyre count.
    /// </summary>
    public int SoftCount { get; init; }

    /// <summary>
    /// Gets the available medium tyre count.
    /// </summary>
    public int MediumCount { get; init; }

    /// <summary>
    /// Gets the available hard tyre count.
    /// </summary>
    public int HardCount { get; init; }

    /// <summary>
    /// Gets the available intermediate tyre count.
    /// </summary>
    public int IntermediateCount { get; init; }

    /// <summary>
    /// Gets the available wet tyre count.
    /// </summary>
    public int WetCount { get; init; }

    /// <summary>
    /// Gets the legacy inventory text used for migration and compact read-only summaries.
    /// </summary>
    public string InventoryText { get; init; } = DefaultInventoryText;

    /// <summary>
    /// Gets the maximum tyre wear percentage allowed for a recommended replacement set.
    /// </summary>
    public int MaxRecommendedWearPercent { get; init; } = DefaultMaxRecommendedWearPercent;

    /// <summary>
    /// Returns a plan with counts and thresholds constrained to supported UI ranges.
    /// </summary>
    public RaceWeekendTyrePlan Normalize()
    {
        var normalized = this with
        {
            SoftCount = ClampCount(SoftCount),
            MediumCount = ClampCount(MediumCount),
            HardCount = ClampCount(HardCount),
            IntermediateCount = ClampCount(IntermediateCount),
            WetCount = ClampCount(WetCount),
            MaxRecommendedWearPercent = Math.Clamp(MaxRecommendedWearPercent, 0, 100)
        };

        return normalized with { InventoryText = FormatInventoryText(normalized) };
    }

    /// <summary>
    /// Creates a structured plan from a legacy inventory text string.
    /// </summary>
    /// <param name="inventoryText">The legacy text to parse.</param>
    /// <param name="maxRecommendedWearPercent">The recommended wear ceiling to preserve.</param>
    public static RaceWeekendTyrePlan FromLegacyInventoryText(
        string? inventoryText,
        int maxRecommendedWearPercent = DefaultMaxRecommendedWearPercent)
    {
        var counts = ParseLegacyInventoryText(inventoryText);
        return new RaceWeekendTyrePlan
        {
            SoftCount = counts.Soft,
            MediumCount = counts.Medium,
            HardCount = counts.Hard,
            IntermediateCount = counts.Intermediate,
            WetCount = counts.Wet,
            MaxRecommendedWearPercent = maxRecommendedWearPercent
        }.Normalize();
    }

    /// <summary>
    /// Formats the structured counts as a compact read-only compatibility summary.
    /// </summary>
    /// <param name="plan">The plan to summarize.</param>
    public static string FormatInventoryText(RaceWeekendTyrePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return $"Soft={ClampCount(plan.SoftCount)}; " +
               $"Medium={ClampCount(plan.MediumCount)}; " +
               $"Hard={ClampCount(plan.HardCount)}; " +
               $"Intermediate={ClampCount(plan.IntermediateCount)}; " +
               $"Wet={ClampCount(plan.WetCount)}";
    }

    /// <summary>
    /// Parses a legacy inventory string into structured counts, defaulting invalid values to zero.
    /// </summary>
    /// <param name="inventoryText">The legacy inventory text.</param>
    public static TyreInventoryCounts ParseLegacyInventoryText(string? inventoryText)
    {
        if (string.IsNullOrWhiteSpace(inventoryText))
        {
            return new TyreInventoryCounts();
        }

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in inventoryText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2 || !int.TryParse(pair[1], out var value))
            {
                continue;
            }

            counts[pair[0]] = ClampCount(value);
        }

        return new TyreInventoryCounts(
            Soft: counts.GetValueOrDefault("Soft"),
            Medium: counts.GetValueOrDefault("Medium"),
            Hard: counts.GetValueOrDefault("Hard"),
            Intermediate: counts.GetValueOrDefault("Intermediate"),
            Wet: counts.GetValueOrDefault("Wet"));
    }

    private static int ClampCount(int value)
    {
        return Math.Clamp(value, 0, MaxInventoryCount);
    }
}

/// <summary>
/// Represents structured tyre counts parsed from race-weekend inventory settings.
/// </summary>
public readonly record struct TyreInventoryCounts(
    int Soft = 0,
    int Medium = 0,
    int Hard = 0,
    int Intermediate = 0,
    int Wet = 0);
