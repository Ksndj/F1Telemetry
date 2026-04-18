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
        var client = new DeepSeekClient(new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called."))));
        var service = new DeepSeekAnalysisService(client, new PromptBuilder());

        var result = await service.AnalyzeAsync(new AIAnalysisContext(), new AISettings { AiEnabled = true, ApiKey = string.Empty });

        Assert.False(result.IsSuccess);
        Assert.Contains("API Key", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
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
