using System.Net.Http.Headers;
using System.Net.Http.Json;
using F1Telemetry.AI.Models;

namespace F1Telemetry.AI.Services;

/// <summary>
/// Sends OpenAI-compatible chat completion requests to DeepSeek.
/// </summary>
public sealed class DeepSeekClient
{
    private const string ChatCompletionsPath = "/chat/completions";
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new client instance.
    /// </summary>
    public DeepSeekClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Sends a chat completion request and returns the assistant JSON content.
    /// </summary>
    public async Task<string> CreateChatCompletionAsync(
        DeepSeekChatCompletionRequest request,
        AISettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(settings);

        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Post,
            NormalizeBaseUrl(settings.BaseUrl) + ChatCompletionsPath);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        requestMessage.Content = JsonContent.Create(request);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(settings.RequestTimeoutSeconds <= 0 ? 10 : settings.RequestTimeoutSeconds));

        using var response = await _httpClient.SendAsync(requestMessage, timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        var completion = await response.Content.ReadFromJsonAsync<DeepSeekChatCompletionResponse>(cancellationToken: timeoutCts.Token);
        return completion?.Choices.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    /// <summary>
    /// Normalizes a DeepSeek base URL so the caller can append the chat completion path exactly once.
    /// </summary>
    public static string NormalizeBaseUrl(string? baseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://api.deepseek.com"
            : baseUrl.Trim();

        normalized = normalized.TrimEnd('/');

        while (normalized.EndsWith(ChatCompletionsPath, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^ChatCompletionsPath.Length].TrimEnd('/');
        }

        return normalized;
    }
}
