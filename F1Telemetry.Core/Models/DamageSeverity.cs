namespace F1Telemetry.Core.Models;

/// <summary>
/// Defines the normalized severity bands used for percentage-based car damage.
/// </summary>
public enum DamageSeverity
{
    /// <summary>
    /// Indicates no damage.
    /// </summary>
    None,

    /// <summary>
    /// Indicates 1-9 percent damage.
    /// </summary>
    Minor,

    /// <summary>
    /// Indicates 10-24 percent damage.
    /// </summary>
    Light,

    /// <summary>
    /// Indicates 25-49 percent damage.
    /// </summary>
    Moderate,

    /// <summary>
    /// Indicates 50-74 percent damage.
    /// </summary>
    Severe,

    /// <summary>
    /// Indicates 75-100 percent damage or a critical fault.
    /// </summary>
    Critical
}
