namespace F1Telemetry.App.Logging;

/// <summary>
/// Normalizes log categories to the public categories shown in the log views.
/// </summary>
public static class LogCategoryFormatter
{
    /// <summary>
    /// Converts legacy or localized categories to the standard log category text.
    /// </summary>
    /// <param name="category">The incoming category text.</param>
    /// <param name="message">The log message used for fallback classification.</param>
    /// <returns>The normalized category text.</returns>
    public static string Normalize(string? category, string? message)
    {
        var value = string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim();
        return value switch
        {
            "System" or "系统" or "异常" or "会话" => "System",
            "UDP" or "协议" => "UDP",
            "RaceEvent" or "事件" or "告警" => "RaceEvent",
            "AI" => "AI",
            "TTS" => "TTS",
            "Storage" or "存储" => "Storage",
            _ when LooksLikeUdpMessage(message) => "UDP",
            _ when string.IsNullOrWhiteSpace(value) => "System",
            _ => value
        };
    }

    private static bool LooksLikeUdpMessage(string? message)
    {
        return !string.IsNullOrWhiteSpace(message)
            && message.Contains("UDP", StringComparison.OrdinalIgnoreCase);
    }
}
