using System.Globalization;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Strategy;
using F1Telemetry.Storage.Models;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents one stored event row in the post-race timeline.
/// </summary>
public sealed record PostRaceReviewEventRowViewModel
{
    /// <summary>
    /// Gets the formatted lap label.
    /// </summary>
    public string LapText { get; init; } = "-";

    /// <summary>
    /// Gets the formatted event timestamp.
    /// </summary>
    public string TimeText { get; init; } = "-";

    /// <summary>
    /// Gets the user-facing event type.
    /// </summary>
    public string EventTypeText { get; init; } = "-";

    /// <summary>
    /// Gets the compact category text used by WPF bindings.
    /// </summary>
    public string Category => EventTypeText;

    /// <summary>
    /// Gets the user-facing severity text.
    /// </summary>
    public string SeverityText { get; init; } = "-";

    /// <summary>
    /// Gets the driver or vehicle text when available.
    /// </summary>
    public string TargetText { get; init; } = "-";

    /// <summary>
    /// Gets the stored event message.
    /// </summary>
    public string Message { get; init; } = "-";

    /// <summary>
    /// Creates a review timeline row from a stored event.
    /// </summary>
    /// <param name="storedEvent">The stored event to project.</param>
    public static PostRaceReviewEventRowViewModel FromStoredEvent(StoredEvent storedEvent)
    {
        ArgumentNullException.ThrowIfNull(storedEvent);

        return new PostRaceReviewEventRowViewModel
        {
            LapText = storedEvent.LapNumber is null ? "未知圈" : $"Lap {storedEvent.LapNumber.Value}",
            TimeText = FormatTimestamp(storedEvent.CreatedAt),
            EventTypeText = FormatEventType(storedEvent.EventType),
            SeverityText = FormatSeverity(storedEvent.Severity),
            TargetText = FormatTarget(storedEvent.DriverName, storedEvent.VehicleIdx),
            Message = string.IsNullOrWhiteSpace(storedEvent.Message) ? "-" : storedEvent.Message
        };
    }

    /// <summary>
    /// Creates a review timeline row from a V3 strategy timeline entry.
    /// </summary>
    /// <param name="entry">The strategy timeline entry.</param>
    public static PostRaceReviewEventRowViewModel FromStrategyTimeline(StrategyTimelineEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new PostRaceReviewEventRowViewModel
        {
            LapText = entry.LapNumber is null ? "未知圈" : $"Lap {entry.LapNumber.Value}",
            TimeText = "-",
            EventTypeText = $"V3 {entry.Category}",
            SeverityText = entry.RiskLevel.ToString(),
            TargetText = "策略时间线",
            Message = string.IsNullOrWhiteSpace(entry.Detail) ? entry.Title : $"{entry.Title} · {entry.Detail}"
        };
    }

    private static string FormatEventType(EventType eventType)
    {
        return eventType switch
        {
            EventType.FrontCarPitted => "前车进站",
            EventType.RearCarPitted => "后车进站",
            EventType.PlayerLapInvalidated => "本圈无效",
            EventType.LowFuel => "低燃油",
            EventType.HighTyreWear => "高胎磨",
            EventType.CarDamage => "车辆损伤",
            EventType.DrsFault => "DRS 故障",
            EventType.ErsFault => "ERS 故障",
            EventType.EngineFailure => "引擎故障",
            EventType.SafetyCar => "安全车",
            EventType.VirtualSafetyCar => "虚拟安全车",
            EventType.YellowFlag => "黄旗",
            EventType.RedFlag => "红旗",
            EventType.AttackWindow => "进攻窗口",
            EventType.DefenseWindow => "防守窗口",
            EventType.LowErs => "低 ERS",
            EventType.FrontOldTyreRisk => "前车旧胎风险",
            EventType.RearNewTyrePressure => "后车新胎压力",
            EventType.TrafficRisk => "交通风险",
            EventType.QualifyingCleanAirWindow => "排位空窗",
            EventType.RacePitWindow => "进站窗口",
            EventType.SafetyCarRestart => "安全车重启",
            EventType.RedFlagTyreChange => "红旗换胎",
            EventType.DataQualityWarning => "数据质量提醒",
            _ => eventType.ToString()
        };
    }

    private static string FormatSeverity(EventSeverity severity)
    {
        return severity switch
        {
            EventSeverity.Information => "信息",
            EventSeverity.Warning => "警告",
            _ => severity.ToString()
        };
    }

    private static string FormatTarget(string? driverName, int? vehicleIdx)
    {
        if (!string.IsNullOrWhiteSpace(driverName))
        {
            return driverName;
        }

        return vehicleIdx is null ? "-" : $"车辆 {vehicleIdx.Value}";
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
