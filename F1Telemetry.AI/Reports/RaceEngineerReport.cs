namespace F1Telemetry.AI.Reports;

/// <summary>
/// Represents a deterministic V3 race engineer report generated from compressed evidence.
/// </summary>
public sealed record RaceEngineerReport
{
    /// <summary>
    /// Gets the report summary.
    /// </summary>
    public string Summary { get; init; } = "-";

    /// <summary>
    /// Gets findings that are directly supported by supplied data.
    /// </summary>
    public IReadOnlyList<string> DataSupportedFindings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets suggestions inferred from the supplied evidence.
    /// </summary>
    public IReadOnlyList<string> InferredSuggestions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets data-quality warnings preserved from the input.
    /// </summary>
    public IReadOnlyList<string> DataQualityWarnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the Markdown report body.
    /// </summary>
    public string Markdown { get; init; } = string.Empty;

    /// <summary>
    /// Gets the AI-safe prompt body that can be sent to a chat model if needed.
    /// </summary>
    public string SafePrompt { get; init; } = string.Empty;
}
