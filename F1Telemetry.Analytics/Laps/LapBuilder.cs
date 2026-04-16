namespace F1Telemetry.Analytics.Laps;

/// <summary>
/// Accumulates player lap samples until a completed lap summary can be produced.
/// </summary>
public sealed class LapBuilder
{
    private readonly List<LapSample> _samples;

    /// <summary>
    /// Initializes a new builder for the specified opening sample.
    /// </summary>
    /// <param name="openingSample">The first sample for the lap.</param>
    /// <param name="shouldEmitWhenClosed">Whether the lap should be emitted when it closes.</param>
    public LapBuilder(LapSample openingSample, bool shouldEmitWhenClosed)
    {
        ArgumentNullException.ThrowIfNull(openingSample);
        _samples = new List<LapSample> { openingSample };
        ShouldEmitWhenClosed = shouldEmitWhenClosed;
    }

    /// <summary>
    /// Gets the lap number represented by the builder.
    /// </summary>
    public int LapNumber => _samples[0].LapNumber;

    /// <summary>
    /// Gets a value indicating whether the lap should produce a summary when it closes.
    /// </summary>
    public bool ShouldEmitWhenClosed { get; }

    /// <summary>
    /// Gets the number of samples captured for the lap.
    /// </summary>
    public int SampleCount => _samples.Count;

    /// <summary>
    /// Gets the first sample in the lap.
    /// </summary>
    public LapSample FirstSample => _samples[0];

    /// <summary>
    /// Gets the latest sample in the lap.
    /// </summary>
    public LapSample LastSample => _samples[^1];

    /// <summary>
    /// Adds a new sample to the lap.
    /// </summary>
    /// <param name="sample">The sample to append.</param>
    public void AddSample(LapSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        _samples.Add(sample);
    }

    /// <summary>
    /// Builds a provisional summary for the lap.
    /// </summary>
    /// <param name="closedAt">The time when the lap was closed.</param>
    /// <param name="closingSample">The sample that triggered the lap closure.</param>
    public LapSummary BuildSummary(DateTimeOffset closedAt, LapSample? closingSample)
    {
        var first = FirstSample;
        var last = LastSample;
        var speedSamples = _samples
            .Where(sample => sample.SpeedKph is not null)
            .Select(sample => sample.SpeedKph!.Value)
            .ToArray();

        return new LapSummary
        {
            LapNumber = LapNumber,
            LapTimeInMs = closingSample?.LastLapTimeInMs ?? last.CurrentLapTimeInMs,
            AverageSpeedKph = speedSamples.Length == 0 ? null : speedSamples.Average(),
            FuelUsed = ComputePositiveDelta(first.FuelRemaining, last.FuelRemaining),
            ErsUsed = ComputePositiveDelta(first.ErsStoreEnergy, last.ErsStoreEnergy),
            TyreWearDelta = ComputePositiveDelta(last.TyreWear, first.TyreWear),
            IsValid = _samples.All(sample => sample.IsValid),
            StartTyre = FormatTyre(first),
            EndTyre = FormatTyre(last),
            StartedInPit = IsPitSample(first),
            EndedInPit = IsPitSample(last),
            ClosedAt = closedAt
        };
    }

    private static float? ComputePositiveDelta(float? start, float? end)
    {
        if (start is null || end is null)
        {
            return null;
        }

        return Math.Max(0f, start.Value - end.Value);
    }

    private static bool IsPitSample(LapSample sample)
    {
        return sample.PitStatus is > 0;
    }

    private static string FormatTyre(LapSample sample)
    {
        if (sample.VisualTyreCompound is null && sample.ActualTyreCompound is null)
        {
            return "-";
        }

        return $"V{sample.VisualTyreCompound?.ToString() ?? "-"} / A{sample.ActualTyreCompound?.ToString() ?? "-"}";
    }
}
