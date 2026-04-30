using System.Net;
using System.Net.Http;
using System.Text;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies DeepSeek request normalization, missing-config handling, and JSON parsing.
/// </summary>
public sealed class DeepSeekAnalysisServiceTests
{
    /// <summary>
    /// Verifies that analysis fails fast when the API key is missing.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_MissingApiKey_ReturnsFailureResult()
    {
        var wasCalled = false;
        var client = new DeepSeekClient(new HttpClient(new StubHttpMessageHandler(_ =>
        {
            wasCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        })));
        var service = new DeepSeekAnalysisService(client, new PromptBuilder());

        var result = await service.AnalyzeAsync(new AIAnalysisContext(), new AISettings { AiEnabled = true, ApiKey = string.Empty });

        Assert.False(result.IsSuccess);
        Assert.Equal(AIErrorMessageFormatter.MissingApiKey, result.ErrorMessage);
        Assert.False(wasCalled);
    }

    /// <summary>
    /// Verifies that a valid DeepSeek JSON response is parsed into the fixed analysis result model.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ValidJson_ParsesFixedResult()
    {
        const string completionJson = """
{
  "choices": [
    {
      "message": {
        "content": "{\"summary\":\"pace ok\",\"tyreAdvice\":\"stay out\",\"fuelAdvice\":\"target +0.2\",\"trafficAdvice\":\"watch front gap\",\"ttsText\":\"pace is okay\"}"
      }
    }
  ]
}
""";
        var client = new DeepSeekClient(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(completionJson, Encoding.UTF8, "application/json")
        })));
        var service = new DeepSeekAnalysisService(client, new PromptBuilder());

        var result = await service.AnalyzeAsync(new AIAnalysisContext(), new AISettings { AiEnabled = true, ApiKey = "configured" });

        Assert.True(result.IsSuccess);
        Assert.Equal("pace ok", result.Summary);
        Assert.Equal("pace is okay", result.TtsText);
    }

    /// <summary>
    /// Verifies impossible refueling advice is normalized before it reaches logs or TTS.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_RefuelingAdvice_NormalizesFuelGuidance()
    {
        const string completionJson = """
{
  "choices": [
    {
      "message": {
        "content": "{\"summary\":\"油量过低，进站加油\",\"tyreAdvice\":\"保胎\",\"fuelAdvice\":\"油量过低，立即进站加油\",\"trafficAdvice\":\"保持距离\",\"ttsText\":\"油量过低，进站加油\"}"
      }
    }
  ]
}
""";
        var client = new DeepSeekClient(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(completionJson, Encoding.UTF8, "application/json")
        })));
        var service = new DeepSeekAnalysisService(client, new PromptBuilder());

        var result = await service.AnalyzeAsync(new AIAnalysisContext(), new AISettings { AiEnabled = true, ApiKey = "configured" });

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("进站加油", result.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("进站加油", result.FuelAdvice, StringComparison.Ordinal);
        Assert.DoesNotContain("进站加油", result.TtsText, StringComparison.Ordinal);
        Assert.Contains("省油", result.FuelAdvice, StringComparison.Ordinal);
        Assert.Contains("省油", result.TtsText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the DeepSeek client normalizes the base URL before sending the request.
    /// </summary>
    [Fact]
    public async Task CreateChatCompletionAsync_NormalizesBaseUrl()
    {
        Uri? observedUri = null;
        var client = new DeepSeekClient(new HttpClient(new StubHttpMessageHandler(request =>
        {
            observedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}", Encoding.UTF8, "application/json")
            };
        })));

        await client.CreateChatCompletionAsync(
            new DeepSeekChatCompletionRequest
            {
                Messages = [new DeepSeekChatMessage("user", "hello")]
            },
            new AISettings
            {
                ApiKey = "configured",
                BaseUrl = " https://api.deepseek.com/chat/completions/ "
            });

        Assert.NotNull(observedUri);
        Assert.Equal("https://api.deepseek.com/chat/completions", observedUri!.ToString());
    }

    /// <summary>
    /// Verifies network failures are normalized without leaking request details.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_NetworkFailure_ReturnsNormalizedErrorWithoutRawException()
    {
        const string secret = "test-secret-key";
        var client = new DeepSeekClient(new HttpClient(new StubHttpMessageHandler(_ =>
            throw new HttpRequestException($"Authorization Bearer {secret} https://secret.example.local"))));
        var service = new DeepSeekAnalysisService(client, new PromptBuilder());

        var result = await service.AnalyzeAsync(
            new AIAnalysisContext(),
            new AISettings
            {
                AiEnabled = true,
                ApiKey = secret,
                BaseUrl = "https://secret.example.local"
            });

        Assert.False(result.IsSuccess);
        Assert.Equal(AIErrorMessageFormatter.NetworkError, result.ErrorMessage);
        Assert.DoesNotContain(secret, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret.example", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies rate limits and service failures are normalized for user logs.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task AnalyzeAsync_ServiceOrRateLimit_ReturnsNormalizedError(HttpStatusCode statusCode)
    {
        const string secret = "test-secret-key";
        var client = new DeepSeekClient(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent($"service failure with {secret}", Encoding.UTF8, "text/plain")
        })));
        var service = new DeepSeekAnalysisService(client, new PromptBuilder());

        var result = await service.AnalyzeAsync(
            new AIAnalysisContext(),
            new AISettings
            {
                AiEnabled = true,
                ApiKey = secret,
                BaseUrl = "https://secret.example.local"
            });

        Assert.False(result.IsSuccess);
        Assert.Equal(AIErrorMessageFormatter.ServiceOrRateLimit, result.ErrorMessage);
        Assert.DoesNotContain(secret, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret.example", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies malformed AI JSON is reported with a normalized parse error.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_InvalidJson_ReturnsNormalizedParseFailure()
    {
        const string completionJson = """
{
  "choices": [
    {
      "message": {
        "content": "not json from https://secret.example.local"
      }
    }
  ]
}
""";
        var client = new DeepSeekClient(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(completionJson, Encoding.UTF8, "application/json")
        })));
        var service = new DeepSeekAnalysisService(client, new PromptBuilder());

        var result = await service.AnalyzeAsync(new AIAnalysisContext(), new AISettings { AiEnabled = true, ApiKey = "test-secret-key" });

        Assert.False(result.IsSuccess);
        Assert.Equal(AIErrorMessageFormatter.ParseFailure, result.ErrorMessage);
        Assert.DoesNotContain("secret.example", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
