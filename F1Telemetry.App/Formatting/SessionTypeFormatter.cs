namespace F1Telemetry.App.Formatting;

/// <summary>
/// Formats raw F1 25 session type identifiers into Chinese race format names.
/// </summary>
public static class SessionTypeFormatter
{
    /// <summary>
    /// Formats a raw session type identifier for user-facing display.
    /// </summary>
    /// <param name="sessionType">The raw session type identifier from the session packet.</param>
    public static string Format(byte? sessionType)
    {
        return sessionType switch
        {
            >= 1 and <= 4 => "练习赛",
            >= 5 and <= 9 => "排位赛",
            >= 10 and <= 14 => "冲刺排位",
            15 or 17 => "正赛",
            16 => "冲刺赛",
            18 => "时间试跑 / 计时赛",
            _ => "未知赛制"
        };
    }
}
