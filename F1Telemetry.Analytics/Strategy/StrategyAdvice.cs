namespace F1Telemetry.Analytics.Strategy;

/// <summary>
/// Represents semi-empirical strategy advice with confidence, risk, and evidence fields.
/// </summary>
public sealed record StrategyAdvice
{
    /// <summary>
    /// Gets the advice type.
    /// </summary>
    public StrategyAdviceType AdviceType { get; init; } = StrategyAdviceType.Observe;

    /// <summary>
    /// Gets the confidence score from zero to one.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Gets the strategy risk level.
    /// </summary>
    public StrategyRiskLevel RiskLevel { get; init; } = StrategyRiskLevel.Unknown;

    /// <summary>
    /// Gets the conditional human-readable advice summary.
    /// </summary>
    public string Summary { get; init; } = "Observe until more strategy evidence is available.";

    /// <summary>
    /// Gets the data fields required by this advice path.
    /// </summary>
    public IReadOnlyList<string> RequiredData { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the required data fields that were missing.
    /// </summary>
    public IReadOnlyList<string> MissingData { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets findings that are directly supported by supplied data.
    /// </summary>
    public IReadOnlyList<string> SupportedFindings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets suggestions inferred from the supported findings.
    /// </summary>
    public IReadOnlyList<string> InferredSuggestions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets warnings about data completeness or model limitations.
    /// </summary>
    public IReadOnlyList<string> DataQualityWarnings { get; init; } = Array.Empty<string>();
}
