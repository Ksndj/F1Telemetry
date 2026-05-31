using F1Telemetry.Analytics.Laps;
using F1Telemetry.App.Formatting;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents a WPF-friendly row for the lap history table.
/// </summary>
public sealed class LapSummaryItemViewModel
{
    private const string DefaultSectorForeground = "#D7E4F3";
    private const string FastestSector1Foreground = "#50E3A4";
    private const string FastestSector2Foreground = "#58A6FF";
    private const string FastestSector3Foreground = "#F6C453";
    private const int MinimumDerivedSampleCount = 2;

    private sealed record DerivedStoredLapMetrics(
        double? AverageSpeedKph,
        float? FuelUsedLitres,
        float? ErsUsed,
        float? TyreWearDelta,
        string? StartTyre,
        string? EndTyre,
        string? PitWindowText);

    /// <summary>
    /// Gets the completed lap number.
    /// </summary>
    public int LapNumber { get; init; }

    /// <summary>
    /// Gets the completed lap label.
    /// </summary>
    public string LapText { get; init; } = "-";

    /// <summary>
    /// Gets the raw completed lap time in milliseconds.
    /// </summary>
    public int? LapTimeInMs { get; init; }

    /// <summary>
    /// Gets the formatted lap time.
    /// </summary>
    public string LapTimeText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted sector summary.
    /// </summary>
    public string SectorsText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted sector 1 time.
    /// </summary>
    public string Sector1Text { get; init; } = "-";

    /// <summary>
    /// Gets the formatted sector 2 time.
    /// </summary>
    public string Sector2Text { get; init; } = "-";

    /// <summary>
    /// Gets the formatted sector 3 time.
    /// </summary>
    public string Sector3Text { get; init; } = "-";

    /// <summary>
    /// Gets a value indicating whether sector 1 is the fastest sector 1 in the selected history session.
    /// </summary>
    public bool IsFastestSector1 { get; init; }

    /// <summary>
    /// Gets a value indicating whether sector 2 is the fastest sector 2 in the selected history session.
    /// </summary>
    public bool IsFastestSector2 { get; init; }

    /// <summary>
    /// Gets a value indicating whether sector 3 is the fastest sector 3 in the selected history session.
    /// </summary>
    public bool IsFastestSector3 { get; init; }

    /// <summary>
    /// Gets the display brush value for sector 1.
    /// </summary>
    public string Sector1Foreground => IsFastestSector1 ? FastestSector1Foreground : DefaultSectorForeground;

    /// <summary>
    /// Gets the display brush value for sector 2.
    /// </summary>
    public string Sector2Foreground => IsFastestSector2 ? FastestSector2Foreground : DefaultSectorForeground;

    /// <summary>
    /// Gets the display brush value for sector 3.
    /// </summary>
    public string Sector3Foreground => IsFastestSector3 ? FastestSector3Foreground : DefaultSectorForeground;

    /// <summary>
    /// Gets the formatted average speed summary.
    /// </summary>
    public string AverageSpeedText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted fuel usage summary in litres.
    /// </summary>
    public string FuelUsedLitresText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted ERS usage summary.
    /// </summary>
    public string ErsUsedText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted tyre wear delta summary.
    /// </summary>
    public string TyreWearDeltaText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted validity summary.
    /// </summary>
    public string ValidityText { get; init; } = "-";

    /// <summary>
    /// Gets a value indicating whether the lap is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the formatted tyre transition summary.
    /// </summary>
    public string TyreWindowText { get; init; } = "-";

    /// <summary>
    /// Gets the raw starting tyre label.
    /// </summary>
    public string StartTyre { get; init; } = "-";

    /// <summary>
    /// Gets the raw ending tyre label.
    /// </summary>
    public string EndTyre { get; init; } = "-";

    /// <summary>
    /// Gets the compact tyre compound label used for comparison.
    /// </summary>
    public string CompoundText => FormatStoredTyreWindow(StartTyre, EndTyre);

    /// <summary>
    /// Gets the formatted pit transition summary.
    /// </summary>
    public string PitWindowText { get; init; } = "-";

