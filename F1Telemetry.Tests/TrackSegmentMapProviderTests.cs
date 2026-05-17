using F1Telemetry.Analytics.Tracks;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the static V3 track segment map provider behavior.
/// </summary>
public sealed class TrackSegmentMapProviderTests
{
    /// <summary>
    /// Verifies the first supported track ids return stable estimated maps.
    /// </summary>
    /// <param name="trackId">The supported F1 game track id.</param>
    /// <param name="expectedTrackName">The expected track name.</param>
    [Theory]
    [InlineData(0, "Australia")]
    [InlineData(2, "Shanghai")]
    [InlineData(13, "Suzuka")]
    public void GetMap_SupportedTrack_ReturnsEstimatedMap(int trackId, string expectedTrackName)
    {
        var provider = new StaticTrackSegmentMapProvider();

        var map = provider.GetMap((sbyte)trackId);

        Assert.Equal((sbyte)trackId, map.TrackId);
        Assert.Equal(expectedTrackName, map.TrackName);
        Assert.Equal(TrackSegmentMapStatus.Estimated, map.Status);
        Assert.Equal(ConfidenceLevel.Low, map.Confidence);
        Assert.Contains(DataQualityWarning.EstimatedTrackMap, map.Warnings);
        Assert.NotEmpty(map.Segments);
        Assert.All(map.Segments, segment =>
        {
            Assert.NotEmpty(segment.SegmentId);
            Assert.NotEmpty(segment.Name);
            Assert.True(segment.EndDistanceMeters > segment.StartDistanceMeters);
            Assert.Equal(ConfidenceLevel.Low, segment.Confidence);
            Assert.Contains(DataQualityWarning.EstimatedTrackMap, segment.Warnings);
        });
    }

    /// <summary>
    /// Verifies an unsupported track id returns a non-throwing fallback map.
    /// </summary>
    [Fact]
    public void GetMap_UnsupportedTrack_ReturnsUnsupportedFallback()
    {
        var provider = new StaticTrackSegmentMapProvider();

        var map = provider.GetMap(44);

        Assert.Equal((sbyte)44, map.TrackId);
        Assert.Equal(TrackSegmentMapStatus.Unsupported, map.Status);
        Assert.Equal(ConfidenceLevel.Unknown, map.Confidence);
        Assert.Empty(map.Segments);
        Assert.Contains(DataQualityWarning.UnsupportedTrack, map.Warnings);
        Assert.Contains("not supported", map.StatusReason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies a missing track id returns a non-throwing unsupported map with a reason.
    /// </summary>
    [Fact]
    public void GetMap_MissingTrackId_ReturnsUnsupportedFallback()
    {
        var provider = new StaticTrackSegmentMapProvider();

        var map = provider.GetMap(null);

        Assert.Null(map.TrackId);
        Assert.Equal(TrackSegmentMapStatus.Unsupported, map.Status);
        Assert.Empty(map.Segments);
        Assert.Contains(DataQualityWarning.UnsupportedTrack, map.Warnings);
        Assert.Contains("No track id", map.StatusReason, StringComparison.OrdinalIgnoreCase);
    }
}
