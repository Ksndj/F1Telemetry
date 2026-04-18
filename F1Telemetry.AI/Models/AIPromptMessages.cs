namespace F1Telemetry.AI.Models;

/// <summary>
/// Represents the system and user messages for a DeepSeek analysis request.
/// </summary>
public sealed record AIPromptMessages
{
    /// <summary>
    /// Gets the system message.
    /// </summary>
    public string SystemMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user message.
    /// </summary>
    public string UserMessage { get; init; } = string.Empty;
}