    /// <summary>
    /// Creates a UI row from the specified lap summary.
    /// </summary>
    /// <param name="summary">The summary to project.</param>
    public static LapSummaryItemViewModel FromSummary(LapSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var sector1Text = FormatLapTime(summary.Sector1TimeInMs);
        var sector2Text = FormatLapTime(summary.Sector2TimeInMs);
        var sector3Text = FormatLapTime(
            ResolveSector3Time(
                summary.LapTimeInMs,
                summary.Sector1TimeInMs,
                summary.Sector2TimeInMs,
                summary.Sector3TimeInMs));

        return new LapSummaryItemViewModel
        {
            LapNumber = summary.LapNumber,
            LapText = $"Lap {summary.LapNumber}",
            LapTimeInMs = summary.LapTimeInMs is null ? null : checked((int)summary.LapTimeInMs.Value),
            LapTimeText = FormatLapTime(summary.LapTimeInMs),
            SectorsText = FormatSectorsText(sector1Text, sector2Text, sector3Text),
            Sector1Text = sector1Text,
            Sector2Text = sector2Text,
            Sector3Text = sector3Text,
            AverageSpeedText = FormatAverageSpeed(summary.AverageSpeedKph),
            FuelUsedLitresText = FormatFuelUsed(summary.FuelUsedLitres),
            ErsUsedText = FormatErsUsed(summary.ErsUsed),
            TyreWearDeltaText = FormatTyreWearDelta(summary.TyreWearDelta),
            ValidityText = summary.IsValid ? "有效" : "无效",
            IsValid = summary.IsValid,
            StartTyre = summary.StartTyre,
            EndTyre = summary.EndTyre,
            TyreWindowText = $"{TyreCompoundFormatter.FormatRawCompoundText(summary.StartTyre)} -> {TyreCompoundFormatter.FormatRawCompoundText(summary.EndTyre)}",
            PitWindowText = $"{FormatPitState(summary.StartedInPit)} -> {FormatPitState(summary.EndedInPit)}"
        };
    }

    /// <summary>
    /// Creates a UI row from the specified stored lap row.
    /// </summary>
    /// <param name="lap">The stored lap row to project.</param>
    /// <param name="isFastestSector1">Whether this row owns the fastest sector 1 time.</param>
    /// <param name="isFastestSector2">Whether this row owns the fastest sector 2 time.</param>
    /// <param name="isFastestSector3">Whether this row owns the fastest sector 3 time.</param>
    public static LapSummaryItemViewModel FromStoredLap(
        StoredLap lap,
        bool isFastestSector1 = false,
        bool isFastestSector2 = false,
        bool isFastestSector3 = false)
    {
        return FromStoredLap(lap, null, isFastestSector1, isFastestSector2, isFastestSector3);
    }

    /// <summary>
    /// Creates a UI row from the specified stored lap row and optional display-only lap samples.
    /// </summary>
    /// <param name="lap">The stored lap row to project.</param>
    /// <param name="samples">The optional stored samples used only to enrich missing display fields.</param>
    /// <param name="isFastestSector1">Whether this row owns the fastest sector 1 time.</param>
    /// <param name="isFastestSector2">Whether this row owns the fastest sector 2 time.</param>
    /// <param name="isFastestSector3">Whether this row owns the fastest sector 3 time.</param>
    public static LapSummaryItemViewModel FromStoredLap(
        StoredLap lap,
        IReadOnlyList<StoredLapSample>? samples,
        bool isFastestSector1 = false,
        bool isFastestSector2 = false,
        bool isFastestSector3 = false)
    {
        ArgumentNullException.ThrowIfNull(lap);

        var sector1Text = FormatLapTime(lap.Sector1TimeInMs);
        var sector2Text = FormatLapTime(lap.Sector2TimeInMs);
        var sector3Text = FormatLapTime(ResolveStoredSector3Time(lap));
        var derived = DeriveStoredLapMetrics(samples);
        var averageSpeedKph = IsPositiveFinite(lap.AverageSpeedKph)
            ? lap.AverageSpeedKph
            : derived?.AverageSpeedKph;
        var fuelUsedLitres = IsFiniteNonNegative(lap.FuelUsedLitres)
            ? lap.FuelUsedLitres
            : derived?.FuelUsedLitres;
        var ersUsed = IsFiniteNonNegative(lap.ErsUsed)
            ? lap.ErsUsed
            : derived?.ErsUsed;
        var tyreWearDelta = derived?.TyreWearDelta;
        var startTyre = SelectStoredTyre(lap.StartTyre, derived?.StartTyre);
        var endTyre = SelectStoredTyre(lap.EndTyre, derived?.EndTyre);

        return new LapSummaryItemViewModel
        {
            LapNumber = lap.LapNumber,
            LapText = $"Lap {lap.LapNumber}",
            LapTimeInMs = lap.LapTimeInMs,
            LapTimeText = FormatLapTime(lap.LapTimeInMs),
            SectorsText = FormatSectorsText(sector1Text, sector2Text, sector3Text),
            Sector1Text = sector1Text,
            Sector2Text = sector2Text,
            Sector3Text = sector3Text,
            IsFastestSector1 = isFastestSector1,
            IsFastestSector2 = isFastestSector2,
            IsFastestSector3 = isFastestSector3,
            AverageSpeedText = FormatAverageSpeed(averageSpeedKph),
            FuelUsedLitresText = FormatFuelUsed(fuelUsedLitres),
            ErsUsedText = FormatErsUsed(ersUsed),
            TyreWearDeltaText = FormatTyreWearDelta(tyreWearDelta),
            ValidityText = lap.IsValid ? "有效" : "无效",
            IsValid = lap.IsValid,
            StartTyre = startTyre,
            EndTyre = endTyre,
            TyreWindowText = FormatStoredTyreWindow(startTyre, endTyre),
            PitWindowText = derived?.PitWindowText ?? "-"
        };
    }

