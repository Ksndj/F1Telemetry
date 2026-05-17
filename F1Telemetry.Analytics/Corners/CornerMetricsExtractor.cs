using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.Tracks;

namespace F1Telemetry.Analytics.Corners;

/// <summary>
/// Extracts corner-level speed, input, and timing metrics from lap samples.
/// </summary>
public sealed class CornerMetricsExtractor
{
    private const int MinimumSamplesPerCorner = 3;
    private const double ThrottleReapplyThreshold = 0.5d;
    private const double BrakeReleaseThreshold = 0.15d;

    /// <summary>
    /// Extracts corner metrics from the supplied lap samples and segment map.
    /// </summary>
    /// <param name="trackMap">The track segment map to evaluate.</param>
    /// <param name="lapSamples">The lap samples to summarize.</param>
    /// <param name="referenceLapSamples">Optional reference or best-lap samples for time-loss comparison.</param>
    /// <returns>A corner metrics result with summaries, confidence, and data quality warnings.</returns>
    public CornerMetricsResult Extract(
        TrackSegmentMap trackMap,
        IReadOnlyList<LapSample> lapSamples,
        IReadOnlyList<LapSample>? referenceLapSamples = null)
    {
        ArgumentNullException.ThrowIfNull(trackMap);
        ArgumentNullException.ThrowIfNull(lapSamples);

        var resultWarnings = CreateWarningSet(trackMap.Warnings);
        if (trackMap.Status == TrackSegmentMapStatus.Unsupported)
        {
            resultWarnings.Add(DataQualityWarning.UnsupportedTrack);
        }

        if (lapSamples.Count == 0)
        {
            resultWarnings.Add(DataQualityWarning.MissingSamples);
        }

        var hasReference = referenceLapSamples is { Count: > 0 };
        if (!hasReference)
        {
            resultWarnings.Add(DataQualityWarning.MissingReferenceLap);
        }

        if (trackMap.Status == TrackSegmentMapStatus.Unsupported || lapSamples.Count == 0)
        {
            return CreateResult(trackMap, Array.Empty<CornerSummary>(), resultWarnings);
        }

        var orderedSamples = GetSamplesWithDistance(lapSamples, resultWarnings);
        if (orderedSamples.Length == 0)
        {
            resultWarnings.Add(DataQualityWarning.MissingSamples);
            return CreateResult(trackMap, Array.Empty<CornerSummary>(), resultWarnings);
        }

        var referenceWarnings = new HashSet<DataQualityWarning>();
        var orderedReferenceSamples = hasReference
            ? GetSamplesWithDistance(referenceLapSamples!, referenceWarnings)
            : Array.Empty<LapSample>();

        if (hasReference && orderedReferenceSamples.Length == 0)
        {
            resultWarnings.Add(DataQualityWarning.MissingReferenceLap);
        }

        var summaries = new List<CornerSummary>();
        foreach (var segment in trackMap.Segments.Where(IsCornerSegment))
        {
            var segmentSamples = orderedSamples
                .Where(sample => segment.ContainsDistance(sample.LapDistance!.Value))
                .OrderBy(sample => sample.LapDistance)
                .ToArray();
            var referenceSegmentSamples = orderedReferenceSamples
                .Where(sample => segment.ContainsDistance(sample.LapDistance!.Value))
                .OrderBy(sample => sample.LapDistance)
                .ToArray();

            var summary = BuildSummary(segment, segmentSamples, referenceSegmentSamples, hasReference, trackMap.Warnings);
            summaries.Add(summary);
            foreach (var warning in summary.Warnings)
            {
                resultWarnings.Add(warning);
            }
        }

        return CreateResult(trackMap, summaries, resultWarnings);
    }

    private static CornerSummary BuildSummary(
        TrackSegment segment,
        IReadOnlyList<LapSample> segmentSamples,
        IReadOnlyList<LapSample> referenceSegmentSamples,
        bool hasReference,
        IReadOnlyList<DataQualityWarning> mapWarnings)
    {
        var warnings = CreateWarningSet(mapWarnings);
        foreach (var warning in segment.Warnings)
        {
            warnings.Add(warning);
        }

        if (segmentSamples.Count == 0)
        {
            warnings.Add(DataQualityWarning.MissingSamples);
        }

        if (segmentSamples.Count > 0 && segmentSamples.Count < MinimumSamplesPerCorner)
        {
            warnings.Add(DataQualityWarning.LowSampleDensity);
        }

        if (!hasReference || referenceSegmentSamples.Count == 0)
        {
            warnings.Add(DataQualityWarning.MissingReferenceLap);
        }
        else if (referenceSegmentSamples.Count < MinimumSamplesPerCorner)
        {
            warnings.Add(DataQualityWarning.LowSampleDensity);
        }

        if (segmentSamples.Any(sample => sample.SpeedKph is null))
        {
            warnings.Add(DataQualityWarning.MissingSpeedSamples);
        }

        if (segmentSamples.Any(sample => sample.CurrentLapTimeInMs is null))
        {
            warnings.Add(DataQualityWarning.MissingTimingSamples);
        }

        if (segmentSamples.Any(sample => sample.Throttle is null))
        {
            warnings.Add(DataQualityWarning.MissingThrottleSamples);
        }

        if (segmentSamples.Any(sample => sample.Brake is null))
        {
            warnings.Add(DataQualityWarning.MissingBrakeSamples);
        }

        if (segmentSamples.Any(sample => sample.Steering is null))
        {
            warnings.Add(DataQualityWarning.MissingSteeringSamples);
        }

        var segmentTime = ComputeSegmentTime(segmentSamples);
        var referenceSegmentTime = ComputeSegmentTime(referenceSegmentSamples);
        var timeLoss = segmentTime is null || referenceSegmentTime is null
            ? (int?)null
            : segmentTime.Value - referenceSegmentTime.Value;

        return new CornerSummary
        {
            Segment = segment,
            EntrySpeedKph = segmentSamples.FirstOrDefault()?.SpeedKph,
            MinSpeedKph = MinOrNull(segmentSamples.Select(sample => sample.SpeedKph)),
            ExitSpeedKph = segmentSamples.LastOrDefault()?.SpeedKph,
            MaxBrake = MaxOrNull(segmentSamples.Select(sample => sample.Brake)),
            ThrottleReapplyDistanceMeters = FindThrottleReapplyDistance(segmentSamples),
            MaxSteering = ComputeMaxSteering(segmentSamples),
            SegmentTimeInMs = segmentTime,
            ReferenceSegmentTimeInMs = referenceSegmentTime,
            TimeLossToReferenceInMs = timeLoss,
            Confidence = ResolveConfidence(warnings),
            Warnings = warnings.OrderBy(warning => warning).ToArray()
        };
    }

