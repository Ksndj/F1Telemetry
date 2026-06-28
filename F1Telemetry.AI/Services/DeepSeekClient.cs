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
    private const int MaxRequestAttempts = 2;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);
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

        var requestUri = NormalizeBaseUrl(settings.BaseUrl) + ChatCompletionsPath;
        var timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds <= 0 ? 10 : settings.RequestTimeoutSeconds);

        for (var attempt = 1; attempt <= MaxRequestAttempts; attempt++)
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            requestMessage.Content = JsonContent.Create(request);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                using var response = await _httpClient.SendAsync(requestMessage, timeoutCts.Token);
                if (IsTransientStatusCode(response.StatusCode) && attempt < MaxRequestAttempts)
                {
                    // 优先读取 Retry-After 头（DeepSeek 429 响应通常携带此头）
                    var retryAfter = RetryDelay; // 默认 250ms
                    if (response.Headers.TryGetValues("Retry-After", out var retryAfterValues))
                    {
                        var retryAfterValue = retryAfterValues.FirstOrDefault();
                        if (retryAfterValue is not null)
                        {
                            if (int.TryParse(retryAfterValue, out var retryAfterSeconds))
                            {
                                // 上限 60 秒，避免等待太久
                                retryAfter = TimeSpan.FromSeconds(Math.Min(retryAfterSeconds, 60));
                            }
                            else if (DateTimeOffset.TryParse(retryAfterValue, out var retryAfterDate))
                            {
                                var delay = retryAfterDate - DateTimeOffset.UtcNow;
                                if (delay > TimeSpan.Zero && delay <= TimeSpan.FromSeconds(60))
                                {
                                    retryAfter = delay;
                                }
                            }
                        }
                    }
                    await Task.Delay(retryAfter, cancellationToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                var completion = await response.Content.ReadFromJsonAsync<DeepSeekChatCompletionResponse>(cancellationToken: timeoutCts.Token);
                return completion?.Choices.FirstOrDefault()?.Message?.Content ?? string.Empty;
            }
            catch (HttpRequestException) when (attempt < MaxRequestAttempts)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < MaxRequestAttempts)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        throw new InvalidOperationException("AI request retry loop exited unexpectedly.");
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

    private static bool IsTransientStatusCode(System.Net.HttpStatusCode statusCode)
    {
        return statusCode == System.Net.HttpStatusCode.TooManyRequests ||
               (int)statusCode >= 500;
    }
}