    internal static int? ResolveStoredSector3Time(StoredLap lap)
    {
        ArgumentNullException.ThrowIfNull(lap);

        return ResolveSector3Time(
            lap.LapTimeInMs,
            lap.Sector1TimeInMs,
            lap.Sector2TimeInMs,
            lap.Sector3TimeInMs);
    }

    private static string FormatPitState(bool inPit)
    {
        return inPit ? "进站" : "赛道";
    }

    private static string FormatAverageSpeed(double? averageSpeedKph)
    {
        if (averageSpeedKph is not { } speed || speed <= 0 || !double.IsFinite(speed))
        {
            return "-";
        }

        return $"{speed:0} km/h";
    }

    private static string FormatFuelUsed(float? fuelUsedLitres)
    {
        if (fuelUsedLitres is not { } fuel || fuel < 0 || !float.IsFinite(fuel))
        {
            return "-";
        }

        return $"{fuel:0.00} L";
    }

    private static string FormatErsUsed(float? ersUsed)
    {
        if (ersUsed is not { } energy || energy < 0 || !float.IsFinite(energy))
        {
            return "-";
        }

        return $"{energy / 1_000_000f:0.00} MJ";
    }

    private static string FormatTyreWearDelta(float? tyreWearDelta)
    {
        if (tyreWearDelta is not { } wearDelta || wearDelta < 0 || !float.IsFinite(wearDelta))
        {
            return "-";
        }

        return $"{wearDelta:0.0}%";
    }

    private static string FormatLapTime(int? milliseconds)
    {
        if (milliseconds is null || milliseconds.Value < 0)
        {
            return "-";
        }

        return FormatLapTime((uint)milliseconds.Value);
    }

    private static string FormatLapTime(uint? milliseconds)
    {
        if (milliseconds is null)
        {
            return "-";
        }

        var time = TimeSpan.FromMilliseconds(milliseconds.Value);
        return time.TotalMinutes >= 1
            ? $"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds:000}"
            : $"{time.Seconds}.{time.Milliseconds:000}s";
    }

    private static string FormatSectorsText(string sector1Text, string sector2Text, string sector3Text)
    {
        return $"{sector1Text} / {sector2Text} / {sector3Text}";
    }

    private static int? ResolveSector3Time(int? lapTime, int? sector1, int? sector2, int? sector3)
    {
        if (sector3 is > 0)
        {
            return sector3;
        }

        if (lapTime is null || sector1 is null || sector2 is null)
        {
            return sector3;
        }

        var inferredSector3 = lapTime.Value - sector1.Value - sector2.Value;
        return inferredSector3 > 0 ? inferredSector3 : sector3;
    }

    private static uint? ResolveSector3Time(uint? lapTime, uint? sector1, uint? sector2, uint? sector3)
    {
        if (sector3 is > 0)
        {
            return sector3;
        }

        if (lapTime is null || sector1 is null || sector2 is null)
        {
            return sector3;
        }

        var sector12 = sector1.Value + sector2.Value;
        return lapTime.Value > sector12 ? lapTime.Value - sector12 : sector3;
    }

