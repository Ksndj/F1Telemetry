namespace F1Telemetry.Core.Models;

/// <summary>
/// Configures optional runtime file logs used for real-race validation.
/// </summary>
public sealed record LogSettings
{
    /// <summary>
    /// Gets a value indicating whether categorized app file logging is enabled.
    /// </summary>
    public bool EnableAppFileLog { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether RaceAssistant audit JSONL logging is enabled.
    /// </summary>
    public bool EnableRaceAssistantAuditLog { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether a short sanitized prompt summary may be written.
    /// </summary>
    public bool RaceAssistantLogPromptSummary { get; init; }

    /// <summary>
    /// Gets the maximum size of one log file before rotating.
    /// </summary>
    public int MaxLogFileSizeMB { get; init; } = 20;

    /// <summary>
    /// Gets the number of days to retain old log files.
    /// </summary>
    public int MaxLogRetentionDays { get; init; } = 14;
}
