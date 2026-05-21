namespace F1Telemetry.App.TrackMaps;

/// <summary>
/// Formats track-map status values for the corner-analysis UI.
/// </summary>
public static class TrackMapStatusFormatter
{
    /// <summary>
    /// Formats the compact status text shown in the track-map header.
    /// </summary>
    /// <param name="status">The track-map status.</param>
    /// <returns>A short user-facing status label.</returns>
    public static string FormatStatus(TrackMapStatus status)
    {
        return status switch
        {
            TrackMapStatus.WaitingMotionData => "等待 Motion 数据",
            TrackMapStatus.MissingMotionData => "该会话缺少 Motion 坐标",
            TrackMapStatus.InsufficientTrackPoints => "轨迹采样不足，暂无法绘制",
            TrackMapStatus.MissingCornerRange => "暂无弯角位置数据",
            TrackMapStatus.Ready => "来源：Motion 轨迹",
            _ => "等待 Motion 数据"
        };
    }

    /// <summary>
    /// Formats the full empty-state body text shown inside the map area.
    /// </summary>
    /// <param name="status">The track-map status.</param>
    /// <returns>A complete empty-state explanation.</returns>
    public static string FormatEmptyState(TrackMapStatus status)
    {
        return status switch
        {
            TrackMapStatus.WaitingMotionData => "等待完整圈轨迹后显示赛道图。",
            TrackMapStatus.MissingMotionData => "当前历史数据缺少 Motion 坐标，暂无法生成真实赛道图。",
            TrackMapStatus.InsufficientTrackPoints => "轨迹采样不足，暂无法绘制。",
            TrackMapStatus.MissingCornerRange => "暂无弯角位置数据。",
            TrackMapStatus.Ready => string.Empty,
            _ => "等待完整圈轨迹后显示赛道图。"
        };
    }

    /// <summary>
    /// Resolves a status from legacy warning text while preserving older call sites.
    /// </summary>
    /// <param name="warningText">The warning or empty-state text.</param>
    /// <returns>The best matching track-map status.</returns>
    public static TrackMapStatus ResolveStatus(string? warningText)
    {
        if (string.IsNullOrWhiteSpace(warningText))
        {
            return TrackMapStatus.Ready;
        }

        return warningText.Trim() switch
        {
            "等待 Motion 数据" => TrackMapStatus.WaitingMotionData,
            "该会话缺少 Motion 坐标" => TrackMapStatus.MissingMotionData,
            "轨迹采样不足，暂无法绘制" => TrackMapStatus.InsufficientTrackPoints,
            "暂无弯角位置数据" => TrackMapStatus.MissingCornerRange,
            _ => TrackMapStatus.WaitingMotionData
        };
    }
}
