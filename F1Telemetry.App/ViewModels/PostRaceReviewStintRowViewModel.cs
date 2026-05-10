using F1Telemetry.App.Formatting;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents one inferred tyre stint row in the post-race review.
/// </summary>
public sealed record PostRaceReviewStintRowViewModel
{
    /// <summary>
    /// Gets the stint label.
    /// </summary>
    public string StintText { get; init; } = "-";

    /// <summary>
    /// Gets the lap range text.
    /// </summary>
    public string LapRangeText { get; init; } = "-";

    /// <summary>
    /// Gets the tyre label inferred from stored lap labels.
    /// </summary>
    public string TyreText { get; init; } = "-";

    /// <summary>
    /// Gets the limited evidence text.
    /// </summary>
    public string EvidenceText { get; init; } = "基于 StartTyre/EndTyre 推断，信息有限";

    /// <summary>
    /// Gets the compact stint summary used by WPF bindings.
    /// </summary>
    public string SummaryText => $"{LapRangeText} · {EvidenceText}";

    /// <summary>
    /// Builds inferred stint rows from stored lap tyre labels.
    /// </summary>
    /// <param name="laps">The ordered stored laps.</param>
    public static IReadOnlyList<PostRaceReviewStintRowViewModel> BuildFromLaps(IReadOnlyList<StoredLap> laps)
    {
        if (laps.Count == 0)
        {
            return Array.Empty<PostRaceReviewStintRowViewModel>();
        }

        var stints = new List<PostRaceReviewStintRowViewModel>();
        var stintStartLap = laps[0].LapNumber;
        var currentTyre = InferTyre(laps[0]);
        var stintIndex = 1;

        foreach (var lap in laps.Skip(1))
        {
            var tyre = InferTyre(lap);
            if (string.Equals(tyre, currentTyre, StringComparison.Ordinal))
            {
                continue;
            }

            stints.Add(CreateRow(stintIndex++, stintStartLap, lap.LapNumber - 1, currentTyre));
            stintStartLap = lap.LapNumber;
            currentTyre = tyre;
        }

        stints.Add(CreateRow(stintIndex, stintStartLap, laps[^1].LapNumber, currentTyre));
        return stints;
    }

    private static PostRaceReviewStintRowViewModel CreateRow(int stintIndex, int startLap, int endLap, string tyre)
    {
        return new PostRaceReviewStintRowViewModel
        {
            StintText = $"Stint {stintIndex}",
            LapRangeText = startLap == endLap ? $"Lap {startLap}" : $"Lap {startLap}-{endLap}",
            TyreText = tyre
        };
    }

    private static string InferTyre(StoredLap lap)
    {
        var endTyre = NormalizeTyre(lap.EndTyre);
        if (endTyre != "-")
        {
            return endTyre;
        }

        return NormalizeTyre(lap.StartTyre);
    }

    private static string NormalizeTyre(string? tyre)
    {
        if (string.IsNullOrWhiteSpace(tyre) || tyre.Trim() == "-" || !tyre.Any(char.IsDigit))
        {
            return "-";
        }

        var formatted = TyreCompoundFormatter.FormatRawCompoundText(tyre);
        return string.IsNullOrWhiteSpace(formatted) || formatted == "未知胎" ? "-" : formatted;
    }
}
