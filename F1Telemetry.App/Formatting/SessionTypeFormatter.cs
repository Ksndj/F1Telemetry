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
}
