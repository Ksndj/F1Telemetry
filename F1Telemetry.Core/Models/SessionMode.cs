namespace F1Telemetry.Core.Models;

/// <summary>
/// Defines the high-level session modes used for display, AI prompts, and TTS filtering.
/// </summary>
public enum SessionMode
{
    /// <summary>
    /// Practice session.
    /// </summary>
    Practice,

    /// <summary>
    /// Qualifying session.
    /// </summary>
    Qualifying,

    /// <summary>
    /// Sprint qualifying session.
    /// </summary>
    SprintQualifying,

    /// <summary>
    /// Sprint race session.
    /// </summary>
    SprintRace,

    /// <summary>
    /// Grand prix race session.
    /// </summary>
    Race,

    /// <summary>
    /// Time trial session.
    /// </summary>
    TimeTrial,

    /// <summary>
    /// Unknown or unsupported session mode.
    /// </summary>
    Unknown
}
