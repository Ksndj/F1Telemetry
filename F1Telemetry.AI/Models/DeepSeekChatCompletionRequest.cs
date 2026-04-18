using System.Text.Json.Serialization;

namespace F1Telemetry.AI.Models;

/// <summary>
/// Represents the OpenAI-compatible DeepSeek chat completion request payload.
/// </summary>
public sealed record DeepSeekChatCompletionRequest
{
    /// <summary>
    /// Gets the model name.
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; init; } = "deepseek-chat";

    /// <summary>
    /// Gets the ordered chat messages.
    /// </summary>
    [JsonPropertyName("messages")]
    public IReadOnlyList<DeepSeekChatMessage> Messages { get; init; } = Array.Empty<DeepSeekChatMessage>();

    /// <summary>
    /// Gets the fixed JSON response format request.
    /// </summary>
    [JsonPropertyName("response_format")]
    public DeepSeekResponseFormat ResponseFormat { get; init; } = new();
}

/// <summary>
/// Represents a single chat message in the DeepSeek request payload.
/// </summary>
public sealed record DeepSeekChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

/// <summary>
/// Represents the response-format instruction for the DeepSeek request.
/// </summary>
public sealed record DeepSeekResponseFormat
{
    /// <summary>
    /// Gets the requested response format type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "json_object";
}
