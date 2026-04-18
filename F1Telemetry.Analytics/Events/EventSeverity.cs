namespace F1Telemetry.Analytics.Events;

/// <summary>
/// Represents the user-facing severity of a detected race event.
/// </summary>
public enum EventSeverity
{
    /// <summary>
    /// Informational event that does not require immediate action.
    /// </summary>
    Information,

    /// <summary>
    /// Warning event that should be surfaced prominently.
    /// </summary>
    Warning
}
