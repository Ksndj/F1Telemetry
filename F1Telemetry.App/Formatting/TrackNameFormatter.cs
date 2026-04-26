namespace F1Telemetry.App.Formatting;

/// <summary>
/// Formats raw F1 25 track identifiers into Chinese display names.
/// </summary>
public static class TrackNameFormatter
{
    /// <summary>
    /// Formats a raw track identifier for user-facing display.
    /// </summary>
    /// <param name="trackId">The raw track identifier from the session packet.</param>
    public static string Format(sbyte? trackId)
    {
        return trackId switch
        {
            0 => "澳洲",
            2 => "上海",
            3 => "巴林",
            4 => "西班牙",
            5 => "摩纳哥",
            6 => "加拿大",
            7 => "英国",
            9 => "匈牙利",
            10 => "比利时",
            11 => "意大利",
            12 => "新加坡",
            13 => "铃鹿",
            14 => "阿布扎比",
            15 => "美国",
            16 => "巴西",
            17 => "奥地利",
            19 => "墨西哥",
            20 => "巴库",
            26 => "赞德福特",
            27 => "伊莫拉",
            29 => "吉达",
            30 => "迈阿密",
            31 => "拉斯维加斯",
            32 => "卡塔尔",
            39 => "英国反向",
            40 => "奥地利反向",
            41 => "赞德福特反向",
            null => "未知赛道",
            _ => $"未知赛道（ID {trackId}）"
        };
    }
}
