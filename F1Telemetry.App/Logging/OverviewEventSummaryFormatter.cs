using F1Telemetry.App.ViewModels;

namespace F1Telemetry.App.Logging;

/// <summary>
/// Builds short, prioritized event summaries for the Overview page.
/// </summary>
public static class OverviewEventSummaryFormatter
{
    private const int DefaultMaxCount = 4;
    private const int DefaultMaxMessageLength = 40;
    private static readonly string[] RawUdpEventCodes = ["BUTN", "SPTP", "SEND"];

    /// <summary>
    /// Builds short overview summaries from full log entries.
    /// </summary>
    /// <param name="logs">The full log entries, newest first.</param>
    /// <param name="maxCount">The maximum number of summaries to return.</param>
    /// <param name="maxMessageLength">The maximum summary message length.</param>
    /// <returns>The prioritized, shortened overview summaries.</returns>
    public static IReadOnlyList<LogEntryViewModel> BuildSummaries(
        IEnumerable<LogEntryViewModel> logs,
        int maxCount = DefaultMaxCount,
        int maxMessageLength = DefaultMaxMessageLength)
    {
        ArgumentNullException.ThrowIfNull(logs);

        var boundedMaxCount = Math.Clamp(maxCount, 1, 5);
        var boundedMaxLength = Math.Max(8, maxMessageLength);

        return logs
            .Select((log, index) => new
            {
                Log = log,
                Index = index,
                Category = LogCategoryFormatter.Normalize(log.Category, log.Message),
                Priority = GetPriority(LogCategoryFormatter.Normalize(log.Category, log.Message), log.Message)
            })
            .Where(item => item.Priority > 0)
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Index)
            .Take(boundedMaxCount)
            .Select(item => new LogEntryViewModel
            {
                Timestamp = item.Log.Timestamp,
                Category = item.Category,
                Message = Truncate(item.Log.Message, boundedMaxLength)
            })
            .ToArray();
    }

    private static int GetPriority(string category, string message)
    {
        if (IsRawUdpEvent(category, message))
        {
            return 0;
        }

        if (string.Equals(category, "AI", StringComparison.Ordinal))
        {
            return 80;
        }

        if (!string.Equals(category, "RaceEvent", StringComparison.Ordinal))
        {
            return 0;
        }

        if (ContainsAny(message, "安全车", "黄旗", "红旗"))
        {
            return 100;
        }

        if (ContainsAny(message, "进站", "Pit", "pitted"))
        {
            return 90;
        }

        if (ContainsAny(message, "圈无效", "无效圈", "invalid"))
        {
            return 86;
        }

        if (ContainsAny(message, "低油", "燃油", "low fuel"))
        {
            return 84;
        }

        if (ContainsSignificantDamage(message))
        {
            return 88;
        }

        if (ContainsAny(message, "高胎磨", "胎磨", "磨损"))
        {
            return 82;
        }

        if (ContainsAny(message, "攻击", "防守", "前车", "后车"))
        {
            return 80;
        }

        if (ContainsAny(message, "碰撞", "最快圈"))
        {
            return 70;
        }

        if (ContainsAny(message, "超车", "OVTK", "Overtake"))
        {
            return 50;
        }

        return 60;
    }

    private static bool IsRawUdpEvent(string category, string message)
    {
        return (string.Equals(category, "UDP", StringComparison.Ordinal)
                || message.Contains("收到赛道事件", StringComparison.Ordinal)
                || message.Contains("原始事件", StringComparison.Ordinal))
            && RawUdpEventCodes.Any(code => message.Contains(code, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsSignificantDamage(string message)
    {
        return ContainsAny(
            message,
            "中度损伤",
            "严重损伤",
            "危急",
            "DRS 故障",
            "ERS 故障",
            "引擎爆缸",
            "引擎卡死");
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return maxLength <= 3
            ? new string('.', maxLength)
            : trimmed[..(maxLength - 3)] + "...";
    }
}
