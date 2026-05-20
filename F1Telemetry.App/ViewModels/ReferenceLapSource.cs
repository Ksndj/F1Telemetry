namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Describes how the corner analysis reference lap was selected.
/// </summary>
public enum ReferenceLapSource
{
    /// <summary>
    /// The user explicitly selected the reference lap.
    /// </summary>
    Manual,

    /// <summary>
    /// The reference lap is the fastest valid lap from the same inferred stint.
    /// </summary>
    SameStintBest,

    /// <summary>
    /// The reference lap is the fastest valid lap on the same compound.
    /// </summary>
    SameCompoundBest,

    /// <summary>
    /// The reference lap is the fastest valid lap in the session.
    /// </summary>
    SessionBest,

    /// <summary>
    /// The reference lap is the previous lap.
    /// </summary>
    PreviousLap,

    /// <summary>
    /// No usable reference lap is available.
    /// </summary>
    None
}
