using System.IO;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.Tracks;
using F1Telemetry.App.TrackMaps;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies Motion-based track-map generation for the corner analysis page.
/// </summary>
public sealed class TrackMapBuilderTests
{
    /// <summary>
    /// Verifies Motion X/Z points are normalized into the UI range.
    /// </summary>
    [Fact]
    public void BuildSnapshot_WithMotionPoints_NormalizesToUnitRange()
    {
        var snapshot = new TrackMapBuilder().BuildSnapshot("uid-1", 0, 3, CreateRectangleSamples(3, 40, 100, 50));

        Assert.True(snapshot.HasDrawableMap);
        Assert.All(snapshot.Points, point =>
        {
            Assert.InRange(point.NormalizedX, 0d, 1d);
            Assert.InRange(point.NormalizedY, 0d, 1d);
        });
    }

    /// <summary>
    /// Verifies normalization preserves the track outline aspect ratio.
    /// </summary>
    [Fact]
    public void BuildSnapshot_WithWideMotionPoints_PreservesAspectRatio()
    {
        var snapshot = new TrackMapBuilder().BuildSnapshot("uid-1", 0, 3, CreateRectangleSamples(3, 40, 100, 50));

        var normalizedWidth = snapshot.Points.Max(point => point.NormalizedX) - snapshot.Points.Min(point => point.NormalizedX);
        var normalizedHeight = snapshot.Points.Max(point => point.NormalizedY) - snapshot.Points.Min(point => point.NormalizedY);
        var ratio = normalizedWidth / normalizedHeight;

        Assert.InRange(ratio, 1.95d, 2.05d);
    }

    /// <summary>
    /// Verifies sparse Motion data returns a clear empty state.
    /// </summary>
    [Fact]
    public void BuildSnapshot_WithTooFewMotionPoints_ReturnsSamplingEmptyState()
    {
        var snapshot = new TrackMapBuilder().BuildSnapshot("uid-1", 0, 3, CreateRectangleSamples(3, 4, 100, 50));

        Assert.False(snapshot.HasDrawableMap);
        Assert.Equal(TrackMapStatus.InsufficientTrackPoints, snapshot.Status);
        Assert.Equal("轨迹采样不足，暂无法绘制", snapshot.WarningText);
    }

    /// <summary>
    /// Verifies samples without Motion coordinates produce the historical missing-Motion state.
    /// </summary>
    [Fact]
    public void BuildSnapshot_WithoutMotionCoordinates_ReturnsMissingMotionState()
    {
        var snapshot = new TrackMapBuilder().BuildSnapshot("uid-1", 0, 3, CreateSamplesWithoutMotion(3, 12));

        Assert.False(snapshot.HasDrawableMap);
        Assert.Equal(TrackMapStatus.MissingMotionData, snapshot.Status);
        Assert.Equal("该会话缺少 Motion 坐标", snapshot.WarningText);
    }

