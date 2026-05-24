using System.Globalization;
using F1Telemetry.Analytics.Corners;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.Tracks;

namespace F1Telemetry.App.TrackMaps;

/// <summary>
/// Builds lightweight normalized track maps from player Motion X/Z lap samples.
/// </summary>
public sealed class TrackMapBuilder
{
    /// <summary>
    /// Minimum number of coordinate samples needed before a track outline is drawable.
    /// </summary>
    public const int MinimumDrawablePoints = 8;

    /// <summary>
    /// Maximum number of points retained for UI binding.
    /// </summary>
    public const int MaxDrawablePoints = 500;

    private const double Padding = 0.08d;

    /// <summary>
    /// Builds a normalized track map snapshot from lap samples.
    /// </summary>
    /// <param name="sessionUid">The game session UID.</param>
    /// <param name="trackId">The track id when known.</param>
    /// <param name="lapNumber">The lap number represented by the samples.</param>
    /// <param name="samples">The candidate lap samples.</param>
    /// <param name="lapLengthMeters">The lap length used to normalize wrapped lap distances.</param>
    /// <returns>A drawable track map or an explicit empty-state snapshot.</returns>
    public TrackMapSnapshot BuildSnapshot(
        string sessionUid,
        int? trackId,
        int lapNumber,
        IEnumerable<LapSample> samples,
        float? lapLengthMeters = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var rawPoints = samples
            .Where(sample => sample.LapDistance is not null
                && sample.WorldPositionX is not null
                && sample.WorldPositionZ is not null
                && float.IsFinite(sample.LapDistance.Value)
                && float.IsFinite(sample.WorldPositionX.Value)
                && float.IsFinite(sample.WorldPositionZ.Value))
            .Select(sample => new RawTrackPoint(
                LapDistanceNormalizer.Normalize(sample.LapDistance!.Value, lapLengthMeters),
                sample.WorldPositionX!.Value,
                sample.WorldPositionZ!.Value))
            .OrderBy(point => point.LapDistance)
            .ToArray();

        if (rawPoints.Length == 0)
        {
            return CreateEmptySnapshot(
                sessionUid,
                trackId,
                lapNumber,
                TrackMapStatus.MissingMotionData,
                "该会话缺少 Motion 坐标");
        }

        var dedupedPoints = DeduplicateByDistance(rawPoints);
        if (dedupedPoints.Count < MinimumDrawablePoints)
        {
            return CreateEmptySnapshot(
                sessionUid,
                trackId,
                lapNumber,
                TrackMapStatus.InsufficientTrackPoints,
                "轨迹采样不足，暂无法绘制");
        }

        var simplifiedPoints = Simplify(dedupedPoints, MaxDrawablePoints);
        var minX = simplifiedPoints.Min(point => point.X);
        var maxX = simplifiedPoints.Max(point => point.X);
        var minZ = simplifiedPoints.Min(point => point.Z);
        var maxZ = simplifiedPoints.Max(point => point.Z);
        var width = maxX - minX;
        var height = maxZ - minZ;
        if (width < 0.001f && height < 0.001f)
        {
            return CreateEmptySnapshot(
                sessionUid,
                trackId,
                lapNumber,
                TrackMapStatus.InsufficientTrackPoints,
                "轨迹采样不足，暂无法绘制");
        }

        width = Math.Max(width, 0.001f);
        height = Math.Max(height, 0.001f);
        var inner = 1d - Padding * 2d;
        var scale = inner / Math.Max(width, height);
        var contentWidth = width * scale;
        var contentHeight = height * scale;
        var offsetX = (1d - contentWidth) / 2d;
        var offsetY = (1d - contentHeight) / 2d;
        var normalized = simplifiedPoints
            .Select(point => new TrackMapPoint
            {
                LapDistance = point.LapDistance,
                X = point.X,
                Z = point.Z,
                NormalizedX = Clamp01(offsetX + (point.X - minX) * scale),
                NormalizedY = Clamp01(1d - (offsetY + (point.Z - minZ) * scale))
            })
            .ToArray();

        return new TrackMapSnapshot
        {
            SessionUid = sessionUid,
            TrackId = trackId,
            LapNumber = lapNumber,
            Points = normalized,
            Source = "Motion 轨迹",
            Quality = ResolveQuality(normalized.Length),
            Status = TrackMapStatus.Ready,
            WarningText = normalized.Length >= 80 ? string.Empty : "Motion 轨迹采样偏少，地图仅供参考"
        };
    }

