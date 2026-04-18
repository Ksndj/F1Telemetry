using System.Text.Json.Serialization;

namespace F1Telemetry.AI.Models;

/// <summary>
/// Represents the subset of the DeepSeek chat completion response used by the app.
/// </summary>
public sealed record DeepSeekChatCompletionResponse
{
    /// <summary>
    /// Gets the available response choices.
    /// </summary>
    [JsonPropertyName("choices")]
    public IReadOnlyList<DeepSeekChatChoice> Choices { get; init; } = Array.Empty<DeepSeekChatChoice>();
}

/// <summary>
/// Represents a single DeepSeek response choice.
/// </summary>
public sealed record DeepSeekChatChoice
{
    /// <summary>
    /// Gets the assistant message payload.
    /// </summary>
    [JsonPropertyName("message")]
    public DeepSeekChatResponseMessage? Message { get; init; }
}

/// <summary>
/// Represents a single response message from DeepSeek.
/// </summary>
public sealed record DeepSeekChatResponseMessage
{
    /// <summary>
    /// Gets the message content.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}