    private static DerivedStoredLapMetrics? DeriveStoredLapMetrics(IReadOnlyList<StoredLapSample>? samples)
    {
        if (samples is null || samples.Count < MinimumDerivedSampleCount)
        {
            return null;
        }

        var orderedSamples = samples
            .OrderBy(sample => sample.SampleIndex)
            .ThenBy(sample => sample.SampledAt)
            .ThenBy(sample => sample.Id)
            .ToArray();
        if (orderedSamples.Length < MinimumDerivedSampleCount)
        {
            return null;
        }

        var tyreWindow = TryDeriveTyreWindow(orderedSamples);
        return new DerivedStoredLapMetrics(
            DeriveAverageSpeed(orderedSamples),
            DeriveEndpointConsumption(orderedSamples, sample => sample.FuelRemainingLitres),
            DeriveEndpointConsumption(orderedSamples, sample => sample.ErsStoreEnergy),
            DeriveTyreWearDelta(orderedSamples),
            tyreWindow?.StartTyre,
            tyreWindow?.EndTyre,
            TryDerivePitWindow(orderedSamples));
    }

    private static double? DeriveAverageSpeed(IReadOnlyList<StoredLapSample> samples)
    {
        var speedSamples = samples
            .Select(sample => sample.SpeedKph)
            .Where(IsPositiveFinite)
            .Select(speed => speed.GetValueOrDefault())
            .ToArray();

        return speedSamples.Length >= MinimumDerivedSampleCount
            ? speedSamples.Average()
            : null;
    }

    private static float? DeriveEndpointConsumption(
        IReadOnlyList<StoredLapSample> samples,
        Func<StoredLapSample, float?> selector)
    {
        var start = selector(samples[0]);
        var end = selector(samples[^1]);
        if (start is not { } startValue
            || end is not { } endValue
            || startValue < 0
            || endValue < 0
            || !float.IsFinite(startValue)
            || !float.IsFinite(endValue))
        {
            return null;
        }

        var delta = startValue - endValue;
        return delta >= 0 && float.IsFinite(delta) ? delta : null;
    }

    private static float? DeriveTyreWearDelta(IReadOnlyList<StoredLapSample> samples)
    {
        var scalarDelta = DeriveEndpointIncrease(samples, sample => sample.TyreWear);
        if (scalarDelta is not null)
        {
            return scalarDelta;
        }

        var wheelDeltas = new[]
        {
            DeriveEndpointIncrease(samples, sample => sample.TyreWearFrontLeft),
            DeriveEndpointIncrease(samples, sample => sample.TyreWearFrontRight),
            DeriveEndpointIncrease(samples, sample => sample.TyreWearRearLeft),
            DeriveEndpointIncrease(samples, sample => sample.TyreWearRearRight)
        };

        return wheelDeltas.All(delta => delta is not null)
            ? wheelDeltas.Average(delta => delta!.Value)
            : null;
    }

    private static float? DeriveEndpointIncrease(
        IReadOnlyList<StoredLapSample> samples,
        Func<StoredLapSample, float?> selector)
    {
        var start = selector(samples[0]);
        var end = selector(samples[^1]);
        if (start is not { } startValue
            || end is not { } endValue
            || startValue < 0
            || endValue < 0
            || !float.IsFinite(startValue)
            || !float.IsFinite(endValue))
        {
            return null;
        }

        var delta = endValue - startValue;
        return delta >= 0 && float.IsFinite(delta) ? delta : null;
    }

    private static (string StartTyre, string EndTyre)? TryDeriveTyreWindow(IReadOnlyList<StoredLapSample> samples)
    {
        if (!TryReadTyreCompound(samples[0], out var startCompound)
            || !TryReadTyreCompound(samples[^1], out var endCompound))
        {
            return null;
        }

        if (!TyreCompoundCompatible(startCompound, endCompound))
        {
            return null;
        }

        foreach (var sample in samples)
        {
            if (!TryReadTyreCompound(sample, out var compound))
            {
                if (sample.VisualTyreCompound is not null || sample.ActualTyreCompound is not null)
                {
                    return null;
                }

                continue;
            }

            if (!TyreCompoundCompatible(compound, startCompound) && !TyreCompoundCompatible(compound, endCompound))
            {
                return null;
            }
        }

        return (FormatRawTyreCompound(startCompound), FormatRawTyreCompound(endCompound));
    }