    /// <summary>
    /// Builds the selected-corner highlight overlay for a drawable track map.
    /// </summary>
    /// <param name="snapshot">The normalized track-map snapshot.</param>
    /// <param name="segment">The selected corner segment, or null when no distance range is known.</param>
    /// <param name="cornerName">The displayed corner name.</param>
    /// <param name="isEstimated">Whether the segment range is estimated.</param>
    /// <param name="lapLengthMeters">The lap length when known.</param>
    /// <returns>A corner map overlay with highlight points and marker coordinates.</returns>
    public CornerTrackMapOverlay BuildOverlay(
        TrackMapSnapshot snapshot,
        TrackSegment? segment,
        string cornerName,
        bool isEstimated,
        float? lapLengthMeters)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.HasDrawableMap)
        {
            return new CornerTrackMapOverlay
            {
                CornerName = cornerName,
                IsEstimated = isEstimated,
                WarningText = snapshot.WarningText
            };
        }

        if (segment is null)
        {
            return new CornerTrackMapOverlay
            {
                CornerName = cornerName,
                IsEstimated = isEstimated,
                WarningText = "暂无弯角位置数据"
            };
        }

        var highlightPoints = snapshot.Points
            .Where(point => segment.ContainsDistance(point.LapDistance))
            .OrderBy(point => LapDistanceNormalizer.ToSegmentRelativeDistance(point.LapDistance, segment, lapLengthMeters))
            .ToArray();

        if (highlightPoints.Length == 0)
        {
            highlightPoints = SelectNearestPoints(snapshot.Points, CalculateMidpoint(segment, lapLengthMeters), 1, segment, lapLengthMeters);
        }
        else if (highlightPoints.Length == 1)
        {
            highlightPoints = SelectNearestPoints(snapshot.Points, highlightPoints[0].LapDistance, 3, segment, lapLengthMeters);
        }

        var markerPoint = SelectNearestPoint(highlightPoints, CalculateMidpoint(segment, lapLengthMeters), lapLengthMeters);
        return new CornerTrackMapOverlay
        {
            CornerName = cornerName,
            StartLapDistance = segment.StartDistanceMeters,
            EndLapDistance = segment.EndDistanceMeters,
            HighlightPoints = highlightPoints,
            MarkerX = markerPoint?.NormalizedX,
            MarkerY = markerPoint?.NormalizedY,
            IsEstimated = isEstimated,
            WarningText = isEstimated ? "估算位置" : string.Empty
        };
    }

    /// <summary>
    /// Creates an explicit empty track-map snapshot.
    /// </summary>
    /// <param name="sessionUid">The game session UID.</param>
    /// <param name="trackId">The track id when known.</param>
    /// <param name="lapNumber">The requested lap number.</param>
    /// <param name="warningText">The empty-state warning text.</param>
    /// <returns>An empty snapshot suitable for UI binding.</returns>
    public static TrackMapSnapshot CreateEmptySnapshot(string sessionUid, int? trackId, int lapNumber, string warningText)
    {
        return CreateEmptySnapshot(
            sessionUid,
            trackId,
            lapNumber,
            TrackMapStatusFormatter.ResolveStatus(warningText),
            warningText);
    }

    /// <summary>
    /// Creates an explicit empty track-map snapshot.
    /// </summary>
    /// <param name="sessionUid">The game session UID.</param>
    /// <param name="trackId">The track id when known.</param>
    /// <param name="lapNumber">The requested lap number.</param>
    /// <param name="status">The structured empty-state status.</param>
    /// <param name="warningText">The empty-state warning text.</param>
    /// <returns>An empty snapshot suitable for UI binding.</returns>
    public static TrackMapSnapshot CreateEmptySnapshot(
        string sessionUid,
        int? trackId,
        int lapNumber,
        TrackMapStatus status,
        string warningText)
    {
        return new TrackMapSnapshot
        {
            SessionUid = sessionUid,
            TrackId = trackId,
            LapNumber = lapNumber,
            Points = Array.Empty<TrackMapPoint>(),
            Source = "Motion 轨迹",
            Quality = "Low",
            Status = status,
            WarningText = string.IsNullOrWhiteSpace(warningText)
                ? TrackMapStatusFormatter.FormatStatus(status)
                : warningText
        };
    }

    private static IReadOnlyList<RawTrackPoint> DeduplicateByDistance(IReadOnlyList<RawTrackPoint> points)
    {
        var output = new List<RawTrackPoint>(points.Count);
        float? lastAcceptedDistance = null;
        foreach (var point in points)
        {
            if (lastAcceptedDistance is not null && Math.Abs(point.LapDistance - lastAcceptedDistance.Value) < 0.5f)
            {
                output[^1] = point;
                continue;
            }

            output.Add(point);
            lastAcceptedDistance = point.LapDistance;
        }

        return output;
    }

    private static IReadOnlyList<RawTrackPoint> Simplify(IReadOnlyList<RawTrackPoint> points, int maxPoints)
    {
        if (points.Count <= maxPoints)
        {
            return points;
        }

        var output = new List<RawTrackPoint>(maxPoints);
        for (var index = 0; index < maxPoints; index++)
        {
            var sourceIndex = (int)Math.Round(index * (points.Count - 1d) / (maxPoints - 1d));
            output.Add(points[sourceIndex]);
        }

        return output;
    }

    private static TrackMapPoint? SelectNearestPoint(
        IReadOnlyList<TrackMapPoint> points,
        float targetDistance,
        float? lapLengthMeters)
    {
        return points.Count == 0
            ? null
            : points.MinBy(point => LapDistanceNormalizer.CircularDistance(point.LapDistance, targetDistance, lapLengthMeters));
    }

    private static TrackMapPoint[] SelectNearestPoints(
        IReadOnlyList<TrackMapPoint> points,
        float targetDistance,
        int count,
        TrackSegment? segment,
        float? lapLengthMeters)
    {
        var selected = points
            .OrderBy(point => LapDistanceNormalizer.CircularDistance(point.LapDistance, targetDistance, lapLengthMeters))
            .Take(count)
            .ToArray();
        return segment is null
            ? selected.OrderBy(point => point.LapDistance).ToArray()
            : selected
                .OrderBy(point => LapDistanceNormalizer.ToSegmentRelativeDistance(point.LapDistance, segment, lapLengthMeters))
                .ToArray();
    }

    private static float CalculateMidpoint(TrackSegment segment, float? lapLengthMeters)
    {
        if (segment.StartDistanceMeters <= segment.EndDistanceMeters || lapLengthMeters is not > 0)
        {
            return (segment.StartDistanceMeters + segment.EndDistanceMeters) / 2f;
        }

        var midpoint = (segment.StartDistanceMeters + segment.EndDistanceMeters + lapLengthMeters.Value) / 2f;
        return midpoint >= lapLengthMeters.Value
            ? midpoint - lapLengthMeters.Value
            : midpoint;
    }

    private static string ResolveQuality(int pointCount)
    {
        return pointCount switch
        {
            >= 250 => "High",
            >= 80 => "Medium",
            _ => "Low"
        };
    }

    private static double Clamp01(double value)
    {
        return Math.Clamp(value, 0d, 1d);
    }

    private readonly record struct RawTrackPoint(float LapDistance, float X, float Z);
}
