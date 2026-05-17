using System.Globalization;
using F1Telemetry.Analytics.Events;

namespace F1Telemetry.Analytics.Strategy;

/// <summary>
/// Infers tyre stints and compact strategy timeline entries from summarized lap and event evidence.
/// </summary>
public sealed class StintStrategyAnalyzer
{
    /// <summary>
    /// Builds a compact strategy analysis from lap summaries and optional race events.
    /// </summary>
    /// <param name="laps">Completed lap inputs in any order.</param>
    /// <param name="events">Optional race events used to mark neutralized or red-flag influence.</param>
    /// <returns>A compact strategy analysis with stints, timeline entries, and data quality warnings.</returns>
    public StrategyAnalysisResult Analyze(
        IReadOnlyList<StrategyLapInput> laps,
        IReadOnlyList<RaceEvent>? events = null)
    {
        ArgumentNullException.ThrowIfNull(laps);

        var orderedLaps = laps
            .Where(lap => lap.LapNumber > 0)
            .OrderBy(lap => lap.LapNumber)
            .ToArray();
        var suppliedEvents = events ?? Array.Empty<RaceEvent>();
        var warnings = new List<string>();

        if (orderedLaps.Length == 0)
        {
            warnings.Add("No completed lap inputs were available for stint analysis.");
            return new StrategyAnalysisResult
            {
                DataQualityWarnings = warnings
            };
        }

        if (orderedLaps.Any(lap => lap.LapTimeInMs is null))
        {
            warnings.Add("Some laps are missing lap time and are excluded from pace metrics.");
        }

        if (orderedLaps.Any(lap => !IsKnownTyre(ResolveTyre(lap))))
        {
            warnings.Add("Some tyre labels are missing, so stint boundaries may be approximate.");
        }

        var influence = BuildInfluenceIndex(suppliedEvents);
        var stints = AnalyzeStints(orderedLaps, influence);
        var timeline = BuildTimeline(stints, suppliedEvents);

        return new StrategyAnalysisResult
        {
            Stints = stints,
            Timeline = timeline,
            DataQualityWarnings = warnings
        };
    }

    /// <summary>
    /// Infers stints from lap tyre labels and pit flags.
    /// </summary>
    /// <param name="laps">Completed lap inputs in any order.</param>
    /// <param name="events">Optional race events used to mark neutralized or red-flag influence.</param>
    /// <returns>The inferred stint summaries.</returns>
    public IReadOnlyList<StintSummary> AnalyzeStints(
        IReadOnlyList<StrategyLapInput> laps,
        IReadOnlyList<RaceEvent>? events = null)
    {
        ArgumentNullException.ThrowIfNull(laps);

        var influence = BuildInfluenceIndex(events ?? Array.Empty<RaceEvent>());
        return AnalyzeStints(
            laps
                .Where(lap => lap.LapNumber > 0)
                .OrderBy(lap => lap.LapNumber)
                .ToArray(),
            influence);
    }

