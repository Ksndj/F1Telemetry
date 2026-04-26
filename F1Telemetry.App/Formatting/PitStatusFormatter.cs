namespace F1Telemetry.App.Formatting;

/// <summary>
/// Formats raw pit status fields into concise Chinese display text.
/// </summary>
public static class PitStatusFormatter
{
    /// <summary>
    /// Formats pit status and completed pit stop count for user-facing display.
    /// </summary>
    /// <param name="pitStatus">The raw pit status value from lap data.</param>
    /// <param name="numPitStops">The completed pit stop count when known.</param>
    public static string Format(byte? pitStatus, byte? numPitStops)
    {
        return pitStatus switch
        {
            1 => "进站中",
            2 => "维修区",
            0 when numPitStops is > 0 => $"已进站 {numPitStops} 次",
            0 => "赛道",
            null when numPitStops is > 0 => $"已进站 {numPitStops} 次",
            _ => "未知"
        };
    }
}