    /// <summary>
    /// Verifies the trajectory store falls back to the most complete lap when the reference lap has no drawable map.
    /// </summary>
    [Fact]
    public void GetPreferredOrBest_WhenReferenceHasNoTrack_FallsBackToMostCompleteLap()
    {
        var store = new InMemoryTrackMapTrajectoryStore();
        store.RecordCompletedLap("uid-1", 0, 2, CreateRectangleSamples(2, 4, 100, 50));
        store.RecordCompletedLap("uid-1", 0, 4, CreateRectangleSamples(4, 80, 120, 80));

        var snapshot = store.GetPreferredOrBest("uid-1", 0, 2);

        Assert.Equal(4, snapshot.LapNumber);
        Assert.True(snapshot.HasDrawableMap);
        Assert.Equal(TrackMapStatus.Ready, snapshot.Status);
        Assert.Contains("采样最完整圈", snapshot.WarningText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies historical sessions without recorded Motion snapshots do not keep waiting for live data.
    /// </summary>
    [Fact]
    public void GetPreferredOrBest_WhenHistoricalSessionHasNoMotion_ReturnsMissingMotionState()
    {
        var snapshot = new InMemoryTrackMapTrajectoryStore().GetPreferredOrBest("uid-missing", 0, 6);

        Assert.False(snapshot.HasDrawableMap);
        Assert.Equal(TrackMapStatus.MissingMotionData, snapshot.Status);
        Assert.Equal("该会话缺少 Motion 坐标", snapshot.WarningText);
        Assert.DoesNotContain("等待 Motion 数据", snapshot.WarningText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies corner distance ranges produce highlight points and a marker.
    /// </summary>
    [Fact]
    public void BuildOverlay_WithCornerDistance_GeneratesHighlightSegment()
    {
        var builder = new TrackMapBuilder();
        var snapshot = builder.BuildSnapshot("uid-1", 0, 3, CreateRectangleSamples(3, 80, 100, 50));
        var segment = new TrackSegment
        {
            SegmentId = "t1",
            Name = "Tarzan",
            SegmentType = TrackSegmentType.Corner,
            CornerNumber = 1,
            StartDistanceMeters = 20,
            EndDistanceMeters = 40
        };

        var overlay = builder.BuildOverlay(snapshot, segment, "T1 Tarzan", false, 100);

        Assert.NotEmpty(overlay.HighlightPoints);
        Assert.NotNull(overlay.MarkerX);
        Assert.NotNull(overlay.MarkerY);
    }

    /// <summary>
    /// Verifies missing corner ranges keep the map visible but show an explicit empty state.
    /// </summary>
    [Fact]
    public void BuildOverlay_WithoutCornerDistance_ShowsMissingCornerPositionState()
    {
        var builder = new TrackMapBuilder();
        var snapshot = builder.BuildSnapshot("uid-1", 0, 3, CreateRectangleSamples(3, 80, 100, 50));

        var overlay = builder.BuildOverlay(snapshot, null, "T1 Tarzan", false, 100);

        Assert.Empty(overlay.HighlightPoints);
        Assert.Equal("暂无弯角位置数据", overlay.WarningText);
    }

    /// <summary>
    /// Verifies large Motion trajectories are simplified before binding to the UI.
    /// </summary>
    [Fact]
    public void BuildSnapshot_WithManyMotionPoints_SimplifiesToLimit()
    {
        var snapshot = new TrackMapBuilder().BuildSnapshot("uid-1", 0, 3, CreateRectangleSamples(3, 1_200, 100, 50));

        Assert.True(snapshot.HasDrawableMap);
        Assert.InRange(snapshot.Points.Count, 1, TrackMapBuilder.MaxDrawablePoints);
    }

    /// <summary>
    /// Verifies the XAML track-map MVP does not depend on official or external image resources.
    /// </summary>
    [Fact]
    public void CornerAnalysisView_DoesNotUseOfficialOrExternalTrackMapAssets()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("F1Telemetry.App", "Views", "CornerAnalysisView.xaml"));

        Assert.DoesNotContain("<Image", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".png", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".jpg", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".svg", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("f1.com", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("formula1", xaml, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies the corner-analysis view keeps the middle content scrollable and exposes chart/map empty states.
    /// </summary>
    [Fact]
    public void CornerAnalysisView_UsesScrollableMainContentAndExplicitEmptyStates()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("F1Telemetry.App", "Views", "CornerAnalysisView.xaml"));

        Assert.Contains("CornerAnalysisMainScrollViewer", xaml, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CornerAnalysisHeader", xaml, StringComparison.Ordinal);
        Assert.Contains("CornerAnalysisFilterBar", xaml, StringComparison.Ordinal);
        Assert.Contains("CornerAnalysisRightDetails", xaml, StringComparison.Ordinal);
        Assert.Contains("最高置信结果", xaml, StringComparison.Ordinal);
        Assert.Contains("参考图状态", xaml, StringComparison.Ordinal);
        Assert.Contains("数据质量提示", xaml, StringComparison.Ordinal);
        Assert.Contains("AI 分析备注", xaml, StringComparison.Ordinal);
        Assert.Contains("MaxHeight=\"132\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TrackMapEmptyStateText", xaml, StringComparison.Ordinal);
        Assert.Contains("HasDrawableTrackMap", xaml, StringComparison.Ordinal);
        Assert.Contains("HasSpeedChart", xaml, StringComparison.Ordinal);
        Assert.Contains("HasBrakeChart", xaml, StringComparison.Ordinal);
        Assert.Contains("HasThrottleChart", xaml, StringComparison.Ordinal);
    }

    private static IReadOnlyList<LapSample> CreateRectangleSamples(int lapNumber, int count, float width, float height)
    {
        var samples = new List<LapSample>(count);
        for (var index = 0; index < count; index++)
        {
            var progress = count == 1 ? 0d : index / (count - 1d);
            var edgeProgress = progress * 4d;
            float x;
            float z;
            if (edgeProgress <= 1d)
            {
                x = (float)(edgeProgress * width);
                z = 0;
            }
            else if (edgeProgress <= 2d)
            {
                x = width;
                z = (float)((edgeProgress - 1d) * height);
            }
            else if (edgeProgress <= 3d)
            {
                x = (float)((3d - edgeProgress) * width);
                z = height;
            }
            else
            {
                x = 0;
                z = (float)((4d - edgeProgress) * height);
            }

            samples.Add(new LapSample
            {
                LapNumber = lapNumber,
                LapDistance = (float)(progress * 100d),
                WorldPositionX = x,
                WorldPositionZ = z,
                IsValid = true
            });
        }

        return samples;
    }

    private static IReadOnlyList<LapSample> CreateSamplesWithoutMotion(int lapNumber, int count)
    {
        return Enumerable.Range(0, count)
            .Select(index => new LapSample
            {
                LapNumber = lapNumber,
                LapDistance = index * 10f,
                IsValid = true
            })
            .ToArray();
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "F1Telemetry.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(new[] { directory!.FullName }.Concat(segments).ToArray());
    }
}
