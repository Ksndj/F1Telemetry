using F1Telemetry.Core.Models;

namespace F1Telemetry.Core.Formatting;

/// <summary>
/// Resolves raw F1 25 session type identifiers into assistant session modes and display text.
/// </summary>
public static class SessionModeFormatter
{
    /// <summary>
    /// Resolves a raw session type identifier into a high-level session mode.
    /// </summary>
    /// <param name="sessionType">The raw session type identifier from the session packet.</param>
    /// <returns>The resolved session mode.</returns>
    public static SessionMode Resolve(byte? sessionType)
    {
        return sessionType switch
        {
            >= 1 and <= 4 => SessionMode.Practice,
            >= 5 and <= 9 => SessionMode.Qualifying,
            >= 10 and <= 14 => SessionMode.SprintQualifying,
            15 or 17 => SessionMode.Race,
            16 => SessionMode.SprintRace,
            18 => SessionMode.TimeTrial,
            _ => SessionMode.Unknown
        };
    }

    /// <summary>
    /// Formats a session mode as Chinese user-facing text.
    /// </summary>
    /// <param name="sessionMode">The resolved session mode.</param>
    /// <returns>The Chinese session type text.</returns>
    public static string FormatDisplayName(SessionMode sessionMode)
    {
        return sessionMode switch
        {
            SessionMode.Practice => "练习赛",
            SessionMode.Qualifying => "排位赛",
            SessionMode.SprintQualifying => "冲刺排位",
            SessionMode.SprintRace => "冲刺赛",
            SessionMode.Race => "正赛",
            SessionMode.TimeTrial => "时间试跑 / 计时赛",
            _ => "未知赛制"
        };
    }

    /// <summary>
    /// Formats the overview focus guidance for a session mode.
    /// </summary>
    /// <param name="sessionMode">The resolved session mode.</param>
    /// <returns>The Chinese focus guidance text.</returns>
    public static string FormatFocus(SessionMode sessionMode)
    {
        return sessionMode switch
        {
            SessionMode.Practice => "关注长距离、轮胎、油耗、圈速趋势",
            SessionMode.Qualifying => "关注有效圈、交通、ERS、轮胎准备",
            SessionMode.SprintQualifying => "关注单圈有效性、交通、ERS、轮胎准备",
            SessionMode.SprintRace => "关注短距离轮胎、ERS、攻防窗口、前后车差距",
            SessionMode.Race => "关注胎龄、油耗、前后车、进站窗口",
            SessionMode.TimeTrial => "关注当前圈、最佳圈、输入稳定性",
            _ => "关注基础状态、圈速、轮胎、燃油"
        };
    }

    /// <summary>
    /// Formats the AI guidance instruction for a session mode.
    /// </summary>
    /// <param name="sessionMode">The resolved session mode.</param>
    /// <returns>The prompt guidance text.</returns>
    public static string FormatPromptGuidance(SessionMode sessionMode)
    {
        return sessionMode switch
        {
            SessionMode.Practice =>
                "重点给长距离节奏、轮胎、油耗、圈速趋势建议。",
            SessionMode.Qualifying or SessionMode.SprintQualifying or SessionMode.TimeTrial =>
                "重点给有效圈、交通、ERS、轮胎准备和输入稳定性建议；避免正赛长距离策略建议。",
            SessionMode.SprintRace =>
                "重点给短距离轮胎、ERS、攻防窗口、前后车差距建议。",
            SessionMode.Race =>
                "重点给轮胎、燃油、交通、进站建议。",
            _ =>
                "重点给现有圈速、轮胎、燃油、ERS 和交通状态建议。"
        };
    }

    /// <summary>
    /// Returns whether pit-window speech is relevant for the session mode.
    /// </summary>
    /// <param name="sessionMode">The resolved session mode.</param>
    /// <returns><see langword="true"/> when pit-window speech should be kept.</returns>
    public static bool AllowsPitWindowSpeech(SessionMode sessionMode)
    {
        return sessionMode is SessionMode.Race or SessionMode.SprintRace or SessionMode.Practice or SessionMode.Unknown;
    }
}
