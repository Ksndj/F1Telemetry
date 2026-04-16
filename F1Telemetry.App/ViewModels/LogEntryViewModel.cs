namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents a timestamped log line shown in the dashboard footer.
/// </summary>
public sealed class LogEntryViewModel
{
    /// <summary>
    /// Gets the display timestamp.
    /// </summary>
    public string Timestamp { get; init; } = "-";

    /// <summary>
    /// Gets the short log category.
    /// </summary>
    public string Category { get; init; } = "-";

    /// <summary>
    /// Gets the log message.
    /// </summary>
    public string Message { get; init; } = "-";
}
