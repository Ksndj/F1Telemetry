namespace F1Telemetry.Analytics.Strategy;

/// <summary>
/// Defines the high-level V3 strategy advice categories emitted from summarized evidence.
/// </summary>
public enum StrategyAdviceType
{
    /// <summary>
    /// Indicates that the available evidence supports observation rather than a directional call.
    /// </summary>
    Observe,

    /// <summary>
    /// Indicates that required strategy evidence is missing.
    /// </summary>
    InsufficientData,

    /// <summary>
    /// Indicates a conditional undercut opportunity.
    /// </summary>
    Undercut,

    /// <summary>
    /// Indicates a conditional overcut or stint-extension opportunity.
    /// </summary>
    Overcut
}
