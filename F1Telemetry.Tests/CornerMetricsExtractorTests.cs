using F1Telemetry.Analytics.Corners;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.Tracks;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies V3 corner metrics extraction from lap samples.
/// </summary>
public sealed class CornerMetricsExtractorTests
{
    /// <summary>
    /// Verifies the extractor computes speed, input, timing, and reference loss for a corner segment.
    /// </summary>
    [Fact]
    public void Extract_WithReferenceLap_ComputesCornerMetrics()
    {
        var extractor = new CornerMetricsExtractor();
        var map = CreateEstimatedMap();

        var result = extractor.Extract(
            map,
            new[]
            {
                CreateSample(90f, 9_800, 210, 0.80, 0.00, 0.02f),
                CreateSample(100f, 10_000, 180, 0.10, 0.30, 0.10f),
                CreateSample(130f, 10_400, 130, 0.00, 0.90, 0.50f),
                CreateSample(160f, 11_000, 95, 0.20, 0.40, -0.80f),
                CreateSample(180f, 11_600, 125, 0.55, 0.05, 0.40f),
                CreateSample(200f, 12_300, 170, 0.90, 0.00, 0.05f),
                CreateSample(230f, 12_900, 205, 0.95, 0.00, 0.01f)
            },
            new[]
            {
                CreateSample(100f, 10_000, 182, 0.10, 0.25, 0.08f),
                CreateSample(130f, 10_300, 135, 0.00, 0.85, 0.45f),
                CreateSample(160f, 10_800, 102, 0.22, 0.30, -0.70f),
                CreateSample(180f, 11_300, 132, 0.60, 0.05, 0.35f),
                CreateSample(200f, 11_900, 176, 0.92, 0.00, 0.04f)
            });

        var corner = Assert.Single(result.Corners);
        Assert.Equal(TrackSegmentMapStatus.Estimated, result.MapStatus);
        Assert.Equal(ConfidenceLevel.Low, result.Confidence);
        Assert.Contains(DataQualityWarning.EstimatedTrackMap, result.Warnings);
        Assert.Equal(180d, corner.EntrySpeedKph);
        Assert.Equal(95d, corner.MinSpeedKph);
        Assert.Equal(170d, corner.ExitSpeedKph);
        Assert.Equal(0.90d, corner.MaxBrake);
        Assert.Equal(180f, corner.ThrottleReapplyDistanceMeters);
        Assert.Equal(0.80d, corner.MaxSteering!.Value, precision: 6);
        Assert.Equal(2_300, corner.SegmentTimeInMs);
        Assert.Equal(1_900, corner.ReferenceSegmentTimeInMs);
        Assert.Equal(400, corner.TimeLossToReferenceInMs);
    }

    /// <summary>
    /// Verifies unsupported maps do not throw and expose unsupported status and warnings.
    /// </summary>
    [Fact]
    public void Extract_UnsupportedTrack_ReturnsWarningResult()
    {
        var extractor = new CornerMetricsExtractor();
        var map = TrackSegmentMap.CreateUnsupported(44, "Track id 44 is not mapped.");

        var result = extractor.Extract(map, new[] { CreateSample(120f, 10_000, 180, 0.5, 0.1, 0.1f) });

        Assert.Equal(TrackSegmentMapStatus.Unsupported, result.MapStatus);
        Assert.Equal(ConfidenceLevel.Unknown, result.Confidence);
        Assert.Empty(result.Corners);
        Assert.Contains(DataQualityWarning.UnsupportedTrack, result.Warnings);
        Assert.Contains(DataQualityWarning.MissingReferenceLap, result.Warnings);
    }

    /// <summary>
    /// Verifies sparse corner samples retain output but lower confidence.
    /// </summary>
    [Fact]
    public void Extract_LowDensityWithoutReference_ReturnsWarnings()
    {
        var extractor = new CornerMetricsExtractor();

        var result = extractor.Extract(
            CreateEstimatedMap(),
            new[] { CreateSample(140f, 10_500, 120, 0.2, 0.7, -0.6f) });

        var corner = Assert.Single(result.Corners);
        Assert.Equal(ConfidenceLevel.Low, result.Confidence);
        Assert.Equal(ConfidenceLevel.Low, corner.Confidence);
        Assert.Null(corner.SegmentTimeInMs);
        Assert.Null(corner.TimeLossToReferenceInMs);
        Assert.Contains(DataQualityWarning.LowSampleDensity, result.Warnings);
        Assert.Contains(DataQualityWarning.MissingReferenceLap, result.Warnings);
        Assert.Contains(DataQualityWarning.EstimatedTrackMap, corner.Warnings);
    }

    /// <summary>
    /// Verifies missing lap samples return a warning result without corner summaries.
    /// </summary>
    [Fact]
    public void Extract_MissingSamples_ReturnsWarningResult()
    {
        var extractor = new CornerMetricsExtractor();

        var result = extractor.Extract(CreateEstimatedMap(), Array.Empty<LapSample>());

        Assert.Empty(result.Corners);
        Assert.Equal(ConfidenceLevel.Unknown, result.Confidence);
        Assert.Contains(DataQualityWarning.MissingSamples, result.Warnings);
        Assert.Contains(DataQualityWarning.MissingReferenceLap, result.Warnings);
        Assert.Contains(DataQualityWarning.EstimatedTrackMap, result.Warnings);
    }

    private static TrackSegmentMap CreateEstimatedMap()
    {
        var segment = new TrackSegment
        {
            SegmentId = "test-corner",
            Name = "Test Corner",
            SegmentType = TrackSegmentType.Corner,
            CornerNumber = 1,
            StartDistanceMeters = 100f,
            EndDistanceMeters = 200f,
            Confidence = ConfidenceLevel.Low,
            Warnings = new[] { DataQualityWarning.EstimatedTrackMap }
        };

        return new TrackSegmentMap
        {
            TrackId = 99,
            TrackName = "Test Circuit",
            Status = TrackSegmentMapStatus.Estimated,
            StatusReason = "Estimated test map.",
            LapLengthMeters = 1_000f,
            Segments = new[] { segment },
            Confidence = ConfidenceLevel.Low,
            Warnings = new[] { DataQualityWarning.EstimatedTrackMap }
        };
    }

    private static LapSample CreateSample(
        float lapDistance,
        uint currentLapTimeInMs,
        double speedKph,
        double throttle,
        double brake,
        float steering)
    {
        return new LapSample
        {
            SampledAt = DateTimeOffset.UtcNow,
            FrameIdentifier = (uint)currentLapTimeInMs,
            LapNumber = 3,
            LapDistance = lapDistance,
            CurrentLapTimeInMs = currentLapTimeInMs,
            SpeedKph = speedKph,
            Throttle = throttle,
            Brake = brake,
            Steering = steering,
            IsValid = true
        };
    }
}
