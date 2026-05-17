using F1Telemetry.Core.Formatting;

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
        return SessionModeFormatter.FormatDisplayName(SessionModeFormatter.Resolve(sessionType));
    }

    /// <summary>
    /// Formats a raw session type identifier with race-weekend context.
    /// </summary>
    /// <param name="sessionType">The raw session type identifier from the session packet.</param>
    /// <param name="totalLaps">The configured lap count from the session packet.</param>
    /// <param name="weekendStructure">The raw weekend session type sequence from the session packet.</param>
    public static string Format(
        byte? sessionType,
        byte? totalLaps,
        IReadOnlyList<byte>? weekendStructure)
    {
        return SessionModeFormatter.FormatDisplayName(
            SessionModeFormatter.Resolve(sessionType, totalLaps, weekendStructure));
    }
}