    private static CornerMetricsResult CreateResult(
        TrackSegmentMap trackMap,
        IReadOnlyList<CornerSummary> summaries,
        HashSet<DataQualityWarning> warnings)
    {
        return new CornerMetricsResult
        {
            TrackId = trackMap.TrackId,
            TrackName = trackMap.TrackName,
            MapStatus = trackMap.Status,
            Confidence = ResolveConfidence(warnings),
            Corners = summaries,
            Warnings = warnings.OrderBy(warning => warning).ToArray()
        };
    }

    private static LapSample[] GetSamplesWithDistance(
        IReadOnlyList<LapSample> samples,
        HashSet<DataQualityWarning> warnings)
    {
        if (samples.Any(sample => sample.LapDistance is null))
        {
            warnings.Add(DataQualityWarning.MissingLapDistance);
        }

        return samples
            .Where(sample => sample.LapDistance is not null)
            .OrderBy(sample => sample.LapDistance)
            .ToArray();
    }

    private static int? ComputeSegmentTime(IReadOnlyList<LapSample> samples)
    {
        if (samples.Count < 2)
        {
            return null;
        }

        var first = samples.First().CurrentLapTimeInMs;
        var last = samples.Last().CurrentLapTimeInMs;
        if (first is null || last is null || last.Value < first.Value)
        {
            return null;
        }

        return checked((int)(last.Value - first.Value));
    }

    private static float? FindThrottleReapplyDistance(IReadOnlyList<LapSample> samples)
    {
        if (samples.Count == 0)
        {
            return null;
        }

        var minSpeedIndex = -1;
        var minSpeed = double.MaxValue;
        for (var index = 0; index < samples.Count; index++)
        {
            var speed = samples[index].SpeedKph;
            if (speed is not null && speed.Value < minSpeed)
            {
                minSpeed = speed.Value;
                minSpeedIndex = index;
            }
        }

        var startIndex = minSpeedIndex >= 0 ? minSpeedIndex : 0;
        for (var index = startIndex; index < samples.Count; index++)
        {
            var sample = samples[index];
            if (sample.Throttle is >= ThrottleReapplyThreshold
                && (sample.Brake ?? 0d) <= BrakeReleaseThreshold
                && sample.LapDistance is not null)
            {
                return sample.LapDistance.Value;
            }
        }

        return null;
    }

    private static double? ComputeMaxSteering(IReadOnlyList<LapSample> samples)
    {
        var steeringValues = samples
            .Select(sample => sample.Steering)
            .Where(steering => steering is not null)
            .Select(steering => Math.Abs((double)steering!.Value))
            .ToArray();

        return steeringValues.Length == 0 ? null : steeringValues.Max();
    }

    private static double? MinOrNull(IEnumerable<double?> values)
    {
        var concreteValues = values
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();

        return concreteValues.Length == 0 ? null : concreteValues.Min();
    }

    private static double? MaxOrNull(IEnumerable<double?> values)
    {
        var concreteValues = values
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();

        return concreteValues.Length == 0 ? null : concreteValues.Max();
    }

    private static bool IsCornerSegment(TrackSegment segment)
    {
        return segment.SegmentType is TrackSegmentType.Corner
            or TrackSegmentType.CornerComplex
            or TrackSegmentType.Chicane;
    }

    private static ConfidenceLevel ResolveConfidence(IReadOnlyCollection<DataQualityWarning> warnings)
    {
        if (warnings.Contains(DataQualityWarning.UnsupportedTrack)
            || warnings.Contains(DataQualityWarning.MissingSamples))
        {
            return ConfidenceLevel.Unknown;
        }

        if (warnings.Contains(DataQualityWarning.EstimatedTrackMap)
            || warnings.Contains(DataQualityWarning.LowSampleDensity)
            || warnings.Contains(DataQualityWarning.MissingLapDistance)
            || warnings.Contains(DataQualityWarning.MissingTimingSamples)
            || warnings.Contains(DataQualityWarning.MissingSpeedSamples)
            || warnings.Contains(DataQualityWarning.MissingThrottleSamples)
            || warnings.Contains(DataQualityWarning.MissingBrakeSamples)
            || warnings.Contains(DataQualityWarning.MissingSteeringSamples))
        {
            return ConfidenceLevel.Low;
        }

        return warnings.Contains(DataQualityWarning.MissingReferenceLap)
            ? ConfidenceLevel.Medium
            : ConfidenceLevel.High;
    }

    private static HashSet<DataQualityWarning> CreateWarningSet(IEnumerable<DataQualityWarning> warnings)
    {
        return new HashSet<DataQualityWarning>(warnings);
    }
}
