namespace F1Telemetry.Storage.Models;

/// <summary>
/// Represents a persisted strategy recommendation for a session or lap.
/// </summary>
public sealed record StoredStrategyAdvice
{
    /// <summary>
    /// Gets the auto-incremented row identifier.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// Gets the associated session identifier.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the associated lap number when the advice is lap-specific.
    /// </summary>
    public int? LapNumber { get; init; }

    /// <summary>
    /// Gets the advice category.
    /// </summary>
    public string AdviceType { get; init; } = "-";

    /// <summary>
    /// Gets the relative priority for display or speech ordering.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Gets the concise recommendation text.
    /// </summary>
    public string Message { get; init; } = "-";

    /// <summary>
    /// Gets the supporting reason for the recommendation.
    /// </summary>
    public string Rationale { get; init; } = "-";

    /// <summary>
    /// Gets the estimated time gain in milliseconds.
    /// </summary>
    public double? ExpectedGainInMs { get; init; }

    /// <summary>
    /// Gets the risk level label.
    /// </summary>
    public string RiskLevel { get; init; } = "-";

    /// <summary>
    /// Gets optional structured details for future strategy engines.
    /// </summary>
    public string? PayloadJson { get; init; }

    /// <summary>
    /// Gets the row creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
}