    /// <summary>
    /// Builds a compact strategy timeline from stints and optional events.
    /// </summary>
    /// <param name="stints">Inferred stint summaries.</param>
    /// <param name="events">Optional race events to include as timeline entries.</param>
    /// <returns>Timeline entries ordered by lap number and category.</returns>
    public IReadOnlyList<StrategyTimelineEntry> BuildTimeline(
        IReadOnlyList<StintSummary> stints,
        IReadOnlyList<RaceEvent>? events = null)
    {
        ArgumentNullException.ThrowIfNull(stints);

        var entries = new List<StrategyTimelineEntry>();
        foreach (var stint in stints.OrderBy(stint => stint.StartLap))
        {
            entries.Add(new StrategyTimelineEntry
            {
                LapNumber = stint.StartLap,
                Category = "Stint",
                Title = string.Create(CultureInfo.InvariantCulture, $"Stint {stint.StintNumber} starts"),
                Detail = FormatStintDetail(stint),
                IsDataSupported = true,
                RiskLevel = stint.HasRedFlagInfluence ? StrategyRiskLevel.Medium : StrategyRiskLevel.Low,
                DataQualityWarnings = stint.Notes
            });
        }

        foreach (var raceEvent in (events ?? Array.Empty<RaceEvent>())
            .Where(raceEvent => raceEvent.LapNumber is not null)
            .Where(raceEvent => IsSafetyCarEvent(raceEvent.EventType) || IsRedFlagEvent(raceEvent.EventType) || raceEvent.EventType == EventType.DataQualityWarning)
            .OrderBy(raceEvent => raceEvent.LapNumber)
            .ThenBy(raceEvent => raceEvent.EventType.ToString(), StringComparer.Ordinal))
        {
            entries.Add(new StrategyTimelineEntry
            {
                LapNumber = raceEvent.LapNumber,
                Category = "RaceEvent",
                Title = raceEvent.EventType.ToString(),
                Detail = string.IsNullOrWhiteSpace(raceEvent.Message) ? raceEvent.EventType.ToString() : raceEvent.Message.Trim(),
                IsDataSupported = true,
                RiskLevel = raceEvent.Severity == EventSeverity.Warning ? StrategyRiskLevel.Medium : StrategyRiskLevel.Low,
                DataQualityWarnings = raceEvent.EventType == EventType.DataQualityWarning
                    ? new[] { "Race event reported missing or uncertain evidence." }
                    : Array.Empty<string>()
            });
        }

        return entries
            .OrderBy(entry => entry.LapNumber ?? int.MaxValue)
            .ThenBy(entry => entry.Category, StringComparer.Ordinal)
            .ThenBy(entry => entry.Title, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<StintSummary> AnalyzeStints(
        IReadOnlyList<StrategyLapInput> orderedLaps,
        StrategyInfluenceIndex influence)
    {
        if (orderedLaps.Count == 0)
        {
            return Array.Empty<StintSummary>();
        }

        var stints = new List<StintSummary>();
        var currentLaps = new List<StrategyLapInput>();
        var currentTyre = ResolveTyre(orderedLaps[0]);
        StrategyLapInput? previousLap = null;

        foreach (var lap in orderedLaps)
        {
            var lapTyre = ResolveTyre(lap);
            var startsNewStint = currentLaps.Count > 0
                && (lap.StartedInPit
                    || previousLap?.EndedInPit == true
                    || !string.Equals(lapTyre, currentTyre, StringComparison.OrdinalIgnoreCase));

            if (startsNewStint)
            {
                stints.Add(BuildStint(stints.Count + 1, currentTyre, currentLaps, influence));
                currentLaps.Clear();
                currentTyre = lapTyre;
            }

            currentLaps.Add(lap);
            previousLap = lap;
        }

        if (currentLaps.Count > 0)
        {
            stints.Add(BuildStint(stints.Count + 1, currentTyre, currentLaps, influence));
        }

        return stints;
    }

    private static StintSummary BuildStint(
        int stintNumber,
        string tyre,
        IReadOnlyList<StrategyLapInput> laps,
        StrategyInfluenceIndex influence)
    {
        var lapNumbers = laps.Select(lap => lap.LapNumber).ToArray();
        var safetyCarLapNumbers = lapNumbers.Where(influence.SafetyCarLapNumbers.Contains).ToArray();
        var redFlagLapNumbers = lapNumbers.Where(influence.RedFlagLapNumbers.Contains).ToArray();
        var adjustedLaps = laps
            .Where(lap => lap.LapTimeInMs is not null)
            .Where(lap => lap.IsValid)
            .Where(lap => !influence.SafetyCarLapNumbers.Contains(lap.LapNumber))
            .Where(lap => !influence.RedFlagLapNumbers.Contains(lap.LapNumber))
            .ToArray();
        var rawTimedLaps = laps
            .Where(lap => lap.LapTimeInMs is not null)
            .ToArray();
        var notes = new List<string>();

        if (adjustedLaps.Length < rawTimedLaps.Length)
        {
            notes.Add("Adjusted metrics exclude invalid, safety car, and red flag influenced laps.");
        }

        if (rawTimedLaps.Length < laps.Count)
        {
            notes.Add("Some laps have no lap time.");
        }

        if (!IsKnownTyre(tyre))
        {
            notes.Add("Tyre label missing; stint boundary confidence is limited.");
        }

        return new StintSummary
        {
            StintNumber = stintNumber,
            StartLap = laps[0].LapNumber,
            EndLap = laps[^1].LapNumber,
            Tyre = tyre,
            LapCount = laps.Count,
            ValidLapCount = laps.Count(lap => lap.IsValid),
            LapNumbers = lapNumbers,
            AdjustedLapNumbers = adjustedLaps.Select(lap => lap.LapNumber).ToArray(),
            SafetyCarLapNumbers = safetyCarLapNumbers,
            RedFlagLapNumbers = redFlagLapNumbers,
            RawAverageLapTimeMs = AverageLapTime(rawTimedLaps),
            AdjustedAverageLapTimeMs = AverageLapTime(adjustedLaps),
            RawBestLapTimeMs = BestLapTime(rawTimedLaps),
            AdjustedBestLapTimeMs = BestLapTime(adjustedLaps),
            AverageFuelUsedLitres = AverageFloat(laps.Select(lap => lap.FuelUsedLitres)),
            AverageErsUsed = AverageFloat(laps.Select(lap => lap.ErsUsed)),
            HasSafetyCarInfluence = safetyCarLapNumbers.Length > 0,
            HasRedFlagInfluence = redFlagLapNumbers.Length > 0,
            Notes = notes
        };
    }

    private static StrategyInfluenceIndex BuildInfluenceIndex(IReadOnlyList<RaceEvent> events)
    {
        var safetyCarLapNumbers = new HashSet<int>();
        var redFlagLapNumbers = new HashSet<int>();

        foreach (var raceEvent in events)
        {
            if (raceEvent.LapNumber is null)
            {
                continue;
            }

            if (IsSafetyCarEvent(raceEvent.EventType))
            {
                safetyCarLapNumbers.Add(raceEvent.LapNumber.Value);
            }

            if (IsRedFlagEvent(raceEvent.EventType))
            {
                redFlagLapNumbers.Add(raceEvent.LapNumber.Value);
            }
        }

        return new StrategyInfluenceIndex(safetyCarLapNumbers, redFlagLapNumbers);
    }

    private static bool IsSafetyCarEvent(EventType eventType)
    {
        return eventType is EventType.SafetyCar or EventType.VirtualSafetyCar or EventType.SafetyCarRestart;
    }

    private static bool IsRedFlagEvent(EventType eventType)
    {
        return eventType is EventType.RedFlag or EventType.RedFlagTyreChange;
    }

    private static string ResolveTyre(StrategyLapInput lap)
    {
        var startTyre = NormalizeTyreLabel(lap.StartTyre);
        var endTyre = NormalizeTyreLabel(lap.EndTyre);

        if (lap.EndedInPit && !lap.StartedInPit && IsKnownTyre(startTyre))
        {
            return startTyre;
        }

        if (IsKnownTyre(endTyre))
        {
            return endTyre;
        }

        return IsKnownTyre(startTyre) ? startTyre : "-";
    }

    private static string NormalizeTyreLabel(string? tyre)
    {
        return string.IsNullOrWhiteSpace(tyre) ? "-" : tyre.Trim();
    }

    private static bool IsKnownTyre(string tyre)
    {
        return !string.IsNullOrWhiteSpace(tyre)
            && !string.Equals(tyre, "-", StringComparison.Ordinal)
            && !string.Equals(tyre, "n/a", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(tyre, "unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static double? AverageLapTime(IReadOnlyList<StrategyLapInput> laps)
    {
        return laps.Count == 0 ? null : laps.Average(lap => lap.LapTimeInMs!.Value);
    }

    private static uint? BestLapTime(IReadOnlyList<StrategyLapInput> laps)
    {
        return laps.Count == 0 ? null : laps.Min(lap => lap.LapTimeInMs!.Value);
    }

    private static double? AverageFloat(IEnumerable<float?> values)
    {
        var presentValues = values
            .Where(value => value is not null)
            .Select(value => (double)value!.Value)
            .ToArray();

        return presentValues.Length == 0 ? null : presentValues.Average();
    }

    private static string FormatStintDetail(StintSummary stint)
    {
        var adjustedAverage = stint.AdjustedAverageLapTimeMs is null
            ? "n/a"
            : string.Format(CultureInfo.InvariantCulture, "{0:0} ms adjusted avg", stint.AdjustedAverageLapTimeMs.Value);
        return string.Format(
            CultureInfo.InvariantCulture,
            "Lap {0}-{1}, tyre {2}, {3}",
            stint.StartLap,
            stint.EndLap,
            stint.Tyre,
            adjustedAverage);
    }

    private sealed record StrategyInfluenceIndex(
        HashSet<int> SafetyCarLapNumbers,
        HashSet<int> RedFlagLapNumbers);
}
