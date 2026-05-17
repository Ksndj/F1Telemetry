namespace F1Telemetry.Analytics.Strategy;

/// <summary>
/// Represents the risk level attached to strategy advice.
/// </summary>
public enum StrategyRiskLevel
{
    /// <summary>
    /// Indicates that risk cannot be estimated from the current evidence.
    /// </summary>
    Unknown,

    /// <summary>
    /// Indicates low operational risk.
    /// </summary>
    Low,

    /// <summary>
    /// Indicates moderate operational risk.
    /// </summary>
    Medium,

    /// <summary>
    /// Indicates high operational risk.
    /// </summary>
    High
}
