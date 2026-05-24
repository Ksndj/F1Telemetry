using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.Tracks;

namespace F1Telemetry.Analytics.Corners;

/// <summary>
/// Detects broad corner windows from lap-distance, speed, brake, throttle, and steering evidence.
/// </summary>
public sealed class CornerAutoDetector
{
    private const float DefaultMonzaLapLengthMeters = 5_798f;
    private const double BrakeEvidenceThreshold = 0.35d;
    private const double ThrottleEvidenceThreshold = 0.55d;
    private const float SteeringEvidenceThreshold = 0.30f;

    private static readonly IReadOnlyList<CornerCalibration> MonzaCalibrations =
    [
        new("auto-mon-t1-t2", "Prima Variante", 1, "T1/T2", TrackSegmentType.Chicane, 520f, 940f),
        new("auto-mon-t4-t5", "Seconda Variante", 4, "T4/T5", TrackSegmentType.Chicane, 1_920f, 2_300f),
        new("auto-mon-lesmo", "Lesmo", 6, "Lesmo", TrackSegmentType.CornerComplex, 2_560f, 3_060f),
        new("auto-mon-ascari", "Ascari", 8, "Ascari", TrackSegmentType.Chicane, 4_120f, 4_660f),
        new("auto-mon-parabolica", "Parabolica", 11, "Parabolica", TrackSegmentType.Corner, 5_120f, 5_680f)
    ];

    /// <summary>
    /// Detects corner candidates for a lap.
    /// </summary>
    /// <param name="trackId">The F1 25 track id.</param>
    /// <param name="lapLengthMeters">The lap length in metres.</param>
    /// <param name="samples">The completed-lap samples.</param>
    /// <returns>Detected broad corner windows.</returns>
    public IReadOnlyList<DetectedCornerCandidate> Detect(
        sbyte? trackId,
        float? lapLengthMeters,
        IReadOnlyList<LapSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (trackId != 11 || samples.Count == 0)
        {
            return Array.Empty<DetectedCornerCandidate>();
        }

        var length = lapLengthMeters is > 0 ? lapLengthMeters.Value : DefaultMonzaLapLengthMeters;
        var normalizedSamples = samples
            .Where(sample => sample.LapDistance is not null && float.IsFinite(sample.LapDistance.Value))
            .Select(sample => sample with
            {
                LapDistance = LapDistanceNormalizer.Normalize(sample.LapDistance!.Value, length)
            })
            .OrderBy(sample => sample.LapDistance)
            .ToArray();
        if (normalizedSamples.Length == 0)
        {
            return Array.Empty<DetectedCornerCandidate>();
        }

        var candidates = new List<DetectedCornerCandidate>();
        foreach (var calibration in MonzaCalibrations)
        {
            var windowSamples = normalizedSamples
                .Where(sample => sample.LapDistance is not null &&
                    sample.LapDistance.Value >= calibration.StartDistanceMeters &&
                    sample.LapDistance.Value <= calibration.EndDistanceMeters)
                .ToArray();
            if (!HasCornerEvidence(windowSamples))
            {
                continue;
            }

            candidates.Add(CreateCandidate(calibration, windowSamples));
        }

        return candidates;
    }

    private static bool HasCornerEvidence(IReadOnlyList<LapSample> samples)
    {
        if (samples.Count == 0)
        {
            return false;
        }

        return samples.Any(sample => sample.Brake is >= BrakeEvidenceThreshold) ||
               samples.Any(sample => sample.Throttle is >= ThrottleEvidenceThreshold) ||
               samples.Any(sample => Math.Abs(sample.Steering ?? 0f) >= SteeringEvidenceThreshold);
    }

    private static DetectedCornerCandidate CreateCandidate(
        CornerCalibration calibration,
        IReadOnlyList<LapSample> samples)
    {
        var hasStrongEvidence = samples.Any(sample => sample.Brake is >= BrakeEvidenceThreshold) &&
                                samples.Any(sample => sample.Throttle is >= ThrottleEvidenceThreshold) &&
                                samples.Any(sample => Math.Abs(sample.Steering ?? 0f) >= SteeringEvidenceThreshold);

        return new DetectedCornerCandidate
        {
            SegmentId = calibration.SegmentId,
            DisplayName = calibration.DisplayName,
            CornerNumber = calibration.CornerNumber,
            CornerLabel = calibration.CornerLabel,
            SegmentType = calibration.SegmentType,
            StartDistanceMeters = calibration.StartDistanceMeters,
            EndDistanceMeters = calibration.EndDistanceMeters,
            Confidence = hasStrongEvidence ? ConfidenceLevel.Medium : ConfidenceLevel.Low,
            Warnings = new[] { DataQualityWarning.EstimatedTrackMap }
        };
    }

    private sealed record CornerCalibration(
        string SegmentId,
        string DisplayName,
        int CornerNumber,
        string CornerLabel,
        TrackSegmentType SegmentType,
        float StartDistanceMeters,
        float EndDistanceMeters);
}
