using System.Text.Json.Serialization;

namespace F1Telemetry.AI.Models;

/// <summary>
/// Represents persisted AI configuration for DeepSeek analysis.
/// </summary>
public sealed record AISettings
{
    /// <summary>
    /// Gets the API key used for authentication.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the DeepSeek base URL.
    /// </summary>
    public string BaseUrl { get; init; } = "https://api.deepseek.com";

    /// <summary>
    /// Gets the model name.
    /// </summary>
    public string Model { get; init; } = "deepseek-chat";

    /// <summary>
    /// Gets a value indicating whether AI analysis is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool AiEnabled { get; init; }

    /// <summary>
    /// Gets the request timeout in seconds.
    /// </summary>
    public int RequestTimeoutSeconds { get; init; } = 10;
}
