using System.Net;

namespace F1Telemetry.AI.Services;

/// <summary>
/// Maps AI failures to short user-facing messages without leaking request details.
/// </summary>
public static class AIErrorMessageFormatter
{
    /// <summary>
    /// Message shown when AI is enabled but no API key is configured.
    /// </summary>
    public const string MissingApiKey = "AI 未配置 API Key";

    /// <summary>
    /// Message shown when the AI request cannot reach the service.
    /// </summary>
    public const string NetworkError = "AI 请求失败：网络错误";

    /// <summary>
    /// Message shown when the AI service rejects, rate-limits, or fails a request.
    /// </summary>
    public const string ServiceOrRateLimit = "AI 请求失败：服务错误/限流";

    /// <summary>
    /// Message shown when the AI returns content that cannot be parsed.
    /// </summary>
    public const string ParseFailure = "AI 返回解析失败";

    /// <summary>
    /// Formats an HTTP request failure for user-facing logs.
    /// </summary>
    public static string FormatHttpFailure(HttpRequestException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.StatusCode is null)
        {
            return NetworkError;
        }

        return exception.StatusCode == HttpStatusCode.TooManyRequests ||
            (int)exception.StatusCode >= 500
            ? ServiceOrRateLimit
            : ServiceOrRateLimit;
    }
}
