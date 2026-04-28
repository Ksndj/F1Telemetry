namespace F1Telemetry.App.Logging;

/// <summary>
/// Represents the display category and message for a raw UDP event code.
/// </summary>
/// <param name="Category">The normalized log category used by the log views.</param>
/// <param name="Message">The readable log message shown to users.</param>
public sealed record RawEventCodeLogDisplay(string Category, string Message);

/// <summary>
/// Converts raw F1 UDP event codes into readable log entries without changing parser semantics.
/// </summary>
public static class RawEventCodeLogFormatter
{
    /// <summary>
    /// Formats a raw UDP event code for dashboard logs.
    /// </summary>
    /// <param name="rawEventCode">The raw four-character UDP event code.</param>
    /// <returns>The display category and readable message for the event code.</returns>
    public static RawEventCodeLogDisplay Format(string? rawEventCode)
    {
        var code = NormalizeCode(rawEventCode);
        return code switch
        {
            "BUTN" => new RawEventCodeLogDisplay("UDP", "原始 UDP 按键事件：BUTN"),
            "SPTP" => new RawEventCodeLogDisplay("UDP", "原始 UDP 测速点事件：SPTP"),
            "SEND" => new RawEventCodeLogDisplay("UDP", "原始 UDP Session 结束事件：SEND"),
            "OVTK" => new RawEventCodeLogDisplay("RaceEvent", "超车事件"),
            "FTLP" => new RawEventCodeLogDisplay("RaceEvent", "最快圈"),
            "COLL" => new RawEventCodeLogDisplay("RaceEvent", "碰撞"),
            "SSTA" => new RawEventCodeLogDisplay("System", "Session 状态变化"),
            "" => new RawEventCodeLogDisplay("UDP", "未知 UDP 事件"),
            _ => new RawEventCodeLogDisplay("UDP", $"未知 UDP 事件：{code}")
        };
    }

    private static string NormalizeCode(string? rawEventCode)
    {
        return string.IsNullOrWhiteSpace(rawEventCode)
            ? string.Empty
            : rawEventCode.Trim().ToUpperInvariant();
    }
}
