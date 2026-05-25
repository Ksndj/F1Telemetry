namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Represents one in-memory race assistant question and answer row.
/// </summary>
public sealed record RaceAssistantHistoryItemViewModel
{
    /// <summary>
    /// Gets when the answer was produced.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    /// <summary>
    /// Gets the user question.
    /// </summary>
    public string Question { get; init; } = string.Empty;

    /// <summary>
    /// Gets the classified intent.
    /// </summary>
    public string Intent { get; init; } = string.Empty;

    /// <summary>
    /// Gets the short answer.
    /// </summary>
    public string Answer { get; init; } = string.Empty;

    /// <summary>
    /// Gets the confidence label.
    /// </summary>
    public string Confidence { get; init; } = string.Empty;
}
