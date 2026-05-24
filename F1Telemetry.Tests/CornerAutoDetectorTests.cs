using F1Telemetry.Analytics.Corners;
using F1Telemetry.Analytics.Laps;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies telemetry-driven corner candidate detection.
/// </summary>
public sealed class CornerAutoDetectorTests
{
    /// <summary>
    /// Verifies Monza-style samples produce the expected major corner candidates.
    /// </summary>
    [Fact]
    public void Detect_MonzaStyleSamples_GeneratesExpectedCornerWindows()
    {
        var detector = new CornerAutoDetector();

        var candidates = detector.Detect(11, 5_798f, CreateMonzaStyleSamples());

        Assert.Contains(candidates, candidate => candidate.SegmentId == "auto-mon-t1-t2" && candidate.DisplayName == "Prima Variante");
        Assert.Contains(candidates, candidate => candidate.SegmentId == "auto-mon-t4-t5" && candidate.DisplayName == "Seconda Variante");
        Assert.Contains(candidates, candidate => candidate.SegmentId == "auto-mon-lesmo" && candidate.DisplayName == "Lesmo");
        Assert.Contains(candidates, candidate => candidate.SegmentId == "auto-mon-ascari" && candidate.DisplayName == "Ascari");
        Assert.Contains(candidates, candidate => candidate.SegmentId == "auto-mon-parabolica" && candidate.DisplayName == "Parabolica");
        Assert.All(candidates, candidate => Assert.InRange(candidate.StartDistanceMeters, 0, 5_798f));
        Assert.All(candidates, candidate => Assert.InRange(candidate.EndDistanceMeters, 0, 5_798f));
    }

    private static IReadOnlyList<LapSample> CreateMonzaStyleSamples()
    {
        return new[]
        {
            CreateSample(480f, 318, 0.90, 0.01, 0.04f),
            CreateSample(560f, 304, 0.10, 0.94, 0.35f),
            CreateSample(720f, 86, 0.20, 0.60, -0.70f),
            CreateSample(910f, 168, 0.82, 0.02, 0.18f),
            CreateSample(1_860f, 305, 0.16, 0.88, 0.26f),
            CreateSample(2_060f, 124, 0.42, 0.24, -0.48f),
            CreateSample(2_300f, 212, 0.90, 0.01, 0.12f),
            CreateSample(2_520f, 268, 0.24, 0.52, 0.72f),
            CreateSample(2_760f, 188, 0.72, 0.03, -0.64f),
            CreateSample(3_020f, 214, 0.88, 0.00, 0.40f),
            CreateSample(4_060f, 300, 0.12, 0.82, 0.46f),
            CreateSample(4_340f, 172, 0.55, 0.10, -0.56f),
            CreateSample(4_660f, 238, 0.86, 0.00, 0.34f),
            CreateSample(5_080f, 320, 0.18, 0.68, 0.58f),
            CreateSample(5_400f, 196, 0.64, 0.05, -0.66f),
            CreateSample(5_680f, 228, 0.92, 0.00, 0.36f)
        };
    }

    private static LapSample CreateSample(float distance, double speed, double throttle, double brake, float steering)
    {
        return new LapSample
        {
            LapNumber = 4,
            LapDistance = distance,
            SpeedKph = speed,
            Throttle = throttle,
            Brake = brake,
            Steering = steering,
            IsValid = true
        };
    }
}