    private static bool TryReadTyreCompound(StoredLapSample sample, out (int? Visual, int? Actual) compound)
    {
        compound = default;
        var visual = NormalizeCompound(sample.VisualTyreCompound);
        var actual = NormalizeCompound(sample.ActualTyreCompound);
        if ((sample.VisualTyreCompound is not null && visual is null)
            || (sample.ActualTyreCompound is not null && actual is null)
            || (visual is null && actual is null))
        {
            return false;
        }

        compound = (visual, actual);
        return true;
    }

    private static int? NormalizeCompound(int? compound)
    {
        return compound is > 0 and <= byte.MaxValue
            ? compound.Value
            : null;
    }

    private static bool TyreCompoundCompatible((int? Visual, int? Actual) sample, (int? Visual, int? Actual) reference)
    {
        var comparedAnyKnownAxis = false;
        var visualMatches = sample.Visual is null || reference.Visual is null || sample.Visual == reference.Visual;
        var actualMatches = sample.Actual is null || reference.Actual is null || sample.Actual == reference.Actual;
        comparedAnyKnownAxis |= sample.Visual is not null && reference.Visual is not null;
        comparedAnyKnownAxis |= sample.Actual is not null && reference.Actual is not null;
        return comparedAnyKnownAxis && visualMatches && actualMatches;
    }

    private static string FormatRawTyreCompound((int? Visual, int? Actual) compound)
    {
        var parts = new List<string>(capacity: 2);
        if (compound.Visual is not null)
        {
            parts.Add($"V{compound.Visual.Value}");
        }

        if (compound.Actual is not null)
        {
            parts.Add($"A{compound.Actual.Value}");
        }

        return string.Join(" / ", parts);
    }

    private static string? TryDerivePitWindow(IReadOnlyList<StoredLapSample> samples)
    {
        if (!TryReadPitStatus(samples[0].PitStatus, out var startPitStatus)
            || !TryReadPitStatus(samples[^1].PitStatus, out var endPitStatus))
        {
            return null;
        }

        var transitionCount = 0;
        var previousPitStatus = startPitStatus;
        foreach (var sample in samples)
        {
            if (!TryReadPitStatus(sample.PitStatus, out var pitStatus))
            {
                return null;
            }

            if (pitStatus != previousPitStatus)
            {
                transitionCount++;
                previousPitStatus = pitStatus;
            }

            if ((pitStatus != startPitStatus && pitStatus != endPitStatus) || transitionCount > 1)
            {
                return null;
            }
        }

        return $"{PitStatusFormatter.Format(startPitStatus, null)} -> {PitStatusFormatter.Format(endPitStatus, null)}";
    }

    private static bool TryReadPitStatus(int? pitStatus, out byte normalizedPitStatus)
    {
        if (pitStatus is >= 0 and <= byte.MaxValue)
        {
            normalizedPitStatus = (byte)pitStatus.Value;
            return normalizedPitStatus <= 2;
        }

        normalizedPitStatus = 0;
        return false;
    }

    private static string SelectStoredTyre(string? storedTyre, string? derivedTyre)
    {
        return IsUsableStoredTyre(storedTyre)
            ? storedTyre!.Trim()
            : IsUsableStoredTyre(derivedTyre)
                ? derivedTyre!.Trim()
                : "-";
    }

    private static bool IsUsableStoredTyre(string? tyre)
    {
        return !string.IsNullOrWhiteSpace(tyre) && tyre.Trim() != "-" && tyre.Any(char.IsDigit);
    }

    private static bool IsPositiveFinite(double? value)
    {
        return value is { } number && number > 0 && double.IsFinite(number);
    }

    private static bool IsFiniteNonNegative(float? value)
    {
        return value is { } number && number >= 0 && float.IsFinite(number);
    }

    private static string FormatStoredTyreWindow(string? startTyre, string? endTyre)
    {
        var startText = FormatStoredTyre(startTyre);
        var endText = FormatStoredTyre(endTyre);

        return startText == "-" && endText == "-"
            ? "-"
            : $"{startText} -> {endText}";
    }

    private static string FormatStoredTyre(string? tyre)
    {
        if (string.IsNullOrWhiteSpace(tyre) || tyre.Trim() == "-" || !tyre.Any(char.IsDigit))
        {
            return "-";
        }

        return TyreCompoundFormatter.FormatRawCompoundText(tyre);
    }
}
